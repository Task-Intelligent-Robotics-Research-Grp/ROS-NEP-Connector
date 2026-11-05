using UnityEngine;
using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using Collada141;

namespace ROSNEPConnector
{
    public class DAELoader : MonoBehaviour
    {
        private class DAEEffect
        {
            public double[] emission { set; get; }
            public double[] ambient { set; get; }
            public double[] diffuse { set; get; }
            public double[] specular { set; get; }
            public double shininess { set; get; }
            public double[] reflective { set; get; }
            public double reflectivity { set; get; }
            //public double transparency { set; get; }
        }

        private class DAEMaterial
        {
            public string url { set; get; }
        }

        private class DAEGeometry
        {
            public class Triangles
            {
                public Vector3[] vertices { set; get; }
                public Vector3[] normals { set; get; }
                public int[] triangles { set; get; }
                public string material { set; get; }

                public void transform(Matrix4x4 matrix4x4)
                {
                    for (int i = 0; i < vertices.Length; ++i)
                    {
                        vertices[i] = matrix4x4.MultiplyPoint(vertices[i]);
                    }
                    for (int i = 0; i < normals.Length; ++i)
                    {
                        normals[i] = matrix4x4.MultiplyVector(normals[i]);
                    }
                }
            }

            public DAEGeometry()
            {
                triangles_list = new List<Triangles>();
            }

            public List<Triangles> triangles_list { set; get; }
        }

        private class DAEVisualScene
        {
            public class Node
            {
                public class InstanceGeometry
                {
                    public InstanceGeometry()
                    {
                        instance_material_targets = new Dictionary<string, string>();
                    }

                    public Dictionary<string, string> instance_material_targets { set; get; }
                }

                public Node()
                {
                    matrix4x4 = Matrix4x4.identity;
                    instance_geometries = new Dictionary<string, InstanceGeometry>();
                }

                public Matrix4x4 matrix4x4;
                public Dictionary<string, InstanceGeometry> instance_geometries { set; get; }
            }

            public DAEVisualScene()
            {
                nodes = new Dictionary<string, Node>();
            }

            public Dictionary<string, Node> nodes { set; get; }
        }

        [SerializeField] private bool recalculateNormals;

        public void CreateMeshFromDAE(GameObject primitive, byte[] data, bool collision=false)
        {
            // Load the Collada model
            COLLADA model;
            using (var stream = new MemoryStream(data))
                model = COLLADA.Load(stream);

            // Iterate on libraries
            var dae_effects = new Dictionary<string, DAEEffect>();
            var dae_materials = new Dictionary<string, DAEMaterial>();
            var dae_geometries = new Dictionary<string, DAEGeometry>();
            var dae_visual_scenes = new Dictionary<string, DAEVisualScene>();

            foreach (var modelItem in model.Items)
            {
                if (modelItem is library_effects)
                {
                    var effects = modelItem as library_effects;

                    if (effects.effect != null)
                        foreach (var effect in effects.effect)
                        {
                            var dae_effect = ProcessDAEEffect(effect);

                            if (dae_effect != null)
                                dae_effects[effect.id] = dae_effect;
                        }
                }
                else if (modelItem is library_materials)
                {
                    var materials = modelItem as library_materials;

                    if (materials.material != null)
                        foreach (var material in materials.material)
                            dae_materials[material.id] = ProcessDAEMaterial(material);
                }
                else if (modelItem is library_geometries)
                {
                    var geometries = modelItem as library_geometries;

                    if (geometries.geometry != null)
                        foreach (var geometry in geometries.geometry)
                            if (geometry.Item is mesh)
                            {
                                var dae_geometry = ProcessDAEGeometry(geometry.Item as mesh);

                                if (dae_geometry != null)
                                    dae_geometries[geometry.id] = dae_geometry;
                            }
                }
                else if (modelItem is library_visual_scenes)
                {
                    var visual_scenes = modelItem as library_visual_scenes;

                    if (visual_scenes.visual_scene != null)
                        foreach (var visual_scene in visual_scenes.visual_scene)
                            dae_visual_scenes[visual_scene.id] = ProcessDAEVisualScene(visual_scene);
                }
            }

            // Create and set materials for each submesh
            foreach (var dae_visual_scene in dae_visual_scenes.Values)
            {
                // Concatenate vertices and normals of all triangles in all geometries in all nodes
                var vertices = new List<Vector3>();
                var normals = new List<Vector3>();
                foreach (var dae_node in dae_visual_scene.nodes.Values)
                {
                    foreach (var url in dae_node.instance_geometries.Keys)
                        foreach (var dae_triangles in dae_geometries[url].triangles_list)
                        {
                            dae_triangles.transform(dae_node.matrix4x4);
                            vertices.AddRange(dae_triangles.vertices);   // Append vertices of current geometry
                            normals.AddRange(dae_triangles.normals);
                        }
                }

                // Create Unity mesh
                var mesh = (collision ? new Mesh() : primitive.GetComponent<MeshFilter>().mesh);
                if (vertices.Count > 60000)
                    mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                mesh.SetVertices(vertices);
                mesh.name = primitive.name;
                if (!collision && !recalculateNormals)
                    mesh.SetNormals(normals);

                // The number of subMesh is the total number of triangle lists in all instance geometries in all nodes.
                int subMeshCount = 0;
                foreach (var dae_node in dae_visual_scene.nodes.Values)
                    foreach (var url in dae_node.instance_geometries.Keys)
                        subMeshCount += dae_geometries[url].triangles_list.Count;
                mesh.subMeshCount = subMeshCount;

                //Debug.Log("--- SetVertices done, #subMeshes=" + mesh.subMeshCount);

                var materials = new Material[mesh.subMeshCount];
                int i = 0;
                int offset = 0;

                foreach (var dae_node in dae_visual_scene.nodes.Values)
                {
                    foreach (var (url, instance_geometry) in dae_node.instance_geometries)
                    {
                        foreach (var dae_triangles in dae_geometries[url].triangles_list)
                        {
                            mesh.SetTriangles(dae_triangles.triangles, i, true, offset); // Set triangles of i-th submesh
                            // Debug.Log("--- #triangles=" + dae_triangles.triangles.Length);
    
                            string instance_material_target;
                            if (dae_triangles.material != null && instance_geometry.instance_material_targets.TryGetValue(dae_triangles.material, out instance_material_target))
                            {
                                DAEMaterial dae_material;
                                if (dae_materials.TryGetValue(instance_material_target, out dae_material))
                                {
                                    DAEEffect dae_effect;
                                    if (dae_material.url != null && dae_effects.TryGetValue(dae_material.url, out dae_effect))
                                    {
                                        materials[i] = CreateMaterial(dae_effect);
                                    }
                                }
                            }

                            ++i;
                            offset += dae_triangles.vertices.Length;
                        }
                    }
                }

                if (collision)
                    primitive.GetComponent<MeshCollider>().sharedMesh = mesh;
                else
                {
                    if (recalculateNormals)
                        mesh.RecalculateNormals();
                    mesh.RecalculateBounds();
                    mesh.RecalculateTangents();

                    primitive.GetComponent<MeshRenderer>().materials = materials;
                }
            }
        }

        private Material CreateMaterial(DAEEffect effect)
        {
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));

            if (effect.emission != null)
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", new Color((float)effect.emission[0], (float)effect.emission[1], (float)effect.emission[2], (float)effect.emission[3]));
            }
            if (effect.diffuse != null)
                material.SetColor("_BaseColor", new Color((float)effect.diffuse[0], (float)effect.diffuse[1], (float)effect.diffuse[2], (float)effect.diffuse[3]));
            material.SetFloat("_GlossyReflections", (float)effect.reflectivity);

            return material;
        }

        private DAEEffect ProcessDAEEffect(effect effect)
        {
            //Debug.Log("*** effect: " + effect.id);

            foreach (var item in effect.Items)
            {
                var technique = item.technique;

                if (technique.Item is effectFx_profile_abstractProfile_COMMONTechniqueBlinn)
                {
                    var profile = technique.Item as effectFx_profile_abstractProfile_COMMONTechniqueBlinn;
                    var dae_effect = new DAEEffect();

                    if (profile.emission != null)
                        dae_effect.emission = (profile.emission.Item as common_color_or_texture_typeColor).Values;
                    if (profile.ambient != null)
                        dae_effect.ambient = (profile.ambient.Item as common_color_or_texture_typeColor).Values;
                    if (profile.diffuse != null)
                        dae_effect.diffuse = (profile.diffuse.Item as common_color_or_texture_typeColor).Values;
                    if (profile.specular != null)
                        dae_effect.specular = (profile.specular.Item as common_color_or_texture_typeColor).Values;
                    if (profile.reflective != null)
                        dae_effect.reflective = (profile.reflective.Item as common_color_or_texture_typeColor).Values;
                    if (profile.shininess != null)
                        dae_effect.shininess = (profile.shininess.Item as common_float_or_param_typeFloat).Value;
                    if (profile.reflectivity != null)
                        dae_effect.reflectivity = (profile.reflectivity.Item as common_float_or_param_typeFloat).Value;

                    return dae_effect;
                }
                else if (technique.Item is effectFx_profile_abstractProfile_COMMONTechniqueConstant)
                {
                    var profile = technique.Item as effectFx_profile_abstractProfile_COMMONTechniqueConstant;
                    var dae_effect = new DAEEffect();

                    if (profile.emission != null)
                        dae_effect.emission = (profile.emission.Item as common_color_or_texture_typeColor).Values;
                    if (profile.reflectivity != null)
                        dae_effect.reflectivity = (profile.reflectivity.Item as common_float_or_param_typeFloat).Value;

                    return dae_effect;
                }
                else if (technique.Item is effectFx_profile_abstractProfile_COMMONTechniqueLambert)
                {
                    var profile = technique.Item as effectFx_profile_abstractProfile_COMMONTechniqueLambert;
                    var dae_effect = new DAEEffect();

                    if (profile.emission != null)
                        dae_effect.emission = (profile.emission.Item as common_color_or_texture_typeColor).Values;
                    if (profile.ambient != null)
                        dae_effect.ambient = (profile.ambient.Item as common_color_or_texture_typeColor).Values;
                    if (profile.diffuse != null)
                        dae_effect.diffuse = (profile.diffuse.Item as common_color_or_texture_typeColor).Values;
                    if (profile.reflectivity != null)
                        dae_effect.reflectivity = (profile.reflectivity.Item as common_float_or_param_typeFloat).Value;

                    return dae_effect;
                }
                else if (technique.Item is effectFx_profile_abstractProfile_COMMONTechniquePhong)
                {
                    var dae_effect = new DAEEffect();
                    var profile = technique.Item as effectFx_profile_abstractProfile_COMMONTechniquePhong;

                    if (profile.emission != null)
                        dae_effect.emission = (profile.emission.Item as common_color_or_texture_typeColor).Values;
                    if (profile.ambient != null)
                        dae_effect.ambient = (profile.ambient.Item as common_color_or_texture_typeColor).Values;
                    if (profile.diffuse != null)
                        dae_effect.diffuse = (profile.diffuse.Item as common_color_or_texture_typeColor).Values;
                    if (profile.specular != null)
                        dae_effect.specular = (profile.specular.Item as common_color_or_texture_typeColor).Values;
                    if (profile.reflective != null)
                        dae_effect.reflective = (profile.reflective.Item as common_color_or_texture_typeColor).Values;
                    if (profile.shininess != null)
                        dae_effect.shininess = (profile.shininess.Item as common_float_or_param_typeFloat).Value;
                    if (profile.reflectivity != null)
                        dae_effect.reflectivity = (profile.reflectivity.Item as common_float_or_param_typeFloat).Value;

                    return dae_effect;
                }
            }

            return null;
        }

        private DAEMaterial ProcessDAEMaterial(material material)
        {
            //Debug.Log("*** material: " + material.id);

            var dae_material = new DAEMaterial();
            dae_material.url = material.instance_effect.url.Remove(0, 1);   // Ommit '#'

            return dae_material;
        }

        private DAEGeometry ProcessDAEGeometry(mesh mesh)
        {
            // Dump value_arrays
            var value_arrays = new Dictionary<string, double[]>();
            foreach (var source in mesh.source)
                if (source.Item is float_array)
                    value_arrays[source.id] = (source.Item as float_array).Values;

            // Dump sources of vertices
            var vertices_sources = new Dictionary<string, string>();
            foreach (var input in mesh.vertices.input)
                vertices_sources[input.semantic] = input.source;

            // Dump items of geometry
            var dae_geometry = new DAEGeometry();
            foreach (var item in mesh.Items)
            {
                // Create a set of triangles for each item
                var dae_triangles = new DAEGeometry.Triangles();

                // Get inputs and vertex indices of triangles or polylist
                InputLocalOffset[] inputs;
                string p;
                if (item is triangles)
                {
                    var triangles = item as triangles;

                    inputs = triangles.input;
                    p = triangles.p;
                    dae_triangles.material = triangles.material;

                }
                else if (item is polylist)
                {
                    var polylist = item as polylist;

                    inputs = polylist.input;
                    p = polylist.p;
                    dae_triangles.material = polylist.material;
                }
                else
                    continue;

                // Extract coordinates or vertices and normals
                Vector3[] normals = null;
                foreach (var input in inputs)
                {
                    if (input.semantic == "VERTEX")
                    {
                        var value_array = value_arrays[vertices_sources["POSITION"].Remove(0, 1)];

                        dae_triangles.vertices = new Vector3[value_array.Length / 3];

                        for (int i = 0; i < dae_triangles.vertices.Length; ++i)
                        {
                            dae_triangles.vertices[i].x = (float)value_array[3 * i];
                            dae_triangles.vertices[i].z = (float)value_array[3 * i + 1];
                            dae_triangles.vertices[i].y = (float)value_array[3 * i + 2];
                        }
                    }
                    else if (input.semantic == "NORMAL")
                    {
                        var value_array = value_arrays[input.source.Remove(0, 1)];

                        normals = new Vector3[value_array.Length / 3];

                        for (int i = 0; i < normals.Length; ++i)
                        {
                            normals[i].x = (float)value_array[3 * i];
                            normals[i].z = (float)value_array[3 * i + 1];
                            normals[i].y = (float)value_array[3 * i + 2];
                        }
                    }
                }

                // Extract vertex indices assigned to each triangle
                var delimiters = new char[] { ' ', '\t', '\r', '\n' };
                var indices = p.Split(delimiters, StringSplitOptions.RemoveEmptyEntries).Select(token => Convert.ToInt32(token)).ToArray();
                var ninputs = inputs.Length;
                dae_triangles.triangles = new int[indices.Length / ninputs];

                //Debug.Log("#inputs=" + ninputs + ", #indices=" + indices.Length);

                // Check if all polygons in polylist are triangles
                if (item is polylist)
                {
                    var vcounts = (item as polylist).vcount.Split(delimiters, StringSplitOptions.RemoveEmptyEntries).Select(token => Convert.ToInt32(token)).ToArray();
                    foreach (var vcount in vcounts)
                        if (vcount != 3)
                            Debug.Log("Not a triangle! vcount=" + vcount);
                }

                // Assign vertex indices to each triangle
                for (int i = 0; i < dae_triangles.triangles.Length; i += 3)
                {
                    dae_triangles.triangles[i] = indices[ninputs * i];
                    dae_triangles.triangles[i + 2] = indices[ninputs * (i + 1)];
                    dae_triangles.triangles[i + 1] = indices[ninputs * (i + 2)];
                }

                if (ninputs > 1 && normals != null)
                {
                    //Debug.Log("#normals=" + normals.Length);
                    dae_triangles.normals = new Vector3[dae_triangles.vertices.Length];

                    for (int i = 0; i < indices.Length; i += ninputs)
                        dae_triangles.normals[indices[i]] = normals[indices[i + 1]];
                }
                else
                    dae_triangles.normals = new Vector3[0];

                // Debug.Log("*** " + dae_triangles.triangles.Length / 3 + " triangles, " + dae_triangles.vertices.Length + " vertices, " + indices.Length + " indices");

                dae_geometry.triangles_list.Add(dae_triangles);
            }

            return dae_geometry;
        }

        private DAEVisualScene ProcessDAEVisualScene(visual_scene visual_scene)
        {
            var dae_visual_scene = new DAEVisualScene();

            foreach (var node in visual_scene.node)
            {
                // Only interested in nodes with instance_geometries
                if (node.instance_geometry == null)
                    continue;

                var dae_node = new DAEVisualScene.Node();

                if (node.ItemsElementName != null)
                {
                    foreach (var item in node.Items)
                        if (item is matrix)
                        {
                            var mat = item as matrix;                        // var matrix4x4 = new Matrix4x4();                        Debug.Log("*** matrix found!");
                            for (int i = 0; i < 4; ++i)
                                for (int j = 0; j < 4; ++j)
                                    dae_node.matrix4x4[i, j] = (float)mat.Values[4 * i + j];                        // dae_node.matrix4x4 = matrix4x4;
                        }
                }

                foreach (var instance_geometry in node.instance_geometry)
                {
                    var dae_instance_geometry = new DAEVisualScene.Node.InstanceGeometry();
                
                    if (instance_geometry.bind_material != null && instance_geometry.bind_material.technique_common != null)
                    {
                        foreach (var technique_common in instance_geometry.bind_material.technique_common)
                            dae_instance_geometry.instance_material_targets[technique_common.symbol] = technique_common.target.Remove(0, 1);                    
                    }
                    dae_node.instance_geometries.Add(instance_geometry.url.Remove(0, 1), dae_instance_geometry);
                }

                dae_visual_scene.nodes.Add(node.id, dae_node);
            }

            return dae_visual_scene;
        }
    }
}