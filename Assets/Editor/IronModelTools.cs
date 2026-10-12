using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Protocol.Editor
{
    public static class IronModelTools
    {
        private const string Folder = "Assets/Art/Resources/Deposits";

        [MenuItem("Protocol/Art/Rebuild Iron Deposit Model")]
        public static void Generate()
        {
            System.IO.Directory.CreateDirectory(Folder);
            AssetDatabase.Refresh();
            var colors = new[] { new Color(.18f, .135f, .105f), new Color(.27f, .045f, .018f), new Color(.32f, .115f, .04f) };
            var names = new[] { "Host rock", "Iron ore", "Oxidized ore" };
            var materials = new Material[3];
            for (int i = 0; i < 3; i++)
            {
                string path = Folder + "/" + names[i] + ".mat";
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null)
                {
                    material = new Material(Resources.Load<Shader>("FogSurface")) { name = names[i] };
                    AssetDatabase.CreateAsset(material, path);
                }
                material.color = colors[i];
                material.SetFloat("_SurfaceDetail", i == 0 ? 1 : .8f);
                material.SetFloat("_Glossiness", i == 0 ? .12f : .28f);
                material.SetFloat("_Metallic", i == 0 ? .04f : .28f);
                material.SetColor("_EmissionColor", Color.black);
                material.enableInstancing = true;
                EditorUtility.SetDirty(material);
                materials[i] = material;
            }
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var uv = new List<Vector2>();
            var triangles = new[] { new List<int>(), new List<int>(), new List<int>() };
            var rocks = new Mesh[5];
            for (int i = 0; i < 5; i++) rocks[i] = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Art/PlanetStudy/Basalt " + i + ".asset");
            var random = new System.Random(6311);
            for (int i = 0; i < 54; i++)
            {
                bool rubble = i >= 19;
                float angle = (float)random.NextDouble() * Mathf.PI * 2;
                float radius = i < 5 ? .45f + i * .24f : rubble ? 2.3f + (float)random.NextDouble() * .85f : 1.65f + (float)random.NextDouble() * .8f;
                var position = new Vector3(Mathf.Cos(angle) * radius, -.05f, Mathf.Sin(angle) * radius * .85f);
                float size = rubble ? .1f + (float)random.NextDouble() * .24f : i < 5 ? 1.1f : .5f + (float)random.NextDouble() * .4f;
                var scale = new Vector3(size * (1 + (float)random.NextDouble() * .3f),
                    size * (i < 5 ? 1.2f + (float)random.NextDouble() * .45f : .7f + (float)random.NextDouble()), size);
                var rotation = Quaternion.Euler((float)random.NextDouble() * 12, (float)random.NextDouble() * 360, (float)random.NextDouble() * 12);
                var mesh = rocks[i % 5];
                var points = mesh.vertices;
                var indices = mesh.triangles;
                var sourceNormals = mesh.normals;
                for (int t = 0; t < indices.Length; t += 3)
                {
                    Vector3 a = points[indices[t]], b = points[indices[t + 1]], c = points[indices[t + 2]];
                    Vector3 center = (a + b + c) / 3;
                    float patch = Mathf.PerlinNoise(center.x * 2.4f + i * 3.7f + 13, center.z * 2.4f + center.y * 1.4f + 27);
                    int material = patch > .63f ? 1 : patch > .55f ? 2 : 0;
                    if (rubble && i % 5 == 0) material = 1;
                    Vector3 pa = position + rotation * Vector3.Scale(a, scale);
                    Vector3 pb = position + rotation * Vector3.Scale(b, scale);
                    Vector3 pc = position + rotation * Vector3.Scale(c, scale);
                    var normal = Vector3.Cross(pb - pa, pc - pa).normalized;
                    int start = vertices.Count;
                    vertices.Add(pa); vertices.Add(pb); vertices.Add(pc);
                    for (int corner = 0; corner < 3; corner++)
                    {
                        var sourceNormal = sourceNormals[indices[t + corner]];
                        var transformedNormal = rotation * new Vector3(sourceNormal.x / scale.x, sourceNormal.y / scale.y, sourceNormal.z / scale.z);
                        normals.Add(Vector3.Lerp(normal, transformedNormal.normalized, .8f).normalized);
                    }
                    uv.Add(new Vector2(a.x, a.y)); uv.Add(new Vector2(b.x, b.y)); uv.Add(new Vector2(c.x, c.y));
                    triangles[material].AddRange(new[] { start, start + 1, start + 2 });
                }
            }
            var combined = new Mesh { name = "Iron deposit", indexFormat = IndexFormat.UInt32, subMeshCount = 3 };
            combined.SetVertices(vertices); combined.SetNormals(normals); combined.SetUVs(0, uv);
            for (int i = 0; i < 3; i++) combined.SetTriangles(triangles[i], i);
            combined.RecalculateBounds();
            string meshPath = Folder + "/Iron deposit.asset";
            var stored = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            if (stored != null)
            {
                EditorUtility.CopySerialized(combined, stored);
                EditorUtility.SetDirty(stored);
                Object.DestroyImmediate(combined);
            }
            else { AssetDatabase.CreateAsset(combined, meshPath); stored = combined; }
            AssetDatabase.SaveAssets();
            var root = new GameObject("Iron deposit");
            try
            {
                root.AddComponent<MeshFilter>().sharedMesh = stored;
                root.AddComponent<MeshRenderer>().sharedMaterials = materials;
                PrefabUtility.SaveAsPrefabAsset(root, Folder + "/IronDeposit.prefab");
                AssetDatabase.SaveAssets();
                Debug.Log("Iron deposit baked: " + vertices.Count / 3 + " triangles, three material groups.");
            }
            finally { Object.DestroyImmediate(root); }
        }
    }
}