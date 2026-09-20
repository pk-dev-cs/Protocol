using System.Collections.Generic;
using UnityEngine;

namespace Protocol
{
    public sealed class FogOfWar : MonoBehaviour
    {
        public const int Resolution = 128;
        public const float RobotSight = 22, BaseSight = 30;
        private readonly bool[] explored = new bool[Resolution * Resolution];
        private readonly bool[] visible = new bool[Resolution * Resolution];
        private readonly Color32[] fogPixels = new Color32[Resolution * Resolution];
        private readonly Color32[] mapPixels = new Color32[Resolution * Resolution];
        private readonly Color[] terrainColors = new Color[Resolution * Resolution];
        private readonly List<Material> materials = new List<Material>();
        private RobotController[] robots;
        private BaseBuilding[] bases;
        private Texture2D fogTexture;

        public Texture2D MinimapTexture { get; private set; }

        public int ExploredCellCount { get; private set; }

        private float nextUpdate;

        public void RefreshUnits() => robots = GetComponentsInChildren<RobotController>();

        public void TreeRemoved(Vector3 position)
        {
            terrainColors[Index(position)] /= .65f;
        }

        public void Initialize(Transform world)
        {
            robots = world.GetComponentsInChildren<RobotController>();
            bases = world.GetComponentsInChildren<BaseBuilding>();
            fogTexture = new Texture2D(Resolution, Resolution, TextureFormat.RGBA32, false, true)
            {
                name = "Visibility",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            MinimapTexture = new Texture2D(Resolution, Resolution, TextureFormat.RGBA32, false)
            {
                name = "Explored map",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Point
            };
            for (int z = 0; z < Resolution; z++)
                for (int x = 0; x < Resolution; x++)
                {
                    var p = CellPosition(x, z);
                    float h = ScenarioLandscape.HeightAt(p.x, p.z);
                    terrainColors[z * Resolution + x] = Color.Lerp(new Color(.26f, .37f, .21f), new Color(.63f, .66f, .55f), h / 22);
                    if (new Vector2(p.x, p.z).magnitude < 21 || Mathf.Abs(p.z - Mathf.Sin(p.x / 30) * 5) < 3.5f)
                        terrainColors[z * Resolution + x] = new Color(.5f, .45f, .3f);
                }

            var trees = world.Find("Trees");
            if (trees != null)
                foreach (Transform tree in trees)
                    terrainColors[Index(tree.position)] *= .65f;
            Refresh();
            Shader.SetGlobalTexture("_ProtocolFogMap", fogTexture);
            Shader.SetGlobalFloat("_ProtocolFogSize", ScenarioLandscape.Size);
            var shader = Resources.Load<Shader>("FogSurface");
            if (shader == null)
                throw new System.InvalidOperationException("Brak shadera mgły wojny w Resources.");
            var replacements = new Dictionary<Material, Material>();
            foreach (var renderer in world.GetComponentsInChildren<Renderer>())
            {
                var list = renderer.sharedMaterials;
                for (int i = 0; i < list.Length; i++)
                {
                    var original = list[i];
                    if (original == null)
                        continue;
                    if (!replacements.TryGetValue(original, out var replacement))
                    {
                        replacement = new Material(original)
                        {
                            shader = shader,
                            name = original.name + " (fog)"
                        };
                        // Standard's emission keyword controls whether its stored color is active.
                        if (!original.IsKeywordEnabled("_EMISSION"))
                            replacement.SetColor("_EmissionColor", Color.black);
                        replacements.Add(original, replacement);
                        materials.Add(replacement);
                    }

                    list[i] = replacement;
                }

                renderer.sharedMaterials = list;
            }
        }

        public static Vector3 CellPosition(
            int x,
            int z) => new Vector3(
            (x + .5f) * ScenarioLandscape.Size / Resolution - ScenarioLandscape.HalfSize,
            0,
            (z + .5f) * ScenarioLandscape.Size / Resolution - ScenarioLandscape.HalfSize);

        private static int Index(Vector3 point)
        {
            int x = Mathf.Clamp(
                Mathf.FloorToInt((point.x + ScenarioLandscape.HalfSize) / ScenarioLandscape.Size * Resolution),
                0,
                Resolution - 1);
            int z = Mathf.Clamp(
                Mathf.FloorToInt((point.z + ScenarioLandscape.HalfSize) / ScenarioLandscape.Size * Resolution),
                0,
                Resolution - 1);
            return z * Resolution + x;
        }

        private static bool InBounds(Vector3 p) => Mathf.Abs(p.x) <= ScenarioLandscape.HalfSize && Mathf.Abs(p.z) <= ScenarioLandscape.HalfSize;

        public bool IsVisible(Vector3 point) => InBounds(point) && visible[Index(point)];

        public bool IsExplored(Vector3 point) => InBounds(point) && explored[Index(point)];

        public void Refresh()
        {
            System.Array.Clear(visible, 0, visible.Length);
            foreach (var robot in robots)
                if (robot != null && robot.isActiveAndEnabled)
                    Reveal(robot.transform.position, RobotSight);
            foreach (var home in bases)
                if (home != null && home.isActiveAndEnabled)
                    Reveal(home.transform.position, BaseSight);
            ExploredCellCount = 0;
            for (int i = 0; i < visible.Length; i++)
            {
                explored[i] |= visible[i];
                if (explored[i])
                    ExploredCellCount++;
                fogPixels[i] = new Color32(0, 0, 0, visible[i] ? (byte)0 : explored[i] ? (byte)170 : (byte)255);
                mapPixels[i] = visible[i] ? terrainColors[i] : explored[i] ? terrainColors[i] * .35f : new Color(.025f, .035f, .05f);
                mapPixels[i].a = 255;
            }

            fogTexture.SetPixels32(fogPixels);
            fogTexture.Apply(false);
            MinimapTexture.SetPixels32(mapPixels);
            MinimapTexture.Apply(false);
        }

        private void Reveal(Vector3 position, float radius)
        {
            int center = Index(position), cx = center % Resolution, cz = center / Resolution;
            int extent = Mathf.CeilToInt(radius / ScenarioLandscape.Size * Resolution);
            for (int z = Mathf.Max(0, cz - extent); z <= Mathf.Min(Resolution - 1, cz + extent); z++)
                for (int x = Mathf.Max(0, cx - extent); x <= Mathf.Min(Resolution - 1, cx + extent); x++)
                {
                    var p = CellPosition(x, z);
                    if ((new Vector2(p.x - position.x, p.z - position.z)).sqrMagnitude <= radius * radius)
                        visible[z * Resolution + x] = true;
                }
        }

        private void Update()
        {
            if (PauseMenu.IsOpen || fogTexture == null || Time.time < nextUpdate)
                return;
            nextUpdate = Time.time + .1f;
            Refresh();
        }

        private void OnDestroy()
        {
            foreach (var material in materials)
                if (material != null)
                    Destroy(material);
            if (fogTexture != null)
                Destroy(fogTexture);
            if (MinimapTexture != null)
                Destroy(MinimapTexture);
        }
    }
}
