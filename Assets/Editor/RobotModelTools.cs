using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Protocol.Editor
{
    public static class RobotModelTools
    {
        private const string Folder = "Assets/Art/Resources/Units";
        private static readonly string[] Names = { "Armor", "Frame", "Rubber", "Steel", "Amber", "White" };

        [MenuItem("Protocol/Art/Rebuild Robot Models")]
        public static void Generate()
        {
            Directory.CreateDirectory(Folder);
            AssetDatabase.Refresh();
            var colors = new[] { new Color(.48f, .075f, .035f), new Color(.075f, .085f, .095f),
                new Color(.038f, .042f, .048f), new Color(.35f, .39f, .43f),
                new Color(.9f, .12f, .018f), new Color(.7f, .86f, .95f) };
            var paint = PaintTexture();
            var materials = new Material[Names.Length];
            for (int i = 0; i < Names.Length; i++)
            {
                string path = Folder + "/Robot " + Names[i] + ".mat";
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null)
                {
                    material = new Material(Resources.Load<Shader>("FogSurface")) { name = "Robot " + Names[i] };
                    AssetDatabase.CreateAsset(material, path);
                }
                material.color = colors[i];
                if (i == 0) material.SetTexture("_MainTex", paint);
                material.SetFloat("_Glossiness", i == 2 ? .12f : i == 3 ? .58f : .36f);
                material.SetFloat("_Metallic", i == 2 || i >= 4 ? 0 : i == 3 ? .8f : .45f);
                material.SetFloat("_SurfaceDetail", i == 0 ? .18f : .06f);
                material.SetColor("_EmissionColor", i >= 4 ? colors[i] * .65f : Color.black);
                material.enableInstancing = true;
                EditorUtility.SetDirty(material);
                materials[i] = material;
            }
            Bake(RobotKind.Harvester, materials);
            Bake(RobotKind.Assembler, materials);
            Bake(RobotKind.Hauler, materials);
            AssetDatabase.SaveAssets();
            Debug.Log("Robot concept models baked: Harvester, Assembler and Hauler, six shared materials per model.");
        }

        private static Texture2D PaintTexture()
        {
            const int size = 256;
            var random = new System.Random(4127);
            var pixels = new Color[size * size];
            for (int i = 0; i < pixels.Length; i++)
            {
                float grain = .87f + (float)random.NextDouble() * .13f;
                pixels[i] = new Color(grain, grain, grain, 1);
            }
            for (int scratch = 0; scratch < 100; scratch++)
            {
                int x = random.Next(size), y = random.Next(size), length = random.Next(2, 15);
                float shade = .45f + (float)random.NextDouble() * .25f;
                for (int step = 0; step < length; step++)
                    pixels[((y + step / 5) % size) * size + (x + step) % size] = new Color(shade, shade, shade, 1);
            }
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, true);
            texture.SetPixels(pixels);
            texture.Apply();
            string path = Folder + "/PaintWear.png";
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.maxTextureSize = size;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
        [MenuItem("Protocol/Art/Build Hauler Model")]
        public static void GenerateHauler()
        {
            var materials = new Material[Names.Length];
            for (int i = 0; i < Names.Length; i++)
            {
                materials[i] = AssetDatabase.LoadAssetAtPath<Material>(Folder + "/Robot " + Names[i] + ".mat");
                if (materials[i] == null) throw new System.InvalidOperationException("Missing shared robot material.");
            }
            Bake(RobotKind.Hauler, materials);
            AssetDatabase.SaveAssets();
        }

        private static void Bake(RobotKind kind, Material[] materials)
        {
            var geometry = new RobotModelGeometry[Names.Length];
            for (int i = 0; i < geometry.Length; i++) geometry[i] = new RobotModelGeometry();
            var model = new Model(geometry);
            model.Body(kind);
            if (kind == RobotKind.Harvester) { model.Wheels(); model.Collector(); }
            else { model.Tracks(); if (kind == RobotKind.Assembler) model.Printer(); else model.TowHook(); }
            var root = new GameObject(kind.ToString());
            try
            {
                int triangles = 0;
                for (int i = 0; i < geometry.Length; i++)
                {
                    var mesh = geometry[i].Mesh(kind + " " + Names[i]);
                    if (kind == RobotKind.Hauler)
                    {
                        var vertices = mesh.vertices;
                        for (int v = 0; v < vertices.Length; v++) vertices[v] = Vector3.Scale(vertices[v], new Vector3(1.1f, .8f, 1.1f));
                        mesh.vertices = vertices;
                        mesh.RecalculateNormals();
                        mesh.RecalculateBounds();
                    }
                    triangles += mesh.triangles.Length / 3;
                    string path = Folder + "/" + mesh.name + ".asset";
                    var stored = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                    if (stored != null)
                    {
                        EditorUtility.CopySerialized(mesh, stored);
                        EditorUtility.SetDirty(stored);
                        Object.DestroyImmediate(mesh);
                    }
                    else { AssetDatabase.CreateAsset(mesh, path); stored = mesh; }
                    var part = new GameObject(Names[i]);
                    part.transform.SetParent(root.transform, false);
                    part.AddComponent<MeshFilter>().sharedMesh = stored;
                    part.AddComponent<MeshRenderer>().sharedMaterial = materials[kind == RobotKind.Hauler && i == 5 ? 4 : i];
                }
                var tip = new GameObject("Tool tip").transform;
                tip.SetParent(root.transform, false);
                tip.localPosition = kind == RobotKind.Harvester ? new Vector3(0, .22f, -2.1f) : new Vector3(0, .65f, -2.03f);
                PrefabUtility.SaveAsPrefabAsset(root, Folder + "/" + kind + ".prefab");
                Debug.Log(kind + " mesh triangles: " + triangles);
            }
            finally { Object.DestroyImmediate(root); }
        }

        private sealed class Model
        {
            private readonly RobotModelGeometry[] mesh;
            public Model(RobotModelGeometry[] mesh) => this.mesh = mesh;
            private void Box(int material, Vector3 p, Vector3 s, float bevel = .04f) =>
                mesh[material].Box(p, s, bevel, Quaternion.identity);
            private void Cylinder(int material, Vector3 p, float r, float length, Vector3 axis, int segments = 20) =>
                mesh[material].Cylinder(p, r, length, axis, segments);

            public void TowHook()
            {
                Box(1, new Vector3(0, .68f, 1.26f), new Vector3(1.45f, .38f, .5f), .06f);
                Box(0, new Vector3(0, .85f, 1.23f), new Vector3(1.25f, .15f, .4f), .04f);
                Cylinder(3, new Vector3(0, .65f, 1.6f), .16f, .3f, Vector3.up, 16);
                for (int side = -1; side <= 1; side += 2)
                    Box(4, new Vector3(side * .52f, .91f, -1.01f), new Vector3(.14f, .13f, .06f), .02f);
            }

            public void Body(RobotKind kind)
            {
                Box(1, new Vector3(0, .64f, .05f), new Vector3(1.48f, .36f, 1.9f), .08f);
                Box(0, new Vector3(0, .9f, .08f), new Vector3(1.5f, .25f, 1.78f), .07f);
                Box(1, new Vector3(0, 1.19f, .21f), new Vector3(1.24f, .62f, 1.27f), .1f);
                Box(0, new Vector3(0, 1.26f, .45f), new Vector3(1.38f, .75f, 1.04f), .14f);
                Box(0, new Vector3(0, 1.65f, .35f), new Vector3(1.22f, .11f, 1.08f), .05f);
                Box(1, new Vector3(0, .85f, -.91f), new Vector3(1.3f, .21f, .17f), .05f);
                Box(5, new Vector3(0, .84f, -1.002f), new Vector3(1.11f, .038f, .022f), .008f);
                Box(1, new Vector3(0, 1.26f, .993f), new Vector3(.85f, .43f, .04f), .025f);
                for (int i = 0; i < 5; i++)
                    Box(3, new Vector3(0, 1.12f + i * .068f, 1.018f), new Vector3(.69f, .022f, .025f), .006f);
                for (int side = -1; side <= 1; side += 2)
                {
                    Box(1, new Vector3(side * .7f, 1.28f, .52f), new Vector3(.025f, .32f, .5f), .008f);
                    for (int i = 0; i < 4; i++)
                        Box(3, new Vector3(side * .718f, 1.18f + i * .068f, .52f), new Vector3(.025f, .022f, .4f), .008f);
                    Box(0, new Vector3(side * .69f, .96f, -.36f), new Vector3(.23f, .23f, .7f), .05f);
                    Box(4, new Vector3(side * .48f, 1.27f, 1.017f), new Vector3(.038f, .31f, .025f), .008f);
                    for (int z = -1; z <= 1; z += 2)
                        Cylinder(3, new Vector3(side * .765f, .91f, z * .62f), .027f, .015f, Vector3.right, 8);
                    Logo(new Vector3(side * .705f, 1.28f, .04f), side, .13f);
                }
                Cylinder(1, new Vector3(.28f, 1.77f, .56f), .2f, .13f, Vector3.up);
                Cylinder(0, new Vector3(.28f, 1.83f, .56f), .19f, .04f, Vector3.up);
                Cylinder(5, new Vector3(.28f, 1.76f, .56f), .202f, .026f, Vector3.up);
                Cylinder(1, new Vector3(-.42f, 1.91f, .67f), .025f, .56f, Vector3.up, 12);
                Cylinder(0, new Vector3(-.42f, 2.08f, .67f), .027f, .18f, Vector3.up, 12);
                if (kind == RobotKind.Assembler)
                {
                    Box(1, new Vector3(0, 1.08f, -.54f), new Vector3(.95f, .51f, .56f), .07f);
                    for (int side = -1; side <= 1; side += 2)
                        Box(4, new Vector3(side * .5f, 1.24f, -.74f), new Vector3(.05f, .33f, .06f), .01f);
                    Box(0, new Vector3(.4f, 1.2f, .83f), new Vector3(.28f, .32f, .25f), .04f);
                }
            }

            private void Logo(Vector3 center, int side, float size)
            {
                var a = center + new Vector3(0, size, 0);
                var b = center + new Vector3(0, -size * .7f, -size * .85f);
                var c = center + new Vector3(0, -size * .7f, size * .85f);
                mesh[3].Bar(a, b, .012f, .014f, .002f);
                mesh[3].Bar(b, c, .012f, .014f, .002f);
                mesh[3].Bar(c, a, .012f, .014f, .002f);
            }

            public void Wheels()
            {
                for (int side = -1; side <= 1; side += 2)
                    for (int end = -1; end <= 1; end += 2)
                    {
                        var p = new Vector3(side * .85f, .45f, end * .66f);
                        Cylinder(2, p, .435f, .35f, Vector3.right, 24);
                        Cylinder(3, p + Vector3.right * side * .184f, .29f, .02f, Vector3.right, 24);
                        Cylinder(0, p + Vector3.right * side * .201f, .253f, .026f, Vector3.right, 24);
                        Cylinder(1, p + Vector3.right * side * .22f, .115f, .03f, Vector3.right);
                        for (int i = 0; i < 24; i++)
                        {
                            float a = i * Mathf.PI * 2 / 24;
                            var tread = p + new Vector3(0, Mathf.Cos(a) * .437f, Mathf.Sin(a) * .437f);
                            mesh[2].Box(tread, new Vector3(.38f, .065f, .095f), .014f, Quaternion.Euler(a * Mathf.Rad2Deg, 0, 0));
                        }
                        for (int i = 0; i < 6; i++)
                        {
                            float a = i * Mathf.PI * 2 / 6;
                            Cylinder(3, p + new Vector3(side * .225f, Mathf.Cos(a) * .17f, Mathf.Sin(a) * .17f),
                                .02f, .02f, Vector3.right, 8);
                        }
                        Logo(p + Vector3.right * side * .238f, side, .07f);
                    }
            }

            public void Tracks()
            {
                for (int side = -1; side <= 1; side += 2)
                {
                    float x = side * .9f;
                    Box(1, new Vector3(x, .46f, 0), new Vector3(.34f, .65f, 1.65f), .16f);
                    for (int wheel = 0; wheel < 4; wheel++)
                    {
                        var p = new Vector3(x + side * .04f, .44f, -.69f + wheel * .46f);
                        Cylinder(1, p, .3f, .39f, Vector3.right, 20);
                        Cylinder(3, p + Vector3.right * side * .203f, .19f, .018f, Vector3.right);
                        Cylinder(0, p + Vector3.right * side * .218f, .135f, .028f, Vector3.right);
                        Cylinder(1, p + Vector3.right * side * .24f, .055f, .02f, Vector3.right, 12);
                    }
                    var loop = new List<Vector3>();
                    for (int i = 0; i < 10; i++) loop.Add(new Vector3(x, .86f, Mathf.Lerp(-.69f, .69f, i / 10f)));
                    for (int i = 0; i < 12; i++)
                    {
                        float a = i * Mathf.PI / 12;
                        loop.Add(new Vector3(x, .44f + Mathf.Cos(a) * .42f, .69f + Mathf.Sin(a) * .42f));
                    }
                    for (int i = 0; i < 10; i++) loop.Add(new Vector3(x, .02f, Mathf.Lerp(.69f, -.69f, i / 10f)));
                    for (int i = 0; i < 12; i++)
                    {
                        float a = i * Mathf.PI / 12;
                        loop.Add(new Vector3(x, .44f - Mathf.Cos(a) * .42f, -.69f - Mathf.Sin(a) * .42f));
                    }
                    for (int i = 0; i < loop.Count; i++)
                    {
                        var a = loop[i]; var b = loop[(i + 1) % loop.Count];
                        mesh[2].Box((a + b) / 2, new Vector3(.067f, .39f, Vector3.Distance(a, b) * .9f),
                            .015f, Quaternion.LookRotation(b - a, Vector3.right));
                    }
                    Box(0, new Vector3(side * 1.105f, .49f, 0), new Vector3(.065f, .32f, 1.14f), .045f);
                    Logo(new Vector3(side * 1.143f, .51f, 0), side, .12f);
                    Box(4, new Vector3(side * .82f, .99f, .6f), new Vector3(.18f, .055f, .45f), .01f);
                }
            }

            private void Joint(Vector3 p, float radius)
            {
                Cylinder(0, p, radius, .42f, Vector3.right, 24);
                for (int side = -1; side <= 1; side += 2)
                {
                    Cylinder(3, p + Vector3.right * side * .218f, radius * .78f, .028f, Vector3.right, 24);
                    Cylinder(1, p + Vector3.right * side * .24f, radius * .57f, .035f, Vector3.right, 24);
                    Cylinder(3, p + Vector3.right * side * .265f, radius * .24f, .025f, Vector3.right, 16);
                }
            }

            private void Boom(Vector3 a, Vector3 b, Vector3 c)
            {
                mesh[0].Bar(a, b, .35f, .24f, .06f);
                mesh[0].Bar(b, c, .3f, .21f, .05f);
                Joint(a, .24f); Joint(b, .2f); Joint(c, .16f);
                for (int side = -1; side <= 1; side += 2)
                {
                    Vector3 offset = Vector3.right * side * .23f;
                    mesh[1].Rod(a + offset, Vector3.Lerp(a, b, .56f) + offset + Vector3.up * .1f, .065f);
                    mesh[3].Rod(Vector3.Lerp(a, b, .46f) + offset + Vector3.up * .1f, b + offset, .029f);
                    mesh[1].Rod(b + offset, Vector3.Lerp(b, c, .55f) + offset - Vector3.up * .1f, .055f);
                    mesh[3].Rod(Vector3.Lerp(b, c, .45f) + offset - Vector3.up * .1f, c + offset, .025f);
                    for (int segment = 0; segment < 12; segment++)
                    {
                        float t0 = segment / 12f, t1 = (segment + 1) / 12f;
                        Vector3 control = b + Vector3.up * .55f;
                        Vector3 p0 = (1 - t0) * (1 - t0) * a + 2 * (1 - t0) * t0 * control + t0 * t0 * c;
                        Vector3 p1 = (1 - t1) * (1 - t1) * a + 2 * (1 - t1) * t1 * control + t1 * t1 * c;
                        mesh[2].Rod(p0 + offset, p1 + offset, .033f, 8);
                    }
                }
            }

            public void Collector()
            {
                var a = new Vector3(0, 1.25f, -.51f);
                var b = new Vector3(0, 1.62f, -1.2f);
                var c = new Vector3(0, .64f, -1.93f);
                Boom(a, b, c);
                mesh[0].Bar(c, new Vector3(0, .4f, -2.13f), .52f, .25f, .05f);
                Box(1, new Vector3(0, .26f, -2.06f), new Vector3(1.15f, .18f, .54f), .045f);
                for (int side = -1; side <= 1; side += 2)
                {
                    Box(0, new Vector3(side * .61f, .36f, -2.1f), new Vector3(.18f, .55f, .65f), .065f);
                    Cylinder(3, new Vector3(side * .72f, .39f, -1.97f), .09f, .03f, Vector3.right, 16);
                    Box(3, new Vector3(side * .58f, .12f, -2.47f), new Vector3(.17f, .16f, .3f), .025f);
                }
                for (int i = 0; i < 4; i++)
                {
                    float x = -.39f + i * .26f;
                    Box(3, new Vector3(x, .14f, -2.32f), new Vector3(.16f, .17f, .58f), .028f);
                    Box(1, new Vector3(x, .24f, -2.07f), new Vector3(.15f, .08f, .23f), .016f);
                }
            }

            public void Printer()
            {
                var a = new Vector3(0, 1.31f, -.43f);
                var b = new Vector3(0, 2.27f, -1.09f);
                var c = new Vector3(0, 1.66f, -2.03f);
                Boom(a, b, c);
                Cylinder(1, new Vector3(0, 1.31f, -2.03f), .25f, .7f, Vector3.up, 24);
                Cylinder(3, new Vector3(0, 1.58f, -2.03f), .255f, .12f, Vector3.up, 24);
                Cylinder(0, new Vector3(0, 1.66f, -2.03f), .22f, .12f, Vector3.up, 24);
                for (int i = 0; i < 6; i++)
                {
                    float a0 = i * Mathf.PI * 2 / 6;
                    Vector3 radial = new Vector3(Mathf.Cos(a0), 0, Mathf.Sin(a0));
                    Box(0, new Vector3(0, 1.26f, -2.03f) + radial * .28f, new Vector3(.14f, .51f, .14f), .035f);
                    Box(4, new Vector3(0, 1.26f, -2.03f) + radial * .354f, new Vector3(.055f, .36f, .055f), .012f);
                    Box(3, new Vector3(0, 1.01f, -2.03f) + radial * .29f, new Vector3(.15f, .07f, .15f), .014f);
                }
                Cylinder(3, new Vector3(0, .94f, -2.03f), .3f, .12f, Vector3.up, 24);
                Cylinder(4, new Vector3(0, .875f, -2.03f), .29f, .045f, Vector3.up, 24);
                Cylinder(1, new Vector3(0, .81f, -2.03f), .19f, .09f, Vector3.up, 24);
                Cylinder(4, new Vector3(0, .755f, -2.03f), .15f, .035f, Vector3.up, 24);
            }
        }

        [MenuItem("Protocol/Art/Preview Robot Models")]
        public static void Preview() => RenderPreview(false);

        [MenuItem("Protocol/Art/Preview Base Model")]
        public static void PreviewBase() => RenderPreview(true);

        [MenuItem("Protocol/Art/Preview Solar Panel Model")]
        public static void PreviewSolar() => RenderPreview(false, true);

        [MenuItem("Protocol/Art/Preview Battery Storage Model")]
        public static void PreviewBattery() => RenderPreview(false, false, true);

        [MenuItem("Protocol/Art/Preview Iron Deposit Model")]
        public static void PreviewIron() => RenderPreview(false, false, false, true);

        private static void RenderPreview(bool baseModel, bool solarModel = false, bool batteryModel = false, bool ironModel = false)
        {
            var scene = EditorSceneManager.NewPreviewScene();
            var ownedMaterials = new List<Material>();
            try
            {
                var root = new GameObject("Robot concept preview");
                SceneManagementMove(root, scene);
                for (int i = 0; i < (baseModel || solarModel || batteryModel || ironModel ? 1 : 2); i++)
                {
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ironModel ? "Assets/Art/Resources/Deposits/IronDeposit.prefab" : batteryModel ? "Assets/Art/Resources/Buildings/BatteryStorage.prefab" : solarModel ? "Assets/Art/Resources/Buildings/SolarPanel.prefab" : baseModel ? "Assets/Art/Resources/Buildings/Base.prefab" : Folder + "/" + (i == 0 ? "Harvester" : "Assembler") + ".prefab");
                    var model = Object.Instantiate(prefab, root.transform);
                    model.transform.localPosition = baseModel || solarModel || batteryModel || ironModel ? Vector3.zero : new Vector3((i - .5f) * 4.3f, 0, 0);
                    foreach (var renderer in model.GetComponentsInChildren<MeshRenderer>())
                    {
                        var materials = renderer.sharedMaterials;
                        for (int slot = 0; slot < materials.Length; slot++)
                        {
                            var material = new Material(materials[slot]);
                            material.SetFloat("_IgnoreWarFog", 1);
                            materials[slot] = material;
                            ownedMaterials.Add(material);
                        }
                        renderer.sharedMaterials = materials;
                    }
                }
                var camera = new GameObject("Preview camera").AddComponent<Camera>();
                camera.transform.SetParent(root.transform);
                camera.transform.position = baseModel ? new Vector3(8, 7, -11) : new Vector3(6.8f, 4.6f, -8.3f);
                camera.transform.LookAt(batteryModel ? new Vector3(0, 1.65f, 0) : baseModel ? new Vector3(0, 1.9f, -.8f) : new Vector3(0, 1.02f, -.5f));
                camera.scene = scene;
                camera.orthographic = true;
                camera.orthographicSize = baseModel ? 4.1f : 3.25f;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(.045f, .06f, .075f);
                camera.enabled = false;
                var light = new GameObject("Preview key").AddComponent<Light>();
                light.transform.SetParent(root.transform);
                light.type = LightType.Directional;
                light.intensity = 2;
                light.color = new Color(1, .88f, .8f);
                light.transform.rotation = Quaternion.Euler(40, -30, 0);
                var fill = new GameObject("Preview fill").AddComponent<Light>();
                fill.transform.SetParent(root.transform);
                fill.type = LightType.Directional;
                fill.intensity = .9f;
                fill.color = new Color(.5f, .7f, 1);
                fill.transform.rotation = Quaternion.Euler(25, 140, 0);
                var target = new RenderTexture(1400, 900, 24);
                var pixels = new Texture2D(1400, 900, TextureFormat.RGB24, false);
                try
                {
                    camera.targetTexture = target;
                    RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
                    var previous = RenderTexture.active;
                    RenderTexture.active = target;
                    pixels.ReadPixels(new Rect(0, 0, 1400, 900), 0, 0);
                    pixels.Apply();
                    RenderTexture.active = previous;
                    Directory.CreateDirectory("Artifacts");
                    File.WriteAllBytes(ironModel ? "Artifacts/IronConceptPreview.png" : batteryModel ? "Artifacts/BatteryConceptPreview.png" : solarModel ? "Artifacts/SolarConceptPreview.png" : baseModel ? "Artifacts/BaseConceptPreview.png" : "Artifacts/RobotConceptPreview.png", pixels.EncodeToPNG());
                }
                finally { camera.targetTexture = null; target.Release(); Object.DestroyImmediate(target); Object.DestroyImmediate(pixels); }
                Debug.Log("Preview saved: Artifacts/RobotConceptPreview.png");
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
                foreach (var material in ownedMaterials) Object.DestroyImmediate(material);
            }
        }

        private static void SceneManagementMove(GameObject owner, UnityEngine.SceneManagement.Scene scene) =>
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(owner, scene);
    }
}