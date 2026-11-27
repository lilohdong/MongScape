using System;
using System.Collections.Generic;

namespace Mongscape.Domain
{
    // What the tutorial offer asks: nothing, the chapter screen ("튜토리얼 할까요?"), or the short basics chapter for earlier members.
    public enum TutorialOffer { None, Full, Short }

    // Plan 6-1b: the tutorial is three chapters. A visit plays one chapter and goes back to the menu.
    public enum TutorialChapter { Basics, Zoo, Prison }

    // Basics (mine): camera, calibration, running in place, lane, jump, crouch, rescue, the warning (step on the first banana and see
    // the slot fill), the banana (dodge the second while the empty slot blinks), the X pause, cart fork. Zoo (a short course on the real zoo map): run (F and J on the keys), jump a fence, arms up under the bars,
    // the lion's mirror. Prison: one stair, the alarm (stand under the searchlight and see the slot fill), hiding.
    public enum TutorialStep { Camera, Calibrate, Cadence, Lane, Jump, Crouch, Rescue, Banana, WarnRule, PauseX, Fork, ZooRun, Fence, Bars, Rearview, Stair, Hide, AlarmRule, Done }

    // What a step waits for: a framed body, a calibrated body, a held pace, a lesson obstacle (or both bananas, or the stair)
    // passed cleanly, a lean at the cart fork, a station the director runs itself (rescue, beat sync, verse, hiding),
    // one real warning taken on purpose, or the pause (gesture or Esc).
    public enum TutorialPass { Framed, Calibrated, Paced, ObstaclePassed, ForkChosen, Station, Warned, Paused, None }

    // The lesson hazards of the course; every random hazard, item and coin is removed.
    public enum TutorialHazard { None, LaneBlock, JumpRow, CrouchRow, Rescue, Bananas }

    // Lessons for one chapter and play style. Steps the style lacks are left out (plan 6-3): keyboard play needs no camera
    // check, calibration, cadence or beat sync (the keys are shown instead of a demo). Members who finished the earlier
    // tutorial get a short basics chapter once: only what this update changed (calibration, rescue, banana and warning rule).
    public sealed class TutorialScript
    {
        private readonly List<TutorialStep> steps = new List<TutorialStep>(12);
        public TutorialChapter Chapter { get; }
        public PlayStyle Style { get; }
        public bool Short { get; }
        public IReadOnlyList<TutorialStep> Steps => steps;
        public int Index { get; private set; }
        public TutorialStep Current => steps[Index];
        public bool Finished => Current == TutorialStep.Done;
        // Retries of the current step and of the whole chapter.
        public int Retries { get; private set; }
        public int TotalRetries { get; private set; }
        // From this many retries of one step, the demo window grows into the joint comparison (plan 6-0).
        public const int FollowAlongRetries = 3;
        public bool FollowAlong => Retries >= FollowAlongRetries;
        public static readonly TutorialStep[] UpdateSteps = { TutorialStep.Calibrate, TutorialStep.Rescue, TutorialStep.WarnRule, TutorialStep.Banana };

        public TutorialScript(TutorialChapter chapter, PlayStyle style, bool forks = true, bool shortVersion = false)
        {
            Chapter = chapter; Style = style; Short = shortVersion && chapter == TutorialChapter.Basics;
            bool camera = PlayStyleInfo.UsesCamera(style);
            foreach (var step in StepsOf(chapter))
            {
                if (!camera && (step == TutorialStep.Camera || step == TutorialStep.Calibrate || step == TutorialStep.Cadence)) continue;
                if (step == TutorialStep.Fork && !forks) continue;
                if (Short && Array.IndexOf(UpdateSteps, step) < 0) continue;
                steps.Add(step);
            }
            steps.Add(TutorialStep.Done);
        }
        public static IReadOnlyList<TutorialStep> StepsOf(TutorialChapter chapter) => chapter == TutorialChapter.Zoo ? ZooSteps : chapter == TutorialChapter.Prison ? PrisonSteps : BasicsSteps;
        private static readonly TutorialStep[] BasicsSteps = { TutorialStep.Camera, TutorialStep.Calibrate, TutorialStep.Cadence, TutorialStep.Lane, TutorialStep.Jump, TutorialStep.Crouch,
            TutorialStep.Rescue, TutorialStep.WarnRule, TutorialStep.Banana, TutorialStep.PauseX, TutorialStep.Fork };
        private static readonly TutorialStep[] ZooSteps = { TutorialStep.ZooRun, TutorialStep.Fence, TutorialStep.Bars, TutorialStep.Rearview };
        private static readonly TutorialStep[] PrisonSteps = { TutorialStep.Stair, TutorialStep.AlarmRule, TutorialStep.Hide };
        public bool Has(TutorialStep step) => steps.Contains(step);
        // The step's pass condition was met: the next lesson starts with a fresh retry count.
        public bool Pass() { if (Finished) return false; Index++; Retries = 0; return true; }
        // A mistake restarts the same step.
        public void Fail() { if (Finished) return; Retries++; TotalRetries++; }
        public TutorialPass Condition => PassOf(Current);

        public static TutorialPass PassOf(TutorialStep step)
        {
            switch (step)
            {
                case TutorialStep.Camera: return TutorialPass.Framed;
                case TutorialStep.Calibrate: return TutorialPass.Calibrated;
                case TutorialStep.Cadence: return TutorialPass.Paced;
                case TutorialStep.Fork: return TutorialPass.ForkChosen;
                case TutorialStep.Rescue: case TutorialStep.ZooRun: case TutorialStep.Fence: case TutorialStep.Bars: case TutorialStep.Rearview: case TutorialStep.Hide: return TutorialPass.Station;
                case TutorialStep.WarnRule: case TutorialStep.AlarmRule: return TutorialPass.Warned;
                case TutorialStep.PauseX: return TutorialPass.Paused;
                case TutorialStep.Done: return TutorialPass.None;
                default: return TutorialPass.ObstaclePassed;
            }
        }
        // Lane, banana and fork steps accept either side (Left stands for both).
        public static MotionCommand? AwaitedOf(TutorialStep step) => step == TutorialStep.Lane || step == TutorialStep.Fork || step == TutorialStep.Banana ? MotionCommand.Left
            : step == TutorialStep.Jump ? MotionCommand.Jump : step == TutorialStep.Crouch ? MotionCommand.Crouch : step == TutorialStep.Stair ? MotionCommand.KneeStep : (MotionCommand?)null;
        public static bool Accepts(TutorialStep step, MotionCommand command)
        {
            if (step == TutorialStep.Lane || step == TutorialStep.Fork || step == TutorialStep.Banana) return command == MotionCommand.Left || command == MotionCommand.Right;
            var awaited = AwaitedOf(step);
            return awaited.HasValue && awaited.Value == command;
        }
        // The runner already shows the awaited action (from any input source).
        public static bool Performed(TutorialStep step, RunnerController runner)
        {
            switch (step)
            {
                case TutorialStep.Lane: return runner.Lane != TutorialCourse.BlockedLane;
                case TutorialStep.Jump: return runner.Y > .01f;
                case TutorialStep.Crouch: return runner.Crouching;
                case TutorialStep.Stair: return runner.KneeStepping;
                case TutorialStep.Fork: return runner.Lane != 0;
                default: return false;
            }
        }

        public static string ChapterTitle(TutorialChapter chapter) => chapter == TutorialChapter.Zoo ? "동물원 연습" : chapter == TutorialChapter.Prison ? "감옥 연습" : "기본";
        public static string Title(TutorialStep step)
        {
            switch (step)
            {
                case TutorialStep.Camera: return "카메라 확인"; case TutorialStep.Calibrate: return "몸 맞추기"; case TutorialStep.Cadence: return "제자리 달리기";
                case TutorialStep.Lane: return "옆으로 가기"; case TutorialStep.Jump: return "점프"; case TutorialStep.Crouch: return "숙이기";
                case TutorialStep.Rescue: return "구출"; case TutorialStep.Banana: return "바나나"; case TutorialStep.WarnRule: return "경고";
                case TutorialStep.PauseX: return "일시정지"; case TutorialStep.Fork: return "갈림길"; case TutorialStep.ZooRun: return "달리기";
                case TutorialStep.Fence: return "울타리"; case TutorialStep.Bars: return "창살"; case TutorialStep.Rearview: return "사자"; case TutorialStep.Stair: return "계단";
                case TutorialStep.Hide: return "탐조등"; case TutorialStep.AlarmRule: return "경보"; default: return "완료";
            }
        }
        // One verb line per step (plan 6-0, writing rules: no separators). The keyboard line only names the keys.
        public static string Instruction(TutorialStep step, PlayStyle style)
        {
            bool keys = style == PlayStyle.Keyboard, upper = style == PlayStyle.UpperBody;
            switch (step)
            {
                case TutorialStep.Camera: return upper ? "허리까지 보이게 앉기" : "발끝까지 보이게 서기";
                case TutorialStep.Calibrate: return "가만히 서기";
                case TutorialStep.Cadence: return upper ? "팔 번갈아 흔들기" : "제자리에서 달리기";
                case TutorialStep.Lane: return keys ? "A 또는 D 누르기" : upper ? "어깨 옆으로 기울이기" : "옆으로 크게 한 걸음";
                case TutorialStep.Jump: return keys ? "Space 누르기" : upper ? "두 팔 옆으로 쫙 펴기" : "점프하기";
                case TutorialStep.Crouch: return keys ? "S 누르고 있기" : upper ? "상체 앞으로 숙이기" : "무릎 굽혀 숙이기";
                case TutorialStep.Rescue: return keys ? "Q 또는 E 누르기" : upper ? "한 팔 옆으로 뻗기" : "벽 쪽으로 손 뻗기";
                case TutorialStep.Banana: return keys ? "A 또는 D로 피하기" : "빨간 바나나 피하기";
                case TutorialStep.PauseX: return keys ? "Esc 누르기" : "두 팔을 X로 겹치기";
                case TutorialStep.Fork: return keys ? "A 또는 D 누르기" : "몸 기울여 길 고르기";
                case TutorialStep.ZooRun: return keys ? "F와 J 번갈아 누르기" : upper ? "팔 번갈아 흔들기" : "제자리에서 달리기";
                case TutorialStep.Fence: return keys ? "Space로 넘기" : upper ? "두 팔 옆으로 쫙 펴기" : "점프해서 넘기";
                case TutorialStep.Bars: return keys ? "W로 만세" : "두 팔 위로 만세";
                case TutorialStep.Stair: return keys ? "W 번갈아 누르기" : upper ? "팔꿈치 당겨 내리기" : "제자리에서 걷듯 무릎 들기";
                case TutorialStep.Hide: return keys ? "S 누르고 버티기" : upper ? "상체 숙여 버티기" : "숙여서 버티기";
                case TutorialStep.Rearview: return "백미러로 사자 보기";
                case TutorialStep.WarnRule: return keys ? "A 또는 D로 바나나 밟기" : "바나나 밟아 보기";
                case TutorialStep.AlarmRule: return keys ? "S 떼고 서 있기" : "서서 기다리기";
                default: return "다 했어요";
            }
        }
        // Jump and crouch are timed: the call waits for the moment acting clears the obstacle (ActionTiming); before that the
        // line only prepares the player.
        public static bool Timed(TutorialStep step) => step == TutorialStep.Jump || step == TutorialStep.Crouch;
        public static string Prepare(TutorialStep step, PlayStyle style) => step == TutorialStep.Jump ? "준비하기" : step == TutorialStep.Crouch ? "준비하기" : Instruction(step, style);

        // The demo clip of the mirror monkey (plan 6-0): the 11 demo motions of art round 2 plus the X and the T of the pause and
        // upper-body jump; null when there is none (keyboard play has no demo, the camera check and calibration need none).
        public const string ClipPrefix = "AN_Monkey_Demo_";
        public static readonly string[] AllClips = { "RunInPlace", "SideStep_L", "SideStep_R", "Jump", "HalfSquat", "ArmReach_L", "ArmReach_R", "CartLean_L", "CartLean_R", "HandsUp", "HighKnee", "ArmsCrossX", "ArmsT" };
        public static string DemoClip(TutorialStep step, PlayStyle style)
        {
            if (style == PlayStyle.Keyboard) return null;
            bool upper = style == PlayStyle.UpperBody;
            string clip;
            switch (step)
            {
                case TutorialStep.Cadence: clip = "RunInPlace"; break;
                case TutorialStep.Lane: clip = upper ? "CartLean_R" : "SideStep_R"; break;
                case TutorialStep.Banana: clip = "SideStep_L"; break;
                case TutorialStep.Jump: clip = upper ? "ArmsT" : "Jump"; break;
                case TutorialStep.Crouch: clip = "HalfSquat"; break;
                case TutorialStep.Rescue: clip = "ArmReach_R"; break;
                case TutorialStep.PauseX: clip = "ArmsCrossX"; break;
                case TutorialStep.Fork: clip = "CartLean_L"; break;
                case TutorialStep.ZooRun: clip = "RunInPlace"; break;
                case TutorialStep.Fence: clip = upper ? "ArmsT" : "Jump"; break;
                case TutorialStep.Bars: clip = "HandsUp"; break;
                case TutorialStep.Stair: clip = "HighKnee"; break;
                case TutorialStep.Hide: clip = "HalfSquat"; break;
                default: return null;
            }
            return ClipPrefix + clip;
        }

        // Offered once per session to everyone who has not finished the basics chapter (guests: on this PC; members: on the account).
        public static bool ShouldOffer(bool basicsDone, bool offeredThisSession) => !offeredThisSession && !basicsDone;
        // Members who finished the earlier tutorial are offered the short basics chapter once (remembered on this PC).
        public static bool ShouldOfferUpdate(bool member, bool earlierDone, bool updateSeen, bool offeredThisSession) => !offeredThisSession && member && earlierDone && !updateSeen;
    }

    // Which chapters are done (a bit each, saved by owner: the account id, or "guest" for this PC). Chapter 2 opens after 1, 3 after 2.
    public static class TutorialProgress
    {
        public const string GuestOwner = "guest";
        public static bool IsDone(int mask, TutorialChapter chapter) => (mask & (1 << (int)chapter)) != 0;
        public static bool IsOpen(int mask, TutorialChapter chapter) => chapter == TutorialChapter.Basics || IsDone(mask, chapter - 1);
        public static int Complete(int mask, TutorialChapter chapter) => mask | (1 << (int)chapter);
        // Locked, open and done as the chapter cards show them.
        public static string State(int mask, TutorialChapter chapter) => IsDone(mask, chapter) ? "완료" : IsOpen(mask, chapter) ? "열림" : "잠김";
    }

    // The fixed tutorial maps: chapter 1 is a warm-up lap in the mine without a fork that holds the lessons, then an ordinary lap
    // whose cart fork is the last lesson; chapters 2 and 3 are short stretches of the zoo and of the prison corridor.
    // Positions are route z in metres.
    public static class TutorialCourse
    {
        public const int Seed = 723, BlockedLane = 0;
        // Mine only: start, the pace chunk (cadence holds here), lane, jump, slide, the rescue spot, bananas, the pause spot.
        public static readonly int[] BasicsLap = { 0, 0, 0, 0, 0, 0, 0, 0 };
        // Long enough for the song's verse and the look in the mirror at song speed.
        public static readonly int[] ZooLap = { 12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12 };
        // The first set of the real prison branch (PrisonCourse): the stair chunk (type 7), the cell chunk with its door and searchlight (type 4), the flat.
        public static readonly int[] PrisonLap = { 7, 4, 5, 5 };
        public const int PaceChunk = 1, LaneChunk = 2, JumpChunk = 3, CrouchChunk = 4, RescueChunk = 5, BananaChunk = 6, PauseChunk = 7;
        public const int PrisonStairChunk = 0, PrisonCellChunk = 1, RescueSide = 1;
        public const float LaneBlockZ = 12f;
        // The stations hold the runner here until they pass: end of the pace chunk (basics), the zoo and the prison after the stair.
        public static float HoldZ(TutorialChapter chapter, TutorialStep step)
        {
            if (chapter == TutorialChapter.Zoo) return 1 * RouteSurface.Length;
            if (chapter == TutorialChapter.Prison) return (PrisonCellChunk + 1) * RouteSurface.Length + 4f;
            return step == TutorialStep.Cadence ? (PaceChunk + 1) * RouteSurface.Length - 4f
                : step == TutorialStep.Rescue ? RescueChunk * RouteSurface.Length + 14f
                : (PauseChunk) * RouteSurface.Length + 10f;
        }
        // Two peels in the blocked lane of the banana chunk: step on the first (the warning), aside for the second.
        public static readonly float[] BananaZ = { 6f, 18f };
        // The prison chapter is the real branch (PrisonCourse); this tells it where the first set starts.
        public static bool PrisonChapter => Script != null && Script.Chapter == TutorialChapter.Prison;
        public static int PrisonIndex(int sequence) => PrisonChapter && sequence >= 0 && sequence < PrisonLap.Length ? sequence : -1;
        public static int ForkLapStart => BasicsLap.Length;
        public static float ForkDecisionZ => ForkLapStart * RouteSurface.Length + CartFork.Decision;
        // The zoo chapter's course (metres from its start): two fences, then two bars, so each lesson meets its own kind.
        public static readonly ZooObstacle[] ZooObstacles = { new ZooObstacle(56f, ZooNoteKind.Jump), new ZooObstacle(82f, ZooNoteKind.Jump),
            new ZooObstacle(110f, ZooNoteKind.ArmsUp), new ZooObstacle(136f, ZooNoteKind.ArmsUp) };
        public static int[] LapOf(TutorialChapter chapter) => chapter == TutorialChapter.Zoo ? ZooLap : chapter == TutorialChapter.Prison ? PrisonLap : BasicsLap;
        public static RoutePlan Plan(RunConfig config, TutorialChapter chapter = TutorialChapter.Basics) => RoutePlan.Tutorial(config, Seed, LapOf(chapter));
        // The lessons of the current script; every hazard when none is set.
        public static TutorialScript Script;
        private static bool Basics => Script == null || Script.Chapter == TutorialChapter.Basics;
        private static bool On(TutorialStep step) => Script == null || Script.Has(step);
        public static TutorialHazard HazardAt(int sequence)
        {
            if (!Basics) return TutorialHazard.None;
            return sequence == LaneChunk && On(TutorialStep.Lane) ? TutorialHazard.LaneBlock
                : sequence == JumpChunk && On(TutorialStep.Jump) ? TutorialHazard.JumpRow : sequence == CrouchChunk && On(TutorialStep.Crouch) ? TutorialHazard.CrouchRow
                : sequence == RescueChunk && On(TutorialStep.Rescue) ? TutorialHazard.Rescue : sequence == BananaChunk && On(TutorialStep.Banana) ? TutorialHazard.Bananas : TutorialHazard.None;
        }
        public static int ChunkOf(TutorialStep step) => step == TutorialStep.Cadence ? PaceChunk : step == TutorialStep.Lane ? LaneChunk
            : step == TutorialStep.Jump ? JumpChunk : step == TutorialStep.Crouch ? CrouchChunk : step == TutorialStep.Rescue ? RescueChunk
            : step == TutorialStep.Banana ? BananaChunk : step == TutorialStep.Stair ? PrisonStairChunk : -1;
        // Restart point after a mistake: past the previous lesson, well before this one.
        public static float RestartZ(TutorialStep step)
        {
            switch (step)
            {
                case TutorialStep.Cadence: return PaceChunk * RouteSurface.Length;
                case TutorialStep.Lane: return LaneChunk * RouteSurface.Length - 4f;
                case TutorialStep.Jump: return JumpChunk * RouteSurface.Length - 8f;
                case TutorialStep.Crouch: return CrouchChunk * RouteSurface.Length - 8f;
                case TutorialStep.Rescue: return RescueChunk * RouteSurface.Length;
                case TutorialStep.WarnRule: return BananaChunk * RouteSurface.Length - 6f;
                // Past the first peel (stepped on already), in front of the second.
                case TutorialStep.Banana: return BananaChunk * RouteSurface.Length + BananaZ[0] + 2f;
                // Early in the corridor before the stair, with time to read the call.
                case TutorialStep.Stair: return 2f;
                // On the cart, before the fork window.
                case TutorialStep.Fork: return (ForkLapStart + 1) * RouteSurface.Length + 2f;
                case TutorialStep.ZooRun: case TutorialStep.Fence: case TutorialStep.Bars: case TutorialStep.Rearview: return HoldZ(TutorialChapter.Zoo, step);
                case TutorialStep.Hide: return PrisonCellChunk * RouteSurface.Length + 2f;
                case TutorialStep.AlarmRule: return PrisonCellChunk * RouteSurface.Length + 2f;
                default: return 2f;
            }
        }
    }
}
