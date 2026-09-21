using UnityEngine;

namespace Protocol
{
    [CreateAssetMenu(menuName = "Protocol/Music Playlist")]
    public sealed class MusicPlaylist : ScriptableObject
    {
        public AudioClip[] Tracks;
        [Min(0)] public float CrossfadeSeconds = 3f;
    }
}
