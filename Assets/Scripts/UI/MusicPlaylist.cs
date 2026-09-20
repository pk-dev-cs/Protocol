using UnityEngine;

namespace Protocol
{
    [CreateAssetMenu(menuName = "Protocol/Music Playlist")]
    public sealed class MusicPlaylist : ScriptableObject
    {
        public AudioClip[] Tracks;
    }
}
