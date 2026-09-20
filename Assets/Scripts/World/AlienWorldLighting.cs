using UnityEngine;
using UnityEngine.Rendering;

namespace Protocol
{
    [DisallowMultipleComponent]
    public sealed class AlienWorldLighting : MonoBehaviour
    {
        [SerializeField] private float cycleDurationSeconds = 2880f;
        [SerializeField, Range(0f, 1f)] private float initialPhase = .16f;
        private Light orbitalLight;
        private Material skybox;
        private float phase;
        private Material originalSkybox;
        private GUIStyle clockStyle;
        private float nextLightScan;

        public float NightAmount { get; private set; }

        public float TimeOfDayHours => phase * 24f;
        public string ClockText
        {
            get
            {
                int minutes = Mathf.FloorToInt(TimeOfDayHours * 60f) % 1440;
                return $"{minutes / 60:00}:{minutes % 60:00}";
            }
        }

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
            if (Time.time >= nextLightScan)
            {
                nextLightScan = Time.time + 1f;
                foreach (var robot in GetComponentsInChildren<RobotController>())
                    NightWorkLight.Ensure(robot.gameObject, this, false);
                foreach (var building in GetComponentsInChildren<SelectableStructure>())
                    NightWorkLight.Ensure(building.gameObject, this, true);
            }
        }

        private void OnGUI()
        {
            if (!Application.isPlaying || PauseMenu.IsOpen)
                return;
            if (clockStyle == null)
            {
                clockStyle = new GUIStyle(GUI.skin.box)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 20,
                    fontStyle = FontStyle.Bold
                };
                clockStyle.normal.textColor = new Color(.85f, .93f, 1f);
            }
            float scale = Mathf.Clamp(Screen.height / 900f, .7f, 1.5f);
            clockStyle.fontSize = Mathf.RoundToInt(20 * scale);
            GUI.Box(new Rect(Screen.width - 166 * scale, 16 * scale, 150 * scale, 46 * scale),
                "CZAS  " + ClockText, clockStyle);
        }

        private void OnDestroy()
        {
            if (originalSkybox == null)
                return;
            RenderSettings.skybox = originalSkybox;
            Destroy(skybox);
        }

        private void Configure(Transform world)
        {
            var sun = world.Find("Sun");
            if (sun != null)
                orbitalLight = sun.GetComponent<Light>();
            if (orbitalLight != null)
            {
                orbitalLight.type = LightType.Directional;
                RenderSettings.sun = orbitalLight;
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
            if (Application.isPlaying && originalSkybox == null && skybox != null)
            {
                originalSkybox = skybox;
                skybox = new Material(originalSkybox) { name = "Planet sky (runtime)" };
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
            RenderSettings.fog = false;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = 85f;
            RenderSettings.fogEndDistance = 360f;
            RenderSettings.reflectionIntensity = .42f;
            ApplyLighting(Application.isPlaying ? phase : initialPhase);
        }

        private void ApplyLighting(float value)
        {
            float orbit = value * Mathf.PI * 2f;
            float solarHeight = Mathf.Sin(orbit - Mathf.PI * .5f);
            float daylight = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(-.08f, .35f, solarHeight));
            float twilight = 1 - Mathf.SmoothStep(0, .35f, Mathf.Abs(solarHeight));
            NightAmount = 1 - daylight;
            if (orbitalLight != null)
            {
                float elevation = Mathf.Asin(Mathf.Abs(solarHeight)) * Mathf.Rad2Deg;
                float azimuth = -38f + value * 360f + (solarHeight < 0 ? 180f : 0);
                orbitalLight.transform.rotation = Quaternion.Euler(elevation, azimuth, 0);
                var dayColor = Color.Lerp(new Color(.93f, .94f, 1f), new Color(1f, .58f, .39f), twilight);
                orbitalLight.color = Color.Lerp(new Color(.42f, .61f, 1f), dayColor, daylight);
                orbitalLight.intensity = Mathf.Lerp(.38f, 1.25f, daylight);
            }
            RenderSettings.ambientLight = Color.Lerp(new Color(.17f, .15f, .25f), new Color(.28f, .3f, .34f), daylight);
            RenderSettings.fogColor = Color.Lerp(new Color(.06f, .1f, .19f), new Color(.32f, .43f, .52f), daylight);
            if (skybox != null)
            {
                skybox.SetFloat("_Daylight", daylight);
                skybox.SetFloat("_Twilight", twilight);
            }
            if (skybox != null && skybox.HasProperty("_Rotation"))
                skybox.SetFloat("_Rotation", value * 7f);
        }
    }
}
