using UnityEngine;

namespace Mongscape.Presentation.Map
{
    // The prison's art v2 pieces for the runtime stage (PrisonStage): the cell door, the searchlight, their motion
    // controllers and the lock sounds. Loaded from Resources/MongscapePrison/PrisonArt.
    [CreateAssetMenu(menuName = "Mongscape/Prison Art")]
    public sealed class PrisonArt : ScriptableObject
    {
        public const string ResourcePath = "MongscapePrison/PrisonArt";
        public GameObject CellDoor, Searchlight;
        public RuntimeAnimatorController CellDoorMotion, SearchlightMotion;
        public AudioClip LockClick, LockOpen, MonkeyRescue;
        public static PrisonArt Load() => Resources.Load<PrisonArt>(ResourcePath);
    }
}
