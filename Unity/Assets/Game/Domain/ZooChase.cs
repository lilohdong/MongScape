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

    // The verse of the zoo tutorial (plan 6-1b, chapter 2): the real song on an easy chart up to the break, five of every eight
    // notes pass. The lion never gets close enough to roar by itself.
    public static class ZooPractice
    {
        public const string Chart = "zoo_chase_v1.easy_a";
        public const float LionCap = .8f;
        public static ZooBeatmap Verse(ZooBeatmap full)
        {
            var map = new ZooBeatmap();
            if (full == null) return map;
            map.Chart = full.Chart; map.Difficulty = full.Difficulty; map.Bpm = full.Bpm; map.Offset = full.Offset; map.Length = full.Length;
            foreach (var note in full.Notes) if (note.Time < full.BreakStart) map.Notes.Add(note);
            return map;
        }
        public static int PassHits(int notes) => (notes * 5 + 7) / 8;
    }

    // The zoo map (plan 1-2a, 1-2b, 1-2c, 6-8): the song sets the speed and the notes, the running beat sets the lion's distance.
    // Song time is the clock: the runner's speed follows Speed(SongTime), so a note at time t sits at EntryZ + DistanceAt(t).
    // Cut (2 s) + shout (1 s) cover the song intro; the speed eases from the entry speed to the song speed during the cut,
    // nothing is judged until the shout ends, and the running beat is not judged for one more second (grace).
    public sealed class ZooChase
    {
        public const float CutSeconds = 2f, ShoutSeconds = 1f, GraceSeconds = 1f, Window = .25f, RoarSeconds = 1.5f;
        // Lion distance units (0 far away, 1 at the heels). The mirror shows from MirrorAt.
        public const float MirrorAt = .4f, AfterBite = .5f, RoarCancelBelow = .85f;
        public const float HitRelief = .08f, ArmsUpMiss = .2f, JumpMiss = .35f, BehindRise = .07f, FollowFall = .04f;
        // Catching the beat again during the roar pushes the lion back fast enough to escape the bite (plan 1-2).
        public const float RoarEscape = .2f;
        // Song speed (m/s): runs the 60 s song over the zoo branch.
        public float SongSpeed = 9f;
        // The player's recognition delay (seconds, positive = the camera reports late), from the sync calibration.
        public float SyncOffset;
        // The roar before a bite (RoarLeadFor).
        public float RoarLead = RoarSeconds;
        // The warning: RoarSeconds plus the run's whole warning cue time (RunSession.WarningCueSeconds, sharp ears included).
        public static float RoarLeadFor(float warningCueSeconds) => RoarSeconds + Math.Max(0f, warningCueSeconds);
        // Tutorial verse: the lion stays at ZooPractice.LionCap or farther, so it never roars or bites by itself.
        public bool Practice;
        // Seconds since the last bite (MaxValue before the first).
        public float SinceBite => Bites > 0 ? SongTime - bitAt : float.MaxValue;
        private float bitAt;
        public LionAction ActionAt(float pounceSeconds) => Phase == ZooPhase.Cut ? LionAction.CageBreakout
            : Roaring ? LionAction.Roar : SinceBite < pounceSeconds ? LionAction.Pounce : LionAction.Run;
        // The tutorial's one look in the mirror: the practice lion comes as close as it may.
        public void ShowLion() { if (Practice && Active) Lion = ZooPractice.LionCap; }
        private float Capped(float lion) => Practice ? Math.Min(lion, ZooPractice.LionCap) : lion;

        public ZooBeatmap Map { get; private set; }
        public ZooPhase Phase { get; private set; } = ZooPhase.Idle;
        public float SongTime { get; private set; }
        public float EntryZ { get; private set; }
        public float EntrySpeed { get; private set; }
        public float Lion { get; private set; }
        public float RoarLeft { get; private set; }
        public bool Roaring => RoarLeft > 0f;
        public int Hits { get; private set; }
        public int Misses { get; private set; }
        public int Bites { get; private set; }
        public bool MirrorVisible => Phase == ZooPhase.Song && (Lion >= MirrorAt || Roaring);
        public bool Judging => Phase == ZooPhase.Song;
        public bool InGrace => Phase == ZooPhase.Song && SongTime < CutSeconds + ShoutSeconds + GraceSeconds;
        public bool InBreak => Map != null && SongTime >= Map.BreakStart && SongTime < Map.BreakEnd;
        public bool Active => Phase != ZooPhase.Idle && Phase != ZooPhase.Done;

        // Judged notes (index into Map.Notes): true hit, false miss.
        public event Action<int, bool> Judged;
        public event Action RoarStarted, Bit, Started, Finished;

        private readonly List<int> state = new List<int>(); // 0 open, 1 hit, 2 miss
        private int nextOpen;

        public void Start(ZooBeatmap map, float entryZ, float entrySpeed)
        {
            Map = map ?? new ZooBeatmap(); EntryZ = entryZ; EntrySpeed = Math.Max(0f, entrySpeed);
            SongTime = 0f; Lion = 0f; RoarLeft = 0f; Hits = Misses = Bites = 0; nextOpen = 0;
            state.Clear(); for (int i = 0; i < Map.Notes.Count; i++) state.Add(0);
            Phase = ZooPhase.Cut; Started?.Invoke();
        }
        public void Stop() { if (Active) { Phase = ZooPhase.Done; Finished?.Invoke(); } RoarLeft = 0f; }
        public void Reset() { Phase = ZooPhase.Idle; Map = null; SongTime = 0f; Lion = 0f; RoarLeft = 0f; state.Clear(); nextOpen = 0; }

        // Speed at song time t: eases linearly from the entry speed to the song speed during the cut, then holds.
        public float Speed(float t) => t >= CutSeconds ? SongSpeed : EntrySpeed + (SongSpeed - EntrySpeed) * Math.Max(0f, t) / CutSeconds;
        public float DistanceAt(float t)
        {
            float c = Math.Min(Math.Max(0f, t), CutSeconds);
            float d = EntrySpeed * c + (SongSpeed - EntrySpeed) * c * c / (2f * CutSeconds);
            return t > CutSeconds ? d + SongSpeed * (t - CutSeconds) : d;
        }
        public float NoteZ(int index) => EntryZ + DistanceAt(Map.Notes[index].Time);
        public bool IsJudged(int index) => index >= 0 && index < state.Count && state[index] != 0;
        public bool WasHit(int index) => index >= 0 && index < state.Count && state[index] == 1;
        // Distance the runner needs for the whole song (with the slowest entry this is the branch length to lay out).
        public static float SongDistance(float songSpeed, float songSeconds) => songSpeed * songSeconds;
        // The zoo branch for RoutePlan.SetBranchLayout: zoo chunks (12) for the song and its tail at song speed, plus a little
        // room for a fast entry, then the return chunk (3). One lane, no stairs.
        public const float SongSeconds = 61.5f;
        public static int[] Branch(float songSpeed)
        {
            int zoo = (int)Math.Ceiling((SongDistance(songSpeed, SongSeconds) + 10f) / RouteSurface.Length);
            var chunks = new int[zoo + 1];
            for (int i = 0; i < zoo; i++) chunks[i] = 12;
            chunks[zoo] = 3;
            return chunks;
        }

        // behind: the running beat falls behind the music (null for the keyboard or with the cadence option off: the lion then
        // only comes on missed notes, plan 5-3).
        public void Tick(float dt, bool? behind)
        {
            if (!Active || dt <= 0f) return;
            SongTime += dt;
            if (Phase == ZooPhase.Cut && SongTime >= CutSeconds) Phase = ZooPhase.Shout;
            if (Phase == ZooPhase.Shout && SongTime >= CutSeconds + ShoutSeconds) Phase = ZooPhase.Song;
            if (Phase != ZooPhase.Song) return;
            // Notes whose window has passed are misses.
            while (nextOpen < state.Count && Map.Notes[nextOpen].Time + Window + SyncOffset < SongTime)
            {
                if (state[nextOpen] == 0) Judge(nextOpen, false);
                nextOpen++;
            }
            if (behind.HasValue && !InGrace) Lion = Capped(Clamp(Lion + (behind.Value ? BehindRise : -(Roaring ? RoarEscape : FollowFall)) * dt));
            UpdateRoar(dt);
            if (Map.Length > 0f && SongTime >= Map.Length) Stop();
        }
        private void UpdateRoar(float dt)
        {
            if (Roaring)
            {
                if (Lion < RoarCancelBelow) { RoarLeft = 0f; return; }
                RoarLeft -= dt;
                if (RoarLeft > 0f) return;
                RoarLeft = 0f; Lion = AfterBite; Bites++; bitAt = SongTime; Bit?.Invoke();
                return;
            }
            if (Lion >= 1f) { RoarLeft = Math.Max(RoarSeconds, RoarLead); RoarStarted?.Invoke(); }
        }
        // A jump or an arms-up from the player at the current song time. Returns the note it hit, or -1.
        public int Input(ZooNoteKind kind)
        {
            if (Phase != ZooPhase.Song) return -1;
            float at = SongTime - SyncOffset; int best = -1; float bestGap = Window + .0001f;
            for (int i = nextOpen; i < state.Count; i++)
            {
                var note = Map.Notes[i];
                if (note.Time - at > Window) break;
                if (state[i] != 0 || note.Kind != kind) continue;
                float gap = Math.Abs(note.Time - at);
                if (gap <= bestGap) { best = i; bestGap = gap; }
            }
            if (best >= 0) Judge(best, true);
            return best;
        }
        private void Judge(int index, bool hit)
        {
            state[index] = hit ? 1 : 2;
            if (hit) { Hits++; Lion = Clamp(Lion - HitRelief); }
            else { Misses++; Lion = Capped(Clamp(Lion + (Map.Notes[index].Kind == ZooNoteKind.Jump ? JumpMiss : ArmsUpMiss))); }
            Judged?.Invoke(index, hit);
        }
        // Pause: the song clock stops with the game. Resume after a count of four beats (the presenter plays the count).
        public float CountInSeconds => Map != null ? Map.BeatSeconds * 4f : 1.5f;
        private static float Clamp(float v) => v < 0f ? 0f : v > 1f ? 1f : v;
    }
}
