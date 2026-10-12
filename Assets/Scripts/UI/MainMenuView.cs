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
        public Button LoadGameButton;
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
        private GameObject savesPage;
        private RectTransform saveContent;
        private ScrollRect saveList;
        private Button loadSelected;
        private Button savesBack;
        private string selectedSave;
        private readonly System.Collections.Generic.List<Button> saveRows = new System.Collections.Generic.List<Button>();
        private ScreenResolutionPicker resolutions;
        private bool fullscreen;
        private bool musicEnabled;
        private int fps;
        private int backgroundFps;
        private Coroutine fade;

        public void Initialize(MainMenu owner)
        {
            menu = owner;
            EnsureLoadGameButton();
            BuildSavesPage();
            LoadGameButton.interactable = GameSaveService.HasSave;
            LoadGameButton.onClick.AddListener(menu.LoadGame);
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

        public void EnsureLoadGameButton()
        {
            if (LoadGameButton != null) return;
            var row = Instantiate(ScenariosButton.gameObject, Home.transform);
            row.name = "Load Game";
            LoadGameButton = row.GetComponentInChildren<Button>();
            LoadGameButton.onClick = new Button.ButtonClickedEvent();
            LoadGameButton.GetComponentInChildren<Text>().text = "Load Game";
            var rect = row.GetComponent<RectTransform>();
            rect.anchoredPosition = ScenariosButton.GetComponent<RectTransform>().anchoredPosition + Vector2.down * 88;
            SettingsButton.GetComponent<RectTransform>().anchoredPosition += Vector2.down * 88;
            QuitButton.GetComponent<RectTransform>().anchoredPosition += Vector2.down * 88;
        }

        public void Show(MenuPage page)
        {
            Home.SetActive(page == MenuPage.Home);
            Scenarios.SetActive(page == MenuPage.Scenarios);
            Settings.SetActive(page == MenuPage.Settings);
            savesPage.SetActive(page == MenuPage.Saves);
            SetMessage("");
            if (page == MenuPage.Saves) RefreshSaves();
            if (page == MenuPage.Settings) ReadSettings();
            SelectFirst(page);
        }

        private static RectTransform SaveRect(string name, Transform parent, Vector2 min, Vector2 max)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            return rect;
        }

        private Button SaveButton(string name, Transform parent, Vector2 min, Vector2 max)
        {
            var button = Instantiate(StartButton, parent);
            button.name = name;
            button.onClick = new Button.ButtonClickedEvent();
            var rect = button.GetComponent<RectTransform>();
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            rect.pivot = new Vector2(.5f, .5f);
            button.GetComponentInChildren<Text>().text = name;
            return button;
        }

        private void BuildSavesPage()
        {
            var source = Scenarios.GetComponent<RectTransform>();
            var page = SaveRect("Saved Games", source.parent, source.anchorMin, source.anchorMax);
            page.offsetMin = source.offsetMin;
            page.offsetMax = source.offsetMax;
            savesPage = page.gameObject;
            var heading = SaveRect("Heading", page, new Vector2(0, .91f), Vector2.one).gameObject.AddComponent<Text>();
            heading.font = StartButton.GetComponentInChildren<Text>().font;
            heading.fontSize = 25;
            heading.color = new Color(.36f, .87f, .83f);
            heading.alignment = TextAnchor.MiddleLeft;
            heading.text = "LOAD GAME";
            heading.raycastTarget = false;
            var viewport = SaveRect("Save list", page, new Vector2(0, .15f), new Vector2(1, .88f));
            viewport.gameObject.AddComponent<RectMask2D>();
            var background = viewport.gameObject.AddComponent<Image>();
            background.color = new Color(.035f, .055f, .07f, .35f);
            saveList = viewport.gameObject.AddComponent<ScrollRect>();
            saveList.viewport = viewport;
            saveList.horizontal = false;
            saveList.movementType = ScrollRect.MovementType.Clamped;
            saveList.scrollSensitivity = 36;
            saveContent = SaveRect("Entries", viewport, new Vector2(0, 1), Vector2.one);
            saveContent.pivot = new Vector2(.5f, 1);
            saveList.content = saveContent;
            var layout = saveContent.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 8;
            layout.padding = new RectOffset(0, 8, 0, 8);
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandHeight = false;
            saveContent.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            loadSelected = SaveButton("Wczytaj", page, Vector2.zero, new Vector2(.48f, .10f));
            savesBack = SaveButton("Wróć", page, new Vector2(.52f, 0), new Vector2(1, .10f));
            loadSelected.onClick.AddListener(() => { if (selectedSave != null) menu.LoadGame(selectedSave); });
            savesBack.onClick.AddListener(() => menu.ShowPage(MenuPage.Home));
            savesPage.SetActive(false);
        }

        private void RefreshSaves()
        {
            selectedSave = null;
            loadSelected.interactable = false;
            foreach (var row in saveRows)
            {
                row.gameObject.SetActive(false);
                Destroy(row.gameObject);
            }
            saveRows.Clear();
            foreach (var entry in GameSaveService.ListSaves())
            {
                var row = SaveButton(entry.Name, saveContent, Vector2.zero, Vector2.one);
                row.gameObject.AddComponent<LayoutElement>().preferredHeight = 84;
                var text = row.GetComponentInChildren<Text>();
                text.fontSize = 23;
                text.supportRichText = false;
                text.text = entry.Name + "\n" + entry.DisplayDate;
                row.onClick.AddListener(() => SelectSave(entry.Id, row));
                saveRows.Add(row);
            }
            LayoutRebuilder.ForceRebuildLayoutImmediate(saveContent);
            saveList.verticalNormalizedPosition = 1;
            if (saveRows.Count == 0) SetMessage("Brak zapisanych rozgrywek.");
        }

        private void SelectSave(string id, Button selected)
        {
            selectedSave = id;
            loadSelected.interactable = true;
            foreach (var row in saveRows)
            {
                var image = row.targetGraphic as Image;
                if (image != null) image.color = row == selected
                    ? new Color(.10f, .28f, .27f, .95f)
                    : new Color(.035f, .055f, .07f, .68f);
            }
            SetMessage("");
        }

        private void SelectFirst(MenuPage page)
        {
            var first = page == MenuPage.Home ? ScenariosButton.gameObject :
                page == MenuPage.Scenarios ? StartButton.gameObject :
                page == MenuPage.Saves ? (saveRows.Count > 0 ? saveRows[0].gameObject : savesBack.gameObject) : Volume.gameObject;
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
