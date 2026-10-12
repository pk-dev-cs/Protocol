using UnityEngine;

namespace Protocol
{
    public static class RobotVisualFactory
    {
        public static Transform Create(string name, Transform parent, Vector3 position, RobotKind kind)
        {
            var root = new GameObject(name).transform;
            root.SetParent(parent, false);
            root.localPosition = position;
            EnsureModel(root, kind);
            return root;
        }

        public static void EnsureModel(Transform owner, RobotKind kind)
        {
            if (owner.Find("Visuals") != null) return;
            var prefab = Resources.Load<GameObject>("Units/" + kind);
            if (prefab == null) throw new System.InvalidOperationException("Missing robot model: " + kind);
            for (int i = owner.childCount - 1; i >= 0; i--)
            {
                var child = owner.GetChild(i);
                if (child.GetComponent<MeshRenderer>() == null) continue;
                child.gameObject.SetActive(false);
                if (Application.isPlaying) Object.Destroy(child.gameObject);
                else Object.DestroyImmediate(child.gameObject);
            }
            GameObject model;
#if UNITY_EDITOR
            if (!Application.isPlaying)
                model = (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(prefab, owner);
            else
#endif
                model = Object.Instantiate(prefab, owner, false);
            model.name = "Visuals";
            var collider = owner.GetComponent<BoxCollider>();
            if (collider == null) collider = owner.gameObject.AddComponent<BoxCollider>();
            collider.center = new Vector3(0, .74f, 0);
            collider.size = new Vector3(1.95f, 1.46f, 1.9f);
        }
    }
}