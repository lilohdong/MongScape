using System;

namespace Mongscape.Domain
{
    // The three maps of a lap (plan 0, 3-3). The mine covers the lap start with the cart fork and the mine branch.
    public enum SectionKind { Mine, Zoo, Prison }
    // Who sets the speed: the mine follows the distance curve, the zoo the song, the prison its fixed set.
    public enum SpeedRule { DistanceCurve, SongFixed, SetFixed }
    // How the map gets harder: the mine by distance, the zoo by layout tier per visit, the prison by set per visit.
    public enum DifficultyRule { DistanceCurve, VisitTier, VisitSet }
    // One chaser per map (plan 6-8): mine = police, zoo = lion, prison = warden.
    public enum ChaserKind { Police, Lion, Warden }
    // The sound a chaser makes when it closes in and when a warning lands (the HUD siren picture stays for all of them).
    public enum ChaserSound { Siren, Roar, AlarmBell }

    public sealed class ChaserDef
    {
        public readonly ChaserKind Kind;
        public readonly ChaserSound Sound;
        // The chase played after the second warning before the capture.
        public readonly float CatchSeconds;
        public ChaserDef(ChaserKind kind, ChaserSound sound, float catchSeconds = 1.2f) { Kind = kind; Sound = sound; CatchSeconds = catchSeconds; }

        public static readonly ChaserDef Police = new ChaserDef(ChaserKind.Police, ChaserSound.Siren);
        public static readonly ChaserDef Lion = new ChaserDef(ChaserKind.Lion, ChaserSound.Roar);
        public static readonly ChaserDef Warden = new ChaserDef(ChaserKind.Warden, ChaserSound.AlarmBell);
        public static ChaserDef Of(ChaserKind kind) => kind == ChaserKind.Lion ? Lion : kind == ChaserKind.Warden ? Warden : Police;
    }

    public sealed class SectionDef
    {
        public readonly SectionKind Kind;
        public readonly SpeedRule Speed;
        public readonly DifficultyRule Difficulty;
        public readonly ChaserDef Chaser;
        public SectionDef(SectionKind kind, SpeedRule speed, DifficultyRule difficulty, ChaserDef chaser) { Kind = kind; Speed = speed; Difficulty = difficulty; Chaser = chaser; }
        // Only the mine follows the distance difficulty curve (hazard odds and speed); the zoo and the prison have their own.
        public bool UsesDistanceCurve => Difficulty == DifficultyRule.DistanceCurve;

        public static readonly SectionDef Mine = new SectionDef(SectionKind.Mine, SpeedRule.DistanceCurve, DifficultyRule.DistanceCurve, ChaserDef.Police);
        public static readonly SectionDef Zoo = new SectionDef(SectionKind.Zoo, SpeedRule.SongFixed, DifficultyRule.VisitTier, ChaserDef.Lion);
        public static readonly SectionDef Prison = new SectionDef(SectionKind.Prison, SpeedRule.SetFixed, DifficultyRule.VisitSet, ChaserDef.Warden);
        public static SectionDef Of(SectionKind kind) => kind == SectionKind.Zoo ? Zoo : kind == SectionKind.Prison ? Prison : Mine;
        // RoutePlan destinations: -1 zoo, 0 prison, 1 mine.
        public static SectionKind KindOfDestination(int destination) => destination < 0 ? SectionKind.Zoo : destination == 0 ? SectionKind.Prison : SectionKind.Mine;
        public static int DestinationOf(SectionKind kind) => kind == SectionKind.Zoo ? -1 : kind == SectionKind.Prison ? 0 : 1;
        // Chunk type alone (laps without a fork, such as the tutorial warm-up).
        public static SectionKind KindOfType(int type) => RouteSurface.IsZoo(type) ? SectionKind.Zoo
            : (type >= 4 && type <= 10) || type == RouteSurface.PrisonExit ? SectionKind.Prison : SectionKind.Mine;
    }

    // One stay in a map. A lap has two: the mine start (fork included), then the chosen branch. The mine branch is a new
    // section too, so leaving the mine start for the mine branch is a boundary like any other.
    public readonly struct SectionSpan : IEquatable<SectionSpan>
    {
        public readonly SectionKind Kind;
        public readonly int Lap;
        public readonly bool Branch;
        // 1 for the first stay of this kind in the run, 2 for the second, ... (0 when not yet entered).
        public readonly int Visit;
        public SectionSpan(SectionKind kind, int lap, bool branch, int visit = 0) { Kind = kind; Lap = lap; Branch = branch; Visit = visit; }
        public SectionDef Def => SectionDef.Of(Kind);
        public bool SameStay(SectionSpan other) => Kind == other.Kind && Lap == other.Lap && Branch == other.Branch;
        public bool Equals(SectionSpan other) => SameStay(other) && Visit == other.Visit;
        public override bool Equals(object obj) => obj is SectionSpan other && Equals(other);
        public override int GetHashCode() => ((int)Kind * 397 ^ Lap) * 2 + (Branch ? 1 : 0);
        public override string ToString() => Kind + " lap " + Lap + (Branch ? " branch" : " start") + " visit " + Visit;

        // Which stay a route chunk belongs to. Without an extended plan the whole route is one mine section.
        public static SectionSpan At(RoutePlan plan, int sequence)
        {
            if (plan == null || !plan.Extended) return new SectionSpan(SectionKind.Mine, 0, false);
            int lap = plan.CycleAt(sequence);
            if (!plan.HasFork(sequence))
            {
                var byType = SectionDef.KindOfType(plan.TypeAt(sequence));
                return new SectionSpan(byType, lap, byType != SectionKind.Mine);
            }
            if (plan.LocalSequence(sequence) < CartFork.DestinationChunk) return new SectionSpan(SectionKind.Mine, lap, false);
            int destination = plan.DestinationAt(sequence);
            var kind = destination == int.MinValue ? SectionDef.KindOfType(plan.TypeAt(sequence)) : SectionDef.KindOfDestination(destination);
            return new SectionSpan(kind, lap, true);
        }
    }

    // Follows the runner through the sections and raises the boundary events (plan 6-8: warnings last until the map is left).
    public sealed class SectionTracker
    {
        private readonly int[] visits = new int[3];
        private bool started;
        public SectionSpan Current { get; private set; }
        public SectionDef Def => Current.Def;
        public ChaserDef Chaser => Current.Def.Chaser;
        // Raised with the stay that ends, then Entered with the new one.
        public event Action<SectionSpan> Exited, Entered;
        // How many stays of this kind the run has started (the current one included).
        public int Visits(SectionKind kind) => visits[(int)kind];

        public SectionTracker() { Reset(); }
        public void Update(RoutePlan plan, float z) => Update(SectionSpan.At(plan, RouteSurface.SequenceAt(z)));
        public void Update(SectionSpan span)
        {
            if (started && span.SameStay(Current)) return;
            var previous = Current; bool had = started;
            visits[(int)span.Kind]++;
            Current = new SectionSpan(span.Kind, span.Lap, span.Branch, visits[(int)span.Kind]);
            started = true;
            if (had) Exited?.Invoke(previous);
            Entered?.Invoke(Current);
        }
        // Back to the mine start without raising events (a new run).
        public void Reset()
        {
            Array.Clear(visits, 0, visits.Length);
            visits[(int)SectionKind.Mine] = 1;
            Current = new SectionSpan(SectionKind.Mine, 0, false, 1);
            started = true;
        }
    }
}
