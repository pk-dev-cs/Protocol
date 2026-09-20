using UnityEngine;

namespace Protocol
{
    public static class GameSettings
    {
        public static float Volume => Mathf.Clamp01(PlayerPrefs.GetFloat("Settings.Volume", 1));
        public static bool MusicEnabled => PlayerPrefs.GetInt("Settings.MusicEnabled", 1) != 0;
        public static float MusicVolume => Mathf.Clamp01(PlayerPrefs.GetFloat("Settings.MusicVolume", .5f));

        public static void SaveMusic(bool enabled, float volume)
        {
            PlayerPrefs.SetInt("Settings.MusicEnabled", enabled ? 1 : 0);
            PlayerPrefs.SetFloat("Settings.MusicVolume", Mathf.Clamp01(volume));
            PlayerPrefs.Save();
            MusicPlayer.ApplySettings();
        }

        public static float CameraSensitivity => Mathf.Clamp(PlayerPrefs.GetFloat("Settings.CameraSensitivity", 1), 0.25f, 2.5f);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void ApplySaved()
        {
            AudioListener.volume = Volume;
            int width = PlayerPrefs.GetInt("Settings.ScreenWidth", 0);
            int height = PlayerPrefs.GetInt("Settings.ScreenHeight", 0);
            if (width > 0 && height > 0)
                Screen.SetResolution(width, height, PlayerPrefs.GetInt("Settings.Fullscreen", Screen.fullScreen ? 1 : 0) == 1);
            else if (PlayerPrefs.HasKey("Settings.Fullscreen"))
                Screen.fullScreen = PlayerPrefs.GetInt("Settings.Fullscreen") == 1;
        }

        public static void Save(float volume, float sensitivity, bool fullscreen, int width = 0, int height = 0)
        {
            PlayerPrefs.SetFloat("Settings.Volume", Mathf.Clamp01(volume));
            PlayerPrefs.SetFloat("Settings.CameraSensitivity", Mathf.Clamp(sensitivity, 0.25f, 2.5f));
            PlayerPrefs.SetInt("Settings.Fullscreen", fullscreen ? 1 : 0);
            if (width > 0 && height > 0)
            {
                PlayerPrefs.SetInt("Settings.ScreenWidth", width);
                PlayerPrefs.SetInt("Settings.ScreenHeight", height);
            }

            PlayerPrefs.Save();
            ApplySaved();
        }
    }
}
