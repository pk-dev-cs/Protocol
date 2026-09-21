using UnityEngine;
using UnityEngine.SceneManagement;

namespace Protocol
{
    public sealed class PauseMenu : MonoBehaviour
    {
        private static PauseMenu active;

        public static bool IsOpen => active != null && active.open;

        private bool open, settings, leaving;
        private float previousTimeScale, volume, sensitivity;
        private bool fullscreen;
        private ScreenResolutionPicker resolution;
        private int fpsLimit;
        private int backgroundFpsLimit;
        private bool musicEnabled;
        private float musicVolume;
        private string message = "";

        private void OnEnable() => active = this;

        public void Open()
        {
            if (open)
                return;
            previousTimeScale = Time.timeScale;
            Time.timeScale = 0;
            open = true;
            settings = false;
            message = "";
            GetComponent<SelectionManager>()?.CancelDrag();
        }

        public void Resume()
        {
            if (!open)
                return;
            open = false;
            Time.timeScale = previousTimeScale;
        }

        public void ReturnToMainMenu()
        {
            if (leaving || !Application.CanStreamedLevelBeLoaded("MainMenu"))
                return;
            leaving = true;
            Resume();
            Time.timeScale = 1;
            SceneManager.LoadSceneAsync("MainMenu");
        }

        private void OnGUI()
        {
            GUI.depth = -100;
            var e = Event.current;
            if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape)
            {
                if (RobotProgrammingPanel.DismissCompletion())
                {
                    e.Use();
                    return;
                }

                if (open && settings)
                    settings = false;
                else if (open)
                    Resume();
                else
                    Open();
                e.Use();
            }

            if (!open)
                return;
            Color old = GUI.color;
            GUI.color = new Color(0, 0, 0, .8f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = old;
            var oldMatrix = GUI.matrix;
            float scale = Mathf.Max(.01f, Mathf.Min(Screen.width / 960f, Screen.height / 640f));
            GUI.matrix = Matrix4x4.TRS(
                new Vector3((Screen.width - 960 * scale) / 2, (Screen.height - 640 * scale) / 2, 0),
                Quaternion.identity,
                Vector3.one * scale);
            GUI.Box(new Rect(180, 60, 600, 540), GUIContent.none);
            var label = new GUIStyle(GUI.skin.label)
            {
                fontSize = 20,
                wordWrap = true
            };
            var title = new GUIStyle(label)
            {
                fontSize = 32,
                fontStyle = FontStyle.Bold
            };
            var button = new GUIStyle(GUI.skin.button)
            {
                fontSize = 22
            };
            GUI.Label(new Rect(220, 88, 520, 54), settings ? "Ustawienia" : "Gra wstrzymana", title);
            if (settings)
            {
                GUI.Label(new Rect(220, 156, 225, 32), "Głośność", label);
                volume = GUI.HorizontalSlider(new Rect(445, 168, 215, 24), volume, 0, 1);
                GUI.Label(new Rect(670, 156, 70, 32), Mathf.RoundToInt(volume * 100) + "%", label);
                MusicSettingsControls.Draw(new Rect(220, 202, 520, 32), ref musicEnabled, ref musicVolume, label);
                GUI.Label(new Rect(220, 250, 225, 32), "Czułość kamery", label);
                sensitivity = GUI.HorizontalSlider(new Rect(445, 262, 215, 24), sensitivity, .25f, 2.5f);
                GUI.Label(new Rect(670, 250, 70, 32), sensitivity.ToString("0.00") + "×", label);
                fullscreen = GUI.Toggle(
                    new Rect(220, 298, 520, 40),
                    fullscreen,
                    " Pełny ekran",
                    new GUIStyle(GUI.skin.toggle) { fontSize = 22 });
                GUI.Label(new Rect(220, 354, 225, 40), "Rozdzielczość", label);
                resolution.Draw(new Rect(445, 346, 295, 40), label, button);
                fpsLimit = FrameRatePicker.Draw(new Rect(220, 398, 520, 32), fpsLimit, label);
                backgroundFpsLimit = FrameRatePicker.DrawBackground(new Rect(220, 440, 520, 32), backgroundFpsLimit, label);
                if (GUI.Button(new Rect(220, 492, 250, 48), "Zastosuj", button))
                {
                    GameSettings.Save(volume, sensitivity, fullscreen, resolution.Selected.x, resolution.Selected.y);
                    FrameRatePolicy.Save(fpsLimit, backgroundFpsLimit);
                    GameSettings.SaveMusic(musicEnabled, musicVolume);
                    message = "Ustawienia zapisane.";
                }

                if (GUI.Button(new Rect(490, 492, 250, 48), "Wróć", button))
                {
                    settings = false;
                    message = "";
                }
            }
            else
            {
                if (GUI.Button(new Rect(220, 160, 520, 56), "Wróć do gry", button))
                    Resume();
                if (GUI.Button(new Rect(220, 235, 520, 56), "Ustawienia", button))
                {
                    settings = true;
                    volume = GameSettings.Volume;
                    sensitivity = GameSettings.CameraSensitivity;
                    fullscreen = Screen.fullScreen;
                    resolution = new ScreenResolutionPicker();
                    fpsLimit = FrameRatePolicy.SavedLimit;
                    backgroundFpsLimit = FrameRatePolicy.SavedBackgroundLimit;
                    musicEnabled = GameSettings.MusicEnabled;
                    musicVolume = GameSettings.MusicVolume;
                }

                if (GUI.Button(new Rect(220, 310, 520, 56), "Wyjdź do menu głównego", button))
                    ReturnToMainMenu();
                if (GUI.Button(new Rect(220, 385, 520, 56), "Wyjdź z gry", button))
                {
                    if (Application.isEditor)
                        message = "Wyjście działa w zbudowanej grze.";
                    else
                        Application.Quit();
                }

                GUI.Label(
                    new Rect(220, 455, 520, 68),
                    "Wyjście ze scenariusza oznacza utratę postępu i niezapisanego kodu.",
                    label);
            }

            GUI.Label(new Rect(220, settings ? 552 : 530, 520, settings ? 32 : 50), message, label);
            GUI.matrix = oldMatrix;
            if (e.isMouse || e.isKey || e.type == EventType.ScrollWheel)
                e.Use();
        }

        private void OnDisable()
        {
            Resume();
            if (active == this)
                active = null;
        }
    }
}
