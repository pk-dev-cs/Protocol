using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Protocol.Editor
{
    public static class BaseModelTools
    {
        private const string Folder = "Assets/Art/Resources/Buildings";
        private static readonly string[] Names = { "Armor", "Frame", "Rubber", "Steel", "Amber", "White" };

        [MenuItem("Protocol/Art/Rebuild Base Model")]
        public static void Generate()
        {
            System.IO.Directory.CreateDirectory(Folder);
            AssetDatabase.Refresh();
            var geometry = new RobotModelGeometry[Names.Length];
            for (int i = 0; i < geometry.Length; i++) geometry[i] = new RobotModelGeometry();
            var model = new Model(geometry);
            model.Build();
            var root = new GameObject("Base");
            try
            {
                int triangles = 0;
                for (int i = 0; i < geometry.Length; i++)
                {
                    var mesh = geometry[i].Mesh("Base " + Names[i]);
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
                    part.AddComponent<MeshRenderer>().sharedMaterial =
                        AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Resources/Units/Robot " + Names[i] + ".mat");
                    if (i == 3)
                    {
                        string steelPath = Folder + "/Base Steel.mat";
                        var steel = AssetDatabase.LoadAssetAtPath<Material>(steelPath);
                        if (steel == null)
                        {
                            steel = new Material(part.GetComponent<MeshRenderer>().sharedMaterial) { name = "Base Steel" };
                            AssetDatabase.CreateAsset(steel, steelPath);
                        }
                        steel.color = new Color(.14f, .17f, .19f);
                        steel.SetFloat("_Metallic", .25f);
                        steel.SetFloat("_Glossiness", .28f);
                        steel.enableInstancing = true;
                        EditorUtility.SetDirty(steel);
                        part.GetComponent<MeshRenderer>().sharedMaterial = steel;
                    }
                    if (part.GetComponent<MeshRenderer>().sharedMaterial == null)
                        throw new System.InvalidOperationException("Generate robot materials before rebuilding base.");
                }
                PrefabUtility.SaveAsPrefabAsset(root, Folder + "/Base.prefab");
                AssetDatabase.SaveAssets();
                Debug.Log("Base model baked: " + triangles + " triangles, six shared materials.");
            }
            finally { Object.DestroyImmediate(root); }
        }

        private sealed class Model
        {
            private readonly RobotModelGeometry[] geometry;
            public Model(RobotModelGeometry[] value) => geometry = value;
            private void Box(int material, Vector3 center, Vector3 size, float bevel = .06f) =>
                geometry[material].Box(center, size, bevel, Quaternion.identity);
            private void Cylinder(int material, Vector3 center, float radius, float length, Vector3 axis) =>
                geometry[material].Cylinder(center, radius, length, axis, 32);

            public void Build()
            {
                Box(1, new Vector3(0, .28f, 0), new Vector3(6.85f, .55f, 5.8f), .14f);
                Box(3, new Vector3(0, .55f, 0), new Vector3(6.75f, .12f, 5.7f));
                Box(1, new Vector3(0, 1.55f, .25f), new Vector3(4.05f, 2.0f, 4.0f), .18f);
                Box(0, new Vector3(0, 2.54f, .25f), new Vector3(4.1f, .5f, 4.12f), .18f);
                // Separate modules retain the silhouette of a compact command and production hub.
                for (int side = -1; side <= 1; side += 2)
                {
                    Module(new Vector3(side * 2.63f, 1.3f, .4f), new Vector3(1.3f, 1.6f, 3.1f));
                    Module(new Vector3(side * 2.54f, .96f, -1.92f), new Vector3(1.5f, .82f, 1.25f));
                    Module(new Vector3(side * 1.46f, 2.15f, -1.82f), new Vector3(.67f, 2.2f, .7f));
                    Box(1, new Vector3(side * 1.43f, .6f, -2.15f), new Vector3(.93f, .35f, 1.1f));
                    Box(3, new Vector3(side * 1.43f, .32f, -2.25f), new Vector3(1.05f, .13f, 1.28f));
                    Box(4, new Vector3(side * 1.45f, 1.6f, -2.205f), new Vector3(.12f, 1.52f, .05f), .025f);
                    for (int z = -1; z <= 1; z++)
                    {
                        Box(1, new Vector3(side * 2.67f, 1.46f, z * .82f + .4f), new Vector3(1.35f, .13f, .19f));
                        Box(3, new Vector3(side * 3.285f, .74f, z * .82f + .4f), new Vector3(.065f, .2f, .3f));
                    }
                    Box(4, new Vector3(side * 3.295f, 1.32f, .65f), new Vector3(.055f, .73f, .12f));
                    Antenna(new Vector3(side * 2.4f, 2.1f, 1.42f), side == 1 ? 1.75f : 1.25f);
                    Core(new Vector3(side * 2.25f, .55f, -1.12f));
                }
                for (int seam = -1; seam <= 1; seam += 2)
                    Box(1, new Vector3(seam * .88f, 2.795f, .3f), new Vector3(.08f, .055f, 3.65f), .01f);
                for (int side = -1; side <= 1; side += 2)
                    for (int vent = 0; vent < 6; vent++)
                        Box(1, new Vector3(side * 3.29f, 1.01f + vent * .135f, .13f), new Vector3(.04f, .065f, .7f), .01f);
                Hangar();
                Tower();
                for (int side = -1; side <= 1; side += 2)
                {
                    Module(new Vector3(side * 1.35f, 2.92f, 1.47f), new Vector3(.85f, .85f, .9f));
                    for (int vent = 0; vent < 5; vent++)
                        Box(3, new Vector3(side * 1.35f, 2.66f + vent * .12f, 1.94f), new Vector3(.55f, .06f, .05f), .01f);
                    Antenna(new Vector3(side * 1.67f, 2.8f, .85f), side == -1 ? 1.75f : 2.1f);
                    Logo(new Vector3(side * 2.63f, 1.31f, -1.186f), .23f);
                }
                Dish();
                for (int vent = 0; vent < 7; vent++)
                    Box(3, new Vector3(0, .85f + vent * .18f, 2.3f), new Vector3(1.25f, .08f, .05f), .015f);
            }

            private void Module(Vector3 center, Vector3 size)
            {
                Box(1, center, size - new Vector3(.12f, .12f, .12f), .12f);
                Box(0, center + Vector3.up * .06f, size, .16f);
                for (int sign = -1; sign <= 1; sign += 2)
                {
                    Box(1, center + new Vector3(sign * size.x * .34f, 0, -size.z * .5f),
                        new Vector3(.13f, size.y * .74f, .11f), .035f);
                    Cylinder(3, center + new Vector3(sign * size.x * .34f, size.y * .28f, -size.z * .5f - .06f),
                        .05f, .035f, Vector3.forward);
                }
                Box(4, center + new Vector3(0, 0, -size.z * .5f - .025f),
                    new Vector3(.1f, size.y * .57f, .055f), .02f);
            }

            private void Hangar()
            {
                Box(2, new Vector3(0, 1.23f, -1.805f), new Vector3(2.18f, 1.48f, .07f));
                for (int i = 0; i < 6; i++)
                    Box(1, new Vector3(0, .65f + i * .21f, -1.85f), new Vector3(2.04f, .15f, .1f), .02f);
                Box(3, new Vector3(0, 2.13f, -1.96f), new Vector3(2.45f, .3f, .28f));
                Box(4, new Vector3(0, 2.13f, -2.11f), new Vector3(1.68f, .07f, .03f), .01f);
                Box(0, new Vector3(0, 2.55f, -1.92f), new Vector3(2.35f, .5f, .42f), .13f);
                Logo(new Vector3(0, 2.57f, -2.145f), .22f);
                var start = new Vector3(0, .47f, -2.12f);
                var end = new Vector3(0, .1f, -5.12f);
                geometry[1].Bar(start, end, 2.3f, .16f, .04f);
                for (int i = 0; i < 11; i++)
                {
                    Vector3 point = Vector3.Lerp(start, end, (i + .5f) / 11f);
                    Box(3, point + Vector3.up * .085f, new Vector3(1.9f, .045f, .2f), .015f);
                    for (int sign = -1; sign <= 1; sign += 2)
                        Box(i % 2 == 0 ? 4 : 1, point + new Vector3(sign * 1.02f, .09f, 0), new Vector3(.09f, .04f, .2f), .01f);
                }
                geometry[3].Bar(start + Vector3.left * 1.16f, end + Vector3.left * 1.16f, .13f, .18f, .02f);
                geometry[3].Bar(start + Vector3.right * 1.16f, end + Vector3.right * 1.16f, .13f, .18f, .02f);
            }

            private void Tower()
            {
                Cylinder(1, new Vector3(0, 2.94f, .38f), 1.1f, .3f, Vector3.up);
                Cylinder(4, new Vector3(0, 3.12f, .38f), 1.02f, .105f, Vector3.up);
                Cylinder(3, new Vector3(0, 3.23f, .38f), 1.06f, .14f, Vector3.up);
                Cylinder(1, new Vector3(0, 3.49f, .38f), .73f, .5f, Vector3.up);
                Cylinder(3, new Vector3(0, 3.79f, .38f), .64f, .12f, Vector3.up);
                Cylinder(0, new Vector3(0, 3.87f, .38f), .48f, .09f, Vector3.up);
                for (int i = 0; i < 12; i++)
                {
                    float angle = i * Mathf.PI * 2 / 12;
                    Vector3 radial = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
                    Box(1, new Vector3(0, 2.95f, .38f) + radial * 1.04f, new Vector3(.23f, .2f, .23f));
                    Box(4, new Vector3(0, 3.48f, .38f) + radial * .725f, new Vector3(.045f, .085f, .045f), .01f);
                }
            }

            private void Core(Vector3 bottom)
            {
                Cylinder(1, bottom + Vector3.up * .11f, .23f, .2f, Vector3.up);
                Cylinder(4, bottom + Vector3.up * .52f, .135f, .62f, Vector3.up);
                Cylinder(3, bottom + Vector3.up * .89f, .23f, .14f, Vector3.up);
                for (int i = 0; i < 4; i++)
                {
                    float angle = i * Mathf.PI * .5f;
                    geometry[1].Rod(bottom + new Vector3(Mathf.Cos(angle) * .17f, .2f, Mathf.Sin(angle) * .17f),
                        bottom + new Vector3(Mathf.Cos(angle) * .17f, .85f, Mathf.Sin(angle) * .17f), .035f);
                }
            }

            private void Antenna(Vector3 bottom, float height)
            {
                Box(1, bottom + Vector3.up * .2f, new Vector3(.25f, .4f, .25f));
                Cylinder(3, bottom + Vector3.up * (height * .5f + .3f), .035f, height, Vector3.up);
                Cylinder(0, bottom + Vector3.up * (height * .65f + .3f), .045f, height * .2f, Vector3.up);
            }

            private void Dish()
            {
                var center = new Vector3(1.72f, 4.25f, .68f);
                Cylinder(1, center, .45f, .065f, new Vector3(0, .15f, -1));
                Cylinder(3, center + new Vector3(0, .015f, -.04f), .41f, .035f, new Vector3(0, .15f, -1));
                geometry[1].Rod(center + new Vector3(0, 0, -.07f), center + new Vector3(0, 0, -.4f), .03f);
                Cylinder(1, center + new Vector3(0, 0, -.4f), .07f, .1f, Vector3.forward);
            }

            private void Logo(Vector3 center, float size)
            {
                Vector3 top = center + Vector3.up * size;
                Vector3 left = center + new Vector3(-size, -size * .65f, 0);
                Vector3 right = center + new Vector3(size, -size * .65f, 0);
                geometry[5].Bar(top, left, .035f, .02f, .005f);
                geometry[5].Bar(left, right, .035f, .02f, .005f);
                geometry[5].Bar(right, top, .035f, .02f, .005f);
            }
        }
    }
}