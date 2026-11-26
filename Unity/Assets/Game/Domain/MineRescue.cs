using System;
using System.Collections.Generic;

namespace Mongscape.Domain
{
    // Monkeys stuck in the mine walls (plan 3-0): lane 1 (left) has them in the left wall, lane 3 (right) in the right wall.
    // Placement is seeded from the route seed and the lap, so the same seed and choices always give the same monkeys.
    public static class MineRescuePlan
    {
        // Local z of the monkey in its chunk: late in the chunk, so the 2 s of clear lane in front fit inside the chunk.
        public const float MonkeyZ = 21f;
        // The monkey shows about this long before the runner passes it, and its lane stays clear for as long.
        public const float SightSeconds = 2f;
        // Chance that the mine start (the one walking chunk before the cart) has a monkey, from the second lap on.
        public const float StartChance = .5f;
        public const int MinBranchSpacing = 2;
        private const int MineBranch = 1;

        // Whether the route chunk has a stuck monkey, and on which wall (-1 left, 1 right).
        public static bool At(RoutePlan plan, int sequence, out int side)
        {
            side = 0;
            if (plan == null || !plan.Extended || sequence <= 0 || !plan.HasFork(sequence)) return false;
            if (plan.TypeAt(sequence) != 0) return false;
            int lap = plan.CycleAt(sequence), local = plan.LocalSequence(sequence);
            bool start = StartHas(plan, lap);
            bool has;
            if (local == 0) has = start;
            else if (local < CartFork.DestinationChunk) has = false;
            else has = plan.DestinationAt(sequence) == MineBranch && BranchPicks(plan, sequence - local, lap, start).Contains(local);
            if (!has) return false;
            side = new Random(unchecked(plan.Seed * 7919 + sequence * 104729 + 31)).Next(2) == 0 ? -1 : 1;
            return true;
        }
        public static float RouteZ(int sequence) => sequence * RouteSurface.Length + MonkeyZ;

        private static Random LapRandom(RoutePlan plan, int lap) => new Random(unchecked(plan.Seed * 92821 + lap * 68917 + 1234567));
        // The very first chunk of a run is the opening row; the start monkey waits for the second lap.
        private static bool StartHas(RoutePlan plan, int lap) => lap >= 1 && LapRandom(plan, lap).NextDouble() < StartChance;
        // One or two monkeys a lap (with the start one counted), on walking chunks of the mine branch, never on the return chunk.
        private static List<int> BranchPicks(RoutePlan plan, int lapStart, int lap, bool start)
        {
            var rng = LapRandom(plan, lap); rng.NextDouble();
            int want = start ? 1 : 1 + rng.Next(2);
            var candidates = new List<int>();
            for (int local = CartFork.DestinationChunk + 1; local < plan.LapChunks(lap) - 1; local++)
                if (plan.TypeAt(lapStart + local) == 0) candidates.Add(local);
            for (int i = candidates.Count - 1; i > 0; i--) { int j = rng.Next(i + 1); int swap = candidates[i]; candidates[i] = candidates[j]; candidates[j] = swap; }
            var picks = new List<int>(2);
            foreach (int local in candidates)
            {
                if (picks.Count >= want) break;
                bool apart = true; foreach (int other in picks) apart &= Math.Abs(other - local) >= MinBranchSpacing;
                if (apart) picks.Add(local);
            }
            return picks;
        }
    }

    // Judges the reach toward a stuck monkey: in the monkey's lane, an arm out toward its wall within the last second before
    // passing it. The call item (while it lasts) rescues without reaching, as long as the runner is in the monkey's lane.
    // A miss has no penalty. The cart never has monkeys.
    public sealed class MineRescueJudge
    {
        public const float WindowSeconds = 1f;
        // A reach seen this recently still counts (pose and keys send it again while the arm is out or the key is held).
        public const float ReachMemorySeconds = .3f;
        private readonly float[] lastReach = { -10f, -10f };
        private float clock;
        private int resolved = -1;
        public int Rescued { get; private set; }
        public int Missed { get; private set; }
        // Route chunk and wall of the monkey, raised once per monkey.
        public event Action<int, int> RescuedAt, MissedAt;
        // Set by the run when the call item is active (trait plan 4-1).
        public Func<bool> CallActive { get; set; }

        public void Reach(int side) { if (side != 0) lastReach[side < 0 ? 0 : 1] = clock; }
        public bool Reaching(int side) => side != 0 && clock - lastReach[side < 0 ? 0 : 1] <= ReachMemorySeconds;
        // The tutorial has no stuck monkeys to judge but keeps the reach clock running.
        public void Advance(float dt) { clock += Math.Max(0f, dt); }
        public void Reset() { clock = 0; lastReach[0] = lastReach[1] = -10f; resolved = -1; Rescued = Missed = 0; }

        public void Tick(float dt, RoutePlan plan, float runnerZ, int lane, float speed)
        {
            clock += Math.Max(0f, dt);
            int here = RouteSurface.SequenceAt(runnerZ);
            for (int sequence = Math.Max(resolved + 1, here - 1); sequence <= here; sequence++)
            {
                if (!MineRescuePlan.At(plan, sequence, out int side)) continue;
                float z = MineRescuePlan.RouteZ(sequence);
                if (runnerZ >= z) { resolved = sequence; Missed++; MissedAt?.Invoke(sequence, side); continue; }
                bool inWindow = z - runnerZ <= WindowSeconds * Math.Max(1f, speed);
                bool call = CallActive != null && CallActive();
                if (inWindow && lane == side && (call || Reaching(side))) { resolved = sequence; Rescued++; RescuedAt?.Invoke(sequence, side); }
            }
        }
    }
}
