using System;
using System.Collections.Generic;
using Mongscape.Domain;
using Mongscape.Presentation.Map;
using UnityEngine;

namespace Mongscape.Presentation
{
    // Runs one tutorial chapter (plan 6-1b) on the ordinary run at full speed. Course lessons (lane, jump, crouch, bananas, stair,
    // cart fork) show as they come into view and the big call follows the action's timing (ActionTiming). Stations that the maps
    // do not provide yet (rescue, beat sync, verse, searchlight hiding) are run here with their own pictures, and picture steps
    // show a short drawing. A real mistake shows "다시 하기" and restarts in front of the same step; the third one grows the demo
    // window into the joint comparison. The bootstrap ticks it before the session moves.
    public sealed class TutorialDirector : MonoBehaviour
    {
        // The lesson shows (and its action counts) from this far ahead.
        public const float ShowDistance = 14f;
        public const float RetrySeconds = 1.6f, DoneSeconds = 2.5f, PraiseSeconds = .9f, PictureSeconds = 4.5f, ReachSeconds = .4f;
        private RunSession session;
        private ChunkSpawner map;
        private Func<bool> poseReady, visible;
        private Action<float> restart;
        private Action finished;
        private HudSkin skin;
        private readonly List<ObstacleController> lesson = new List<ObstacleController>(3);
        private int seenMisses;
        private float retryAt = -1, doneAt = -1, praiseUntil = -1, forgiveUntilZ = -1;
        private string cue;
        public TutorialScript Script { get; private set; }
        public bool Active => Script != null && session != null && session.IsTutorial;
        // Distance to the current lesson (obstacle or fork decision); infinity when none is in sight.
        public float LessonDistance { get; private set; } = float.PositiveInfinity;
        // The current lesson is in view and not done yet.
        public bool Waiting { get; private set; }
        public bool Retrying => retryAt >= 0;
        public string Message { get; private set; }
        // The big call on screen ("↑  점프!", "←  옆으로 피하기  →", ...), null while a timed call is not due yet.
        public string Cue => cue;
        // The X pause is judged during its step (the bootstrap leaves the gesture off in the other steps).
        public bool PauseGestureOpen => Active && Script.Current == TutorialStep.PauseX;
        // Inputs of the camera and pace lessons (set by the bootstrap): the body framed head to ankles (waist up for the
        // upper body), steps a minute, the speed share from the cadence.
        public Func<bool> Framed;
        // No camera or model (developer tests, a missing webcam): the camera and pace lessons pass at once instead of waiting forever.
        public Func<bool> NoCamera;
        public Func<float> StepsPerMinute, PaceScale;
        public float RunCadence = 100f;
        // The arm readings the stations need (camera or keys): one arm out, both hands up.
        public Func<bool> Reaching, HandsUp;
        // Sound hooks: a beat tick, and a cleared station. The saved sync offset in ms and where to save a new one.
        public Action Beat, Cleared;
        public Func<int> SyncMs;
        public Action<int> SaveSync;
        // The mirror monkey window (character clip); null keeps it hidden.
        public TutorialDemo Demo;
        // The player's joints (null without a tracked camera), calibrated pose and thresholds, for the joint comparison that
        // joins the follow-along view.
        public Func<PoseFrame> MyPose;
        public Func<PoseBaseline> MyBaseline;
        public Func<MotionTuning> Tuning;
        private readonly PoseFrame reference = new PoseFrame();
        // Seconds the camera lesson waits for a framed body, the pace lesson for a held pace.
        public const float FramedSeconds = 3f, PacedSeconds = 5f;
        private float framedFor, pacedFor, reachFor, pictureFor;
        private bool cadenceSpeed = true, pauseSeen, wasJumping, wasHands, rescued, stationBegun;
        private float rescuedAt;
        private int bigAtStart, lastTick;
        private readonly BeatSync sync = new BeatSync();
        private readonly BeatVerse verse = new BeatVerse();
        private readonly HideSet hide = new HideSet();

        public void Configure(RunSession run, ChunkSpawner spawner, Func<bool> calibrated, Action<float> restartAt, Action complete, Func<bool> canDraw)
        {
            session = run; map = spawner; poseReady = calibrated; restart = restartAt; finished = complete; visible = canDraw;
        }
        public void Begin(TutorialChapter chapter, PlayStyle style, bool forks, bool cadenceOn = true, bool shortVersion = false)
        {
            Script = new TutorialScript(chapter, style, forks, shortVersion); TutorialCourse.Script = Script; cadenceSpeed = cadenceOn; bigAtStart = BigTotal; seenMisses = session.TutorialMisses;
            retryAt = doneAt = praiseUntil = forgiveUntilZ = -1;
            ResetStation(); Waiting = false; LessonDistance = float.PositiveInfinity; Message = null; cue = null;
        }
        public void End() { TutorialCourse.Script = null; Demo?.Show(null); Script = null; Waiting = false; retryAt = doneAt = praiseUntil = forgiveUntilZ = -1; Message = cue = null; if (session != null) session.SpeedScale = 1; }
        // The player paused (X gesture or Esc) while the pause step waits: it passes when play goes on again.
        public void NotifyPaused(PauseReason reason)
        {
            if (Active && Script.Current == TutorialStep.PauseX && (reason == PauseReason.Gesture || reason == PauseReason.Key)) pauseSeen = true;
        }
        private void ResetStation()
        {
            framedFor = pacedFor = reachFor = pictureFor = 0; pauseSeen = wasJumping = wasHands = rescued = stationBegun = false; lastTick = -1;
        }

        public void Tick(float now)
        {
            if (!Active || session.Flow.State != GameFlowState.Running || session.Paused) return;
            var runner = session.Runner;
            if (doneAt >= 0) { session.SpeedScale = .5f; if (now - doneAt >= DoneSeconds) { var done = finished; End(); done?.Invoke(); } return; }
            if (retryAt >= 0)
            {
                session.SpeedScale = 0;
                if (now < retryAt) return;
                retryAt = -1; Waiting = false; Message = null; session.SpeedScale = 1; ResetStation();
                restart?.Invoke(TutorialCourse.RestartZ(Script.Current));
                seenMisses = session.TutorialMisses; return;
            }
            if (session.TutorialMisses > seenMisses)
            {
                // A slip inside the chunk of a lesson already passed (an early jump landing on its crate) is forgiven.
                if (runner.Z < forgiveUntilZ) seenMisses = session.TutorialMisses;
                else { Miss(now); return; }
            }
            session.SpeedScale = 1;
            var step = Script.Current;
            Demo?.Show(TutorialScript.DemoClip(step, Script.Style));
            switch (Script.Condition)
            {
                case TutorialPass.Framed:
                    // Head to ankles (waist up for the upper body) in view for three seconds in a row.
                    session.SpeedScale = 0; Waiting = true; cue = TutorialScript.Title(step);
                    Message = TutorialScript.Instruction(step, Script.Style);
                    if (NoCamera != null && NoCamera()) { Next(now); return; }
                    framedFor = Framed == null || Framed() ? framedFor + Time.deltaTime : 0;
                    if (Framed == null || framedFor >= FramedSeconds) Next(now);
                    return;
                case TutorialPass.Calibrated:
                    session.SpeedScale = 0; Waiting = true; Message = TutorialScript.Instruction(step, Script.Style);
                    if (poseReady == null || poseReady()) Next(now);
                    return;
                case TutorialPass.Paced:
                {
                    // Run in place: five seconds at a running pace (or any stepping when the cadence option is off) pass.
                    // The runner waits at the end of the pace chunk.
                    if (NoCamera != null && NoCamera()) { Next(now); return; }
                    Hold(step); Waiting = true; cue = TutorialScript.Title(step);
                    Message = TutorialScript.Instruction(step, Script.Style);
                    float spm = StepsPerMinute?.Invoke() ?? 0;
                    bool paced = cadenceSpeed ? spm >= RunCadence : spm > 0;
                    pacedFor = paced ? pacedFor + Time.deltaTime : Mathf.Max(0, pacedFor - Time.deltaTime);
                    if (StepsPerMinute == null || pacedFor >= PacedSeconds) Next(now);
                    return;
                }
                case TutorialPass.Picture:
                    Hold(step); Waiting = true; cue = null; Message = TutorialScript.Instruction(step, Script.Style);
                    pictureFor += Time.deltaTime;
                    if (pictureFor >= PictureSeconds) Next(now);
                    return;
                case TutorialPass.Paused:
                    Hold(step); Waiting = true; cue = null; Message = TutorialScript.Instruction(step, Script.Style);
                    if (pauseSeen) Next(now);
                    return;
                case TutorialPass.Station:
                    Hold(step); Waiting = true; cue = null; Message = TutorialScript.Instruction(step, Script.Style);
                    TickStation(step, now);
                    return;
                case TutorialPass.ObstaclePassed when step == TutorialStep.Banana:
                {
                    FindBananas();
                    if (peels.Count == 0) { Hide(); return; }
                    bool passed = true; foreach (var peel in peels) passed &= peel.Passed;
                    if (passed) { Next(now); return; }
                    LessonDistance = peels[0].transform.position.z - runner.Z;
                    Show(LessonDistance <= ShowDistance, false, now);
                    return;
                }
                case TutorialPass.ForkChosen:
                {
                    var plan = RouteSurface.ActivePlan;
                    LessonDistance = TutorialCourse.ForkDecisionZ - runner.Z;
                    if (plan == null || !plan.Extended) { Next(now); return; }
                    if (plan.ForkLocked(runner.Z)) { if (plan.ChoiceAt(RouteSurface.SequenceAt(runner.Z)) != 0) Next(now); else Miss(now); return; }
                    // The lean counts anywhere in the fork window.
                    bool open = plan.ForkWindow(runner.Z);
                    Show(open || LessonDistance <= ShowDistance, open && TutorialScript.Performed(step, runner), now);
                    return;
                }
                case TutorialPass.ObstaclePassed when step == TutorialStep.Stair:
                {
                    // The stair chunk is the lesson: the first knee step at its foot completes it. Standing still on the steps is a miss
                    // (the stair judge) and restarts the step.
                    float start = TutorialCourse.PrisonStairChunk * RouteSurface.Length;
                    if (runner.Z >= start + RouteSurface.Length) { Next(now); return; }
                    LessonDistance = start + StairJudge.From + 1f - runner.Z;
                    Show(LessonDistance <= ShowDistance, TutorialScript.Performed(step, runner), now);
                    return;
                }
                case TutorialPass.ObstaclePassed:
                {
                    FindLesson();
                    if (lesson.Count == 0) { Hide(); return; }
                    bool passed = true; foreach (var hazard in lesson) passed &= hazard.Passed;
                    if (passed) { Next(now); return; }
                    LessonDistance = lesson[0].transform.position.z - runner.Z;
                    Show(LessonDistance <= ShowDistance, TutorialScript.Performed(step, runner), now);
                    return;
                }
            }
        }

        // The four stations the director runs itself.
        private void TickStation(TutorialStep step, float now)
        {
            var runner = session.Runner;
            // The runner must stand at the station's spot first.
            if (runner.Z < TutorialCourse.HoldZ(Script.Chapter, step) - .5f) return;
            bool jumping = runner.Y > .01f, jumpEdge = jumping && !wasJumping; wasJumping = jumping;
            bool hands = HandsUp != null && HandsUp(), handsEdge = hands && !wasHands; wasHands = hands;
            if (NoCamera != null && NoCamera() && PlayStyleInfo.UsesCamera(Script.Style)) { Next(now); return; }
            switch (step)
            {
                case TutorialStep.Rescue:
                    // One arm out for a moment: the stuck monkey comes along.
                    reachFor = Reaching != null && Reaching() ? reachFor + Time.deltaTime : 0;
                    if (reachFor >= ReachSeconds) { rescued = true; rescuedAt = now; Next(now); }
                    return;
                case TutorialStep.Sync:
                {
                    if (!stationBegun) { sync.Begin(now); stationBegun = true; lastTick = -1; }
                    TickBeats(now, sync.Start, BeatSync.Interval);
                    if (jumpEdge) sync.Jump(now);
                    if (sync.Done) { SaveSync?.Invoke(Mathf.RoundToInt((float)sync.Offset * 1000f)); Next(now); }
                    else if (sync.Over(now)) Miss(now);
                    return;
                }
                case TutorialStep.Verse:
                {
                    if (!stationBegun) { verse.Begin(now, (SyncMs?.Invoke() ?? 0) / 1000.0); stationBegun = true; lastTick = -1; }
                    TickBeats(now, verse.Start, BeatVerse.Interval);
                    if (jumpEdge) verse.Act(NoteKind.Jump, now);
                    if (handsEdge) verse.Act(NoteKind.Hands, now);
                    if (verse.Over(now)) { if (verse.Passed) Next(now); else Miss(now); }
                    return;
                }
                case TutorialStep.Hide:
                    if (!stationBegun) { hide.Begin(); stationBegun = true; }
                    hide.Tick(Time.deltaTime, runner.Crouching, runner.DeepSliding);
                    if (hide.Spotted) Miss(now);
                    else if (hide.Passed) Next(now);
                    return;
            }
        }
        // One tick sound per interval from the start of a beat lesson.
        private void TickBeats(float now, double start, double interval)
        {
            int n = (int)Math.Floor((now - start) / interval);
            if (n != lastTick && n >= 0) { lastTick = n; Beat?.Invoke(); }
        }
        // While the lesson is in view its instruction shows and its action completes it.
        private void Show(bool inView, bool performed, float now)
        {
            if (!inView) { Hide(); return; }
            if (performed) { Next(now); return; }
            var step = Script.Current;
            Waiting = true;
            var call = ActionCuePresenter.Find(session, map, Script.Style);
            if (TutorialScript.Timed(step))
            {
                // Jump and crouch: the big call and the instruction wait for the action's timing.
                cue = call;
                Message = call != null ? TutorialScript.Instruction(step, Script.Style) : TutorialScript.Prepare(step, Script.Style);
            }
            else
            {
                cue = call ?? TutorialScript.Title(step);
                Message = TutorialScript.Instruction(step, Script.Style);
            }
        }
        // The pace lessons and stations keep the runner at their spot until they pass (the speed gauge still follows the steps).
        private void Hold(TutorialStep step) { session.SpeedScale = session.Runner.Z >= TutorialCourse.HoldZ(Script.Chapter, step) ? 0 : 1; }
        private readonly List<FloorItemController> peels = new List<FloorItemController>(2);
        private void FindBananas()
        {
            peels.Clear();
            if (map == null) return;
            foreach (var item in map.Active)
            {
                if (item.Chunk.Sequence != TutorialCourse.BananaChunk || !item.Chunk.gameObject.activeSelf) continue;
                foreach (var peel in item.Chunk.DynamicFloorItems) if (peel != null && peel.gameObject.activeSelf && peel.Kind == FloorItemKind.BananaPeel) peels.Add(peel);
            }
            peels.Sort((a, b) => a.transform.position.z.CompareTo(b.transform.position.z));
        }
        // Big moves made during the step: the praise says so, but they are never required.
        private int BigTotal { get { if (session == null) return 0; int n = 0; foreach (int c in session.BigMoves) n += c; return n; } }
        private bool bigPraise;
        private void Hide() { LessonDistance = float.PositiveInfinity; Waiting = false; Message = null; cue = null; }
        private void Next(float now)
        {
            var step = Script.Current;
            int chunk = TutorialCourse.ChunkOf(step);
            if (chunk >= 0) forgiveUntilZ = Mathf.Max(forgiveUntilZ, (chunk + 1) * RouteSurface.Length);
            bigPraise = BigTotal > bigAtStart; bigAtStart = BigTotal;
            bool station = TutorialScript.PassOf(step) == TutorialPass.Station;
            ResetStation();
            Script.Pass(); Waiting = false; LessonDistance = float.PositiveInfinity; session.SpeedScale = 1; cue = null;
            Message = Script.Finished ? TutorialScript.Instruction(TutorialStep.Done, Script.Style) : null;
            if (station) Cleared?.Invoke();
            if (Script.Finished) doneAt = now;
            else if (step != TutorialStep.Calibrate) praiseUntil = now + PraiseSeconds;
        }
        private void Miss(float now)
        {
            seenMisses = session.TutorialMisses; Script.Fail(); retryAt = now + RetrySeconds; praiseUntil = -1;
            Waiting = false; session.SpeedScale = 0; cue = null; Message = "다시 하기";
        }
        // Active hazards of this step's lesson chunk (one lane block or one full row).
        private void FindLesson()
        {
            lesson.Clear();
            int sequence = TutorialCourse.ChunkOf(Script.Current);
            if (map == null || sequence < 0) return;
            foreach (var item in map.Active)
            {
                if (item.Chunk.Sequence != sequence || !item.Chunk.gameObject.activeSelf) continue;
                Collect(item.Chunk.Obstacles); Collect(item.Chunk.ExerciseObstacles); Collect(item.Chunk.DynamicObstacles);
            }
        }
        private void Collect(IList<ObstacleController> hazards)
        {
            if (hazards == null) return;
            foreach (var hazard in hazards) if (hazard != null && hazard.gameObject.activeSelf) lesson.Add(hazard);
        }

        private void OnGUI()
        {
            if (!Active || session.Flow.State != GameFlowState.Running || session.Paused || (visible != null && !visible())) return;
            if (Event.current.type != EventType.Repaint) return;
            skin ??= new HudSkin();
            var old = GUI.matrix; GUI.matrix = ViewportLayout.GuiMatrix; GUI.depth = -5;
            var step = Script.Current;
            // The step's title and its place in the chapter (no separators between the two).
            var header = new Rect(440, 92, 400, 56);
            skin.Card(header, Palette.A(Palette.Ground, .85f));
            GUI.color = Palette.Gold;
            GUI.Label(new Rect(header.x, header.y + 2, header.width, 32), step == TutorialStep.Done ? "다 했어요" : TutorialScript.Title(step), skin.Centered(24, FontStyle.Bold));
            GUI.color = Palette.Sand;
            if (step != TutorialStep.Done) GUI.Label(new Rect(header.x, header.y + 32, header.width, 22), TutorialScript.ChapterTitle(Script.Chapter) + "  " + (Script.Index + 1) + " / " + (Script.Steps.Count - 1), skin.Centered(15, FontStyle.Normal));
            // Mistakes and the finish fill the banner; a lesson in view shows its call (when due) with the instruction below.
            bool praise = Time.realtimeSinceStartup < praiseUntil && !Waiting;
            string big = Retrying || Script.Finished ? Message : Waiting ? cue : praise ? (bigPraise ? "크게! 좋아요!" : "좋아요!") : null;
            if (big != null)
            {
                var banner = new Rect(240, 160, 800, 110);
                skin.Card(banner, Palette.A(Palette.Ground, .88f));
                GUI.color = Retrying ? new Color(1, .7f, .3f) : praise ? new Color(.45f, 1f, .6f) : Palette.Gold;
                GUI.Label(banner, big, skin.Centered(58, FontStyle.Bold));
            }
            if (!Retrying && !Script.Finished && !string.IsNullOrEmpty(Message))
            {
                var line = new Rect(240, 276, 800, 46);
                skin.Card(line, Palette.A(Palette.Ground, .82f));
                GUI.color = Palette.Cream; GUI.Label(line, Message, skin.Centered(24, FontStyle.Bold));
            }
            if (!Retrying) DrawStation(step);
            DrawDemo();
            GUI.color = Color.white; GUI.matrix = old;
        }
        private void DrawStation(TutorialStep step)
        {
            float now = Time.realtimeSinceStartup;
            switch (step)
            {
                case TutorialStep.Cadence: DrawPace(); break;
                case TutorialStep.Rescue: TutorialPictures.Rescue(skin, rescued, now - rescuedAt, Mathf.Clamp01(reachFor / ReachSeconds), Script.Style); break;
                case TutorialStep.Sync: if (stationBegun) TutorialPictures.Beats(skin, now - (float)sync.Start, (float)BeatSync.Interval, BeatSync.LeadBeats, BeatSync.Beats, sync.Hits); break;
                case TutorialStep.Verse: if (stationBegun) TutorialPictures.Verse(skin, verse, now, Script.Style); break;
                case TutorialStep.Hide: TutorialPictures.Hide(skin, hide); break;
                case TutorialStep.WarnRule: TutorialPictures.WarnRule(skin, pictureFor / PictureSeconds); break;
                case TutorialStep.Rearview: TutorialPictures.Rearview(skin, pictureFor / PictureSeconds); break;
                case TutorialStep.AlarmRule: TutorialPictures.AlarmRule(skin, pictureFor / PictureSeconds); break;
            }
        }
        // The pace lesson gauge: the speed share from the cadence (70 % walking to 130 % sprinting) and the steps a minute.
        private void DrawPace()
        {
            var box = new Rect(440, 336, 400, 64);
            skin.Card(box, Palette.A(Palette.Ground, .85f));
            float scale = PaceScale?.Invoke() ?? 1, spm = StepsPerMinute?.Invoke() ?? 0;
            var track = new Rect(box.x + 16, box.y + 38, box.width - 32, 12);
            GUI.color = new Color(1, 1, 1, .2f); GUI.DrawTexture(track, Texture2D.whiteTexture);
            GUI.color = spm >= RunCadence ? new Color(.45f, 1f, .6f) : Palette.Gold;
            GUI.DrawTexture(new Rect(track.x, track.y, track.width * Mathf.InverseLerp(.7f, 1.3f, scale), track.height), Texture2D.whiteTexture);
            GUI.color = Palette.Cream;
            GUI.Label(new Rect(box.x, box.y + 4, box.width, 30), "분당 " + Mathf.RoundToInt(spm) + "걸음", skin.Centered(19, FontStyle.Bold));
        }
        // The mirror monkey: the character clip in the bottom-left, flipped like a mirror; after three misses of one step it grows
        // into the follow-along view.
        private void DrawDemo()
        {
            var texture = Demo?.Texture;
            if (texture == null) return;
            bool follow = Script.FollowAlong;
            var frame = follow ? new Rect(24, 330, 330, 360) : new Rect(24, 474, 210, 230);
            skin.Card(frame, Palette.A(Palette.Ground, .9f));
            GUI.color = follow ? Palette.Gold : Palette.Cream;
            GUI.Label(new Rect(frame.x, frame.y + 2, frame.width, 26), follow ? "따라 하기" : "시범", skin.Centered(follow ? 22 : 17, FontStyle.Bold));
            GUI.color = Color.white;
            float side = frame.width - 12;
            // Mirrored: the monkey raises the arm that the player sees on the same side of the picture.
            GUI.DrawTextureWithTexCoords(new Rect(frame.x + 6, frame.y + 30, side, Mathf.Min(side, frame.height - 36)), texture, new Rect(1, 0, -1, 1));
            if (follow) DrawJoints(new Rect(frame.xMax + 12, frame.y, 430, frame.height));
        }
        // The joint comparison is on screen (the follow-along view of a step that has one), where the HUD notice would sit.
        public bool ComparingJoints => Active && Script != null && !Script.Finished && Script.FollowAlong && Demo?.Texture != null && JointDemo.Compares(Script.Current);
        // After three misses: the reference stick figure doing the lesson next to the player's own joints, each with the
        // line the classifier uses (the player's from their calibration; green once crossed).
        private void DrawJoints(Rect panel)
        {
            var step = Script.Current;
            if (!JointDemo.Compares(step)) return;
            var tuning = Tuning?.Invoke() ?? new MotionTuning();
            skin.Card(panel, Palette.A(Palette.Ground, .9f));
            GUI.color = Palette.Gold; GUI.Label(new Rect(panel.x, panel.y + 2, panel.width, 26), JointDemo.Title, skin.Centered(20, FontStyle.Bold));
            float w = (panel.width - 18) * .5f, h = panel.height - 66;
            var left = new Rect(panel.x + 6, panel.y + 32, w, h); var right = new Rect(left.xMax + 6, left.y, w, h);
            var refBase = JointDemo.ReferenceBaseline(Script.Style);
            JointDemo.Reference(step, Script.Style, Time.realtimeSinceStartup, reference);
            var refLine = JointDemo.Target(step, refBase, tuning);
            Figure(left, reference, refLine, JointDemo.Crossed(step, refBase, tuning, reference));
            var mine = MyPose?.Invoke(); var myBase = MyBaseline != null ? MyBaseline() : default;
            if (mine != null && mine.HasPose) Figure(right, mine, JointDemo.Target(step, myBase, tuning), JointDemo.Crossed(step, myBase, tuning, mine));
            else { skin.Card(right, new Color(0, 0, 0, .35f)); GUI.color = Palette.Sand; GUI.Label(right, "카메라에 내 몸이\n보이면 여기에 나와요", skin.Centered(16, FontStyle.Normal)); }
            GUI.color = Palette.Cream;
            GUI.Label(new Rect(left.x, left.yMax + 2, w, 24), JointDemo.ReferenceLabel, skin.Centered(18, FontStyle.Bold));
            GUI.Label(new Rect(right.x, right.yMax + 2, w, 24), JointDemo.MineLabel, skin.Centered(18, FontStyle.Bold));
            GUI.color = Color.white;
        }
        private static readonly Color BoneColor = new Color(.3f, 1f, .55f), JointColor = new Color(1f, .85f, .2f), LineColor = new Color(1f, .78f, .2f), CrossedColor = new Color(.35f, 1f, .5f);
        // One stick figure in a cell, fitted to the frame's aspect, with its target line and the line's label.
        private void Figure(Rect cell, PoseFrame f, TargetLine line, bool crossed)
        {
            skin.Card(cell, new Color(0, 0, 0, .35f));
            float aspect = f.Aspect > 0 ? f.Aspect : 1;
            var view = cell.width / cell.height > aspect ? new Rect(cell.center.x - cell.height * aspect * .5f, cell.y, cell.height * aspect, cell.height)
                : new Rect(cell.x, cell.center.y - cell.width / aspect * .5f, cell.width, cell.width / aspect);
            if (Event.current.type == EventType.Repaint)
            {
                if (line.Shown)
                {
                    GUI.color = crossed ? CrossedColor : LineColor;
                    if (line.Vertical)
                    {
                        foreach (float x in new[] { line.At, line.Other }) if (x >= 0 && x <= 1) GUI.DrawTexture(new Rect(view.x + x * view.width - 1.5f, view.y, 3, view.height), Texture2D.whiteTexture);
                    }
                    else if (line.At >= 0 && line.At <= 1) GUI.DrawTexture(new Rect(view.x, view.y + line.At * view.height - 1.5f, view.width, 3), Texture2D.whiteTexture);
                }
                GUI.color = BoneColor;
                for (int b = 0; b < PoseJoint.Bones.GetLength(0); b++)
                {
                    var p = f.Points[PoseJoint.Bones[b, 0]]; var q = f.Points[PoseJoint.Bones[b, 1]];
                    if (p.Visibility < .5f || q.Visibility < .5f) continue;
                    for (int s = 0; s <= 8; s++) { float t = s / 8f; Dot(view, Mathf.Lerp(p.X, q.X, t), Mathf.Lerp(p.Y, q.Y, t), 4); }
                }
                GUI.color = JointColor;
                for (int i = 11; i < 29; i++) if (f.Points[i].Visibility >= .5f) Dot(view, f.Points[i].X, f.Points[i].Y, 7);
                if (f.Points[0].Visibility >= .5f) Dot(view, f.Points[0].X, f.Points[0].Y, 10);
            }
            if (line.Shown)
            {
                GUI.color = crossed ? CrossedColor : LineColor;
                GUI.Label(new Rect(cell.x, cell.y + 4, cell.width, 22), crossed ? "좋아요!" : line.Label, skin.Centered(15, FontStyle.Bold));
            }
            GUI.color = Color.white;
        }
        private static void Dot(Rect view, float x, float y, float size)
        {
            if (x < 0 || x > 1 || y < 0 || y > 1) return;
            GUI.DrawTexture(new Rect(view.x + x * view.width - size * .5f, view.y + y * view.height - size * .5f, size, size), Texture2D.whiteTexture);
        }
        private void OnDestroy() { skin?.Dispose(); TutorialPictures.Dispose(); }
    }
}
