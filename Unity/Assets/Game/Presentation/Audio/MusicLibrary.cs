using UnityEngine;

namespace Mongscape.Presentation
{
    // The layered music loops (Content/Audio/Music, made by Tools/Music). Every layer of a song has the same length and
    // starts at the same dspTime; a game state only changes layer volumes (see MusicPlayer). Loaded from
    // Resources/MongscapeMusic/MusicLibrary. The zoo song is played by the zoo map itself.
    [CreateAssetMenu(menuName = "Mongscape/Music Library")]
    public sealed class MusicLibrary : ScriptableObject
    {
        public const string ResourcePath = "MongscapeMusic/MusicLibrary";
        public AudioClip MenuBase, MenuRhythm;
        public AudioClip MineBase, MineTension;
        public AudioClip PrisonBase, PrisonSteps, PrisonTension, PrisonHide;
        public AudioClip ResultBase, ResultRhythm;
        public static MusicLibrary Load() => Resources.Load<MusicLibrary>(ResourcePath);
    }
}
