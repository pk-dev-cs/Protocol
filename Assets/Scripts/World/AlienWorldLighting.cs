using UnityEngine;
using UnityEngine.Rendering;

namespace Protocol
{
    [DisallowMultipleComponent]
    public sealed class AlienWorldLighting : MonoBehaviour
    {
        [SerializeField] private float cycleDurationSeconds = 240f;
        [SerializeField, Range(0f, 1f)] private float initialPhase = .16f;
        private Light orbitalLight;
        private Material skybox;
        private float phase;

        public static AlienWorldLighting Ensure(Transform world)
        {
            var value = world.GetComponent<AlienWorldLighting>();
            if (value == null)
                value = world.gameObject.AddComponent<AlienWorldLighting>();
            value.Configure(world);
            return value;
        }

        private void Awake() => Configure(transform);

        private void OnEnable()
        {
            phase = initialPhase;
            Configure(transform);
        }

        private void Update()
        {
            if (!Application.isPlaying)
                return;
            phase = Mathf.Repeat(phase + Time.deltaTime / Mathf.Max(30f, cycleDurationSeconds), 1f);
            ApplyLighting(phase);
        }

        private void Configure(Transform world)
        {
            var sun = world.Find("Sun");
            if (sun != null)
                orbitalLight = sun.GetComponent<Light>();
            if (orbitalLight != null)
            {
                orbitalLight.type = LightType.Directional;
                orbitalLight.shadows = LightShadows.Soft;
                orbitalLight.shadowStrength = .78f;
                orbitalLight.bounceIntensity = .35f;
            }

            skybox = RenderSettings.skybox;
            if (skybox == null || skybox.shader == null || skybox.shader.name != "Protocol/Alien Night Sky")
            {
                skybox = Resources.Load<Material>("Materials/AlienNightSky");
                if (skybox == null)
                {
                    var shader = Shader.Find("Protocol/Alien Night Sky");
                    if (shader != null)
                        skybox = new Material(shader) { name = "Alien Night Sky" };
                }
                if (skybox != null)
                    RenderSettings.skybox = skybox;
            }

            var cameraTransform = world.Find("Main Camera");
            if (cameraTransform != null && cameraTransform.TryGetComponent<Camera>(out var camera))
            {
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.black;
                camera.farClipPlane = Mathf.Max(camera.farClipPlane, 600f);
                camera.allowHDR = true;
            }

            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = 85f;
            RenderSettings.fogEndDistance = 360f;
            RenderSettings.reflectionIntensity = .42f;
            ApplyLighting(Application.isPlaying ? phase : initialPhase);
        }

        private void ApplyLighting(float value)
        {
            float orbit = value * Mathf.PI * 2f;
            float blend = Mathf.Sin(orbit) * .5f + .5f;
            if (orbitalLight != null)
            {
                float elevation = 38f + Mathf.Sin(orbit * .73f) * 11f;
                orbitalLight.transform.rotation = Quaternion.Euler(elevation, -38f + value * 360f, 0);
                orbitalLight.color = Color.Lerp(new Color(.36f,.55f,1), new Color(1,.38f,.72f), blend);
                orbitalLight.intensity = Mathf.Lerp(.72f, 1.08f, blend);
            }
            RenderSettings.ambientLight = Color.Lerp(new Color(.075f,.11f,.25f), new Color(.22f,.08f,.28f), blend);
            RenderSettings.fogColor = Color.Lerp(new Color(.025f,.07f,.16f), new Color(.16f,.045f,.19f), blend);
            if (skybox != null && skybox.HasProperty("_Rotation"))
                skybox.SetFloat("_Rotation", value * 7f);
        }
    }
}