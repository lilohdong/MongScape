using System;
using System.Collections.Generic;

namespace Mongscape.Domain
{
    // Arm readings of one pose frame that the tutorial stations need (the classifier has no reach or hands-up reading yet).
    // Sideways distances are in shoulder widths from the shoulder centre, like the classifier's upper-body reach.
    public static class ArmPose
    {
        public const float ReachWidths = 1.1f, RestWidths = .85f, MinVisibility = .4f;
        private static bool Seen(PoseFrame f, int joint) => f.Points[joint].Visibility >= MinVisibility;
        private static float Width(PoseFrame f) => Math.Abs(f.Points[PoseJoint.RightShoulder].X - f.Points[PoseJoint.LeftShoulder].X);
        private static float Out(PoseFrame f, int wrist)
        {
            float centre = (f.Points[PoseJoint.LeftShoulder].X + f.Points[PoseJoint.RightShoulder].X) * .5f, width = Math.Max(1e-4f, Width(f));
            return Math.Abs(f.Points[wrist].X - centre) / width;
        }
        private static bool Usable(PoseFrame f) => f != null && f.HasPose && Seen(f, PoseJoint.LeftShoulder) && Seen(f, PoseJoint.RightShoulder) && Width(f) > 1e-3f;
        // One arm held out to the side (either side) while the other hangs near the body: the rescue reach.
        public static bool Reach(PoseFrame f)
        {
            if (!Usable(f) || !Seen(f, PoseJoint.LeftWrist) || !Seen(f, PoseJoint.RightWrist)) return false;
            float left = Out(f, PoseJoint.LeftWrist), right = Out(f, PoseJoint.RightWrist);
            return left >= ReachWidths && right < RestWidths || right >= ReachWidths && left < RestWidths;
        }
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

    public enum NoteKind { Jump, Hands }

    // One verse of the zoo practice (chapter 2): eight notes on the beat, jumps and hands-up, judged against the sync offset.
    // Five of eight pass; the notes come one per two beats so the first verse never rushes.
    public sealed class BeatVerse
    {
        public const double Interval = .75, Window = .35, Lead = 3;
        public const int PassHits = 5;
        public static readonly NoteKind[] Notes = { NoteKind.Jump, NoteKind.Jump, NoteKind.Hands, NoteKind.Jump, NoteKind.Hands, NoteKind.Jump, NoteKind.Hands, NoteKind.Hands };
        private readonly bool[] hit = new bool[Notes.Length];
        public double Start { get; private set; }
        public double Offset { get; private set; }
        public int Hits { get; private set; }
        public int Count => Notes.Length;
        public bool Passed => Hits >= PassHits;
        public void Begin(double now, double syncOffset) { Start = now; Offset = syncOffset; Hits = 0; Array.Clear(hit, 0, hit.Length); }
        public double NoteTime(int n) => Start + Lead * Interval + n * Interval;
        public bool IsHit(int n) => hit[n];
        public bool Over(double now) => now > NoteTime(Notes.Length - 1) + Window;
        // The action of the kind at `time` (an edge: the jump or the moment the hands went up). Returns whether a note was hit.
        public bool Act(NoteKind kind, double time)
        {
            double t = time - Offset;
            for (int n = 0; n < Notes.Length; n++)
            {
                if (hit[n] || Notes[n] != kind || Math.Abs(t - NoteTime(n)) > Window) continue;
                hit[n] = true; Hits++; return true;
            }
            return false;
        }
    }

    // One searchlight set (plan 6-2 prison practice): the light sweeps in and the player has to stay crouched until it passes.
    // The cell lock gauge fills while hiding, twice as fast when the crouch is deep. Standing up in the light too long is a miss.
    public sealed class HideSet
    {
        public const double Approach = 2, Sweep = 5, FillSeconds = 3.5, SpotGrace = .8;
        public double Elapsed { get; private set; }
        // 0..1 of the lock gauge.
        public float Gauge { get; private set; }
        public bool Spotted { get; private set; }
        public bool Passed { get; private set; }
        private double exposed;
        public bool Lit => Elapsed >= Approach && Elapsed < Approach + Sweep && !Passed;
        // 0 while the light is far, up to 1 at the middle of the sweep (drawing the beam).
        public float Beam => Lit ? (float)Math.Sin((Elapsed - Approach) / Sweep * Math.PI) : 0;
        public void Begin() { Elapsed = 0; Gauge = 0; Spotted = Passed = false; exposed = 0; }
        public void Tick(double dt, bool crouching, bool deep)
        {
            if (Spotted || Passed || dt <= 0) return;
            Elapsed += dt;
            if (!Lit) { if (Elapsed >= Approach + Sweep) Passed = true; return; }
            if (crouching) { exposed = 0; Gauge = (float)Math.Min(1, Gauge + dt / FillSeconds * (deep ? 2 : 1)); }
            else { exposed += dt; if (exposed >= SpotGrace) Spotted = true; }
        }
    }
}
