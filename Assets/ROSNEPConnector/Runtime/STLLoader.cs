using UnityEngine;
using System;
using System.Collections.Generic;
using System.Text;

namespace ROSNEPConnector
{
    public class STLLoader : MonoBehaviour
    {
        [SerializeField] private bool recalculateNormals;

        public void CreateMeshFromSTL(GameObject element, byte[] data, bool collision=false)
        {
            if (IsBinarySTL(data))
                CreateMeshFromBinarySTL(element, data, collision);
            else
                CreateMeshFromAsciiSTL(element, data, collision);

        }

        private bool IsBinarySTL(byte[] data)
        {
            // Assume binary format and read the potential number of faces.
            var nfaces = BitConverter.ToInt32(data, 80);  // Restore #faces
            return data.Length == 50 * nfaces + 84;
        }

        private void CreateMeshFromAsciiSTL(GameObject element, byte[] data, bool collision)
        {
            var triangles = new List<int>();
            var vertices = new List<Vector3>();
            var vindices = new Dictionary<Vector3, int>();
            var normals = new List<Vector3>();
            var normal = new Vector3();

            var delimiters = new char[] { ' ', '\t', '\r', '\n' };
            var tokens = Encoding.ASCII.GetString(data).Split(delimiters, StringSplitOptions.RemoveEmptyEntries);

            for (int i = 0; i < tokens.Length; ++i)
            {
                if (tokens[i] == "normal")
                {
                    normal.x = Convert.ToSingle(tokens[++i]);
                    normal.z = Convert.ToSingle(tokens[++i]);
                    normal.y = Convert.ToSingle(tokens[++i]);
                }
                else if (tokens[i] == "loop")
                {
                    var triangle = new int[3];

                    for (int e = 3; --e >= 0;)
                    {
                        ++i;  // Skip "vertex"

                        var vertex = new Vector3();
                        vertex.x = Convert.ToSingle(tokens[++i]);
                        vertex.z = Convert.ToSingle(tokens[++i]);
                        vertex.y = Convert.ToSingle(tokens[++i]);

                        if (!vindices.TryGetValue(vertex, out triangle[e]))
                        {
                            triangle[e] = vertices.Count;
                            vindices[vertex] = vertices.Count;
                            vertices.Add(vertex);
                            normals.Add(normal);
                        }
                    }

                    triangles.Add(triangle[0]);
                    triangles.Add(triangle[1]);
                    triangles.Add(triangle[2]);
                }
            }

            var mesh = (collision ? new Mesh() : element.GetComponent<MeshFilter>().mesh);
            if (vertices.Count > 60000)
                mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            if (collision)
            {
                mesh.name = element.name + "/collider";
                element.GetComponent<MeshCollider>().sharedMesh = mesh;
            }
            else
            {
                mesh.name = element.name;

                if (recalculateNormals)
                    mesh.RecalculateNormals();
                else
                    mesh.SetNormals(normals);
                mesh.RecalculateBounds();
                mesh.RecalculateTangents();
            }
        }

        private void CreateMeshFromBinarySTL(GameObject element, byte[] data, bool collision)
        {
            int idx = 80;   // Skip first 80 bytes.
            var nfaces = BitConverter.ToInt32(data, idx);  // Restore #faces
            idx += sizeof(int);

            var triangles = new int[3 * nfaces];
            var vertices = new List<Vector3>();
            var vindices = new Dictionary<Vector3, int>(); // (new Vector3EqualityComparer());
            var normals = new List<Vector3>();

            for (int i = 0; i < nfaces; ++i)
            {
                var normal = new Vector3();
                normal.x = BitConverter.ToSingle(data, idx);
                idx += sizeof(float);
                normal.z = BitConverter.ToSingle(data, idx);
                idx += sizeof(float);
                normal.y = BitConverter.ToSingle(data, idx);
                idx += sizeof(float);

                for (int e = 3; --e >= 0;)
                {
                    var vertex = new Vector3();
                    vertex.x = BitConverter.ToSingle(data, idx);
                    idx += sizeof(float);
                    vertex.z = BitConverter.ToSingle(data, idx);
                    idx += sizeof(float);
                    vertex.y = BitConverter.ToSingle(data, idx);
                    idx += sizeof(float);

                    if (!vindices.TryGetValue(vertex, out triangles[3 * i + e]))
                    {
                        triangles[3 * i + e] = vertices.Count;
                        vindices[vertex] = vertices.Count;
                        vertices.Add(vertex);
                        normals.Add(normal);
                    }
                }

                idx += sizeof(UInt16);  // Skip flags
            }
 
            var mesh = (collision ? new Mesh() : element.GetComponent<MeshFilter>().mesh);
            if (vertices.Count > 60000)
                mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            if (collision)
            {
                mesh.name = element.name + "/collider";
                element.GetComponent<MeshCollider>().sharedMesh = mesh;
            }
            else
            {
                mesh.name = element.name;

                if (recalculateNormals)
                    mesh.RecalculateNormals();
                else
                    mesh.SetNormals(normals);
                mesh.RecalculateBounds();
                mesh.RecalculateTangents();
            }
        }
    }
}
