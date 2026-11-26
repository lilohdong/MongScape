using System;
using System.Collections.Generic;

namespace Mongscape.Domain
{
    // The hands-up reading of one pose frame (the classifier reads the reach itself, Reading.Reach).
    // Sideways distances are in shoulder widths from the shoulder centre, like the classifier's upper-body reach.
    public static class ArmPose
    {
        public const float MinVisibility = .4f;
        private static bool Seen(PoseFrame f, int joint) => f.Points[joint].Visibility >= MinVisibility;
        private static float Width(PoseFrame f) => Math.Abs(f.Points[PoseJoint.RightShoulder].X - f.Points[PoseJoint.LeftShoulder].X);
        private static bool Usable(PoseFrame f) => f != null && f.HasPose && Seen(f, PoseJoint.LeftShoulder) && Seen(f, PoseJoint.RightShoulder) && Width(f) > 1e-3f;
        // Both wrists above the nose: the hands-up (만세) note.
        public static bool HandsUp(PoseFrame f)
        {
            if (!Usable(f) || !Seen(f, PoseJoint.Nose) || !Seen(f, PoseJoint.LeftWrist) || !Seen(f, PoseJoint.RightWrist)) return false;
            float nose = f.Points[PoseJoint.Nose].Y;
            return f.Points[PoseJoint.LeftWrist].Y < nose && f.Points[PoseJoint.RightWrist].Y < nose;
        }
    }

    // Beat sync (plan 6-1b, chapter 2): four beats, jump on each. The median of how late (positive) or early each jump was against
    // the nearest beat is the player's delay, saved as the sync offset that the zoo song judges with.
    public sealed class BeatSync
    {
        public const int Beats = 4, LeadBeats = 2;
        public const double Interval = .6, Window = .3, MaxOffset = .4;
        private readonly List<double> offsets = new List<double>(Beats);
        private readonly bool[] taken = new bool[Beats];
        public double Start { get; private set; }
        public int Hits => offsets.Count;
        public bool Done => Hits >= Beats;
        // Beat n (0 based) sounds at Start + Lead + n * Interval; Start is when the lesson began.
        public double BeatTime(int n) => Start + LeadBeats * Interval + n * Interval;
        public void Begin(double now) { Start = now; offsets.Clear(); Array.Clear(taken, 0, taken.Length); }
        // The beat that sounds in (or just before) this moment, -1 before the first beat (for drawing the pulse).
        public int BeatAt(double now) => now < BeatTime(0) ? -1 : Math.Min(Beats - 1, (int)Math.Floor((now - BeatTime(0)) / Interval));
        // A jump at `time`: counted when it falls within the window of a beat nobody jumped on yet. Returns whether it counted.
        public bool Jump(double time)
        {
            int best = -1; double bestGap = double.MaxValue;
            for (int n = 0; n < Beats; n++) { double gap = Math.Abs(time - BeatTime(n)); if (!taken[n] && gap < bestGap) { best = n; bestGap = gap; } }
            if (best < 0 || bestGap > Window) return false;
            taken[best] = true; offsets.Add(time - BeatTime(best)); return true;
        }
        // The song is over for this lesson: the lesson failed when not every beat was caught.
        public bool Over(double now) => now > BeatTime(Beats - 1) + Window;
        // Median of the hits, clamped; 0 without hits.
        public double Offset
        {
            get
            {
                if (offsets.Count == 0) return 0;
                var sorted = new List<double>(offsets); sorted.Sort();
                double median = sorted.Count % 2 == 1 ? sorted[sorted.Count / 2] : (sorted[sorted.Count / 2 - 1] + sorted[sorted.Count / 2]) * .5;
                return Math.Max(-MaxOffset, Math.Min(MaxOffset, median));
            }
        }
    }
}
