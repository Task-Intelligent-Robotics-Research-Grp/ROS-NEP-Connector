using System;
using System.Threading;
using System.Text;
using System.Collections.Generic;
using UnityEngine;
using NepStdMsgs;
using NepGeometryMsgs;
using NepShapeMsgs;
using NepVisualizationMsgs;
using NepTF2Msgs;
using Nep;
using Newtonsoft.Json;
using Cysharp.Threading.Tasks;

namespace ROSNEPConnector
{
    [RequireComponent(typeof(STLLoader))]
    [RequireComponent(typeof(DAELoader))]

    public class RobotDescription : MonoBehaviour
    {
        // NEP messages from ROS
        public class NepLinkGeometry
        {
            public NepLinkGeometry()
            {
                origin = new NepPose();
                primitive = new NepSolidPrimitive();
            }

            public NepPose origin { set; get; }
            public NepSolidPrimitive primitive { set; get; }
            public byte[] data { set; get; }
        }
        public class NepMaterial
        {
            public NepMaterial()
            {
                color = new NepColorRGBA();
            }

            public string name { set; get; }
            public NepColorRGBA color { set; get; }
            public UInt32 texture_height { set; get; }
            public UInt32 texture_width { set; get; }
            public byte[] texture_data { set; get; }
        }

        public class NepLink
        {
            public NepLink()
            {
                transform = new NepTransformStamped();
                visual_array = new NepLinkGeometry[0];
                collision_array = new NepLinkGeometry[0];
                material_array = new NepMaterial[0];
            }

            public NepLink(NepTransformStamped nep_transform)
            {
                transform = nep_transform;
                visual_array = new NepLinkGeometry[0];
                collision_array = new NepLinkGeometry[0];
                material_array = new NepMaterial[0];
            }

            public NepTransformStamped transform { set; get; }
            public NepLinkGeometry[] visual_array { set; get; }
            public NepLinkGeometry[] collision_array { set; get; }
            public NepMaterial[] material_array { set; get; }
        }

        // Request/response to/from ROS services
        public class NepGetLinksReq
        {
        }
        public class NepGetLinksRes
        {
            public NepLink[] links { set; get; }
        }

        public class NepGetCollisionObjectReq
        {
            public NepGetCollisionObjectReq(string object_type_)
            {
                object_type = object_type_;
            }

            public string object_type { set; get; }
        }
        public class NepGetCollisionObjectRes
        {
            public NepLinkGeometry[] visual_array { set; get; }
            public NepLinkGeometry[] collision_array { set; get; }
            public NepMaterial[] material_array { set; get; }
        }

        // Link definition corresponding to ROS links defined in URDF
        public class Link
        {
            // member variables
            private GameObject _link;
            private GameObject[] _elements;
            private GameObject[] _colliders;

            // public member functions
            public Link(Transform parent_transform, NepLink nep_link)
            {
                _link = new GameObject();
                _link.name = nep_link.transform.child_frame_id;
                _link.transform.name = _link.name;
                _link.transform.SetParent(parent_transform);
                _link.transform.localPosition = NepGlobal.NepToUnity(nep_link.transform.transform.translation);
                _link.transform.localRotation = NepGlobal.NepToUnity(nep_link.transform.transform.rotation);

                SetPrimitives(nep_link.visual_array, nep_link.collision_array, nep_link.material_array);
            }

            public void Clear()
            {
                Destroy(_link);
            }

            public void SetPrimitives(NepLinkGeometry[] visual_array, NepLinkGeometry[] collision_array, NepMaterial[] material_array)
            {
                bool has_mesh = false;
                _elements = new GameObject[visual_array.Length];
                for (int i = 0; i < _elements.Length; ++i)
                {
                    _elements[i] = CreatePrimitive(visual_array[i], material_array[i], i);
                    has_mesh |= (visual_array[i].primitive.type == 0);
                }
                if (has_mesh)
                {
                    _colliders = new GameObject[collision_array.Length];
                    for (int i = 0; i < _colliders.Length; ++i)
                        _colliders[i] = CreatePrimitive(collision_array[i], null, i);
                }        
            }

            public bool HasPrimitives()
            {
                return (_elements.Length > 0);
            }

            // private member functions
            private GameObject CreatePrimitive(NepLinkGeometry nep_geometry, NepMaterial nep_material, int i)
            {
                GameObject primitive;
                var collision = (nep_material == null);
                var nep_scale = new NepVector3();

                switch (nep_geometry.primitive.type)
                {
                    case 0:     // Mesh
                        nep_scale.x = (float)nep_geometry.primitive.dimensions[0];
                        nep_scale.y = (float)nep_geometry.primitive.dimensions[1];
                        nep_scale.z = (float)nep_geometry.primitive.dimensions[2];
                        primitive = new GameObject();
                        if (collision)
                            primitive.AddComponent<MeshCollider>();
                        else
                        {
                            primitive.AddComponent<MeshRenderer>();
                            primitive.AddComponent<MeshFilter>();
                        }
                        break;
                    case 1:     // Box
                        nep_scale.x = (float)nep_geometry.primitive.dimensions[0];
                        nep_scale.y = (float)nep_geometry.primitive.dimensions[1];
                        nep_scale.z = (float)nep_geometry.primitive.dimensions[2];
                        primitive = GameObject.CreatePrimitive(PrimitiveType.Cube);
                        if (collision)
                        {
                            GameObject.Destroy(primitive.GetComponent<MeshRenderer>());
                            GameObject.Destroy(primitive.GetComponent<MeshFilter>());
                        }
                        break;
                    case 2:     // Sphere
                        nep_scale.x = (float)(2.0 * nep_geometry.primitive.dimensions[0]);
                        nep_scale.y = nep_scale.x;
                        nep_scale.z = nep_scale.x;
                        primitive = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                        if (collision)
                        {
                            GameObject.Destroy(primitive.GetComponent<MeshRenderer>());
                            GameObject.Destroy(primitive.GetComponent<MeshFilter>());
                        }
                        break;
                    case 3:    // Cylinder
                        nep_scale.x = (float)(2.0 * nep_geometry.primitive.dimensions[1]);
                        nep_scale.y = nep_scale.x;
                        nep_scale.z = (float)(0.5 * nep_geometry.primitive.dimensions[0]);
                        primitive = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                        if (collision)
                        {
                            GameObject.Destroy(primitive.GetComponent<MeshRenderer>());
                            GameObject.Destroy(primitive.GetComponent<MeshFilter>());
                        }
                        break;
                    default:    // Null
                        return null;
                }

                primitive.name = _link.name + (collision ? "/collider" : "/element") + i.ToString();
                primitive.transform.name = primitive.name;
                primitive.transform.SetParent(_link.transform);
                primitive.transform.localScale = NepGlobal.NepToUnity(nep_scale);
                primitive.transform.localPosition = NepGlobal.NepToUnity(nep_geometry.origin.position);
                primitive.transform.localRotation = NepGlobal.NepToUnity(nep_geometry.origin.orientation);

                if (nep_geometry.primitive.type == 0)    // If the primitive type is mesh, create a mesh from raw data.
                {
                    var magicstr = Encoding.UTF8.GetString(nep_geometry.data, 0, 5);
                    if (magicstr == "<?xml" || magicstr == "<COLL")
                        _link.transform.root.GetComponent<DAELoader>().CreateMeshFromDAE(primitive, nep_geometry.data, collision);
                    else
                        _link.transform.root.GetComponent<STLLoader>().CreateMeshFromSTL(primitive, nep_geometry.data, collision);
                }

                if (!collision)
                {
                    if (nep_material.texture_height == 0)
                    {
                        primitive.GetComponent<MeshRenderer>().material.SetColor("_Color",
                                                                                new Color(nep_material.color.r,
                                                                                        nep_material.color.g,
                                                                                        nep_material.color.b,
                                                                                        nep_material.color.a));
                    }
                    else
                    {
                        // Create texture from incoming texture data
                        var texture = new Texture2D((int)nep_material.texture_width, (int)nep_material.texture_height, TextureFormat.RGB24, true);
                        texture.SetPixelData(nep_material.texture_data, 0);
                        texture.Apply();

                        // Create cubemap
                        var cubemap_width = Math.Max(4 * ((texture.width - 1) / 4 + 1), 4 * ((texture.height - 1) / 4 + 1));
                        Debug.Log("*** cubemap_width=" + cubemap_width);
                        var cubemap = new Cubemap(cubemap_width, TextureFormat.RGBA32, false);
                        Graphics.ConvertTexture(texture, 0, cubemap, (int)CubemapFace.PositiveX);
                        Graphics.ConvertTexture(texture, 0, cubemap, (int)CubemapFace.NegativeX);
                        Graphics.ConvertTexture(texture, 0, cubemap, (int)CubemapFace.PositiveY);
                        Graphics.ConvertTexture(texture, 0, cubemap, (int)CubemapFace.NegativeY);
                        Graphics.ConvertTexture(texture, 0, cubemap, (int)CubemapFace.PositiveZ);
                        Graphics.ConvertTexture(texture, 0, cubemap, (int)CubemapFace.NegativeZ);
                        cubemap.Apply();

                        var shader = Shader.Find("Hidden/CubeCopy");
                        var material = new Material(shader);
                        if (material == null)
                        {
                            Debug.Log("*** Failed to create material!");
                            return primitive;
                        }
                        material.SetTexture("_MainTex", cubemap);
                        primitive.GetComponent<MeshRenderer>().material = material;
                    }
                }

                return primitive;
            }
        }

        // Member variables
        private Dictionary<string, Link> _links = new Dictionary<string, Link>();
        private NepClient<NepGetLinksReq, NepGetLinksRes> _getLinks;
        private NepCallback _transformsCallback;
        private NepCallback _markerArrayCallback;
        private NepClient<NepGetCollisionObjectReq, NepGetCollisionObjectRes> _getCollisionObject;
        private SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);

        // Member functions
        private async void Start()
        {
            var scene_name = gameObject.scene.name;

            // Create service client of aist_msgs/srv/GetLinks type
            _getLinks = new NepClient<NepGetLinksReq, NepGetLinksRes>(scene_name + "/get_links", scene_name + "/links");

            // Create subscriber callback for the topic of tf2_msgs/TFMessage type
            Debug.Log("*** Creating callback[" + scene_name + "/transforms]");
            _transformsCallback = NepGlobal.Node.NewCallback(scene_name + "/transforms", TransformsCallback, NepGlobal.IP);
            _transformsCallback.Start();

            // Create subscriber callback for the topic of visualization_msgs/Marker type
            Debug.Log("*** Creating callback[" + scene_name + "/collision_marker]");
            _markerArrayCallback = NepGlobal.Node.NewCallback(scene_name + "/collision_marker", MarkerArrayCallback, NepGlobal.IP);
            _markerArrayCallback.Start();

            // Create service client of aist_msgs/srv/GetMeshResource type
            _getCollisionObject = new NepClient<NepGetCollisionObjectReq, NepGetCollisionObjectRes>(scene_name + "/get_collision_object",
                                                                                           scene_name + "/collision_object");

            // Wait 1.0sec before sending request for links.
            await UniTask.Delay(TimeSpan.FromSeconds(1f));

            Debug.Log("*** Send request for service[" + scene_name + "/get_links]");
            var nep_links = await _getLinks.CallAsync(new NepGetLinksReq());
            Debug.Log("*** Received response from service[" + scene_name + "/get_links]");
        
            _links.Clear();

            foreach (var nep_link in nep_links.links)
            {
                //Debug.Log("*** parent=" + nep_link.transform.header.frame_id + ", me=" + nep_link.transform.child_frame_id);
                var parent_transform = Utility.FindTransform(nep_link.transform.header.frame_id, transform);
                if (parent_transform == null)
                    parent_transform = transform;
                _links.TryAdd(nep_link.transform.child_frame_id, new Link(parent_transform, nep_link));
            }
        }

        private void TransformsCallback(string message)
        {
            //Debug.Log(message);
            var nep_tf_message = JsonConvert.DeserializeObject<NepTFMessage>(message);

            foreach (var nep_transform in nep_tf_message.transforms)
            {
                var found = Utility.FindTransform(nep_transform.child_frame_id, transform);
                if (found != null)
                {
                    if (found.parent.name != nep_transform.header.frame_id)
                    {
                        var parent_transform = Utility.FindTransform(nep_transform.header.frame_id, transform);
                        if (parent_transform == null)
                            continue;
                        found.SetParent(parent_transform);
                    }
                    found.localPosition = NepGlobal.NepToUnity(nep_transform.transform.translation);
                    found.localRotation = NepGlobal.NepToUnity(nep_transform.transform.rotation);
                }
                else
                {
                    var parent_transform = Utility.FindTransform(nep_transform.header.frame_id, transform);
                    if (parent_transform != null)
                        _links.TryAdd(nep_transform.child_frame_id, new Link(parent_transform, new NepLink(nep_transform)));
                }
            }
        }

        private async void MarkerArrayCallback(string message)
        {        //Debug.Log(message);
            var nep_marker_array = JsonConvert.DeserializeObject<NepMarkerArray>(message);

            await _semaphore.WaitAsync();   // This callback should be mutually exclusive.
            try
            {
                foreach (var nep_marker in nep_marker_array.markers)
                {
                    Link collision_object;

                    switch (nep_marker.action)
                    {
                        case 0: // ADD/MODIFY
                            if (_links.TryGetValue(nep_marker.header.frame_id, out collision_object) &&
                                !collision_object.HasPrimitives())
                            {
                                Debug.Log("*** Send request for collision object of type[" + nep_marker.text + "]");
                                var nep_collision_object = await _getCollisionObject.CallAsync(new NepGetCollisionObjectReq(nep_marker.text));
                                Debug.Log("*** Received response with collision object of type[" + nep_marker.text + "]");
                                collision_object.SetPrimitives(nep_collision_object.visual_array, nep_collision_object.collision_array, nep_collision_object.material_array);
                            }
                            break;
                        case 2: // DELETE
                            if (_links.Remove(nep_marker.header.frame_id, out collision_object))
                            {
                                Debug.Log("*** Remove collision object with base link[" + nep_marker.header.frame_id + "]");
                                collision_object.Clear();
                            }
                            break;
                        default:
                            break;
                    }
                }
            }
            finally
            {
                _semaphore.Release();
            }
        }
    }
}
