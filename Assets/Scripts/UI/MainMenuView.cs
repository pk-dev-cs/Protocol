using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Protocol
{
    [Serializable]
    public sealed class MainMenuChoice
    {
        public Button Previous;
        public Button Next;
        public Text Value;
        private string[] labels;
        private int index;
        private Action<int> changed;

        public void Bind(string[] values, int selected, Action<int> onChanged)
        {
            labels = values;
            index = Mathf.Clamp(selected, 0, labels.Length - 1);
            changed = onChanged;
            Previous.onClick.RemoveAllListeners();
            Next.onClick.RemoveAllListeners();
            Previous.onClick.AddListener(() => Step(-1));
            Next.onClick.AddListener(() => Step(1));
            Value.text = labels[index];
        }

        private void Step(int direction)
        {
            index = (index + direction + labels.Length) % labels.Length;
            Value.text = labels[index];
            changed(index);
        }
    }

    public sealed class MainMenuView : MonoBehaviour
    {
        public GameObject Home;
        public GameObject Scenarios;
        public GameObject Settings;
        public Button ScenariosButton;
        public Button SettingsButton;
        public Button QuitButton;
        public Button StartButton;
        public Button ScenariosBack;
        public Button SettingsBack;
        public Button ApplyButton;
        public Button MusicButton;
        public Button FullscreenButton;
        public Text MusicValue;
        public Text FullscreenValue;
        public Text VolumeValue;
        public Text MusicVolumeValue;
        public Text SensitivityValue;
        public Text Message;
        public Slider Volume;
        public Slider MusicVolume;
        public Slider Sensitivity;
        public MainMenuChoice Resolution;
        public MainMenuChoice Fps;
        public MainMenuChoice BackgroundFps;
        public CanvasGroup Interaction;
        public CanvasGroup Curtain;
        private MainMenu menu;
        private ScreenResolutionPicker resolutions;
        private bool fullscreen;
        private bool musicEnabled;
        private int fps;
        private int backgroundFps;
        private Coroutine fade;

        public void Initialize(MainMenu owner)
        {
            menu = owner;
            ScenariosButton.onClick.AddListener(() => menu.ShowPage(MenuPage.Scenarios));
            SettingsButton.onClick.AddListener(() => menu.ShowPage(MenuPage.Settings));
            QuitButton.onClick.AddListener(menu.Quit);
            StartButton.onClick.AddListener(menu.StartScenario);
            ScenariosBack.onClick.AddListener(() => menu.ShowPage(MenuPage.Home));
            SettingsBack.onClick.AddListener(() => menu.ShowPage(MenuPage.Home));
            ApplyButton.onClick.AddListener(Apply);
            MusicButton.onClick.AddListener(() => { musicEnabled = !musicEnabled; UpdateValues(); });
            FullscreenButton.onClick.AddListener(() => { fullscreen = !fullscreen; UpdateValues(); });
            Volume.onValueChanged.AddListener(_ => UpdateValues());
            MusicVolume.onValueChanged.AddListener(_ => UpdateValues());
            Sensitivity.onValueChanged.AddListener(_ => UpdateValues());
            Curtain.alpha = 1;
            Curtain.blocksRaycasts = true;
        }

        public void Show(MenuPage page)
        {
            Home.SetActive(page == MenuPage.Home);
            Scenarios.SetActive(page == MenuPage.Scenarios);
            Settings.SetActive(page == MenuPage.Settings);
            SetMessage("");
            if (page == MenuPage.Settings) ReadSettings();
            SelectFirst(page);
        }

        private void SelectFirst(MenuPage page)
        {
            var first = page == MenuPage.Home ? ScenariosButton.gameObject :
                page == MenuPage.Scenarios ? StartButton.gameObject : Volume.gameObject;
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(first);
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape) && menu != null && !menu.IsLoading && menu.Page != MenuPage.Home)
                menu.ShowPage(MenuPage.Home);
        }

        private void ReadSettings()
        {
            Volume.SetValueWithoutNotify(GameSettings.Volume);
            MusicVolume.SetValueWithoutNotify(GameSettings.MusicVolume);
            Sensitivity.SetValueWithoutNotify(GameSettings.CameraSensitivity);
            fullscreen = Screen.fullScreen;
            musicEnabled = GameSettings.MusicEnabled;
            fps = FrameRatePolicy.SavedLimit;
            backgroundFps = FrameRatePolicy.SavedBackgroundLimit;
            resolutions = new ScreenResolutionPicker();
            var sizes = new string[resolutions.Options.Count];
            for (int i = 0; i < sizes.Length; i++)
                sizes[i] = resolutions.Options[i].x + " × " + resolutions.Options[i].y;
            Resolution.Bind(sizes, resolutions.SelectedIndex, i => resolutions.SelectedIndex = i);
            Fps.Bind(Array.ConvertAll(FrameRatePolicy.Limits, i => i.ToString()),
                Array.IndexOf(FrameRatePolicy.Limits, fps), i => fps = FrameRatePolicy.Limits[i]);
            BackgroundFps.Bind(Array.ConvertAll(FrameRatePolicy.BackgroundLimits, i => i.ToString()),
                Array.IndexOf(FrameRatePolicy.BackgroundLimits, backgroundFps), i => backgroundFps = FrameRatePolicy.BackgroundLimits[i]);
            UpdateValues();
        }

        private void UpdateValues()
        {
            VolumeValue.text = Mathf.RoundToInt(Volume.value * 100) + "%";
            MusicVolumeValue.text = Mathf.RoundToInt(MusicVolume.value * 100) + "%";
            SensitivityValue.text = Sensitivity.value.ToString("0.00") + "×";
            MusicValue.text = musicEnabled ? "WŁ." : "WYŁ.";
            FullscreenValue.text = fullscreen ? "WŁ." : "WYŁ.";
            MusicVolume.interactable = musicEnabled;
        }

        private void Apply()
        {
            GameSettings.Save(Volume.value, Sensitivity.value, fullscreen, resolutions.Selected.x, resolutions.Selected.y);
            FrameRatePolicy.Save(fps, backgroundFps);
            GameSettings.SaveMusic(musicEnabled, MusicVolume.value);
            SetMessage("Zapisano");
        }

        public void SetMessage(string message) => Message.text = message;

        public void SetBusy(bool busy)
        {
            Interaction.interactable = !busy;
            Interaction.blocksRaycasts = !busy;
            if (busy && EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
            if (!busy) SelectFirst(menu.Page);
        }

        public IEnumerator Fade(bool toBlack)
        {
            // A launch can interrupt the initial fade without two routines fighting over alpha.
            if (fade != null) StopCoroutine(fade);
            fade = StartCoroutine(AnimateFade(toBlack));
            yield return fade;
            fade = null;
        }

        private IEnumerator AnimateFade(bool toBlack)
        {
            Curtain.blocksRaycasts = true;
            float start = Curtain.alpha;
            float end = toBlack ? 1 : 0;
            for (float time = 0; time < .25f; time += Time.unscaledDeltaTime)
            {
                Curtain.alpha = Mathf.Lerp(start, end, Mathf.SmoothStep(0, 1, time / .25f));
                yield return null;
            }
            Curtain.alpha = end;
            Curtain.blocksRaycasts = toBlack;
        }
    }
}
