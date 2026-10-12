using UnityEngine;
using UnityEngine.AI;

namespace Protocol
{
    public sealed class BuildingPlacement : MonoBehaviour
    {
        [SerializeField] private ConstructionRecipe solar = new ConstructionRecipe();
        [SerializeField] private ConstructionRecipe station = new ConstructionRecipe
        {
            Kind = ConstructionKind.ChargingStation, WoodCost = 100, DurationSeconds = 20
        };
        [SerializeField] private ConstructionRecipe batteryStorage = new ConstructionRecipe
        {
            Kind = ConstructionKind.BatteryStorage, WoodCost = 150, DurationSeconds = 25
        };
        private AssemblerConstruction builder;
        private ConstructionRecipe recipe;
        private GameObject preview;
        private Material previewMaterial;
        private SelectionManager selection;
        private Collider terrain;
        private Camera worldCamera;
        private string message = "";
        public bool IsPlacing => builder != null && recipe != null;

        private void Start()
        {
            selection = GetComponent<SelectionManager>();
            terrain = transform.Find("World/Terrain").GetComponent<Collider>();
            worldCamera = transform.Find("World/Main Camera").GetComponent<Camera>();
            selection.SelectionChanged += OnSelectionChanged;
        }

        public ConstructionRecipe Recipe(ConstructionKind kind) => kind == ConstructionKind.SolarPanel ? solar : kind == ConstructionKind.ChargingStation ? station : batteryStorage;

        public void Begin(AssemblerConstruction assembler, ConstructionKind kind)
        {
            Cancel();
            if (assembler == null || !assembler.isActiveAndEnabled || assembler.GetComponent<RobotEnergy>().IsDepleted) return;
            recipe = JsonUtility.FromJson<ConstructionRecipe>(JsonUtility.ToJson(Recipe(kind)));
            if (!recipe.IsValid) { recipe = null; return; }
            builder = assembler;
            selection.CancelDrag();
            preview = new GameObject("Construction preview");
            preview.layer = 2;
            preview.transform.SetParent(transform, false);
            previewMaterial = new Material(Resources.Load<Shader>("FogSurface"));
            previewMaterial.EnableKeyword("_EMISSION");
            var line = preview.AddComponent<LineRenderer>();
            line.sharedMaterial = previewMaterial;
            line.useWorldSpace = false;
            line.loop = true;
            line.positionCount = 4;
            line.widthMultiplier = .18f;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            float x = recipe.Footprint.x / 2, z = recipe.Footprint.y / 2;
            line.SetPositions(new[] { new Vector3(-x, .2f, -z), new Vector3(-x, .2f, z),
                new Vector3(x, .2f, z), new Vector3(x, .2f, -z) });
            preview.SetActive(false);
        }

        public void Cancel()
        {
            builder = null;
            recipe = null;
            if (preview != null) Destroy(preview);
            if (previewMaterial != null) Destroy(previewMaterial);
            preview = null;
            previewMaterial = null;
        }

        public bool HandleWorldInput(Event e)
        {
            if (!IsPlacing) return false;
            if (e.type == EventType.MouseDown && e.button == 1)
            {
                Cancel();
                e.Use();
                return true;
            }
            bool overUI = UnitPanel.ContainsPointer(e.mousePosition);
            var ray = worldCamera.ScreenPointToRay(new Vector3(e.mousePosition.x, Screen.height - e.mousePosition.y, 0));
            bool hitGround = !overUI && terrain.Raycast(ray, out _, worldCamera.farClipPlane);
            preview.SetActive(hitGround);
            bool valid = false;
            Vector3 position = default;
            if (hitGround && terrain.Raycast(ray, out RaycastHit hit, worldCamera.farClipPlane))
            {
                position = hit.point;
                preview.transform.position = position;
                valid = Validate(builder, recipe, position, out _, out message);
                Color color = valid ? new Color(.2f, 1, .65f) : new Color(1, .2f, .15f);
                previewMaterial.color = color;
                previewMaterial.SetColor("_EmissionColor", color);
            }
            if (e.type == EventType.MouseDown && e.button == 0 && !overUI)
            {
                if (valid && builder.TryBegin(this, recipe, position)) Cancel();
                e.Use();
            }
            if (e.type == EventType.Repaint && hitGround)
                GUI.Box(new Rect(Mathf.Clamp(e.mousePosition.x + 18, 0, Screen.width - 330),
                    Mathf.Clamp(e.mousePosition.y + 18, 0, Screen.height - 32), 330, 30), valid ? recipe.DisplayName : message);
            return true;
        }

        public bool Validate(AssemblerConstruction assembler, ConstructionRecipe data, Vector3 position,
            out Vector3 approach, out string reason)
        {
            approach = default;
            reason = "Niedostępne miejsce";
            if (assembler == null || !assembler.isActiveAndEnabled || data == null || !data.IsValid ||
                !float.IsFinite(position.x) || !float.IsFinite(position.y) || !float.IsFinite(position.z)) return false;
            if (assembler.GetComponent<RobotEnergy>().IsDepleted) { reason = "Brak energii robota"; return false; }
            if (GetComponent<ResourceManager>().Wood < data.WoodCost) { reason = "Brak wood"; return false; }
            float hx = data.Footprint.x / 2, hz = data.Footprint.y / 2;
            if (Mathf.Abs(position.x) + hx > ScenarioLandscape.HalfSize - 2 ||
                Mathf.Abs(position.z) + hz > ScenarioLandscape.HalfSize - 2) return false;
            var fog = GetComponent<FogOfWar>();
            for (int x = -1; x <= 1; x++)
                for (int z = -1; z <= 1; z++)
                {
                    var p = position + new Vector3(x * hx, 0, z * hz);
                    if (fog != null && !fog.IsExplored(p)) { reason = "Nieodkryty teren"; return false; }
                    if (Mathf.Abs(ScenarioLandscape.HeightAt(p.x, p.z) - position.y) > data.MaximumHeightDifference)
                    { reason = "Teren jest zbyt nierówny"; return false; }
                }
            foreach (var collider in Physics.OverlapBox(position + Vector3.up * 1.5f,
                new Vector3(hx + .3f, 1.4f, hz + .3f), Quaternion.identity, ~0, QueryTriggerInteraction.Ignore))
            {
                if (collider == terrain) continue;
                reason = "Miejsce jest zajęte";
                return false;
            }
            if (!NavMesh.SamplePosition(assembler.transform.position, out NavMeshHit start, 3, NavMesh.AllAreas)) return false;
            Vector3[] offsets = { new Vector3(0, 0, -hz - 2.5f), new Vector3(hx + 2.5f, 0, 0),
                new Vector3(0, 0, hz + 2.5f), new Vector3(-hx - 2.5f, 0, 0) };
            var path = new NavMeshPath();
            foreach (var offset in offsets)
            {
                var target = position + offset;
                target.y = ScenarioLandscape.HeightAt(target.x, target.z);
                if (!NavMesh.SamplePosition(target, out NavMeshHit end, 1, NavMesh.AllAreas)) continue;
                if (NavMesh.CalculatePath(start.position, end.position, NavMesh.AllAreas, path) && path.status == NavMeshPathStatus.PathComplete)
                {
                    approach = end.position;
                    reason = "";
                    return true;
                }
            }
            reason = "Brak trasy do budowy";
            return false;
        }

        private void OnSelectionChanged()
        {
            if (IsPlacing && (selection.SelectedRobot == null || selection.SelectedRobot.gameObject != builder.gameObject)) Cancel();
        }

        private void Update()
        {
            if (recipe != null && (builder == null || !builder.isActiveAndEnabled)) Cancel();
            if (preview != null && PauseMenu.IsOpen) preview.SetActive(false);
        }

        private void OnDisable() => Cancel();
        private void OnDestroy()
        {
            if (selection != null) selection.SelectionChanged -= OnSelectionChanged;
        }
    }
}
