using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Protocol
{
    public enum MenuPage
    {
        Home,
        Scenarios,
        Settings
    }

    public sealed class MainMenu : MonoBehaviour
    {
        public MenuPage Page { get; private set; }

        public bool IsLoading { get; private set; }

        private float volume, sensitivity;
        private bool fullscreen;
        private ScreenResolutionPicker resolution;
        private int fpsLimit;
        private int backgroundFpsLimit;
        private bool musicEnabled;
        private float musicVolume;
        private string message = "";

        private void Awake()
        {
            Time.timeScale = 1;
            var cameraObject = new GameObject("Menu Camera");
            cameraObject.transform.SetParent(transform, false);
            var camera = cameraObject.AddComponent<Camera>();
            cameraObject.AddComponent<AudioListener>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.035f, 0.055f, 0.08f);
            camera.cullingMask = 0;
            ShowPage(MenuPage.Home);
        }

        public void ShowPage(MenuPage page)
        {
            if (IsLoading)
                return;
            Page = page;
            message = "";
            if (page == MenuPage.Settings)
            {
                volume = GameSettings.Volume;
                sensitivity = GameSettings.CameraSensitivity;
                fullscreen = Screen.fullScreen;
                resolution = new ScreenResolutionPicker();
                fpsLimit = FrameRatePolicy.SavedLimit;
                backgroundFpsLimit = FrameRatePolicy.SavedBackgroundLimit;
                musicEnabled = GameSettings.MusicEnabled;
                musicVolume = GameSettings.MusicVolume;
            }
        }

        public void StartScenario()
        {
            if (IsLoading)
                return;
            if (!Application.CanStreamedLevelBeLoaded("Stage01"))
            {
                message = "Scenariusz 1 nie jest dostępny w tym buildzie.";
                return;
            }

            IsLoading = true;
            StartCoroutine(LoadScenario());
        }

        private IEnumerator LoadScenario()
        {
            yield return null;
            var operation = SceneManager.LoadSceneAsync("Stage01");
            if (operation == null)
            {
                IsLoading = false;
                message = "Nie udało się wczytać scenariusza.";
                yield break;
            }

            while (!operation.isDone)
                yield return null;
        }

        private void OnGUI()
        {
            float scale = Mathf.Max(0.01f, Mathf.Min(Screen.width / 960f, Screen.height / 640f));
            Matrix4x4 previousMatrix = GUI.matrix;
            Color previousColor = GUI.color;
            bool previousEnabled = GUI.enabled;
            GUI.matrix = Matrix4x4.TRS(
                new Vector3((Screen.width - 960 * scale) / 2, (Screen.height - 640 * scale) / 2, 0),
                Quaternion.identity,
                Vector3.one * scale);
            var text = new GUIStyle(GUI.skin.label)
            {
                fontSize = 18,
                wordWrap = true
            };
            text.normal.textColor = new Color(0.72f, 0.8f, 0.86f);
            var heading = new GUIStyle(text)
            {
                fontSize = 34,
                fontStyle = FontStyle.Bold
            };
            heading.normal.textColor = Color.white;
            var brand = new GUIStyle(heading)
            {
                fontSize = 64
            };
            var button = new GUIStyle(GUI.skin.button)
            {
                fontSize = 22,
                padding = new RectOffset(20, 20, 12, 12)
            };
            GUI.color = new Color(0.12f, 0.85f, 0.77f);
            GUI.DrawTexture(new Rect(56, 60, 6, 92), Texture2D.whiteTexture);
            GUI.color = previousColor;
            GUI.Label(new Rect(80, 52, 700, 82), "PROTOCOL", brand);
            GUI.enabled = !IsLoading;
            if (Page == MenuPage.Home)
            {
                if (GUI.Button(new Rect(80, 245, 340, 62), "Scenariusze", button))
                    ShowPage(MenuPage.Scenarios);
                if (GUI.Button(new Rect(80, 325, 340, 62), "Ustawienia", button))
                    ShowPage(MenuPage.Settings);
                if (GUI.Button(new Rect(80, 405, 340, 62), "Wyjdź z gry", button))
                    Quit();
                GUI.Label(new Rect(490, 325, 390, 54), "Twoje roboty. Twój kod.", heading);
            }
            else if (Page == MenuPage.Scenarios)
            {
                GUI.Label(new Rect(80, 205, 800, 50), "Scenariusze", heading);
                GUI.Box(new Rect(80, 278, 800, 188), GUIContent.none);
                GUI.Label(new Rect(104, 296, 700, 48), "Scenariusz 1", heading);
                GUI.Label(
                    new Rect(104, 357, 470, 86),
                    "Baza, kopalnia i dwa roboty. Automatyzacja wydobycia iron.",
                    text);
                if (GUI.Button(new Rect(630, 364, 220, 60), "Rozpocznij", button))
                    StartScenario();
            }
            else
            {
                GUI.Label(new Rect(80, 195, 800, 50), "Ustawienia", heading);
                GUI.Label(new Rect(80, 250, 225, 32), "Głośność", text);
                volume = GUI.HorizontalSlider(new Rect(305, 262, 490, 24), volume, 0, 1);
                GUI.Label(new Rect(805, 250, 70, 32), Mathf.RoundToInt(volume * 100) + "%", text);
                MusicSettingsControls.Draw(new Rect(80, 298, 795, 32), ref musicEnabled, ref musicVolume, text);
                GUI.Label(new Rect(80, 346, 225, 32), "Czułość kamery", text);
                sensitivity = GUI.HorizontalSlider(new Rect(305, 358, 490, 24), sensitivity, 0.25f, 2.5f);
                GUI.Label(new Rect(805, 346, 70, 32), sensitivity.ToString("0.00") + "×", text);
                fullscreen = GUI.Toggle(
                    new Rect(80, 394, 420, 36),
                    fullscreen,
                    " Pełny ekran",
                    new GUIStyle(GUI.skin.toggle) { fontSize = 22 });
                GUI.Label(new Rect(80, 442, 225, 40), "Rozdzielczość", text);
                resolution.Draw(new Rect(305, 434, 570, 46), text, button);
                fpsLimit = FrameRatePicker.Draw(new Rect(80, 490, 795, 32), fpsLimit, text);
                backgroundFpsLimit = FrameRatePicker.DrawBackground(new Rect(80, 530, 795, 32), backgroundFpsLimit, text);
                if (GUI.Button(new Rect(630, 578, 250, 56), "Zastosuj", button))
                {
                    GameSettings.Save(volume, sensitivity, fullscreen, resolution.Selected.x, resolution.Selected.y);
                    FrameRatePolicy.Save(fpsLimit, backgroundFpsLimit);
                    GameSettings.SaveMusic(musicEnabled, musicVolume);
                    message = "Ustawienia zapisane.";
                }
            }

            float footerY = Page == MenuPage.Settings ? 578 : 538;
            if (Page != MenuPage.Home && GUI.Button(new Rect(80, footerY, 240, 56), "Wróć", button))
                ShowPage(MenuPage.Home);
            GUI.enabled = previousEnabled;
            GUI.Label(new Rect(350, footerY + 8, Page == MenuPage.Settings ? 260 : 530, 50),
                IsLoading ? "Wczytywanie scenariusza…" : message, text);
            GUI.matrix = previousMatrix;
            GUI.color = previousColor;
        }

        private void Quit()
        {
            if (Application.isEditor)
            {
                message = "Wyjście z gry działa w zbudowanej aplikacji.";
                return;
            }

            Application.Quit();
        }
    }
}
