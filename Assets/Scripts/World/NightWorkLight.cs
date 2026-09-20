using UnityEngine;

namespace Protocol
{
    public sealed class NightWorkLight : MonoBehaviour
    {
        private AlienWorldLighting cycle;
        private Light workLight;
        private Renderer lamp;
        private Material material;
        private FogOfWar visibility;
        private bool building;

        public static void Ensure(GameObject owner, AlienWorldLighting lighting, bool isBuilding)
        {
            if (owner.GetComponent<NightWorkLight>() != null)
                return;
            var controller = owner.AddComponent<NightWorkLight>();
            controller.cycle = lighting;
            controller.building = isBuilding;
            controller.visibility = lighting.GetComponentInParent<FogOfWar>();
            controller.CreateLamp();
        }

        private void CreateLamp()
        {
            var source = new GameObject("Night work light");
            source.transform.SetParent(transform, false);
            source.transform.localPosition = new Vector3(0, building ? 4f : 1.8f, 0);
            workLight = source.AddComponent<Light>();
            workLight.type = LightType.Point;
            workLight.color = building ? new Color(1f, .75f, .44f) : new Color(.55f, .85f, 1f);
            workLight.range = building ? 17f : 9f;
            workLight.shadows = LightShadows.None;
            workLight.cullingMask = ~(1 << 31);
            var bulb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            bulb.name = "Work lamp";
            bulb.transform.SetParent(source.transform, false);
            bulb.transform.localScale = Vector3.one * (building ? .35f : .18f);
            var collider = bulb.GetComponent<Collider>();
            collider.enabled = false;
            Destroy(collider);
            material = new Material(Resources.Load<Shader>("FogSurface"));
            material.color = workLight.color;
            lamp = bulb.GetComponent<Renderer>();
            lamp.sharedMaterial = material;
            lamp.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            Update();
        }

        private void Update()
        {
            if (cycle == null || workLight == null)
                return;
            float night = cycle.NightAmount;
            bool visible = visibility == null || visibility.IsVisible(transform.position);
            workLight.enabled = visible && night > .02f;
            workLight.intensity = night * (building ? 25f : 12f);
            lamp.enabled = visible && night > .02f;
            material.SetColor("_EmissionColor", workLight.color * night * 3f);
        }

        private void OnDestroy()
        {
            if (material != null)
                Destroy(material);
        }
    }
}
