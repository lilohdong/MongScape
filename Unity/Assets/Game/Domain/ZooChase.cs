using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Mongscape.Domain
{
    public enum ZooNoteKind { Jump, ArmsUp }
    public enum ZooTier { Easy, Normal, Hard }

    public readonly struct ZooNote
    {
        public readonly int Beat;
        public readonly float Time;
        public readonly ZooNoteKind Kind;
        public ZooNote(int beat, float time, ZooNoteKind kind) { Beat = beat; Time = time; Kind = kind; }
    }

    // One note chart of the zoo song (plan 1-2b, 1-2c): the beatmap JSON from Tools/Music/zoo (bpm, offset_sec, notes).
    public sealed class ZooBeatmap
    {
        public string Chart = "", Difficulty = "";
        public float Bpm = 160f, Offset, Length = 60f;
        public readonly List<ZooNote> Notes = new List<ZooNote>();
        public float BeatSeconds => 60f / Math.Max(1f, Bpm);
        public float BarSeconds => BeatSeconds * 4f;
        public ZooTier Tier => Difficulty == "easy" ? ZooTier.Easy : Difficulty == "hard" ? ZooTier.Hard : ZooTier.Normal;
        // The break (bars 19-22): the iron gate sprint, no notes.
        public float BreakStart => 18 * BarSeconds + Offset;
        public float BreakEnd => 22 * BarSeconds + Offset;

        private static readonly Regex NoteRx = new Regex("\\{[^{}]*?\"beat\"\\s*:\\s*(\\d+)[^{}]*?\"type\"\\s*:\\s*\"(jump|arms_up)\"[^{}]*\\}");
        private static float Number(string json, string key, float fallback)
        {
            var m = Regex.Match(json, "\"" + key + "\"\\s*:\\s*(-?[0-9.]+)");
            return m.Success && float.TryParse(m.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out float v) ? v : fallback;
        }
        private static string Text(string json, string key)
        {
            var m = Regex.Match(json, "\"" + key + "\"\\s*:\\s*\"([^\"]*)\"");
            return m.Success ? m.Groups[1].Value : "";
        }
        public static ZooBeatmap Parse(string json)
        {
            var map = new ZooBeatmap();
            if (string.IsNullOrEmpty(json)) return map;
            map.Chart = Text(json, "chart"); map.Difficulty = Text(json, "difficulty");
            map.Bpm = Number(json, "bpm", 160f); map.Offset = Number(json, "offset_sec", 0f); map.Length = Number(json, "length_sec", 60f);
            int notesAt = json.IndexOf("\"notes\"", StringComparison.Ordinal);
            if (notesAt < 0) return map;
            foreach (Match m in NoteRx.Matches(json, notesAt))
            {
                int beat = int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
                map.Notes.Add(new ZooNote(beat, map.Offset + beat * map.BeatSeconds, m.Groups[2].Value == "jump" ? ZooNoteKind.Jump : ZooNoteKind.ArmsUp));
            }
            map.Notes.Sort((a, b) => a.Beat.CompareTo(b.Beat));
            return map;
        }

        // Plan 3-3: the zoo visit picks the tier (1st easy, 2nd normal, 3rd and later hard), a random chart inside the tier.
        public static ZooTier TierForVisit(int visit) => visit <= 1 ? ZooTier.Easy : visit == 2 ? ZooTier.Normal : ZooTier.Hard;
        public static ZooBeatmap Pick(IList<ZooBeatmap> charts, int visit, Random random)
        {
            if (charts == null || charts.Count == 0) return null;
            var tier = TierForVisit(visit); var same = new List<ZooBeatmap>();
            foreach (var chart in charts) if (chart != null && chart.Tier == tier) same.Add(chart);
            if (same.Count == 0) foreach (var chart in charts) if (chart != null && chart.Tier == ZooTier.Normal) same.Add(chart);
            if (same.Count == 0) same.AddRange(charts);
            return same[(random ?? new Random()).Next(same.Count)];
        }
    }

    public enum ZooPhase { Idle, Cut, Shout, Song, Done }

    // What the lion is doing (the art clips AN_Lion_*): breaking out of the cage in the entry cut, running behind, roaring as
    // the warning before a bite, and pouncing when it bites.
    public enum LionAction { CageBreakout, Run, Roar, Pounce }

    // The zoo tutorial's short practice course: a few fences and bars, five of every eight passed. The lion never gets close
    // enough to roar by itself.
    public static class ZooPractice
    {
        public const string Chart = "zoo_chase_v1.easy_a";
        public const float LionCap = .8f, Length = 200f;
        public static int PassHits(int obstacles) => (obstacles * 5 + 7) / 8;
    }

    // A fence (jump) or a narrowing set of bars (arms up) at a distance from the zoo entry.
    public readonly struct ZooObstacle
    {
        public readonly float Distance;
        public readonly ZooNoteKind Kind;
        public ZooObstacle(float distance, ZooNoteKind kind) { Distance = distance; Kind = kind; }
    }

    // The zoo map (owner decision 2026-11-27 08:19): run away from the lion. The song only plays in the background.
    // The runner's speed is the zoo base speed times the running pace (CadencePace, 70 % walking to 130 % sprinting, the same
    // rule as the mine). The lion runs at the zoo base speed times LionPace (a little faster each visit); the difference opens
    // or closes the gap. A tripped fence or a missed set of bars lets the lion close in. Fences and bars stand at distances,
    // not on beats. Without a cadence (the option off) the lion only comes on missed obstacles.
    // The iron gate stretch (the sprint) lies between GateStart and GateEnd: the lion sprints too.
    public sealed class ZooChase
    {
        public const float CutSeconds = 2f, ShoutSeconds = 1f, GraceSeconds = 1f, RoarSeconds = 1.5f;
        // Lion distance units (0 far away, 1 at the heels). The mirror shows from MirrorAt.
        public const float MirrorAt = .4f, AfterBite = .5f, RoarCancelBelow = .85f;
        public const float HitRelief = .08f, ArmsUpMiss = .2f, JumpMiss = .35f;
        // Gap change per second for each 1.0 of speed share between the lion and the runner (walking against a first-visit
        // lion closes the gap from far to the heels in about 20 s).
        public const float GapRate = .35f;
        // Running faster than the lion during the roar pushes it back hard enough to escape the bite.
        public const float RoarEscape = .2f;
        // How far before (Ahead) and after (Behind) an obstacle a jump or an arms-up still counts, in metres (generous).
        public const float Ahead = 7f, Behind = 1.5f;
        // The zoo course length in metres, and the metres kept clear for the entry cut and the shout, and before the exit.
        public const float CourseLength = 540f, StartClear = 34f, EndClear = 30f;
        // The lion's sprint share during the iron gate stretch.
        public const float GateLionPace = 1.15f;
        public static float LionPaceFor(int visit) => Math.Min(1f, .85f + .05f * Math.Max(0, visit - 1));
        public static float GateStartOf(float length) => length * .55f;
        public static float GateEndOf(float length) => length * .7f;
        // Gap between obstacles by visit (metres): wider on the first visit.
        public static float SpacingFor(int visit) => visit <= 1 ? 24f : visit == 2 ? 19f : 15f;

        // Places fences and bars over a course: fences twice as often as bars, none in the entry, the gate stretch or the exit.
        public static List<ZooObstacle> Layout(float length, int visit, Random random, bool gate = true)
        {
            random ??= new Random(); var list = new List<ZooObstacle>();
            float spacing = SpacingFor(visit), d = StartClear;
            float gateStart = GateStartOf(length), gateEnd = GateEndOf(length);
            while (d < length - EndClear)
            {
                if (!gate || d < gateStart - 8f || d > gateEnd + 8f)
                    list.Add(new ZooObstacle(d, random.Next(3) == 2 ? ZooNoteKind.ArmsUp : ZooNoteKind.Jump));
                d += spacing * (.85f + .3f * (float)random.NextDouble());
            }
            return list;
        }

        // Zoo base speed (m/s) and the lion's share of it.
        public float BaseSpeed = 9f;
        public float LionPace = .85f;
        public float Length { get; private set; } = CourseLength;
        public bool HasGate { get; private set; } = true;
        public float GateStart => GateStartOf(Length);
        public float GateEnd => GateEndOf(Length);
        // The roar before a bite (RoarLeadFor).
        public float RoarLead = RoarSeconds;
        public static float RoarLeadFor(float warningCueSeconds) => RoarSeconds + Math.Max(0f, warningCueSeconds);
        public bool Practice;
        public float SinceBite => Bites > 0 ? Clock - bitAt : float.MaxValue;
        private float bitAt;
        public LionAction ActionAt(float pounceSeconds) => Phase == ZooPhase.Cut ? LionAction.CageBreakout
            : Roaring ? LionAction.Roar : SinceBite < pounceSeconds ? LionAction.Pounce : LionAction.Run;
        public void ShowLion() { if (Practice && Active) Lion = ZooPractice.LionCap; }
        private float Capped(float lion) => Practice ? Math.Min(lion, ZooPractice.LionCap) : lion;

        public readonly List<ZooObstacle> Obstacles = new List<ZooObstacle>();
        public ZooPhase Phase { get; private set; } = ZooPhase.Idle;
        // Seconds since the zoo started (the cut and the shout, the roar).
        public float Clock { get; private set; }
        // Metres run since the entry.
        public float Distance { get; private set; }
        public float EntryZ { get; private set; }
        public float Lion { get; private set; }
        public float RoarLeft { get; private set; }
        public bool Roaring => RoarLeft > 0f;
        public int Hits { get; private set; }
        public int Misses { get; private set; }
        public int Bites { get; private set; }
        public bool MirrorVisible => Phase == ZooPhase.Song && (Lion >= MirrorAt || Roaring);
        public bool Judging => Phase == ZooPhase.Song;
        public bool InGrace => Phase == ZooPhase.Song && Clock < CutSeconds + ShoutSeconds + GraceSeconds;
        public bool InGate => HasGate && Distance >= GateStart && Distance < GateEnd;
        public bool Active => Phase != ZooPhase.Idle && Phase != ZooPhase.Done;

        // Judged obstacles (index into Obstacles): true passed, false missed.
        public event Action<int, bool> Judged;
        public event Action RoarStarted, Bit, Started, Finished;

        private readonly List<int> state = new List<int>(); // 0 open, 1 passed, 2 missed
        private int nextOpen;

        public void Start(IList<ZooObstacle> obstacles, float entryZ, float length = CourseLength, bool gate = true)
        {
            Obstacles.Clear(); if (obstacles != null) Obstacles.AddRange(obstacles);
            EntryZ = entryZ; Length = length; HasGate = gate;
            Clock = 0f; Distance = 0f; Lion = 0f; RoarLeft = 0f; Hits = Misses = Bites = 0; nextOpen = 0;
            state.Clear(); for (int i = 0; i < Obstacles.Count; i++) state.Add(0);
            Phase = ZooPhase.Cut; Started?.Invoke();
        }
        public void Stop() { if (Active) { Phase = ZooPhase.Done; Finished?.Invoke(); } RoarLeft = 0f; }
        public void Reset() { Phase = ZooPhase.Idle; Obstacles.Clear(); Clock = 0f; Distance = 0f; Lion = 0f; RoarLeft = 0f; state.Clear(); nextOpen = 0; }

        public float ObstacleZ(int index) => EntryZ + Obstacles[index].Distance;
        public bool IsJudged(int index) => index >= 0 && index < state.Count && state[index] != 0;
        public bool WasHit(int index) => index >= 0 && index < state.Count && state[index] == 1;
        // The zoo branch for RoutePlan.SetBranchLayout: zoo chunks (12) for the course and a little room, then the return
        // chunk (3). One lane, no stairs.
        public static int[] Branch()
        {
            int zoo = (int)Math.Ceiling((CourseLength + 20f) / RouteSurface.Length);
            var chunks = new int[zoo + 1];
            for (int i = 0; i < zoo; i++) chunks[i] = 12;
            chunks[zoo] = 3;
            return chunks;
        }

        // distance: metres the runner has covered since the entry. pace: the runner's speed share (CadencePace.Scale), or null
        // without a cadence (the lion then only comes on missed obstacles).
        public void Tick(float dt, float distance, float? pace)
        {
            if (!Active || dt <= 0f) return;
            Clock += dt; Distance = Math.Max(Distance, distance);
            if (Phase == ZooPhase.Cut && Clock >= CutSeconds) Phase = ZooPhase.Shout;
            if (Phase == ZooPhase.Shout && Clock >= CutSeconds + ShoutSeconds) Phase = ZooPhase.Song;
            if (Phase != ZooPhase.Song) return;
            while (nextOpen < state.Count && Obstacles[nextOpen].Distance + Behind < Distance)
            {
                if (state[nextOpen] == 0) Judge(nextOpen, false);
                nextOpen++;
            }
            if (pace.HasValue && !InGrace)
            {
                float rate = ((InGate ? GateLionPace : LionPace) - pace.Value) * GapRate;
                if (Roaring && rate < 0f) rate = -RoarEscape;
                Lion = Capped(Clamp(Lion + rate * dt));
            }
            UpdateRoar(dt);
            if (Distance >= Length) Stop();
        }
        private void UpdateRoar(float dt)
        {
            if (Roaring)
            {
                if (Lion < RoarCancelBelow) { RoarLeft = 0f; return; }
                RoarLeft -= dt;
                if (RoarLeft > 0f) return;
                RoarLeft = 0f; Lion = AfterBite; Bites++; bitAt = Clock; Bit?.Invoke();
                return;
            }
            if (Lion >= 1f) { RoarLeft = Math.Max(RoarSeconds, RoarLead); RoarStarted?.Invoke(); }
        }
        // A jump or an arms-up at the current distance. Returns the obstacle it passed, or -1.
        public int Input(ZooNoteKind kind)
        {
            if (Phase != ZooPhase.Song) return -1;
            for (int i = nextOpen; i < state.Count; i++)
            {
                float ahead = Obstacles[i].Distance - Distance;
                if (ahead > Ahead) break;
                if (state[i] != 0 || Obstacles[i].Kind != kind || ahead < -Behind) continue;
                Judge(i, true); return i;
            }
            return -1;
        }
        private void Judge(int index, bool hit)
        {
            state[index] = hit ? 1 : 2;
            if (hit) { Hits++; Lion = Clamp(Lion - HitRelief); }
            else { Misses++; Lion = Capped(Clamp(Lion + (Obstacles[index].Kind == ZooNoteKind.Jump ? JumpMiss : ArmsUpMiss))); }
            Judged?.Invoke(index, hit);
        }
        private static float Clamp(float v) => v < 0f ? 0f : v > 1f ? 1f : v;
    }

    // Keyboard running (owner 2026-11-27 08:25): F and J pressed in turn count one step each, the same steps a minute as running
    // in place (the same key twice in a row counts nothing). Counts over the last Window seconds, like the camera.
    public sealed class KeyStepCadence
    {
        public const float Window = 3f;
        private readonly Queue<double> steps = new Queue<double>();
        private int last = -1;
        // key: 0 for F, 1 for J. Returns true when it counted a step.
        public bool Press(int key, double now)
        {
            if (key == last) return false;
            last = key; steps.Enqueue(now); return true;
        }
        public float StepsPerMinute(double now)
        {
            while (steps.Count > 0 && now - steps.Peek() > Window) steps.Dequeue();
            return steps.Count * 60f / Window;
        }
        public void Reset() { steps.Clear(); last = -1; }
    }
}
