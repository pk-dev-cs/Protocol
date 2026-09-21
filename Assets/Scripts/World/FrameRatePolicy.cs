using UnityEngine;

namespace Protocol
{
    public sealed class FrameRatePolicy : MonoBehaviour
    {
        public static readonly int[] Limits = { 30, 45, 60, 90, 120 };
        public static readonly int[] BackgroundLimits = { 15, 20, 30, 45, 60 };
        public static int SavedBackgroundLimit
        {
            get
            {
                int value = PlayerPrefs.GetInt("Settings.BackgroundFpsLimit", 30);
                return System.Array.IndexOf(BackgroundLimits, value) >= 0 ? value : 30;
            }
        }

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
            Save(value, SavedBackgroundLimit);
        }

        public static void Save(int value, int backgroundValue)
        {
            if (System.Array.IndexOf(Limits, value) < 0 ||
                System.Array.IndexOf(BackgroundLimits, backgroundValue) < 0)
                return;
            PlayerPrefs.SetInt("Settings.FpsLimit", value);
            PlayerPrefs.SetInt("Settings.BackgroundFpsLimit", backgroundValue);
            PlayerPrefs.Save();
            Apply(Application.isFocused);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            var owner = new GameObject("Frame rate policy");
            DontDestroyOnLoad(owner);
            owner.AddComponent<FrameRatePolicy>();
            Apply(Application.isFocused);
        }

        private void OnApplicationFocus(bool focused)
        {
            Apply(focused);
        }

        private static void Apply(bool focused)
        {
            Application.runInBackground = true;
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = focused ? SavedLimit : SavedBackgroundLimit;
        }
    }
}
