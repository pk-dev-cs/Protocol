using UnityEngine;

namespace Protocol
{
    public sealed class MainMenuPresentation : MonoBehaviour
    {
        public Light SensorLight;
        public Camera MenuCamera;
        public Transform Hero;
        private float sensorIntensity;
        private float aspect;

        private void Start()
        {
            foreach (var child in GetComponentsInChildren<Transform>())
                if (child.Find("Chassis") != null && child.Find("Cargo tray") != null)
                    RobotVisualFactory.EnsureModel(child, RobotKind.Harvester);
            sensorIntensity = SensorLight != null ? SensorLight.intensity : 0;
            FrameHero();
        }

        private void LateUpdate()
        {
            if (SensorLight != null)
                SensorLight.intensity = sensorIntensity * (1 + .06f * Mathf.Sin(Time.unscaledTime * .8f));
            if (MenuCamera != null && !Mathf.Approximately(aspect, MenuCamera.aspect)) FrameHero();
        }

        private void FrameHero()
        {
            if (MenuCamera == null || Hero == null) return;
            aspect = MenuCamera.aspect;
            float halfWidth = Mathf.Tan(MenuCamera.fieldOfView * .5f * Mathf.Deg2Rad) * aspect;
            var towardHero = Hero.position + Vector3.up * .83f - MenuCamera.transform.position;
            var center = Quaternion.LookRotation(towardHero);
            MenuCamera.transform.rotation = center * Quaternion.Euler(0, -Mathf.Atan(halfWidth * .43f) * Mathf.Rad2Deg, 0);
        }
    }
}
