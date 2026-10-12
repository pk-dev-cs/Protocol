using UnityEngine;

namespace Protocol
{
    [DisallowMultipleComponent]
    public sealed class GameCursor : MonoBehaviour
    {
        private const string TexturePath = "UI/CursorRedCyberHand";
        private static readonly Vector2 Hotspot = new Vector2(2, 0);
        private Texture2D texture;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            var cursorTexture = Resources.Load<Texture2D>(TexturePath);
            if (cursorTexture == null)
            {
                Debug.LogWarning("Game cursor texture is missing: " + TexturePath);
                return;
            }

            var owner = new GameObject("Game cursor");
            DontDestroyOnLoad(owner);
            var cursor = owner.AddComponent<GameCursor>();
            cursor.texture = cursorTexture;
            cursor.Apply();
        }

        private void OnEnable()
        {
            texture = Resources.Load<Texture2D>(TexturePath);
            if (texture != null) Apply();
        }

        private void Apply() => Cursor.SetCursor(texture, Hotspot, CursorMode.ForceSoftware);

        private void OnApplicationFocus(bool hasFocus)
        {
            if (hasFocus && texture != null) Apply();
        }

        private void OnDestroy() => Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
    }
}