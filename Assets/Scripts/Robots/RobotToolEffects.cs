using UnityEngine;
using UnityEngine.Rendering;

namespace Protocol
{
    [RequireComponent(typeof(AssemblerConstruction))]
    public sealed class RobotToolEffects : MonoBehaviour
    {
        private AssemblerConstruction construction;
        private RobotEnergy energy;
        private Transform tip;
        private LineRenderer beam;

        private void Start()
        {
            construction = GetComponent<AssemblerConstruction>();
            energy = GetComponent<RobotEnergy>();
            tip = transform.Find("Visuals/Tool tip");
            var source = new GameObject("Construction beam");
            source.transform.SetParent(transform, false);
            beam = source.AddComponent<LineRenderer>();
            beam.sharedMaterial = Resources.Load<Material>("Units/Robot Amber");
            beam.useWorldSpace = true;
            beam.positionCount = 2;
            beam.startWidth = .055f;
            beam.endWidth = .085f;
            beam.numCapVertices = 4;
            beam.shadowCastingMode = ShadowCastingMode.Off;
            beam.receiveShadows = false;
            beam.enabled = false;
        }

        private void OnDisable()
        {
            if (beam != null) beam.enabled = false;
        }

        private void LateUpdate()
        {
            if (beam == null || tip == null) return;
            var target = construction.WorkPosition;
            beam.enabled = target.HasValue && energy != null && energy.CurrentConsumptionKW > 0;
            if (!beam.enabled) return;
            beam.SetPosition(0, tip.position);
            beam.SetPosition(1, target.Value + Vector3.up * .25f);
        }
    }
}