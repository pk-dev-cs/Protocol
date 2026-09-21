using UnityEngine;

namespace Protocol
{
    public sealed class MusicPlayer : MonoBehaviour
    {
        private static MusicPlayer active;
        private MusicPlaylist playlist;
        private AudioSource source;
        private int nextTrack;
        private bool paused;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            if (active != null)
                return;
            var owner = new GameObject("Music player");
            DontDestroyOnLoad(owner);
            active = owner.AddComponent<MusicPlayer>();
            active.playlist = Resources.Load<MusicPlaylist>("MusicPlaylist");
            active.source = owner.AddComponent<AudioSource>();
            active.source.playOnAwake = false;
            active.source.spatialBlend = 0;
            active.source.ignoreListenerPause = true;
            ApplySettings();
        }

        public static void ApplySettings()
        {
            if (active == null || active.source == null)
                return;
            active.source.volume = GameSettings.MusicVolume;
            if (!GameSettings.MusicEnabled)
            {
                if (!active.paused)
                    active.source.Pause();
                active.paused = true;
            }
            else if (active.paused)
            {
                active.paused = false;
                active.source.UnPause();
            }
        }

        private void Update()
        {
            if (paused || source == null || source.isPlaying || playlist == null ||
                playlist.Tracks == null || playlist.Tracks.Length == 0)
                return;
            for (int attempt = 0; attempt < playlist.Tracks.Length; attempt++)
            {
                var clip = playlist.Tracks[nextTrack];
                nextTrack = (nextTrack + 1) % playlist.Tracks.Length;
                if (clip == null)
                    continue;
                source.clip = clip;
                source.Play();
                break;
            }
        }

        private void OnDestroy()
        {
            if (active == this)
                active = null;
        }
    }
}
