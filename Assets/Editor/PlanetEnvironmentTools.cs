using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace Protocol.Editor
{
    public static class PlanetEnvironmentTools
    {
        private const string Folder = "Assets/Art/PlanetStudy";

        [MenuItem("Protocol/Apply Detailed Planet Environment")]
        public static void Apply()
        {
            if (Application.isPlaying)
                throw new System.InvalidOperationException("Stop Play Mode before rebuilding the environment.");
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != "Assets/Scenes/Stage01.unity")
                throw new System.InvalidOperationException("Open Stage01 first.");
            var bootstrap = Object.FindAnyObjectByType<ScenarioBootstrap>();
            var world = bootstrap.transform.Find("World");
            if (world == null)
                throw new System.InvalidOperationException("Prepare the scenario before applying this pass.");
            if (!AssetDatabase.IsValidFolder(Folder))
                AssetDatabase.CreateFolder("Assets/Art", "PlanetStudy");

            var ground = Material("Mineral regolith", new Color(.25f, .29f, .3f), 1, Color.black);
            ground.SetFloat("_TerrainBlend", 1);
            var rock = Material("Weathered basalt", new Color(.23f, .28f, .32f), 1, Color.black);
            var teal = Material("Blue silver leaves", new Color(.22f, .49f, .46f), .25f, new Color(.005f,.028f,.025f));
            var rose = Material("Rose succulent", new Color(.47f, .22f, .32f), .2f, new Color(.035f,.006f,.017f));
            var bark = Material("Fibrous stalk", new Color(.18f,.21f,.22f), .85f, Color.black);
            teal.SetFloat("_WindStrength", .035f);
            rose.SetFloat("_WindStrength", .025f);
            var fern = SaveMesh(Leaves(32, 13), "Curved leaf rosette");
            var fernMedium = SaveMesh(Leaves(16, 13, 5), "Curved leaf rosette medium");
            var fernFar = SaveMesh(Leaves(8, 13, 3), "Curved leaf rosette distant");
            var grasses = SaveMesh(Leaves(17, 37), "Fine leaf tuft");
            var rocks = new Mesh[5];
            for (int i = 0; i < rocks.Length; i++)
                rocks[i] = SaveMesh(Boulder(i), "Basalt " + i);

            var terrain = world.Find("Terrain").GetComponent<MeshRenderer>();
            Undo.RecordObject(terrain, "Texture terrain");
            terrain.sharedMaterials = new[] { ground, ground, ground };
            world.Find("Terrain").GetComponent<MeshFilter>().sharedMesh.RecalculateTangents();
            EditorUtility.SetDirty(world.Find("Terrain").GetComponent<MeshFilter>().sharedMesh);

            var ore = Material("Iron mineral deposit", new Color(.42f, .28f, .18f), 1, new Color(.015f,.006f,.001f));
            ore.SetFloat("_Metallic", .42f);
            int mineralIndex = 0;
            foreach (Transform mineral in world.Find("Mine"))
            {
                if (mineral.name != "Ore crystal") continue;
                var filter = mineral.GetComponent<MeshFilter>();
                var renderer = mineral.GetComponent<MeshRenderer>();
                Undo.RecordObject(filter, "Weather mineral deposits");
                Undo.RecordObject(renderer, "Texture mineral deposits");
                filter.sharedMesh = rocks[mineralIndex++ % rocks.Length];
                renderer.sharedMaterial = ore;
            }

            DensifyForest(world.Find("Trees"));
            int trees = 0;
            foreach (Transform tree in world.Find("Trees"))
            {
                foreach (Transform part in tree)
                {
                    var renderer = part.GetComponent<MeshRenderer>();
                    if (renderer == null) continue;
                    Undo.RecordObject(renderer, "Replace vegetation material");
                    if (part.name == "Trunk") { renderer.sharedMaterial = bark; continue; }
                    var filter = part.GetComponent<MeshFilter>();
                    Undo.RecordObject(filter, "Replace conical crown");
                    filter.sharedMesh = fern;
                    renderer.sharedMaterial = trees % 4 == 0 ? rose : teal;
                    ConfigureCrownLod(part, renderer, fernMedium, fernFar);
                }
                trees++;
            }

            var old = world.Find("Planet detail study");
            if (old != null) Undo.DestroyObjectImmediate(old.gameObject);
            var root = new GameObject("Planet detail study");
            Undo.RegisterCreatedObjectUndo(root, "Create planet details");
            root.transform.SetParent(world, false);
            var random = new System.Random(7483);
            for (int i = 0; i < 700; i++)
            {
                float x = Range(random, -58, 58), z = Range(random, -58, 58);
                float radius = new Vector2(x,z).magnitude;
                if (radius > 60 || radius < 15 || Mathf.Abs(z - Mathf.Sin(x / 30) * 5) < 6)
                    continue;
                if (x > -16 && x < 16 && z > -12 && z < 12) continue;
                if (Mathf.PerlinNoise(x * .11f + 73, z * .11f + 32) < .42f) continue;
                bool isRock = i % 7 == 0;
                float scale = isRock ? Range(random, .35f, 1.5f) : Range(random, .25f, .85f);
                var item = new GameObject(isRock ? "Basalt outcrop" : "Understory rosette");
                item.transform.SetParent(root.transform, false);
                item.transform.localPosition = new Vector3(x, ScenarioLandscape.HeightAt(x,z) - .04f, z);
                item.transform.localRotation = Quaternion.Euler(0, Range(random,0,360), 0);
                item.transform.localScale = new Vector3(scale, scale * (isRock ? .7f : 1.3f), scale);
                item.AddComponent<MeshFilter>().sharedMesh = isRock ? rocks[i % rocks.Length] : grasses;
                if (isRock)
                    ScenarioNavigation.EnsureBoulderCollider(item);
                var renderer = item.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = isRock ? rock : i % 5 == 0 ? rose : teal;
                renderer.shadowCastingMode = isRock ? ShadowCastingMode.On : ShadowCastingMode.Off;
            }

            BuildGrass(world);
            EditorUtility.SetDirty(ground);
            AlienWorldLighting.Ensure(world);
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"Planet study: {trees} harvestable plants upgraded, {root.transform.childCount} detail clusters added.");
        }

        private static float Range(System.Random random, float min, float max) => min + (float)random.NextDouble() * (max - min);

        private static void BuildGrass(Transform world)
        {
            var previous = world.Find("Planet grass");
            if (previous != null)
                Undo.DestroyObjectImmediate(previous.gameObject);
            var root = new GameObject("Planet grass").transform;
            root.SetParent(world, false);
            Undo.RegisterCreatedObjectUndo(root.gameObject, "Create batched grass");
            var material = Material("Silver violet grass", new Color(.25f, .38f, .34f), 0, Color.black);
            material.SetFloat("_WindStrength", .045f);
            material.SetFloat("_Glossiness", .12f);
            var random = new System.Random(20260921);
            for (int z = 0; z < 16; z++)
                for (int x = 0; x < 16; x++)
                {
                    var origin = new Vector3(x * 16 - 128, 0, z * 16 - 128);
                    var points = new List<Vector3>();
                    for (int sample = 0; sample < 100; sample++)
                    {
                        float px = origin.x + Range(random, .5f, 15.5f);
                        float pz = origin.z + Range(random, .5f, 15.5f);
                        if (new Vector2(px, pz).magnitude < 16 ||
                            Mathf.Abs(pz - Mathf.Sin(px / 30) * 5) < 5)
                            continue;
                        if (Mathf.PerlinNoise(px * .07f + 17, pz * .07f + 29) < .36f)
                            continue;
                        points.Add(new Vector3(px, ScenarioLandscape.HeightAt(px, pz) - .03f, pz) - origin);
                    }
                    if (points.Count == 0)
                        continue;
                    var tile = new GameObject($"Grass {x:00}-{z:00}").transform;
                    tile.SetParent(root, false);
                    tile.localPosition = origin;
                    var high = SaveMesh(GrassMesh(points, 1, x + z * 16), $"Grass {x:00}-{z:00} near");
                    var low = SaveMesh(GrassMesh(points, 4, x + z * 16), $"Grass {x:00}-{z:00} far");
                    var nearRenderer = LodRenderer(tile, "Near grass", high, material);
                    var farRenderer = LodRenderer(tile, "Far grass", low, material);
                    nearRenderer.shadowCastingMode = farRenderer.shadowCastingMode = ShadowCastingMode.Off;
                    var lod = tile.gameObject.AddComponent<LODGroup>();
                    lod.SetLODs(new[]
                    {
                        new LOD(.24f, new Renderer[] { nearRenderer }),
                        new LOD(.085f, new Renderer[] { farRenderer })
                    });
                    lod.RecalculateBounds();
                }
            EditorUtility.SetDirty(material);
        }

        private static Mesh GrassMesh(List<Vector3> points, int stride, int seed)
        {
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            var uv = new List<Vector2>();
            for (int tuft = 0; tuft < points.Count; tuft += stride)
            {
                var random = new System.Random(seed * 1000 + tuft);
                for (int blade = 0; blade < 5; blade++)
                {
                    float angle = Range(random, 0, Mathf.PI * 2);
                    var side = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * Range(random, .045f, .09f);
                    var bend = new Vector3(-Mathf.Sin(angle), 0, Mathf.Cos(angle)) * Range(random, .15f, .4f);
                    float height = Range(random, .55f, 1.1f);
                    var p = points[tuft];
                    int start = vertices.Count;
                    vertices.Add(p - side);
                    vertices.Add(p + side);
                    vertices.Add(p + Vector3.up * height * .55f + bend * .3f - side * .6f);
                    vertices.Add(p + Vector3.up * height * .55f + bend * .3f + side * .6f);
                    vertices.Add(p + Vector3.up * height + bend);
                    uv.Add(new Vector2(0, 0));
                    uv.Add(new Vector2(1, 0));
                    uv.Add(new Vector2(0, .55f));
                    uv.Add(new Vector2(1, .55f));
                    uv.Add(new Vector2(.5f, 1));
                    triangles.AddRange(new[] { start, start + 2, start + 1,
                        start + 1, start + 2, start + 3, start + 2, start + 4, start + 3 });
                }
            }
            int count = vertices.Count, indexCount = triangles.Count;
            vertices.AddRange(vertices.ToArray());
            uv.AddRange(uv.ToArray());
            for (int i = 0; i < indexCount; i += 3)
                triangles.AddRange(new[] { triangles[i + 2] + count, triangles[i + 1] + count, triangles[i] + count });
            var mesh = new Mesh();
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uv);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void DensifyForest(Transform forest)
        {
            var positions = new List<Vector2>();
            var templates = new List<Transform>();
            foreach (Transform tree in forest)
            {
                positions.Add(new Vector2(tree.position.x, tree.position.z));
                templates.Add(tree);
            }
            if (templates.Count == 0)
                return;
            var random = new System.Random(20260920);
            for (int attempt = 0; attempt < 100000 && positions.Count < 1680; attempt++)
            {
                float x = Range(random, -118, 118), z = Range(random, -118, 118);
                var position = new Vector2(x, z);
                if (position.magnitude < 28 || Mathf.Abs(z - Mathf.Sin(x / 30) * 5) < 7)
                    continue;
                if (Mathf.PerlinNoise((x + 300) / 32, (z + 200) / 32) < .43f)
                    continue;
                bool occupied = positions.Exists(p => (p - position).sqrMagnitude < 7.84f);
                if (occupied)
                    continue;
                var tree = Object.Instantiate(templates[random.Next(templates.Count)].gameObject, forest);
                Undo.RegisterCreatedObjectUndo(tree, "Densify forest");
                tree.name = "Xenoflora " + (positions.Count + 1);
                tree.transform.localPosition = new Vector3(x, ScenarioLandscape.HeightAt(x, z) - .08f, z);
                tree.transform.localRotation = Quaternion.Euler(0, Range(random, 0, 360), 0);
                tree.transform.localScale = Vector3.one * Range(random, .8f, 1.1f);
                positions.Add(position);
            }
        }

        private static Material Material(string name, Color color, float detail, Color emission)
        {
            string path = Folder + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Protocol/FogSurface"));
                AssetDatabase.CreateAsset(material, path);
            }
            material.color = color;
            material.SetFloat("_SurfaceDetail", detail);
            material.SetFloat("_Glossiness", .24f);
            material.SetFloat("_Metallic", .05f);
            material.SetColor("_EmissionColor", emission);
            material.EnableKeyword("_EMISSION");
            material.enableInstancing = true;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Mesh SaveMesh(Mesh mesh, string name)
        {
            mesh.name = name;
            string path = Folder + "/" + name + ".asset";
            var saved = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (saved == null) { AssetDatabase.CreateAsset(mesh, path); return mesh; }
            EditorUtility.CopySerialized(mesh, saved);
            Object.DestroyImmediate(mesh);
            EditorUtility.SetDirty(saved);
            return saved;
        }

        private static Mesh Leaves(int count, int seed, int segments = 10)
        {
            var vertices = new List<Vector3>();
            var uv = new List<Vector2>();
            var triangles = new List<int>();
            var random = new System.Random(seed);
            for (int leaf = 0; leaf < count; leaf++)
            {
                float angle = leaf * 2.399963f;
                var outward = new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle));
                var side = Vector3.Cross(Vector3.up,outward);
                float spread = Range(random,.32f,1.05f);
                float height = Range(random,.4f,1.2f);
                float width = Range(random,.06f,.15f);
                int start = vertices.Count;
                for (int j = 0; j <= segments; j++)
                {
                    float t = j / (float)segments;
                    var center = outward * (spread * t * t) + Vector3.up * (height * (t - .48f * t * t));
                    float w = Mathf.Sin(Mathf.PI * t) * width + .001f;
                    vertices.Add(center - side * w);
                    vertices.Add(center + Vector3.up * w * .25f);
                    vertices.Add(center + side * w);
                    uv.Add(new Vector2(0,t)); uv.Add(new Vector2(.5f,t)); uv.Add(new Vector2(1,t));
                    if (j == segments) continue;
                    for (int k = 0; k < 2; k++)
                    {
                        int a = start + j * 3 + k;
                        triangles.AddRange(new[] { a, a+3, a+1, a+1, a+3, a+4 });
                    }
                }
            }
            int vertexCount = vertices.Count, indexCount = triangles.Count;
            vertices.AddRange(vertices.ToArray()); uv.AddRange(uv.ToArray());
            for (int i = 0; i < indexCount; i += 3)
                triangles.AddRange(new[] { triangles[i+2]+vertexCount, triangles[i+1]+vertexCount, triangles[i]+vertexCount });
            var mesh = new Mesh();
            mesh.SetVertices(vertices); mesh.SetUVs(0,uv); mesh.SetTriangles(triangles,0);
            mesh.RecalculateNormals(); mesh.RecalculateTangents(); mesh.RecalculateBounds();
            return mesh;
        }

        private static void ConfigureCrownLod(Transform crown, MeshRenderer detailed, Mesh medium, Mesh distant)
        {
            var group = crown.GetComponent<LODGroup>();
            if (group == null)
                group = crown.gameObject.AddComponent<LODGroup>();
            var mediumRenderer = LodRenderer(crown, "Medium crown", medium, detailed.sharedMaterial);
            var distantRenderer = LodRenderer(crown, "Distant crown", distant, detailed.sharedMaterial);
            distantRenderer.shadowCastingMode = ShadowCastingMode.Off;
            group.SetLODs(new[]
            {
                new LOD(.20f, new Renderer[] { detailed }),
                new LOD(.07f, new Renderer[] { mediumRenderer }),
                new LOD(.005f, new Renderer[] { distantRenderer })
            });
            group.RecalculateBounds();
        }

        private static MeshRenderer LodRenderer(Transform parent, string name, Mesh mesh, Material material)
        {
            var child = parent.Find(name);
            if (child == null)
            {
                child = new GameObject(name).transform;
                child.SetParent(parent, false);
                child.gameObject.AddComponent<MeshFilter>();
                child.gameObject.AddComponent<MeshRenderer>();
            }
            child.GetComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = child.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            return renderer;
        }

        private static Mesh Boulder(int seed)
        {
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            var uv = new List<Vector2>();
            const int rings = 16, sides = 24;
            for (int y = 0; y <= rings; y++)
                for (int x = 0; x <= sides; x++)
                {
                    float phi = Mathf.PI * y / rings, theta = 2 * Mathf.PI * x / sides;
                    var p = new Vector3(Mathf.Sin(phi)*Mathf.Cos(theta),Mathf.Cos(phi),Mathf.Sin(phi)*Mathf.Sin(theta));
                    float noise = Mathf.PerlinNoise(p.x * 2 + seed * 7 + 31, p.z * 2 + p.y * 1.7f + 47);
                    p *= .72f + noise * .42f;
                    p.y = Mathf.Max(-.45f,p.y) + .42f;
                    vertices.Add(p); uv.Add(new Vector2(x/(float)sides,y/(float)rings));
                    if (y == rings || x == sides) continue;
                    int a = y * (sides+1) + x;
                    triangles.AddRange(new[] {a,a+1,a+sides+1,a+1,a+sides+2,a+sides+1});
                }
            var mesh = new Mesh();
            mesh.SetVertices(vertices); mesh.SetUVs(0,uv); mesh.SetTriangles(triangles,0);
            mesh.RecalculateNormals(); mesh.RecalculateTangents(); mesh.RecalculateBounds();
            return mesh;
        }
    }
}
