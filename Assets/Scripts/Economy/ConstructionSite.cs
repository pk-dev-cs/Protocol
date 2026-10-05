using UnityEngine;
using UnityEngine.AI;

namespace Protocol
{
    [RequireComponent(typeof(SelectableStructure))]
    public sealed class ConstructionSite : MonoBehaviour
    {
        public ConstructionRecipe Recipe { get; private set; }
        public float Progress { get; private set; }
        private Material material;
        private Transform progressVisual;

        public static ConstructionSite Create(Transform parent, Vector3 position, ConstructionRecipe recipe)
        {
            var root = new GameObject(recipe.DisplayName + " — construction");
            root.transform.SetParent(parent, false);
            root.transform.position = position;
            var site = root.AddComponent<ConstructionSite>();
            site.Recipe = recipe;
            site.material = new Material(Resources.Load<Shader>("FogSurface")) { color = new Color(.15f, .65f, .58f) };
            site.progressVisual = Part(root.transform, "Foundation", new Vector3(0, .1f, 0),
                new Vector3(recipe.Footprint.x, .2f, recipe.Footprint.y), site.material);
            AddObstacle(root, recipe.Footprint);
            return site;
        }

        public void SetProgress(float value)
        {
            Progress = Mathf.Clamp01(value);
            float height = .2f + Progress * 1.5f;
            progressVisual.localPosition = new Vector3(0, height / 2, 0);
            progressVisual.localScale = new Vector3(Recipe.Footprint.x, height, Recipe.Footprint.y);
        }

        public static Transform Part(Transform parent, string name, Vector3 position, Vector3 size, Material material)
        {
            var part = GameObject.CreatePrimitive(PrimitiveType.Cube);
            part.name = name;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localScale = size;
            part.GetComponent<Renderer>().sharedMaterial = material;
            part.GetComponent<Collider>().enabled = false;
            Destroy(part.GetComponent<Collider>());
            return part.transform;
        }

        public static void AddObstacle(GameObject root, Vector2 footprint)
        {
            var collider = root.AddComponent<BoxCollider>();
            collider.center = Vector3.up * 1.5f;
            collider.size = new Vector3(footprint.x, 3, footprint.y);
            var obstacle = root.AddComponent<NavMeshObstacle>();
            obstacle.shape = NavMeshObstacleShape.Box;
            obstacle.center = collider.center;
            obstacle.size = collider.size;
            obstacle.carving = true;
        }

        private void OnDestroy()
        {
            if (material != null) Destroy(material);
        }
    }
}
