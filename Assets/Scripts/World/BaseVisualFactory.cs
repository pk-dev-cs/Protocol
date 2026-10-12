using UnityEngine;

namespace Protocol
{
    public static class BaseVisualFactory
    {
        public static void EnsureModel(Transform owner)
        {
            if (owner.Find("Visuals") != null) return;
            var prefab = Resources.Load<GameObject>("Buildings/Base");
            if (prefab == null) throw new System.InvalidOperationException("Missing base model: Buildings/Base");
            foreach (Transform child in owner)
            {
                var renderer = child.GetComponent<MeshRenderer>();
                if (renderer != null) renderer.enabled = false;
            }
            GameObject model;
#if UNITY_EDITOR
            if (!Application.isPlaying)
                model = (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(prefab, owner);
            else
#endif
                model = Object.Instantiate(prefab, owner, false);
            model.name = "Visuals";
        }
    }
}