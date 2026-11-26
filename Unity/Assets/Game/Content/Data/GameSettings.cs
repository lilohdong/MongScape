using Mongscape.Domain;
using UnityEngine;

namespace Mongscape.Content
{
    [CreateAssetMenu(menuName = "Mongscape/Game Settings")]
    public sealed class GameSettings : ScriptableObject
    {
        public RunConfig Run = new RunConfig();
        [Header("Body tracking (Inference Engine)")]
        public PoseCameraOptions Camera = new PoseCameraOptions();
        public MotionTuning Motion = new MotionTuning();
        [Header("Map rendering budget")]
        public RenderBudget Render = new RenderBudget();
        [Header("Items")]
        [Range(0, 1)] public float ItemChancePerChunk = .35f;
        [Range(0, 1)] public float MusicVolume = .35f, EffectsVolume = .7f;
    }
}
