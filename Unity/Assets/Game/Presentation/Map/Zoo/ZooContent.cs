using UnityEngine;

namespace Mongscape.Presentation.Map
{
    // Serialized references for the zoo map (Resources/ZooContent.asset): the song, the note signals, the six charts,
    // the art from the art 2nd pass and the mirror shader. Anything missing falls back to simple shapes or silence.
    public sealed class ZooContent : ScriptableObject
    {
        public AudioClip Music, SignalJump, SignalArmsUp;
        // Lead of each signal sound before its note (Content/Audio/Music/Zoo/zoo_signals.json).
        public float JumpLead = 0f, ArmsUpLead = .0938f;
        public TextAsset[] Charts = new TextAsset[0];
        public GameObject Lion, Fence, Puddle, SqueezeBars, IronGate, LionCage, Exit;
        public Shader Mirror;
        private static ZooContent loaded; private static bool tried;
        public static ZooContent Load()
        {
            if (!tried) { tried = true; loaded = Resources.Load<ZooContent>("ZooContent"); }
            return loaded;
        }
    }
}
