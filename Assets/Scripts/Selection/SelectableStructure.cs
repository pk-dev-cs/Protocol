using UnityEngine;

namespace Protocol
{
    public sealed class SelectableStructure : MonoBehaviour
    {
        public bool IsMine => gameObject.name == "Mine";

        public string DisplayName => IsMine ? "Kopalnia iron" : "Baza główna";

        public string Description => IsMine ? "Złoże iron • niewyczerpane\nWydobycie: 1 iron / 0,75 s na robota." : "Dostawy iron i wood\nProgramowalna produkcja harvesterów.";

        public bool IsSelected { get; private set; }

        private LineRenderer outline;
        private Material material;

        public void SetSelected(bool selected)
        {
            IsSelected = selected;
            if (selected && outline == null)
            {
                var border = new GameObject("Structure selection");
                border.transform.SetParent(transform, false);
                outline = border.AddComponent<LineRenderer>();
                outline.useWorldSpace = false;
                outline.loop = true;
                outline.positionCount = 4;
                outline.widthMultiplier = .16f;
                outline.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                outline.receiveShadows = false;
                material = new Material(Resources.Load<Shader>("FogSurface"));
                material.color = new Color(.3f, 1, .65f);
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", material.color);
                outline.sharedMaterial = material;
                outline.SetPositions(new[] {
                    new Vector3(-4.3f, .14f, -3.6f),
                    new Vector3(-4.3f, .14f, 3.6f),
                    new Vector3(4.3f, .14f, 3.6f),
                    new Vector3(4.3f, .14f, -3.6f)
                });
            }

            if (outline != null)
                outline.enabled = selected;
        }

        private void OnDisable() => SetSelected(false);

        private void OnDestroy()
        {
            if (material != null)
                Destroy(material);
        }
    }
}
