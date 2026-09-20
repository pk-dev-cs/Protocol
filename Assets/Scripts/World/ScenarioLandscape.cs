using System.Collections.Generic;
using UnityEngine;

namespace Protocol
{
    /// <summary>Deterministic alien terrain and harvestable xenoflora for the first scenario.</summary>
    public sealed class ScenarioLandscape : MonoBehaviour
    {
        public const float Size = 256;
        public const float HalfSize = Size / 2;
        private readonly List<Object> owned = new List<Object>();
        public static void Ensure(Transform world)

        {
            if (world.GetComponent<ScenarioLandscape>() != null)
                return;
            var landscape = world.gameObject.AddComponent<ScenarioLandscape>();
            landscape.Generate();
        }

        public void Regenerate() => Generate();

        public static float HeightAt(float x, float z)

        {
            float radius = Mathf.Sqrt(x * x + z * z);
            float clearing = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(23, 43, radius));
            float broad = Mathf.PerlinNoise((x + 351) / 64, (z + 173) / 64) * 9;
            float detail = Mathf.PerlinNoise((x + 81) / 23, (z + 449) / 23) * 2;
            float hillA = 13 * Mathf.Exp(-((x + 66) * (x + 66) + (z - 48) * (z - 48)) / 780);
            float hillB = 16 * Mathf.Exp(-((x - 73) * (x - 73) + (z - 66) * (z - 66)) / 1000);
            float hillC = 10 * Mathf.Exp(-((x - 37) * (x - 37) + (z + 72) * (z + 72)) / 700);
            float ridges = Mathf.Abs(Mathf.PerlinNoise((x + 510) / 41, (z + 290) / 41) - .5f) * 3.2f;
            return clearing * (broad + detail + ridges + hillA + hillB + hillC);
        }

        private Material MakeMaterial(string name, Color color)

        {
            var material = new Material(Resources.Load<Shader>("FogSurface"))
            {
                name = name,
                color = color
            };
            material.enableInstancing = true;
            material.SetFloat("_Glossiness", .16f);
            material.SetFloat("_Metallic", .08f);
            if (name.StartsWith("Alien"))
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", color * .28f);
            }
            owned.Add(material);
            return material;
        }

        private void RemoveOld(string name)

        {
            var old = transform.Find(name);
            if (old == null)
                return;
            old.gameObject.SetActive(false);
            if (Application.isPlaying)
                Destroy(old.gameObject);
            else
                DestroyImmediate(old.gameObject);
        }

        private void Generate()

        {
            RemoveOld("Terrain");
            RemoveOld("Terrain markings");
            RemoveOld("Trees");
            new GameObject("Terrain markings").transform.SetParent(transform, false);
            var grass = MakeMaterial("Alien Lowlands", new Color(.055f, .18f, .23f));
            var earth = MakeMaterial("Alien Clearing", new Color(.22f, .13f, .31f));
            var stone = MakeMaterial("Alien Highlands", new Color(.105f, .12f, .25f));
            const int cells = 128;
            var vertices = new Vector3[(cells + 1) * (cells + 1)];
            var uv = new Vector2[vertices.Length];
            var triangles = new[]
            {
                new List<int>(),
                new List<int>(),
                new List<int>()
            };
            for (int z = 0; z <= cells; z++)
                for (int x = 0; x <= cells; x++)
                {
                    float px = x * (Size / cells) - HalfSize, pz = z * (Size / cells) - HalfSize;
                    int i = z * (cells + 1) + x;
                    vertices[i] = new Vector3(px, HeightAt(px, pz), pz);
                    uv[i] = new Vector2(x / (float)cells, z / (float)cells);
                }

            for (int z = 0; z < cells; z++)
                for (int x = 0; x < cells; x++)
                {
                    int i = z * (cells + 1) + x;
                    var center = (vertices[i] + vertices[i + cells + 2]) * .5f;
                    float road = Mathf.Abs(center.z - Mathf.Sin(center.x / 30) * 5);
                    int material = center.y > 13 ? 2 : (new Vector2(center.x, center.z).magnitude < 21 || road < 3.5f ? 1 : 0);
                    triangles[material].AddRange(new[] { i, i + cells + 1, i + 1, i + 1, i + cells + 1, i + cells + 2 });
                }

            var mesh = new Mesh
            {
                name = "Scenario Landscape Surface",
                vertices = vertices,
                uv = uv,
                subMeshCount = 3
            };
            for (int i = 0; i < 3; i++)
                mesh.SetTriangles(triangles[i], i);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            owned.Add(mesh);
            var terrain = new GameObject("Terrain");
            terrain.transform.SetParent(transform, false);
            terrain.AddComponent<MeshFilter>().sharedMesh = mesh;
            terrain.AddComponent<MeshRenderer>().sharedMaterials = new[]
            {
                grass,
                earth,
                stone
            };
            terrain.AddComponent<MeshCollider>().sharedMesh = mesh;
            BuildForest();
        }

        private void BuildForest()

        {
            var root = new GameObject("Trees").transform;
            root.SetParent(transform, false);
            var bark = MakeMaterial("Alien Stalk", new Color(.11f, .07f, .18f));
            var leaves = new[]
            {
                MakeMaterial("Alien Crown Cyan", new Color(.13f, .62f, .72f)),
                MakeMaterial("Alien Crown Rose", new Color(.72f, .18f, .48f)),
                MakeMaterial("Alien Crown Violet", new Color(.38f, .20f, .68f))
            };
            var cone = MakeCone();
            owned.Add(cone);
            var random = new System.Random(104729);
            var positions = new List<Vector2>();
            for (int attempt = 0; attempt < 9000 && positions.Count < 420; attempt++)
            {
                float x = (float)random.NextDouble() * 236 - 118, z = (float)random.NextDouble() * 236 - 118;
                if (new Vector2(x, z).magnitude < 28 || Mathf.Abs(z - Mathf.Sin(x / 30) * 5) < 7)
                    continue;
                if (Mathf.PerlinNoise((x + 300) / 32, (z + 200) / 32) < .43f)
                    continue;
                bool near = false;
                foreach (var p in positions)
                    if ((p - new Vector2(x, z)).sqrMagnitude < 22)
                    {
                        near = true;
                        break;
                    }

                if (near)
                    continue;
                positions.Add(new Vector2(x, z));
                float height = 5.5f + (float)random.NextDouble() * 5;
                var tree = new GameObject("Xenoflora " + positions.Count).transform;
                tree.SetParent(root, false);
                tree.localPosition = new Vector3(x, HeightAt(x, z) - .08f, z);
                var trunk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                trunk.name = "Trunk";
                trunk.transform.SetParent(tree, false);
                trunk.transform.localPosition = Vector3.up * height * .29f;
                trunk.transform.localScale = new Vector3(.42f, height * .29f, .42f);
                trunk.GetComponent<Renderer>().sharedMaterial = bark;
                // The canopy is visual only; the trunk blocks walking and raycasts.
                for (int tier = 0; tier < 3; tier++)
                {
                    var crown = new GameObject("Crown");
                    crown.transform.SetParent(tree, false);
                    crown.transform.localPosition = new Vector3((tier - 1) * .16f, height * (.34f + tier * .18f), tier % 2 == 0 ? .12f : -.12f);
                    float radius = height * (.23f - tier * .025f);
                    crown.transform.localScale = new Vector3(radius, height * (.34f - tier * .025f), radius);
                    crown.AddComponent<MeshFilter>().sharedMesh = cone;
                    crown.AddComponent<MeshRenderer>().sharedMaterial = leaves[random.Next(leaves.Length)];
                }
            }
        }

        private static Mesh MakeCone()

        {
            const int sides = 7;
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            for (int i = 0; i < sides; i++)
            {
                float a = i * Mathf.PI * 2 / sides, b = (i + 1) * Mathf.PI * 2 / sides;
                int first = vertices.Count;
                vertices.Add(new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)));
                vertices.Add(Vector3.up);
                vertices.Add(new Vector3(Mathf.Cos(b), 0, Mathf.Sin(b)));
                triangles.Add(first);
                triangles.Add(first + 1);
                triangles.Add(first + 2);
            }

            var mesh = new Mesh
            {
                name = "Scenario Pine Crown"
            };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            return mesh;
        }

        private void OnDestroy()

        {
            if (!Application.isPlaying)
                return;
            foreach (var item in owned)
                if (item != null)
                    Destroy(item);
        }
    }
}
