using UnityEditor;
using UnityEngine;

namespace Protocol.Editor
{
    public static class BatteryModelTools
    {
        private const string Folder = "Assets/Art/Resources/Buildings";
        private static readonly string[] Names = { "Armor", "Frame", "Rubber", "Steel", "Amber", "Shell" };

        [MenuItem("Protocol/Art/Rebuild Battery Storage Model")]
        public static void Generate()
        {
            System.IO.Directory.CreateDirectory(Folder);
            AssetDatabase.Refresh();
            var shell = AssetDatabase.LoadAssetAtPath<Material>(Folder + "/Battery Shell.mat");
            if (shell == null)
            {
                shell = new Material(Resources.Load<Shader>("FogSurface")) { name = "Battery Shell" };
                AssetDatabase.CreateAsset(shell, Folder + "/Battery Shell.mat");
            }
            shell.color = new Color(.58f, .54f, .47f);
            shell.SetFloat("_Metallic", .18f);
            shell.SetFloat("_Glossiness", .3f);
            shell.SetFloat("_SurfaceDetail", .08f);
            shell.SetTexture("_MainTex", AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/Resources/Units/PaintWear.png"));
            shell.enableInstancing = true;
            EditorUtility.SetDirty(shell);
            var geometry = new RobotModelGeometry[Names.Length];
            for (int i = 0; i < geometry.Length; i++) geometry[i] = new RobotModelGeometry();
            new Model(geometry).Build();
            var root = new GameObject("Battery Storage");
            try
            {
                int triangles = 0;
                for (int i = 0; i < geometry.Length; i++)
                {
                    var mesh = geometry[i].Mesh("Battery " + Names[i]);
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
                    part.AddComponent<MeshRenderer>().sharedMaterial = i == 5 ? shell : i == 3
                        ? AssetDatabase.LoadAssetAtPath<Material>(Folder + "/Base Steel.mat")
                        : AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Resources/Units/Robot " + Names[i] + ".mat");
                }
                PrefabUtility.SaveAsPrefabAsset(root, Folder + "/BatteryStorage.prefab");
                AssetDatabase.SaveAssets();
                Debug.Log("Battery Storage model baked: " + triangles + " triangles, six material groups.");
            }
            finally { Object.DestroyImmediate(root); }
        }

        private sealed class Model
        {
            private readonly RobotModelGeometry[] geometry;
            public Model(RobotModelGeometry[] value) => geometry = value;
            private void Box(int material, Vector3 center, Vector3 size, float bevel = .05f) =>
                geometry[material].Box(center, size, bevel, Quaternion.identity);
            private void Cylinder(int material, Vector3 center, float radius, float length, Vector3 axis) =>
                geometry[material].Cylinder(center, radius, length, axis, 32);

            public void Build()
            {
                Box(1, new Vector3(0, .18f, 0), new Vector3(5.4f, .36f, 4.15f), .1f);
                Box(3, new Vector3(0, .38f, 0), new Vector3(5.3f, .1f, 4.05f));
                for (int x = -1; x <= 1; x += 2)
                    for (int z = -1; z <= 1; z += 2)
                        Module(new Vector3(x * 1.57f, .43f, z * 1.0f));
                Box(1, new Vector3(0, 1.08f, .23f), new Vector3(1.55f, 1.3f, 3.12f), .12f);
                Box(5, new Vector3(0, 1.42f, -1.17f), new Vector3(1.38f, 1.37f, .48f), .13f);
                Box(0, new Vector3(0, 1.66f, -1.431f), new Vector3(.91f, .68f, .065f), .025f);
                Logo(new Vector3(0, 1.68f, -1.47f));
                Box(1, new Vector3(0, .87f, -1.6f), new Vector3(.78f, .86f, .32f));
                Box(2, new Vector3(0, .83f, -1.774f), new Vector3(.55f, .66f, .04f));
                for (int row = 0; row < 7; row++)
                    Box(4, new Vector3(0, .54f + row * .08f, -1.8f), new Vector3(.43f, .018f, .025f), .004f);
                for (int side = -1; side <= 1; side += 2)
                {
                    Box(3, new Vector3(side * .49f, .82f, -1.73f), new Vector3(.15f, .86f, .21f));
                    Box(4, new Vector3(side * .49f, .78f, -1.843f), new Vector3(.035f, .43f, .025f), .008f);
                    for (int z = -1; z <= 1; z += 2)
                    {
                        var foot = new Vector3(side * 2.18f, .51f, z * 1.72f);
                        geometry[5].Bar(foot + new Vector3(0, .35f, .1f), foot + new Vector3(0, -.26f, -.24f), .44f, .3f, .055f);
                        Box(4, foot + new Vector3(0, -.09f, -.4f), new Vector3(.06f, .18f, .045f), .01f);
                    }
                    for (int z = -1; z <= 1; z += 2)
                    {
                        geometry[1].Rod(new Vector3(side * .57f, 1.72f, z * .92f), new Vector3(side * 1.38f, 1.72f, z * .92f), .1f);
                        Cylinder(0, new Vector3(side * .9f, 1.72f, z * .92f), .12f, .28f, Vector3.right);
                    }
                    Box(3, new Vector3(side * 1.7f, .42f, -2.085f), new Vector3(1.1f, .25f, .16f));
                    Box(4, new Vector3(side * 1.7f, .45f, -2.17f), new Vector3(.4f, .035f, .025f), .008f);
                }
                Box(3, new Vector3(0, 1.91f, .27f), new Vector3(1.43f, .22f, 2.0f));
                for (int row = 0; row < 8; row++)
                    Box(1, new Vector3(0, 2.03f, -.45f + row * .18f), new Vector3(1.18f, .03f, .065f), .008f);
                Box(4, new Vector3(0, 1.91f, -.75f), new Vector3(.6f, .055f, .03f), .008f);
                geometry[3].Bar(new Vector3(0, .42f, -1.93f), new Vector3(0, .07f, -2.46f), 1.12f, .09f, .02f);
                for (int row = 0; row < 4; row++)
                    Box(1, new Vector3(0, .14f + row * .078f, -2.4f + row * .12f), new Vector3(.94f, .025f, .035f), .006f);
                Cylinder(1, new Vector3(.52f, 2.2f, .88f), .14f, .38f, Vector3.up);
                Cylinder(3, new Vector3(.52f, 3.0f, .88f), .025f, 1.45f, Vector3.up);
                Cylinder(0, new Vector3(.52f, 2.73f, .88f), .055f, .43f, Vector3.up);
                Cylinder(4, new Vector3(.77f, 2.63f, .88f), .035f, .69f, Vector3.up);
                for (int row = 0; row < 7; row++)
                    Box(3, new Vector3(0, .62f + row * .15f, 1.818f), new Vector3(1.18f, .065f, .035f), .01f);
            }

            private void Module(Vector3 bottom)
            {
                Cylinder(1, bottom + Vector3.up * .12f, .7f, .24f, Vector3.up);
                Cylinder(0, bottom + Vector3.up * .29f, .65f, .12f, Vector3.up);
                Cylinder(5, bottom + Vector3.up * 1.17f, .635f, 1.68f, Vector3.up);
                Cylinder(1, bottom + Vector3.up * 2.04f, .7f, .17f, Vector3.up);
                Cylinder(4, bottom + Vector3.up * 2.15f, .685f, .075f, Vector3.up);
                Cylinder(5, bottom + Vector3.up * 2.25f, .7f, .14f, Vector3.up);
                Cylinder(1, bottom + Vector3.up * 2.36f, .48f, .07f, Vector3.up);
                Cylinder(3, bottom + Vector3.up * 2.45f, .23f, .16f, Vector3.up);
                for (int i = 0; i < 12; i++)
                {
                    float angle = i * Mathf.PI * 2 / 12;
                    Vector3 radial = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
                    geometry[3].Rod(bottom + radial * .638f + Vector3.up * .43f, bottom + radial * .638f + Vector3.up * 1.92f, .009f, 8);
                    for (int sign = -1; sign <= 1; sign += 2)
                        Cylinder(0, bottom + radial * .65f + Vector3.up * (1.17f + sign * .66f), .023f, .028f, radial);
                }
                Box(0, bottom + new Vector3(0, 1.18f, -.641f), new Vector3(.22f, 1.46f, .13f));
                Box(4, bottom + new Vector3(0, 1.18f, -.715f), new Vector3(.075f, 1.27f, .035f), .01f);
                for (int side = -1; side <= 1; side += 2)
                {
                    geometry[1].Rod(bottom + new Vector3(side * .57f, .3f, .31f), bottom + new Vector3(side * .57f, 2.05f, .31f), .075f);
                    Cylinder(0, bottom + new Vector3(side * .57f, 1.17f, .31f), .085f, .3f, Vector3.up);
                }
            }

            private void Logo(Vector3 center)
            {
                geometry[5].Bar(center + Vector3.up * .18f, center + new Vector3(-.18f, -.12f, 0), .032f, .02f, .004f);
                geometry[5].Bar(center + new Vector3(-.18f, -.12f, 0), center + new Vector3(.18f, -.12f, 0), .032f, .02f, .004f);
                geometry[5].Bar(center + new Vector3(.18f, -.12f, 0), center + Vector3.up * .18f, .032f, .02f, .004f);
            }
        }
    }
}