#define MILLIMETERS

using System;
using System.Text;
using System.Collections.Generic;
using System.IO;
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
                visual = new NepLinkGeometry();
                collision = new NepLinkGeometry();
            }

            public NepLink(NepTransformStamped nep_transform)
            {
                transform = nep_transform;
                visual = new NepLinkGeometry();
                visual.primitive = new NepSolidPrimitive();
                visual.primitive.type = 255;
                collision.primitive = new NepSolidPrimitive();
                collision.primitive.type = 255;
            }

            public NepTransformStamped transform { set; get; }
            public NepLinkGeometry visual { set; get; }
            public NepLinkGeometry collision { set; get; }
            public NepMaterial material { set; get; }
        }

        // Request/response to/from ROS services
        public class NepGetLinksReq
        {
        }
        public class NepGetLinksRes
        {
            public NepLink[] links { set; get; }
        }

        public class NepGetMeshResourceReq
        {
            public NepGetMeshResourceReq(string mesh_resource_)
            {
                mesh_resource = mesh_resource_;
            }

            public string mesh_resource { set; get; }
        }
        public class NepGetMeshResourceRes
        {
            public string mesh_resource { set; get; }
            public byte[] data { set; get; }
        }

        // Link definition corresponding to ROS links defined in URDF
        public class Link
        {
            // member variables
            private GameObject _link;
            private GameObject _element;

            // public member functions
            public Link(Transform parent_transform, NepLink nep_link)
            {
                _link = new GameObject();
                _link.name = nep_link.transform.child_frame_id;
                _link.transform.name = _link.name;
                _link.transform.SetParent(parent_transform);
                _link.transform.localPosition = NepGlobal.NepToUnity(nep_link.transform.transform.translation);
                _link.transform.localRotation = NepGlobal.NepToUnity(nep_link.transform.transform.rotation);

                var nep_scale = new NepVector3();

                switch (nep_link.visual.primitive.type)
                {
                    case 0:     // Mesh
                        nep_scale.x = (float)nep_link.visual.primitive.dimensions[0];
                        nep_scale.y = (float)nep_link.visual.primitive.dimensions[1];
                        nep_scale.z = (float)nep_link.visual.primitive.dimensions[2];
                        _element = new GameObject();
                        _element.AddComponent<MeshRenderer>();
                        _element.AddComponent<MeshFilter>();
                        break;
                    case 1:     // Box
                        nep_scale.x = (float)nep_link.visual.primitive.dimensions[0];
                        nep_scale.y = (float)nep_link.visual.primitive.dimensions[1];
                        nep_scale.z = (float)nep_link.visual.primitive.dimensions[2];
                        _element = GameObject.CreatePrimitive(PrimitiveType.Cube);
                        break;
                    case 2:     // Sphere
                        nep_scale.x = (float)(2.0 * nep_link.visual.primitive.dimensions[0]);
                        nep_scale.y = nep_scale.x;
                        nep_scale.z = nep_scale.x;
                        _element = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                        break;
                    case 3:    // Cylinder
                        nep_scale.x = (float)(2.0 * nep_link.visual.primitive.dimensions[1]);
                        nep_scale.y = nep_scale.x;
                        nep_scale.z = (float)(0.5 * nep_link.visual.primitive.dimensions[0]);
                        _element = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                        break;
                    default:    // Null
                        _element = null;
                        return;
                }

                _element.name = _link.name + "/element";
                _element.transform.name = _element.name;
                _element.transform.SetParent(_link.transform);
                _element.transform.localScale = NepGlobal.NepToUnity(nep_scale);
                _element.transform.localPosition = NepGlobal.NepToUnity(nep_link.visual.origin.position);
                _element.transform.localRotation = NepGlobal.NepToUnity(nep_link.visual.origin.orientation);

                if (nep_link.material.texture_height == 0)
                {
                    _element.GetComponent<MeshRenderer>().material.SetColor("_Color",
                                                                            new Color(nep_link.material.color.r,
                                                                                      nep_link.material.color.g,
                                                                                      nep_link.material.color.b,
                                                                                      nep_link.material.color.a));
                }
                else
                {
                    // Create texture from incoming texture data
                    var texture = new Texture2D((int)nep_link.material.texture_width, (int)nep_link.material.texture_height, TextureFormat.RGB24, true);
                    texture.SetPixelData(nep_link.material.texture_data, 0);
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
                        return;
                    }
                    material.SetTexture("_MainTex", cubemap);
                    _element.GetComponent<MeshRenderer>().material = material;
                }

                if (nep_link.visual.primitive.type == 0)    // If the element type is mesh, create a mesh from raw data.
                {
                    var magicstr = Encoding.UTF8.GetString(nep_link.visual.data, 0, 5);
                    if (magicstr == "<?xml" || magicstr == "<COLL")
                        _link.transform.root.GetComponent<DAELoader>().CreateMeshFromDAE(_element, nep_link.visual.data);
                    else
                        _link.transform.root.GetComponent<STLLoader>().CreateMeshFromSTL(_element, nep_link.visual.data);
                }
            }

            // private member functions
            private void OnDestroy()
            {
                foreach (var material in _element.GetComponent<MeshRenderer>().materials)
                    Destroy(material);
                Destroy(_element.GetComponent<MeshFilter>().mesh);
            }
        }

        // Marker definition corresponding to ROS visualization markers
        public class Marker
        {
            // member variables
            private GameObject _element;

            // public member functions
            public Marker(Transform parent_transform, NepMarker nep_marker, byte[] data)
            {
                var nep_scale = nep_marker.scale;

                switch (nep_marker.type)
                {
                    case 1:     // CUBE
                        _element = GameObject.CreatePrimitive(PrimitiveType.Cube);
                        break;
                    case 2:     // SPHERE
                        _element = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                        break;
                    case 3:    // CYLINDER
                        nep_scale.z *= 0.5;
                        _element = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                        break;
                    case 10:    // MESH_RESOURCE
                        _element = new GameObject();
                        _element.AddComponent<MeshRenderer>();
                        _element.AddComponent<MeshFilter>();
                        break;

                    default:
                        return;
                }

                _element.name = MarkerName(nep_marker);
                _element.transform.name = _element.name;
                _element.transform.SetParent(parent_transform);
                _element.transform.localScale = NepGlobal.NepToUnity(nep_scale);
                SetPose(nep_marker.pose);

                if (nep_marker.type == 10)    // If the element type is MESH_RESOURCE, create a mesh from raw data.
                {
                    var magicstr = Encoding.UTF8.GetString(data, 0, 5);
                    if (magicstr == "<?xml" || magicstr == "<COLL")
                        _element.transform.root.GetComponent<DAELoader>().CreateMeshFromDAE(_element, data);
                    else
                        _element.transform.root.GetComponent<STLLoader>().CreateMeshFromSTL(_element, data);
                }

                _element.GetComponent<MeshRenderer>().material.SetColor("_Color",
                                                                        new Color(nep_marker.color.r,
                                                                                  nep_marker.color.g,
                                                                                  nep_marker.color.b,
                                                                                  nep_marker.color.a));
            }

            public void SetPose(NepPose nep_pose)
            {
                _element.transform.localPosition = NepGlobal.NepToUnity(nep_pose.position);
                _element.transform.localRotation = NepGlobal.NepToUnity(nep_pose.orientation);
            }

            // private member functions
            private void OnDestroy()
            {
                foreach (var material in _element.GetComponent<MeshRenderer>().materials)
                    Destroy(material);
                Destroy(_element.GetComponent<MeshFilter>().mesh);
            }

            public static string MarkerName(NepMarker nep_marker)
            {
                return nep_marker.ns + "/collision_object_manager/collision_marker_" + Convert.ToString(nep_marker.id);
            }
        }

        // Member variables
        [SerializeField] private string testModel;
        private List<Link> _links = new List<Link>();
        private Dictionary<string, byte[]> _mesh_resources = new Dictionary<string, byte[]>();
        private Dictionary<Int32, Marker> _markers = new Dictionary<Int32, Marker>();
        private bool _get_links_completed = false;
        private NepPublisher _getLinksReqPub;
        private NepCallback _getLinksResCallback;
        private NepCallback _transformsCallback;
        private NepCallback _markerCallback;
        private NepPublisher _getMeshResourceReqPub;
        private NepCallback _getMeshResouceResCallback;

        // Member functions
        private async void Start()
        {
            if (testModel != "")
            {
                // Only for testing.
                var link = new NepLink();
                link.transform.header.frame_id = "base_link";
                link.transform.child_frame_id = "tmp_link";
                link.transform.transform.translation.x = 0;
                link.transform.transform.translation.y = 0;
                link.transform.transform.translation.z = 0;
                link.transform.transform.rotation.x = 0;
                link.transform.transform.rotation.y = 0;
                link.transform.transform.rotation.z = 0;
                link.transform.transform.rotation.w = 1;

                link.visual.origin.position = new NepPoint();
                link.visual.origin.position.x = 0;
                link.visual.origin.position.y = 0;
                link.visual.origin.position.z = 0;
                link.visual.origin.orientation = new NepQuaternion();
                link.visual.origin.orientation.x = 0;
                link.visual.origin.orientation.y = 0;
                link.visual.origin.orientation.z = 0;
                link.visual.origin.orientation.w = 1;

                link.visual.primitive.type = 0; // 0: Mesh, 1: Box, 2: Sphere, 3: Cylinder
                link.visual.primitive.dimensions = new double[3];
#if MILLIMETERS
                link.visual.primitive.dimensions[0] = 0.001;
                link.visual.primitive.dimensions[1] = 0.001;
                link.visual.primitive.dimensions[2] = 0.001;
#else
                link.visual.primitive.dimensions[0] = 1;
                link.visual.primitive.dimensions[1] = 1;
                link.visual.primitive.dimensions[2] = 1;
#endif
                link.material.name = "cyan";
                link.material.color.r = 0;
                link.material.color.g = 1;
                link.material.color.b = 0;
                link.material.color.a = 1;

                //link.data = File.ReadAllBytes("/Users/ueshiba/work/ur5e/wrist2.dae");
                //link.data = File.ReadAllBytes("C:/Users/user1/Documents/TRanbo.stl");
                link.visual.data = File.ReadAllBytes(testModel);

                _links.Add(new Link(transform, link));

                return;
            }

            var scene_name = gameObject.scene.name;

            // Create publisher and corresponding callback for the service of nep_bridge/GetLinks type
            Debug.Log("*** Creating publisher[" + scene_name + "/get_links] and callback[" + scene_name + "/links]");
            _getLinksReqPub = NepGlobal.Node.NewPub(scene_name + "/get_links", "json", NepGlobal.Conf);
            _getLinksResCallback = NepGlobal.Node.NewCallback(scene_name + "/links", GetLinksResCallback, NepGlobal.IP);
            _getLinksResCallback.Start();

            // Create subscriber callback for the topic of tf2_msgs/TFMessage type
            Debug.Log("*** Creating callback[" + scene_name + "/transforms]");
            _transformsCallback = NepGlobal.Node.NewCallback(scene_name + "/transforms", TransformsCallback, NepGlobal.IP);
            _transformsCallback.Start();

            // Create subscriber callback for the topic of visualization_msgs/Marker type
            Debug.Log("*** Creating callback[" + scene_name + "/collision_marker]");
            _markerCallback = NepGlobal.Node.NewCallback(scene_name + "/collision_marker", MarkerCallback, NepGlobal.IP);
            _markerCallback.Start();

            // Create publlisher and corresponding callback for the service of aist_msgs/GetMeshResource type
            Debug.Log("*** Creating publisher[" + scene_name + "/get_mesh_resource] and callback[" + scene_name + "/mesh_resource]");
            _getMeshResourceReqPub = NepGlobal.Node.NewPub(scene_name + "/get_mesh_resource", "json", NepGlobal.Conf);
            _getMeshResouceResCallback = NepGlobal.Node.NewCallback(scene_name + "/mesh_resource", GetMeshResourceResCallback, NepGlobal.IP);
            _getMeshResouceResCallback.Start();

            // Wait 1.0sec before sending request for links.
            await UniTask.Delay(TimeSpan.FromSeconds(1f));

            Debug.Log("*** Send request to service[" + scene_name + "/get_links]");
            _get_links_completed = false;
            _getLinksReqPub.Publish(new NepGetLinksReq());

            await UniTask.WaitUntil(() => _get_links_completed, cancellationToken: this.GetCancellationTokenOnDestroy());
            Debug.Log("*** Received response from service[" + scene_name + "/get_links]");
        }

        private void GetLinksResCallback(string message)
        {
            //Debug.Log(message);
            var nep_links = JsonConvert.DeserializeObject<NepGetLinksRes>(message).links;

            if (nep_links == null)
                return;

            _links.Clear();

            foreach (var nep_link in nep_links)
            {
                //Debug.Log("*** parent=" + nep_link.transform.header.frame_id + ", me=" + nep_link.transform.child_frame_id);
                var parent_transform = Utility.FindTransform(nep_link.transform.header.frame_id, transform);
                if (parent_transform == null)
                    parent_transform = transform;
                _links.Add(new Link(parent_transform, nep_link));
            }

            _get_links_completed = true;
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
                        _links.Add(new Link(parent_transform, new NepLink(nep_transform)));
                }
            }
        }

        private void MarkerCallback(string message)
        {        //Debug.Log(message);
            var nep_marker = JsonConvert.DeserializeObject<NepMarker>(message);
            // Debug.Log("*** Marker with base_link[" + nep_marker.header.frame_id + "] detected");

            switch (nep_marker.action)
            {
                case 0: // ADD/MODIFY
                    break;
                case 2: // DELETE
                    _markers.Remove(nep_marker.id);
                    return;
                case 3: // DELETEALL
                    _markers.Clear();
                    return;
                default:
                    return;
            }

            // Search for a marker with ID given by nep_marker.id. If not found, create one and register.
            Marker marker;
            if (!_markers.TryGetValue(nep_marker.id, out marker))
            {
                byte[] data;
                if (nep_marker.type != 10)  // not MESH_RESOURCE
                    data = null;
                else if (!_mesh_resources.TryGetValue(nep_marker.mesh_resource, out data))
                {
                    _getMeshResourceReqPub.Publish(new NepGetMeshResourceReq(nep_marker.mesh_resource));
                    return;
                }

                // Search for a transform named nep_marker.header.frame_id, i.e. marker's base link.
                var parent_transform = Utility.FindTransform(nep_marker.header.frame_id, transform);
                if (parent_transform != null)
                    _markers[nep_marker.id] = new Marker(parent_transform, nep_marker, data);
            }
            else
            {
                marker.SetPose(nep_marker.pose);
            }
        }

        private void GetMeshResourceResCallback(string message)
        {
            var nep_resource_res = JsonConvert.DeserializeObject<NepGetMeshResourceRes>(message);
            // Debug.Log("*** Response to request for mesh resource[" + nep_resource_res.mesh_resource + "]");

            _mesh_resources[nep_resource_res.mesh_resource] = nep_resource_res.data;
        }
    }
}
