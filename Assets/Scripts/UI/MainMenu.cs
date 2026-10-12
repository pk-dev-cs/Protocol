using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Protocol
{
    public enum MenuPage
    {
        Home,
        Scenarios,
        Settings,
        Saves
    }

    [RequireComponent(typeof(MainMenuView))]
    public sealed class MainMenu : MonoBehaviour
    {
        public MenuPage Page { get; private set; }
        public bool IsLoading { get; private set; }
        private MainMenuView view;

        private void Awake()
        {
            Time.timeScale = 1;
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
            view = GetComponent<MainMenuView>();
            view.Initialize(this);
            ShowPage(MenuPage.Home);
        }

        private IEnumerator Start()
        {
            view.SetBusy(true);
            yield return view.Fade(false);
            view.SetBusy(false);
        }

        public void ShowPage(MenuPage page)
        {
            if (IsLoading) return;
            Page = page;
            view.Show(page);
        }

        public void StartScenario()
        {
            if (IsLoading || !view.Interaction.interactable) return;
            if (!Application.CanStreamedLevelBeLoaded("Stage01"))
            {
                view.SetMessage("Scenariusz 1 nie jest dostępny w tym buildzie.");
                return;
            }
            IsLoading = true;
            view.SetBusy(true);
            if (!ScenarioLoadingScreen.Begin(OnLoadingFailed)) OnLoadingFailed("Ładowanie już trwa.");
        }

        public void LoadGame() => ShowPage(MenuPage.Saves);

        public void LoadGame(string id)
        {
            if (IsLoading || !view.Interaction.interactable) return;
            if (!GameSaveService.PrepareLoad(id, out string error))
            {
                view.SetMessage(error);
                return;
            }
            IsLoading = true;
            view.SetBusy(true);
            if (!ScenarioLoadingScreen.Begin(OnLoadingFailed)) OnLoadingFailed("Ładowanie już trwa.");
        }

        private void OnLoadingFailed(string message)
        {
            GameSaveService.ClearPending();
            if (this == null) return;
            IsLoading = false;
            view.SetBusy(false);
            view.SetMessage(message);
        }

        public void Quit()
        {
            if (IsLoading) return;
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
