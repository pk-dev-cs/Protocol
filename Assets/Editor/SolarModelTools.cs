using UnityEditor;
using UnityEngine;

namespace Protocol.Editor
{
    public static class SolarModelTools
    {
        private const string Folder = "Assets/Art/Resources/Buildings";
        private static readonly string[] Names = { "Armor", "Frame", "Rubber", "Steel", "Amber", "White", "Cells" };

        [MenuItem("Protocol/Art/Rebuild Solar Panel Model")]
        public static void Generate()
        {
            System.IO.Directory.CreateDirectory(Folder);
            AssetDatabase.Refresh();
            var cells = AssetDatabase.LoadAssetAtPath<Material>(Folder + "/Solar Cells.mat");
            if (cells == null)
            {
                cells = new Material(Resources.Load<Shader>("FogSurface")) { name = "Solar Cells" };
                AssetDatabase.CreateAsset(cells, Folder + "/Solar Cells.mat");
            }
            cells.color = new Color(.028f, .05f, .075f);
            cells.SetFloat("_Metallic", .12f);
            cells.SetFloat("_Glossiness", .42f);
            cells.SetFloat("_SurfaceDetail", .025f);
            cells.enableInstancing = true;
            EditorUtility.SetDirty(cells);
            var geometry = new RobotModelGeometry[Names.Length];
            for (int i = 0; i < geometry.Length; i++) geometry[i] = new RobotModelGeometry();
            new Model(geometry).Build();
            var root = new GameObject("Solar Panel");
            try
            {
                int triangles = 0;
                for (int i = 0; i < geometry.Length; i++)
                {
                    var mesh = geometry[i].Mesh("Solar " + Names[i]);
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
                    part.AddComponent<MeshRenderer>().sharedMaterial = i == 6 ? cells : i == 3
                        ? AssetDatabase.LoadAssetAtPath<Material>(Folder + "/Base Steel.mat")
                        : AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Resources/Units/Robot " + Names[i] + ".mat");
                }
                var surface = new GameObject("Cell surface").transform;
                surface.SetParent(root.transform, false);
                surface.localPosition = new Vector3(0, 2.5f, 0);
                surface.localRotation = Quaternion.Euler(-28, 0, 0);
                PrefabUtility.SaveAsPrefabAsset(root, Folder + "/SolarPanel.prefab");
                AssetDatabase.SaveAssets();
                Debug.Log("Solar model baked: " + triangles + " triangles. Cell normal faces local -Z.");
            }
            finally { Object.DestroyImmediate(root); }
        }

        private sealed class Model
        {
            private readonly RobotModelGeometry[] geometry;
            private readonly Quaternion tilt = Quaternion.Euler(-28, 0, 0);
            private readonly Vector3 rackCenter = new Vector3(0, 2.5f, 0);
            public Model(RobotModelGeometry[] value) => geometry = value;
            private void Box(int material, Vector3 center, Vector3 size, float bevel = .05f) =>
                geometry[material].Box(center, size, bevel, Quaternion.identity);
            private void RackBox(int material, Vector3 center, Vector3 size, float bevel = .025f) =>
                geometry[material].Box(rackCenter + tilt * center, size, bevel, tilt);
            private void Cylinder(int material, Vector3 center, float radius, float length, Vector3 axis) =>
                geometry[material].Cylinder(center, radius, length, axis, 24);

            public void Build()
            {
                Box(1, new Vector3(0, .26f, 0), new Vector3(2.5f, .52f, 1.75f), .14f);
                Box(0, new Vector3(0, .63f, 0), new Vector3(2.1f, .42f, 1.6f), .12f);
                Box(3, new Vector3(0, .29f, -.92f), new Vector3(1.8f, .27f, .13f));
                Box(4, new Vector3(0, .67f, -.815f), new Vector3(.75f, .06f, .03f), .01f);
                for (int side = -1; side <= 1; side += 2)
                {
                    Box(0, new Vector3(side * .89f, .56f, -.04f), new Vector3(.24f, .65f, 1.8f), .075f);
                    for (int end = -1; end <= 1; end += 2)
                    {
                        Vector3 foot = new Vector3(side * 1.85f, .18f, end * 1.32f);
                        geometry[1].Bar(new Vector3(side * .85f, .28f, end * .55f), foot, .25f, .22f, .035f);
                        Box(3, foot, new Vector3(.8f, .25f, .63f));
                        Box(0, foot + Vector3.up * .25f, new Vector3(.48f, .27f, .44f), .065f);
                        Box(4, foot + new Vector3(0, .03f, -.326f), new Vector3(.35f, .035f, .025f), .008f);
                        Cylinder(1, foot + Vector3.up * .44f, .16f, .075f, Vector3.up);
                    }
                }
                Cylinder(1, new Vector3(0, .97f, 0), .65f, .26f, Vector3.up);
                Cylinder(3, new Vector3(0, 1.12f, 0), .61f, .07f, Vector3.up);
                Box(0, new Vector3(0, 1.46f, .03f), new Vector3(.68f, .7f, .58f), .13f);
                for (int side = -1; side <= 1; side += 2)
                {
                    Cylinder(1, new Vector3(side * .48f, 1.67f, .1f), .36f, .25f, Vector3.right);
                    Cylinder(0, new Vector3(side * .62f, 1.67f, .1f), .3f, .08f, Vector3.right);
                    Cylinder(3, new Vector3(side * .675f, 1.67f, .1f), .19f, .05f, Vector3.right);
                    geometry[0].Bar(new Vector3(side * .4f, 1.4f, .1f), new Vector3(side * .4f, 2.27f, .31f), .24f, .35f, .06f);
                    geometry[1].Rod(new Vector3(side * .65f, .62f, .69f), new Vector3(side * .65f, 1.48f, .63f), .09f);
                    geometry[3].Rod(new Vector3(side * .65f, 1.48f, .63f), new Vector3(side * .65f, 2.22f, .38f), .047f);
                    for (int segment = 0; segment < 10; segment++)
                    {
                        float a = segment / 10f, b = (segment + 1) / 10f;
                        geometry[2].Rod(Hose(side, a), Hose(side, b), .055f);
                    }
                }
                Logo(new Vector3(0, 1.43f, -.27f));
                for (int module = -1; module <= 1; module++)
                {
                    float x = module * 1.64f;
                    RackBox(1, new Vector3(x, 0, 0), new Vector3(1.62f, .2f, 3.2f), .07f);
                    RackBox(0, new Vector3(x, .08f, 0), new Vector3(1.59f, .15f, 3.16f), .075f);
                    RackBox(6, new Vector3(x, .175f, 0), new Vector3(1.37f, .05f, 2.92f), .01f);
                    for (int line = 0; line <= 4; line++)
                        RackBox(3, new Vector3(x - .66f + line * .33f, .204f, 0), new Vector3(.01f, .009f, 2.87f), .002f);
                    for (int line = 0; line <= 9; line++)
                        RackBox(3, new Vector3(x, .204f, -1.44f + line * .32f), new Vector3(1.33f, .009f, .009f), .002f);
                    for (int side = -1; side <= 1; side += 2)
                    {
                        RackBox(3, new Vector3(x + side * .705f, .18f, 0), new Vector3(.055f, .055f, 2.95f), .01f);
                        for (int end = -1; end <= 1; end += 2)
                        {
                            RackBox(3, new Vector3(x + side * .7f, .19f, end * 1.43f), new Vector3(.18f, .1f, .22f));
                            RackBox(1, new Vector3(x + side * .7f, .04f, end * 1.54f), new Vector3(.21f, .22f, .17f));
                        }
                    }
                    RackBox(4, new Vector3(x, .09f, -1.605f), new Vector3(.4f, .035f, .025f), .007f);
                    for (int rib = -1; rib <= 1; rib++)
                        RackBox(1, new Vector3(x, -.145f, rib * 1.1f), new Vector3(1.5f, .1f, .11f));
                }
            }

            private static Vector3 Hose(int side, float amount) =>
                new Vector3(side * .8f, .65f + amount * 1.6f, .7f + Mathf.Sin(amount * Mathf.PI) * .37f);

            private void Logo(Vector3 center)
            {
                geometry[5].Bar(center + Vector3.up * .15f, center + new Vector3(-.15f, -.1f, 0), .025f, .015f, .004f);
                geometry[5].Bar(center + new Vector3(-.15f, -.1f, 0), center + new Vector3(.15f, -.1f, 0), .025f, .015f, .004f);
                geometry[5].Bar(center + new Vector3(.15f, -.1f, 0), center + Vector3.up * .15f, .025f, .015f, .004f);
            }
        }
    }
}