using UnityEngine;

namespace Protocol
{
    public sealed class RobotController : MonoBehaviour
    {
        [SerializeField, Min(1)]
        private int cargoCapacity = 10;
        [SerializeField]
        private string unitType = "Harvester";

        public string UnitType => unitType;

        public string DisplayName => gameObject.name;

        public string Status => LuaRuntime != null && LuaRuntime.State == LuaProgramState.Error ? "Error" : LuaRuntime != null && LuaRuntime.ActionStatus.Length > 0 ? LuaRuntime.ActionStatus : HarvestCycle != null && (HarvestCycle.IsRunning || HarvestCycle.State == HarvestState.Error) ? HarvestCycle.State.ToString() : Movement != null && Movement.IsMoving ? "Moving" : Movement != null && Movement.Result == MovementResult.Failed ? "Error" : "Idle";

        public RobotMovement Movement { get; private set; }

        public Transform MineDestination { get; private set; }

        public Transform BaseDestination { get; private set; }

        public RobotInventory Inventory { get; private set; }

        public RobotHarvestCycle HarvestCycle { get; private set; }

        public RobotLuaRuntime LuaRuntime { get; private set; }

        public int CurrentCargo => Inventory != null ? Inventory.Cargo : 0;

        public int CargoCapacity => Inventory != null ? Inventory.Capacity : cargoCapacity;

        public bool IsSelected { get; private set; }

        private LineRenderer selectionRing;
        private Material ringMaterial;

        public void InitializeLua(RobotLuaRuntime runtime) => LuaRuntime = runtime;

        public void InitializeEconomy(RobotInventory inventory, RobotHarvestCycle cycle, Transform baseDestination)
        {
            inventory.Initialize(cargoCapacity);
            Inventory = inventory;
            HarvestCycle = cycle;
            BaseDestination = baseDestination;
        }

        public void InitializeMovement(RobotMovement movement, Transform mineDestination)
        {
            Movement = movement;
            MineDestination = mineDestination;
        }

        public void SetSelected(bool selected)
        {
            IsSelected = selected;
            if (selected && selectionRing == null)
                CreateSelectionRing();
            if (selectionRing != null)
                selectionRing.enabled = selected;
        }

        private void CreateSelectionRing()
        {
            var ring = new GameObject("Selection ring");
            ring.transform.SetParent(transform, false);
            ring.transform.localPosition = new Vector3(0, 0.09f, 0);
            selectionRing = ring.AddComponent<LineRenderer>();
            selectionRing.useWorldSpace = false;
            selectionRing.loop = true;
            selectionRing.widthMultiplier = 0.09f;
            selectionRing.positionCount = 48;
            selectionRing.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            selectionRing.receiveShadows = false;
            ringMaterial = new Material(Resources.Load<Shader>("FogSurface"));
            var color = new Color(0.25f, 1f, 0.48f);
            ringMaterial.color = color;
            ringMaterial.EnableKeyword("_EMISSION");
            ringMaterial.SetColor("_EmissionColor", color);
            selectionRing.sharedMaterial = ringMaterial;
            for (int i = 0; i < selectionRing.positionCount; i++)
            {
                float angle = i * Mathf.PI * 2f / selectionRing.positionCount;
                selectionRing.SetPosition(i, new Vector3(Mathf.Cos(angle) * 1.45f, 0, Mathf.Sin(angle) * 1.45f));
            }
        }

        private void OnDisable() => SetSelected(false);

        private void OnDestroy()
        {
            if (ringMaterial == null)
                return;
            if (Application.isPlaying)
                Destroy(ringMaterial);
            else
                DestroyImmediate(ringMaterial);
        }
    }
}
