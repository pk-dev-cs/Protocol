using UnityEngine;
using UnityEngine.Rendering;

namespace Protocol
{
    [DisallowMultipleComponent]
    public sealed class MoveCommandMarker : MonoBehaviour
    {
        private const float Duration = .45f;
        private const int Segments = 32;
        private LineRenderer line;
        private Material material;
        private Collider terrain;
        private Vector3 destination;
        private float startedAt;

        public void Show(Vector3 point, Collider ground)
        {
            if (line == null)
            {
                var shader = Resources.Load<Shader>("MoveCommandMarker");
                if (shader == null)
                {
                    Debug.LogWarning("Move command marker shader is missing.", this);
                    return;
                }
                var owner = new GameObject("Move command marker");
                owner.transform.SetParent(transform, false);
                line = owner.AddComponent<LineRenderer>();
                line.useWorldSpace = true;
                line.loop = true;
                line.positionCount = Segments;
                line.widthMultiplier = .09f;
                line.shadowCastingMode = ShadowCastingMode.Off;
                line.receiveShadows = false;
                line.lightProbeUsage = LightProbeUsage.Off;
                line.reflectionProbeUsage = ReflectionProbeUsage.Off;
                material = new Material(shader);
                line.sharedMaterial = material;
            }

            terrain = ground;
            destination = point;
            startedAt = Time.unscaledTime;
            enabled = true;
            line.enabled = true;
            Draw(0);
        }

        private void Update()
        {
            if (line == null || !line.enabled)
                return;
            float progress = (Time.unscaledTime - startedAt) / Duration;
            if (progress >= 1)
            {
                Hide();
                return;
            }
            Draw(progress);
        }

        private void Draw(float progress)
        {
            float radius = Mathf.Lerp(.95f, .6f, Mathf.SmoothStep(0, 1, progress));
            var color = new Color(.25f, 1f, .65f, .9f * (1 - progress));
            line.startColor = line.endColor = color;
            Bounds bounds = terrain != null ? terrain.bounds : default;
            for (int i = 0; i < Segments; i++)
            {
                float angle = i * Mathf.PI * 2 / Segments;
                Vector3 point = destination + new Vector3(Mathf.Cos(angle) * radius, 0, Mathf.Sin(angle) * radius);
                if (terrain != null && terrain.enabled)
                {
                    var ray = new Ray(new Vector3(point.x, bounds.max.y + 1, point.z), Vector3.down);
                    if (terrain.Raycast(ray, out RaycastHit hit, bounds.size.y + 2))
                        point = hit.point;
                }
                line.SetPosition(i, point + Vector3.up * .1f);
            }
        }

        public void Hide()
        {
            if (line != null)
                line.enabled = false;
            enabled = false;
        }

        private void OnDisable()
        {
            if (line != null)
                line.enabled = false;
        }

        private void OnDestroy()
        {
            if (line != null)
                Destroy(line.gameObject);
            if (material != null)
                Destroy(material);
        }
    }
}
