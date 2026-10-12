using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Protocol
{
    public sealed class ScenarioLoadingScreen : MonoBehaviour
    {
        private static ScenarioLoadingScreen active;
        public static bool IsVisible => active != null;

        private CanvasGroup group;
        private Text heading;
        private Text status;
        private RectTransform progress;
        private RectTransform activity;
        private RectTransform scan;
        private Button returnButton;
        private string targetScene;
        private bool sceneRendered;
        private bool activating;
        private float displayedProgress;
        private Action<string> failed;

        public static bool Begin(Action<string> onFailure)
        {
            if (active != null) return false;
            var owner = new GameObject("Scenario loading screen");
            var screen = owner.AddComponent<ScenarioLoadingScreen>();
            screen.failed = onFailure;
            screen.StartCoroutine(screen.Load("Stage01", "SCENARIUSZ 1"));
            return true;
        }

        private void Awake()
        {
            active = this;
            DontDestroyOnLoad(gameObject);
            BuildView();
            RenderPipelineManager.endCameraRendering += CameraRendered;
            Camera.onPostRender += CameraRendered;
        }

        private void OnDestroy()
        {
            RenderPipelineManager.endCameraRendering -= CameraRendered;
            Camera.onPostRender -= CameraRendered;
            if (active == this) active = null;
        }

        private void CameraRendered(ScriptableRenderContext context, Camera camera) => CameraRendered(camera);

        private void CameraRendered(Camera camera)
        {
            if (camera != null && camera.isActiveAndEnabled && camera.CompareTag("MainCamera") &&
                camera.gameObject.scene.name == targetScene && camera.targetTexture == null)
                sceneRendered = true;
        }

        private IEnumerator Load(string sceneName, string title)
        {
            targetScene = sceneName;
            sceneRendered = false;
            activating = false;
            displayedProgress = 0;
            heading.text = title;
            status.text = sceneName == "Stage01" ? "Przygotowywanie terenu" : "Powrót do menu";
            returnButton.gameObject.SetActive(false);
            progress.gameObject.SetActive(true);
            activity.gameObject.SetActive(false);
            SetProgress(0);
            yield return Fade(1);
            // Let the overlay reach the display before scene loading can occupy the main thread.
            yield return null;

            AsyncOperation operation = null;
            string error = null;
            try
            {
                operation = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
                if (operation == null) error = "Nie udało się rozpocząć ładowania sceny.";
                else operation.allowSceneActivation = false;
            }
            catch (Exception exception)
            {
                error = "Nie udało się rozpocząć ładowania sceny.";
                Debug.LogException(exception);
            }

            if (error != null)
            {
                if (failed != null)
                {
                    failed(error);
                    yield return Fade(0);
                    Destroy(gameObject);
                }
                else ShowFailure(error);
                yield break;
            }

            while (operation.progress < .9f)
            {
                SetProgress(operation.progress / .9f);
                yield return null;
            }

            activating = true;
            status.text = sceneName == "Stage01" ? "Uruchamianie scenariusza" : "Otwieranie menu";
            progress.gameObject.SetActive(false);
            activity.gameObject.SetActive(true);
            yield return null;
            operation.allowSceneActivation = true;
            while (!operation.isDone) yield return null;
            if (GameSaveService.HasPendingLoad)
            {
                status.text = "Odtwarzanie zapisu";
                yield return null;
                var restore = GameSaveService.RestorePending();
                while (true)
                {
                    object next = null;
                    bool more = false;
                    try { more = restore.MoveNext(); if (more) next = restore.Current; }
                    catch (Exception exception)
                    {
                        error = "Nie udało się odtworzyć zapisu. Wróć do menu.";
                        Debug.LogException(exception);
                    }
                    if (error != null)
                    {
                        (restore as IDisposable)?.Dispose();
                        GameSaveService.ClearPending();
                        ShowFailure(error);
                        yield break;
                    }
                    if (!more) break;
                    yield return next;
                }
            }
            failed = null;

            float deadline = Time.realtimeSinceStartup + 15;
            while (!Ready())
            {
                if (Time.realtimeSinceStartup > deadline)
                {
                    ShowFailure("Nie udało się przygotować widoku sceny.");
                    yield break;
                }
                yield return null;
            }

            yield return Fade(0);
            Destroy(gameObject);
        }

        private bool Ready()
        {
            if (!sceneRendered || SceneManager.GetActiveScene().name != targetScene) return false;
            if (targetScene != "Stage01") return true;
            var bootstrap = FindAnyObjectByType<ScenarioBootstrap>();
            return bootstrap != null && bootstrap.IsInitialized;
        }

        private void ShowFailure(string message)
        {
            status.text = message;
            activity.gameObject.SetActive(false);
            returnButton.gameObject.SetActive(true);
            if (EventSystem.current == null)
            {
                var events = new GameObject("Loading recovery input", typeof(EventSystem), typeof(StandaloneInputModule));
                events.transform.SetParent(transform, false);
            }
            EventSystem.current.SetSelectedGameObject(returnButton.gameObject);
        }

        private void Update()
        {
            float phase = Mathf.Repeat(Time.unscaledTime * .22f, 1);
            scan.anchorMin = new Vector2(phase - .06f, 0);
            scan.anchorMax = new Vector2(phase, 1);
            if (activating)
            {
                float position = Mathf.PingPong(Time.unscaledTime * .6f, .78f);
                activity.anchorMin = new Vector2(position, 0);
                activity.anchorMax = new Vector2(position + .22f, 1);
            }
        }

        private void SetProgress(float value)
        {
            displayedProgress = Mathf.Max(displayedProgress, Mathf.Clamp01(value));
            progress.anchorMax = new Vector2(displayedProgress, 1);
        }

        private IEnumerator Fade(float target)
        {
            float start = group.alpha;
            for (float elapsed = 0; elapsed < .2f; elapsed += Time.unscaledDeltaTime)
            {
                group.alpha = Mathf.Lerp(start, target, Mathf.SmoothStep(0, 1, elapsed / .2f));
                yield return null;
            }
            group.alpha = target;
        }

        private void BuildView()
        {
            var canvasObject = new GameObject("Loading Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
            canvasObject.transform.SetParent(transform, false);
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 32000;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = .5f;
            group = canvasObject.GetComponent<CanvasGroup>();
            group.alpha = 0;
            var background = Panel("Violet background", canvasObject.transform, Vector2.zero, Vector2.one, new Color(.025f, .018f, .052f));
            background.GetComponent<Image>().raycastTarget = true;
            var contours = Rect("Topography", canvasObject.transform, Vector2.zero, Vector2.one);
            contours.gameObject.AddComponent<LoadingTopography>().color = new Color(.30f, .45f, .51f, .16f);
            scan = Panel("Atmospheric scan", canvasObject.transform, Vector2.zero, new Vector2(.06f, 1), new Color(.3f, .65f, .65f, .025f));
            heading = Label("", canvasObject.transform, new Vector2(.16f, .52f), new Vector2(.84f, .61f), 40, new Color(.92f, .94f, .97f));
            status = Label("", canvasObject.transform, new Vector2(.15f, .465f), new Vector2(.85f, .515f), 19, new Color(.56f, .65f, .73f));
            var track = Panel("Progress track", canvasObject.transform, new Vector2(.33f, .422f), new Vector2(.67f, .425f), new Color(.13f, .19f, .23f));
            progress = Panel("Progress", track, Vector2.zero, new Vector2(0, 1), new Color(.3f, .84f, .77f));
            activity = Panel("Activation", track, Vector2.zero, new Vector2(.22f, 1), new Color(.3f, .84f, .77f));
            Label("P R O T O C O L", canvasObject.transform, new Vector2(.25f, .07f), new Vector2(.75f, .12f), 20, new Color(.55f, .67f, .71f));
            var buttonRect = Panel("Return to menu", canvasObject.transform, new Vector2(.38f, .30f), new Vector2(.62f, .36f), new Color(.1f, .22f, .25f));
            var buttonImage = buttonRect.GetComponent<Image>();
            buttonImage.raycastTarget = true;
            returnButton = buttonRect.gameObject.AddComponent<Button>();
            returnButton.targetGraphic = buttonImage;
            Label("Wróć do menu", buttonRect, Vector2.zero, Vector2.one, 20, Color.white);
            returnButton.onClick.AddListener(() => StartCoroutine(Load("MainMenu", "PROTOCOL")));
            returnButton.gameObject.SetActive(false);
        }

        private static RectTransform Rect(string name, Transform parent, Vector2 min, Vector2 max)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            return rect;
        }

        private static RectTransform Panel(string name, Transform parent, Vector2 min, Vector2 max, Color color)
        {
            var rect = Rect(name, parent, min, max);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return rect;
        }

        private static Text Label(string text, Transform parent, Vector2 min, Vector2 max, int size, Color color)
        {
            var label = Rect("Label", parent, min, max).gameObject.AddComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.text = text;
            label.fontSize = size;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = color;
            label.raycastTarget = false;
            return label;
        }
    }
}
