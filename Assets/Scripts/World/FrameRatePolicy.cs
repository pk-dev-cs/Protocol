using UnityEngine;

namespace Protocol
{
    public sealed class FrameRatePolicy : MonoBehaviour
    {
        public static readonly int[] Limits = { 30, 45, 60, 90, 120 };
        public static int SavedLimit
        {
            get
            {
                int value = PlayerPrefs.GetInt("Settings.FpsLimit", 120);
                return System.Array.IndexOf(Limits, value) >= 0 ? value : 120;
            }
        }

        public static void Save(int value)
        {
            if (System.Array.IndexOf(Limits, value) < 0)
                return;
            PlayerPrefs.SetInt("Settings.FpsLimit", value);
            PlayerPrefs.Save();
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = Application.isFocused ? value : 20;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            var owner = new GameObject("Frame rate policy");
            DontDestroyOnLoad(owner);
            owner.AddComponent<FrameRatePolicy>();
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = SavedLimit;
            Application.runInBackground = false;
        }

        private void OnApplicationFocus(bool focused)
        {
            Application.targetFrameRate = focused ? SavedLimit : 20;
        }
    }
}
