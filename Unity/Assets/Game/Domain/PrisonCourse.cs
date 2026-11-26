using System;

namespace Mongscape.Domain
{
    // Where the runner is in the prison branch (plan 2-2): three sets of [stairs, hide at a cell door, flat], then the roof.
    public enum PrisonPhase { None, Stairs, Approach, Hold, Flat, Roof }

    // The prison branch (plan 2, 2-3a, 6-8 prison row, 3-3). A fixed order, no distance curve:
    //   [stairs ~10 s: knee steps] -> [hide ~5 s at a cell door: crouch under the searchlight] -> [flat ~3 s] x 3 -> roof.
    // Hiding: the runner stops in front of a cell door. The searchlight comes closer (Lead), then sweeps over the door for
    // HideSeconds (5, 6, 7 by visit). Crouched (shoulders under the line) = hidden. The lock gauge over the door fills while
    // hidden, faster the deeper the crouch: a half squat fills it before the light has passed, a shallow duck only hides.
    // A full gauge opens the door and a monkey joins. Standing up while the light is on = alarm bell, one warning, that cell
    // stays shut, and the next stairs are a warden chase (a missed knee beat there is a capture). Ordinary missed knee beats
    // in the prison only slow the runner. Three cells and no alarm: one more monkey on the roof.
    public sealed class PrisonCourse
    {
        // Chunk order of the branch, from the destination chunk on (11 chunks: the extended lap's destination length).
        public const int Sets = 3;
        public static readonly int[] Chunks = { 7, 4, 5, 7, 5, 4, 7, 4, 5, 6, RouteSurface.PrisonExit };
        // RoutePlan branch hook: the same fixed sets on every lap (the visit makes them harder, not the layout).
        public static int[] Branch(int lap) => (int[])Chunks.Clone();
        // Index of a route chunk inside a chosen prison branch, or -1.
        public static int BranchIndex(RoutePlan plan, int sequence)
        {
            if (plan == null || !plan.Extended || !plan.HasFork(sequence) || plan.DestinationAt(sequence) != 0) return -1;
            int index = plan.LocalSequence(sequence) - CartFork.DestinationChunk;
            return index >= 0 && index < Chunks.Length ? index : -1;
        }
        // Where the runner stops in the cell chunk (local metres): the door is drawn beside this point.
        public const float DoorZ = 12f;
        // Seconds the searchlight takes to reach the door after the runner stops (hiding early is free).
        public const float LeadSeconds = 1.5f;
        // Standing must last this long under the light to count (one dropped pose frame is not an alarm).
        public const float SpotGrace = .25f;
        // Speeds by phase (m/s): a stair chunk takes about 10 s, the flat chunk about 3 s.
        public const float StairSpeed = 2.4f, ApproachSpeed = 6f, FlatSpeed = 8f;
        // Lock gauge fill per second is (DepthBase + DepthGain * depth) / hold time: a deep squat (depth 1) held from the stop
        // fills it at 80% of the hold, depth .67 just in time, a shallow duck (about .3) never. The keyboard fills at the
        // base keyboard rate (no depth).
        public const float DepthBase = .5f, DepthGain = .75f, KeyboardRate = 1.1f;

        public PrisonPhase Phase { get; private set; }
        // 0..2 while in a set (-1 outside the branch).
        public int Set { get; private set; } = -1;
        // 1 for the first prison visit of the run, 2 for the second, ...
        public int Visit { get; private set; } = 1;
        public float HideSeconds => HideSecondsFor(Visit);
        public static float HideSecondsFor(int visit) => visit <= 1 ? 5f : visit == 2 ? 6f : 7f;
        // Knee beat on the prison stairs: the allowed gap between steps shrinks a little each visit.
        public static float StairGapScale(int visit) => visit <= 1 ? 1f : visit == 2 ? .9f : .8f;
        public float GapScale => StairGapScale(Visit);
        // Seconds since the runner stopped at the door (0 outside the hold).
        public float HoldClock { get; private set; }
        public float HoldSeconds => LeadSeconds + HideSeconds;
        // 0..1 over the light's sweep (0 before it reaches the door).
        public float LightProgress => Phase == PrisonPhase.Hold ? Clamp01((HoldClock - LeadSeconds) / HideSeconds) : 0f;
        public bool LightOn => Phase == PrisonPhase.Hold && HoldClock >= LeadSeconds;
        public float LockGauge { get; private set; }
        public bool Hidden { get; private set; }
        // Per set of this visit: the cell opened, the alarm went off.
        public readonly bool[] Opened = new bool[Sets], Spotted = new bool[Sets];
        public int Alarms { get; private set; }
        // Monkeys freed this visit (cells plus the roof bonus).
        public int Rescued { get; private set; }
        public bool RoofBonus { get; private set; }
        // The warden chases on the stairs of this set (-1: none).
        public int ChasedSet { get; private set; } = -1;
        public bool WardenChasing => Phase == PrisonPhase.Stairs && Set >= 0 && Set == ChasedSet;
        // The speed the prison wants now (null outside the branch: the base speed applies), and where the runner must stop.
        public float? Speed { get; private set; }
        public bool Holding => Phase == PrisonPhase.Hold;

        // Searchlight saw the runner (set index): alarm bell, one warning, next stairs chased.
        public event Action<int> Alarm;
        // A cell door opened (set index): a monkey joins.
        public event Action<int> CellOpened;
        // Three cells and no alarm: one more monkey on the roof.
        public event Action RoofRescue;
        // The runner stopped at a door (set index): the searchlight starts its approach.
        public event Action<int> HoldStarted;

        private float spotted;
        // Per set: the hold at its door is over.
        private readonly bool[] held = new bool[Sets];

        // Role of a branch index (0..10): set and phase by chunk; -1 set for the roof chunks.
        public static PrisonPhase PhaseOf(int branchIndex, float localZ, out int set)
        {
            set = -1;
            if (branchIndex < 0 || branchIndex >= Chunks.Length) return PrisonPhase.None;
            if (branchIndex >= Sets * 3) return PrisonPhase.Roof;
            set = branchIndex / 3;
            int role = branchIndex % 3;
            // A hair before the door counts: the runner is stopped exactly at it, give or take float rounding.
            return role == 0 ? PrisonPhase.Stairs : role == 2 ? PrisonPhase.Flat : localZ < DoorZ - .05f ? PrisonPhase.Approach : PrisonPhase.Hold;
        }

        // A new visit to the prison branch (visit = how many prison stays the run has had, this one included).
        public void Begin(int visit)
        {
            Visit = Math.Max(1, visit);
            Array.Clear(Opened, 0, Sets); Array.Clear(Spotted, 0, Sets);
            Alarms = Rescued = 0; RoofBonus = false; ChasedSet = -1; LockGauge = 0f; HoldClock = 0f; Hidden = false; spotted = 0f;
            Array.Clear(held, 0, Sets); Phase = PrisonPhase.None; Set = -1; Speed = null;
        }

        public void Reset() { Begin(1); Visit = 1; }

        // branchIndex: chunk index inside the prison branch (-1 outside); localZ: metres into that chunk.
        // hidden: crouched under the line (pose) or holding S (keyboard); depth: 0..1 crouch depth, negative for the keyboard.
        // Returns the local z the runner may not pass this tick (the door while holding), or float.MaxValue.
        public float Tick(float dt, int branchIndex, float localZ, bool hidden, float depth)
        {
            var phase = PhaseOf(branchIndex, localZ, out int set);
            // After the light has passed (or the alarm), the rest of the cell chunk is walked like the flat.
            if (phase == PrisonPhase.Hold && held[set]) phase = PrisonPhase.Flat;
            if (phase == PrisonPhase.Hold && Phase != PrisonPhase.Hold) StartHold(set);
            Phase = phase; Set = set;
            Hidden = hidden && phase != PrisonPhase.None;
            switch (phase)
            {
                case PrisonPhase.None: Speed = null; return float.MaxValue;
                case PrisonPhase.Stairs: Speed = StairSpeed; return float.MaxValue;
                case PrisonPhase.Approach: Speed = ApproachSpeed; return held[set] ? float.MaxValue : DoorZ;
                case PrisonPhase.Flat: Speed = FlatSpeed; return float.MaxValue;
                case PrisonPhase.Roof:
                    Speed = null;
                    if (!RoofBonus && Rescued >= Sets && Alarms == 0) { RoofBonus = true; Rescued++; RoofRescue?.Invoke(); }
                    return float.MaxValue;
            }
            // Hold.
            Speed = 0f;
            HoldClock += Math.Max(0f, dt);
            if (hidden && !Opened[Set] && !Spotted[Set])
            {
                float rate = depth < 0f ? KeyboardRate : DepthBase + DepthGain * Clamp01(depth);
                LockGauge = Math.Min(1f, LockGauge + rate * dt / HoldSeconds);
                if (LockGauge >= 1f) { Opened[Set] = true; Rescued++; CellOpened?.Invoke(Set); }
            }
            if (LightOn && !hidden && !Spotted[Set])
            {
                spotted += dt;
                if (spotted >= SpotGrace) { Spot(); return float.MaxValue; }
            }
            else spotted = 0f;
            if (HoldClock >= HoldSeconds) { Release(); return float.MaxValue; }
            return DoorZ;
        }

        private void StartHold(int set)
        {
            HoldClock = 0f; LockGauge = Opened[set] ? 1f : 0f; spotted = 0f;
            HoldStarted?.Invoke(set);
        }
        private void Spot()
        {
            Spotted[Set] = true; Alarms++; LockGauge = 0f;
            if (Set + 1 < Sets) ChasedSet = Set + 1;
            int set = Set;
            Release();
            Alarm?.Invoke(set);
        }
        // The light has passed (or the alarm rang): walk on through the rest of the cell chunk.
        private void Release() { held[Set] = true; Phase = PrisonPhase.Flat; Speed = FlatSpeed; HoldClock = 0f; spotted = 0f; }

        private static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;
    }
}
