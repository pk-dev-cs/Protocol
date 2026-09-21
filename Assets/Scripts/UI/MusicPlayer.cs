using UnityEngine;

namespace Protocol
{
    public sealed class MusicPlayer : MonoBehaviour
    {
        private static MusicPlayer active;
        private MusicPlaylist playlist;
        private AudioSource source;
        private AudioSource incoming;
        private int nextTrack;
        private bool paused;
        private bool transitioning;
        private double trackEnd;
        private double incomingEnd;
        private double transitionStart;
        private double pausedAt;
        private float transitionDuration;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            if (active == null)
                new GameObject("Music player").AddComponent<MusicPlayer>();
        }

        private void Awake()
        {
            if (active != null && active != this)
            {
                enabled = false;
                Destroy(this);
                return;
            }

            active = this;
            DontDestroyOnLoad(gameObject);
            playlist = Resources.Load<MusicPlaylist>("MusicPlaylist");
            source = CreateSource();
            incoming = CreateSource();
            ApplySettings();
        }

        private AudioSource CreateSource()
        {
            var audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.loop = false;
            audioSource.spatialBlend = 0;
            audioSource.ignoreListenerPause = true;
            return audioSource;
        }

        private void OnEnable()
        {
            if (source == null)
                return;

            if (active != null && active != this)
            {
                enabled = false;
                Destroy(this);
                return;
            }

            active = this;
            ApplySettings();
        }

        private void OnDisable()
        {
            if (source != null && incoming != null)
                SetPaused(true);
        }

        private void OnApplicationFocus(bool focused)
        {
            if (source == null || incoming == null)
                return;
            source.mute = !focused;
            incoming.mute = !focused;
        }

        public static void ApplySettings()
        {
            if (active == null || active.source == null)
                return;

            active.SetPaused(!GameSettings.MusicEnabled || !active.isActiveAndEnabled);
            active.ApplyVolumes(active.paused ? active.pausedAt : AudioSettings.dspTime);
        }

        private void SetPaused(bool shouldPause)
        {
            if (shouldPause != paused)
            {
                double now = AudioSettings.dspTime;
                if (shouldPause)
                {
                    pausedAt = now;
                    source.Pause();
                    incoming.Pause();
                }
                else
                {
                    double duration = now - pausedAt;
                    trackEnd += duration;
                    incomingEnd += duration;
                    transitionStart += duration;
                    source.UnPause();
                    incoming.UnPause();
                }

                paused = shouldPause;
            }
        }

        private AudioClip TakeNextTrack()
        {
            if (playlist == null || playlist.Tracks == null || playlist.Tracks.Length == 0)
                return null;

            for (int attempt = 0; attempt < playlist.Tracks.Length; attempt++)
            {
                nextTrack %= playlist.Tracks.Length;
                var clip = playlist.Tracks[nextTrack];
                nextTrack = (nextTrack + 1) % playlist.Tracks.Length;
                if (clip != null && clip.samples > 0 && clip.frequency > 0)
                    return clip;
            }

            return null;
        }

        private static double PlayTrack(AudioSource target, AudioClip clip, double now)
        {
            target.Stop();
            target.clip = clip;
            target.Play();
            return now + (double)clip.samples / clip.frequency;
        }

        private void Update()
        {
            if (paused || source == null)
                return;

            double now = AudioSettings.dspTime;
            if (transitioning && now >= transitionStart + transitionDuration)
            {
                source.Stop();
                source.clip = null;
                var previous = source;
                source = incoming;
                incoming = previous;
                trackEnd = incomingEnd;
                transitioning = false;
                LogPlayback("transition complete");
            }

            if (!transitioning)
            {
                float fade = playlist != null ? Mathf.Max(0, playlist.CrossfadeSeconds) : 0;
                if (source.clip != null)
                    fade = Mathf.Min(fade, source.clip.length * .5f);

                if (source.clip == null || now >= trackEnd - fade)
                {
                    var clip = TakeNextTrack();
                    if (clip != null)
                    {
                        transitionDuration = Mathf.Min(fade, clip.length * .5f,
                            (float)System.Math.Max(0, trackEnd - now));
                        if (source.clip == null || transitionDuration <= 0)
                        {
                            trackEnd = PlayTrack(source, clip, now);
                            LogPlayback("track started");
                        }
                        else
                        {
                            incoming.volume = 0;
                            incomingEnd = PlayTrack(incoming, clip, now);
                            transitionStart = now;
                            transitioning = true;
                            LogPlayback("transition started");
                        }
                    }
                }
            }

            ApplyVolumes(now);
        }

        private void ApplyVolumes(double now)
        {
            source.mute = !Application.isFocused;
            incoming.mute = !Application.isFocused;
            float progress = transitioning
                ? Mathf.Clamp01((float)((now - transitionStart) / transitionDuration))
                : 0;
            float volume = GameSettings.MusicVolume;
            source.volume = volume * Mathf.Cos(progress * Mathf.PI * .5f);
            incoming.volume = transitioning ? volume * Mathf.Sin(progress * Mathf.PI * .5f) : 0;
        }

        private void OnDestroy()
        {
            if (source != null)
            {
                source.Stop();
                Destroy(source);
            }

            if (incoming != null)
            {
                incoming.Stop();
                Destroy(incoming);
            }

            if (active == this)
                active = null;
        }

        private void LogPlayback(string action)
        {
            Debug.Log($"[Music] player={GetEntityId()} {action}; " +
                $"current={source.clip?.name}, next={incoming.clip?.name}, " +
                $"fade={transitionDuration:F2}s, dsp={AudioSettings.dspTime:F2}", this);
        }
    }
}
