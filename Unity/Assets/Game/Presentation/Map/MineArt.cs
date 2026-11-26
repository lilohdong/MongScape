using UnityEngine;

namespace Mongscape.Presentation.Map
{
    // Art v2 pieces the mine and the cart fork place at runtime (Resources/MongscapeArt/MineArt). Every field may be empty:
    // the views then build a plain stand-in, so a missing import never breaks a run.
    public sealed class MineArt : ScriptableObject
    {
        public GameObject Monkey, WallLeft, WallRight;
        // WaveRight goes with the left wall (the hand nearer the runner), WaveLeft with the right wall.
        public RuntimeAnimatorController WaveLeft, WaveRight, RescuePop;
        // The troop following the runner (MonkeyTroopView) loops this clip.
        public AnimationClip Run;
        public GameObject SignMine, SignZoo, SignPrison, SignBlocked;
        public AudioClip Rescue, ForkBlocked;

        public const string ResourcePath = "MongscapeArt/MineArt";
        private static MineArt loaded;
        private static bool tried;
        public static MineArt Load()
        {
            if (!tried) { tried = true; loaded = Resources.Load<MineArt>(ResourcePath); }
            return loaded;
        }
    }
}
