using System.Linq;
using Mongscape.Domain;
using NUnit.Framework;

namespace Mongscape.Tests
{
    public sealed class TutorialScriptTests
    {
        static TutorialStep[] Of(TutorialChapter chapter, PlayStyle style, bool forks = true, bool shortVersion = false)
            => new TutorialScript(chapter, style, forks, shortVersion).Steps.ToArray();

        [Test]
        public void ChaptersListTheirStepsAndFollowThePlayStyle()
        {
            CollectionAssert.AreEqual(new[] { TutorialStep.Camera, TutorialStep.Calibrate, TutorialStep.Cadence, TutorialStep.Lane, TutorialStep.Jump, TutorialStep.Crouch, TutorialStep.Rescue,
                TutorialStep.Banana, TutorialStep.WarnRule, TutorialStep.PauseX, TutorialStep.Fork, TutorialStep.Done }, Of(TutorialChapter.Basics, PlayStyle.FullBody));
            CollectionAssert.AreEqual(Of(TutorialChapter.Basics, PlayStyle.FullBody), Of(TutorialChapter.Basics, PlayStyle.UpperBody));
            CollectionAssert.AreEqual(new[] { TutorialStep.Lane, TutorialStep.Jump, TutorialStep.Crouch, TutorialStep.Rescue, TutorialStep.Banana, TutorialStep.WarnRule, TutorialStep.PauseX, TutorialStep.Fork, TutorialStep.Done },
                Of(TutorialChapter.Basics, PlayStyle.Keyboard), "Keyboard play skips the camera check, calibration and cadence; Esc is the pause");
            CollectionAssert.DoesNotContain(Of(TutorialChapter.Basics, PlayStyle.Keyboard, false), TutorialStep.Fork, "A route without forks has no lean lesson");
            CollectionAssert.AreEqual(new[] { TutorialStep.Sync, TutorialStep.Verse, TutorialStep.Rearview, TutorialStep.Done }, Of(TutorialChapter.Zoo, PlayStyle.FullBody));
            CollectionAssert.AreEqual(new[] { TutorialStep.Verse, TutorialStep.Rearview, TutorialStep.Done }, Of(TutorialChapter.Zoo, PlayStyle.Keyboard), "No sync adjustment on the keyboard");
            CollectionAssert.AreEqual(new[] { TutorialStep.Stair, TutorialStep.Hide, TutorialStep.AlarmRule, TutorialStep.Done }, Of(TutorialChapter.Prison, PlayStyle.UpperBody));
            CollectionAssert.AreEqual(new[] { TutorialStep.Calibrate, TutorialStep.Rescue, TutorialStep.Banana, TutorialStep.WarnRule, TutorialStep.Done },
                Of(TutorialChapter.Basics, PlayStyle.FullBody, true, true), "The short version for members: calibration, rescue, banana and the warning rule");
            CollectionAssert.AreEqual(new[] { TutorialStep.Rescue, TutorialStep.Banana, TutorialStep.WarnRule, TutorialStep.Done }, Of(TutorialChapter.Basics, PlayStyle.Keyboard, true, true));
            CollectionAssert.AreEqual(Of(TutorialChapter.Zoo, PlayStyle.FullBody), Of(TutorialChapter.Zoo, PlayStyle.FullBody, true, true), "Only the basics have a short version");
        }

        [Test]
        public void EveryStepHasAOneLineVerbWithoutSeparators([Values] PlayStyle style)
        {
            foreach (TutorialChapter chapter in new[] { TutorialChapter.Basics, TutorialChapter.Zoo, TutorialChapter.Prison })
                foreach (var step in new TutorialScript(chapter, style).Steps)
                {
                    string title = TutorialScript.Title(step), line = TutorialScript.Instruction(step, style);
                    Assert.IsNotEmpty(title); Assert.IsNotEmpty(line);
                    Assert.IsFalse(line.Contains("\n") || line.Contains("·") || title.Contains("·"), step + ": one line, no middle dot");
                }
            Assert.IsFalse(TutorialScript.ChapterTitle(TutorialChapter.Zoo).Contains("·"));
        }

        [Test]
        public void TheMirrorMonkeyUsesTheElevenDemoClipsAndTheXAndTheT()
        {
            Assert.AreEqual(13, TutorialScript.AllClips.Length);
            Assert.IsNull(TutorialScript.DemoClip(TutorialStep.Jump, PlayStyle.Keyboard), "Keyboard play has no demo");
            Assert.IsNull(TutorialScript.DemoClip(TutorialStep.Camera, PlayStyle.FullBody)); Assert.IsNull(TutorialScript.DemoClip(TutorialStep.Calibrate, PlayStyle.FullBody));
            Assert.AreEqual("AN_Monkey_Demo_Jump", TutorialScript.DemoClip(TutorialStep.Jump, PlayStyle.FullBody));
            Assert.AreEqual("AN_Monkey_Demo_ArmsT", TutorialScript.DemoClip(TutorialStep.Jump, PlayStyle.UpperBody), "The upper body jumps with the T pose");
            Assert.AreEqual("AN_Monkey_Demo_ArmsCrossX", TutorialScript.DemoClip(TutorialStep.PauseX, PlayStyle.FullBody));
            var used = new System.Collections.Generic.HashSet<string>();
            foreach (PlayStyle style in new[] { PlayStyle.FullBody, PlayStyle.UpperBody })
                foreach (TutorialStep step in System.Enum.GetValues(typeof(TutorialStep))) { var clip = TutorialScript.DemoClip(step, style); if (clip != null) used.Add(clip.Substring(TutorialScript.ClipPrefix.Length)); }
            foreach (string clip in TutorialScript.AllClips.Where(c => !c.StartsWith("CartLean_") && c != "SideStep_R" && c != "ArmReach_L")) CollectionAssert.Contains(used, clip, clip + " is shown somewhere");
            var script = new TutorialScript(TutorialChapter.Basics, PlayStyle.FullBody);
            script.Fail(); script.Fail(); Assert.IsFalse(script.FollowAlong); script.Fail(); Assert.IsTrue(script.FollowAlong, "Three failures of one step open the joint comparison");
            script.Pass(); Assert.IsFalse(script.FollowAlong, "A new step starts small again");
        }

        [Test]
        public void EachStepAwaitsItsCommandAndPassCondition()
        {
            Assert.AreEqual(TutorialPass.Calibrated, TutorialScript.PassOf(TutorialStep.Calibrate));
            Assert.AreEqual(TutorialPass.ObstaclePassed, TutorialScript.PassOf(TutorialStep.Jump)); Assert.AreEqual(MotionCommand.Jump, TutorialScript.AwaitedOf(TutorialStep.Jump));
            Assert.AreEqual(MotionCommand.KneeStep, TutorialScript.AwaitedOf(TutorialStep.Stair));
            Assert.AreEqual(TutorialPass.ForkChosen, TutorialScript.PassOf(TutorialStep.Fork));
            foreach (var step in new[] { TutorialStep.Rescue, TutorialStep.Sync, TutorialStep.Verse, TutorialStep.Hide }) Assert.AreEqual(TutorialPass.Station, TutorialScript.PassOf(step));
            foreach (var step in new[] { TutorialStep.WarnRule, TutorialStep.Rearview, TutorialStep.AlarmRule }) Assert.AreEqual(TutorialPass.Picture, TutorialScript.PassOf(step));
            Assert.AreEqual(TutorialPass.Paused, TutorialScript.PassOf(TutorialStep.PauseX));
            Assert.IsTrue(TutorialScript.Accepts(TutorialStep.Lane, MotionCommand.Right)); Assert.IsTrue(TutorialScript.Accepts(TutorialStep.Fork, MotionCommand.Left));
            Assert.IsFalse(TutorialScript.Accepts(TutorialStep.Jump, MotionCommand.Crouch)); Assert.IsFalse(TutorialScript.Accepts(TutorialStep.Done, MotionCommand.Jump));
        }

        [Test]
        public void StepsAdvanceInOrderUntilDoneAndFailureRetriesTheSameStep()
        {
            var script = new TutorialScript(TutorialChapter.Basics, PlayStyle.Keyboard);
            Assert.AreEqual(TutorialStep.Lane, script.Current);
            Assert.IsTrue(script.Pass()); Assert.AreEqual(TutorialStep.Jump, script.Current);
            script.Fail(); script.Fail();
            Assert.AreEqual(TutorialStep.Jump, script.Current); Assert.AreEqual(2, script.Retries); Assert.AreEqual(2, script.TotalRetries);
            script.Pass(); Assert.AreEqual(TutorialStep.Crouch, script.Current); Assert.AreEqual(0, script.Retries); Assert.AreEqual(2, script.TotalRetries);
            while (!script.Finished) script.Pass();
            Assert.AreEqual(TutorialStep.Done, script.Current); Assert.IsFalse(script.Pass(), "Done is final");
        }

        [Test]
        public void TutorialSessionNeverEndsInACapture()
        {
            var run = new RunSession(new RunConfig()); run.Flow.Transition(GameFlowState.TitleIdle); run.Flow.Transition(GameFlowState.LoadingIntro); run.Flow.Transition(GameFlowState.Running);
            run.IsTutorial = true;
            Assert.IsTrue(run.Hit(true)); Assert.IsTrue(run.Fail(CaptureReason.BrokenRail));
            Assert.AreEqual(GameFlowState.Running, run.Flow.State); Assert.AreEqual(2, run.TutorialMisses); Assert.AreEqual(0, run.Chase.Pressure);
            run.SpeedScale = 0; float z = run.Runner.Z; run.Tick(.5f); Assert.AreEqual(z, run.Runner.Z, "A held tutorial runner does not move");
            Assert.IsTrue(run.Slip(SlipSource.Banana)); Assert.AreEqual(3, run.TutorialMisses, "A banana in the tutorial is a counted miss"); Assert.AreEqual(0, run.SlipStrikes, "never a strike");
            run.IsTutorial = false; Assert.AreEqual(0, run.TutorialMisses);
            Assert.IsTrue(run.Hit(true)); Assert.AreEqual(1, run.Warnings, "Outside the tutorial a hit is a warning");
        }

        [Test]
        public void BasicsCourseHasAWarmupLapThenTheFork()
        {
            var plan = TutorialCourse.Plan(new RunConfig());
            for (int i = 0; i < TutorialCourse.BasicsLap.Length; i++) Assert.AreEqual(TutorialCourse.BasicsLap[i], plan.TypeAt(i));
            for (int i = 0; i < TutorialCourse.ForkLapStart; i++) Assert.IsTrue(RouteSurface.IsMine(plan.TypeAt(i)), "The warm-up lap stays in the mine");
            Assert.AreEqual(1, plan.TypeAt(TutorialCourse.ForkLapStart + 1), "The cart follows the warm-up lap");
            Assert.AreEqual(TutorialCourse.ForkLapStart - 1, TutorialCourse.PauseChunk, "The pause spot closes the warm-up");
            Assert.AreEqual(TutorialHazard.LaneBlock, TutorialCourse.HazardAt(TutorialCourse.LaneChunk));
            Assert.AreEqual(TutorialHazard.JumpRow, TutorialCourse.HazardAt(TutorialCourse.JumpChunk)); Assert.AreEqual(TutorialHazard.CrouchRow, TutorialCourse.HazardAt(TutorialCourse.CrouchChunk));
            Assert.AreEqual(TutorialHazard.Bananas, TutorialCourse.HazardAt(TutorialCourse.BananaChunk)); Assert.AreEqual(TutorialHazard.Rescue, TutorialCourse.HazardAt(TutorialCourse.RescueChunk));
            Assert.Less(TutorialCourse.HoldZ(TutorialChapter.Basics, TutorialStep.Cadence), TutorialCourse.LaneChunk * RouteSurface.Length, "The pace lesson holds before the lane lesson");
            Assert.Less(TutorialCourse.RestartZ(TutorialStep.Jump), TutorialCourse.JumpChunk * RouteSurface.Length + 7, "Restart is in front of the step's obstacle");
            Assert.Greater(TutorialCourse.RestartZ(TutorialStep.Jump), TutorialCourse.LaneChunk * RouteSurface.Length + TutorialCourse.LaneBlockZ, "and past the previous one");
            Assert.Greater(TutorialCourse.RestartZ(TutorialStep.Fork), TutorialCourse.ForkLapStart * RouteSurface.Length, "A fork retry is on the cart");
            Assert.Less(TutorialCourse.RestartZ(TutorialStep.Fork), TutorialCourse.ForkLapStart * RouteSurface.Length + CartFork.Start, "and before the fork window");
            Assert.IsTrue(plan.ForkWindow(TutorialCourse.ForkLapStart * RouteSurface.Length + 110)); Assert.IsFalse(plan.ForkWindow(110), "The warm-up lap has no fork");
            plan.UpdateChoice(TutorialCourse.ForkDecisionZ, -1); Assert.IsTrue(plan.ForkLocked(TutorialCourse.ForkDecisionZ));
        }

        [Test]
        public void ZooAndPrisonCoursesStayInTheirMap()
        {
            var zoo = TutorialCourse.Plan(new RunConfig(), TutorialChapter.Zoo);
            TutorialCourse.Script = new TutorialScript(TutorialChapter.Zoo, PlayStyle.FullBody);
            try { for (int i = 0; i < TutorialCourse.ZooLap.Length; i++) { Assert.IsTrue(RouteSurface.IsZoo(zoo.TypeAt(i))); Assert.AreEqual(TutorialHazard.None, TutorialCourse.HazardAt(i), "No lesson obstacles in the zoo"); } }
            finally { TutorialCourse.Script = null; }
            var prison = TutorialCourse.Plan(new RunConfig(), TutorialChapter.Prison);
            Assert.AreEqual(7, prison.TypeAt(TutorialCourse.PrisonStairChunk), "The stair lesson is the prison stair chunk");
            Assert.IsTrue(RouteSurface.IsStair(prison.TypeAt(TutorialCourse.PrisonStairChunk)));
            Assert.AreEqual(TutorialCourse.PrisonStairChunk, TutorialCourse.ChunkOf(TutorialStep.Stair));
            Assert.Greater(TutorialCourse.HoldZ(TutorialChapter.Prison, TutorialStep.Hide), (TutorialCourse.PrisonStairChunk + 1) * RouteSurface.Length, "The searchlight waits after the stair");
        }

        [Test]
        public void ChaptersOpenOneByOneAndAreSavedByOwner()
        {
            Assert.IsTrue(TutorialProgress.IsOpen(0, TutorialChapter.Basics)); Assert.IsFalse(TutorialProgress.IsOpen(0, TutorialChapter.Zoo)); Assert.IsFalse(TutorialProgress.IsOpen(0, TutorialChapter.Prison));
            int mask = TutorialProgress.Complete(0, TutorialChapter.Basics);
            Assert.IsTrue(TutorialProgress.IsOpen(mask, TutorialChapter.Zoo)); Assert.IsFalse(TutorialProgress.IsOpen(mask, TutorialChapter.Prison), "Chapter 3 needs chapter 2");
            mask = TutorialProgress.Complete(mask, TutorialChapter.Zoo);
            Assert.IsTrue(TutorialProgress.IsOpen(mask, TutorialChapter.Prison)); Assert.IsTrue(TutorialProgress.IsDone(mask, TutorialChapter.Basics), "A done chapter stays open for another go");
            Assert.AreEqual("잠김", TutorialProgress.State(0, TutorialChapter.Zoo)); Assert.AreEqual("열림", TutorialProgress.State(0, TutorialChapter.Basics)); Assert.AreEqual("완료", TutorialProgress.State(mask, TutorialChapter.Zoo));
            string root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "mongscape-tutmarks-" + System.Guid.NewGuid().ToString("N"));
            try
            {
                var store = new Mongscape.Infrastructure.PlayerProgressStore(root);
                Assert.AreEqual(0, store.TutorialChapters("guest")); store.MarkTutorial("guest", TutorialChapter.Basics); store.MarkTutorial("m1", TutorialChapter.Basics); store.MarkTutorial("m1", TutorialChapter.Zoo);
                Assert.AreEqual(1, store.TutorialChapters("guest")); Assert.AreEqual(3, store.TutorialChapters("m1"), "Each owner has their own chapters");
                Assert.AreEqual(3, new Mongscape.Infrastructure.PlayerProgressStore(root).TutorialChapters("m1"), "and they survive a restart");
            }
            finally { if (System.IO.Directory.Exists(root)) System.IO.Directory.Delete(root, true); }
        }

        [Test]
        public void OfferedOnceToEveryoneWhoHasNotFinishedTheBasics()
        {
            Assert.IsTrue(TutorialScript.ShouldOffer(false, false)); Assert.IsFalse(TutorialScript.ShouldOffer(true, false), "Finished basics are not offered again");
            Assert.IsFalse(TutorialScript.ShouldOffer(false, true), "Once per session");
            Assert.IsTrue(TutorialScript.ShouldOfferUpdate(true, true, false, false)); Assert.IsFalse(TutorialScript.ShouldOfferUpdate(true, true, true, false));
            Assert.IsFalse(TutorialScript.ShouldOfferUpdate(false, false, false, false), "Guests get the full chapter screen instead");
        }

        static PoseFrame Frame(float leftWristX, float rightWristX, float wristY)
        {
            var f = new PoseFrame { HasPose = true, Aspect = 1 };
            void Set(int joint, float x, float y) => f.Points[joint] = new PoseLandmark { X = x, Y = y, Visibility = 1 };
            Set(PoseJoint.Nose, .5f, .17f); Set(PoseJoint.LeftShoulder, .43f, .28f); Set(PoseJoint.RightShoulder, .57f, .28f);
            Set(PoseJoint.LeftWrist, leftWristX, wristY); Set(PoseJoint.RightWrist, rightWristX, wristY);
            return f;
        }

        [Test]
        public void ArmPoseReadsTheHandsUp()
        {
            Assert.IsTrue(ArmPose.HandsUp(Frame(.4f, .6f, .05f))); Assert.IsFalse(ArmPose.HandsUp(Frame(.4f, .6f, .5f)));
            Assert.IsFalse(ArmPose.HandsUp(Frame(.2f, .8f, .3f)), "The T pose has the hands level with the shoulders");
        }

        [Test]
        public void BeatSyncMeasuresTheMedianDelayOfFourJumps()
        {
            var sync = new BeatSync(); sync.Begin(10);
            Assert.AreEqual(-1, sync.BeatAt(10)); Assert.AreEqual(0, sync.BeatAt(sync.BeatTime(0) + .1));
            Assert.IsFalse(sync.Jump(sync.BeatTime(0) - .6), "A jump far from every beat does not count");
            Assert.IsTrue(sync.Jump(sync.BeatTime(0) + .10)); Assert.IsTrue(sync.Jump(sync.BeatTime(1) + .14)); Assert.IsTrue(sync.Jump(sync.BeatTime(2) + .06)); Assert.IsFalse(sync.Done);
            Assert.IsFalse(sync.Jump(sync.BeatTime(2) + .07), "A beat counts once");
            Assert.IsTrue(sync.Jump(sync.BeatTime(3) + .12)); Assert.IsTrue(sync.Done);
            Assert.AreEqual(.11, sync.Offset, 1e-6, "Median of .06 .10 .12 .14");
            var late = new BeatSync(); late.Begin(0); Assert.IsFalse(late.Over(late.BeatTime(3))); Assert.IsTrue(late.Over(late.BeatTime(3) + BeatSync.Window + .01));
            Assert.AreEqual(0, late.Offset);
        }

        [Test]
        public void TheVerseNeedsFiveOfEightNotesAndJudgesWithTheOffset()
        {
            var verse = new BeatVerse(); verse.Begin(0, .1);
            Assert.AreEqual(8, verse.Count); Assert.IsFalse(verse.Passed);
            Assert.IsFalse(verse.Act(NoteKind.Hands, verse.NoteTime(0) + .1), "The first note is a jump");
            Assert.IsTrue(verse.Act(NoteKind.Jump, verse.NoteTime(0) + .1), "A jump .1 s late lands on the note because the offset is .1");
            Assert.IsFalse(verse.Act(NoteKind.Jump, verse.NoteTime(0) + .1), "A note is hit once");
            Assert.IsTrue(verse.Act(NoteKind.Jump, verse.NoteTime(1) + .1)); Assert.IsTrue(verse.Act(NoteKind.Hands, verse.NoteTime(2) + .1));
            Assert.IsTrue(verse.Act(NoteKind.Jump, verse.NoteTime(3) + .1)); Assert.IsFalse(verse.Passed); Assert.IsTrue(verse.Act(NoteKind.Hands, verse.NoteTime(4) + .1)); Assert.IsTrue(verse.Passed);
            Assert.IsFalse(verse.Over(verse.NoteTime(7))); Assert.IsTrue(verse.Over(verse.NoteTime(7) + BeatVerse.Window + .01));
        }


        [Test]
        public void ThePrisonChapterRunsTheRealFirstSetOfThePrisonBranch()
        {
            Assert.AreEqual(-1, PrisonCourse.BranchIndex(null, 0), "Outside the prison chapter the plan decides");
            TutorialCourse.Script = new TutorialScript(TutorialChapter.Prison, PlayStyle.Keyboard);
            try
            {
                Assert.AreEqual(0, PrisonCourse.BranchStart(null, 2)); Assert.AreEqual(1, PrisonCourse.BranchIndex(null, 1)); Assert.AreEqual(-1, PrisonCourse.BranchIndex(null, TutorialCourse.PrisonLap.Length));
                for (int i = 0; i < 3; i++) Assert.AreEqual(PrisonCourse.Chunks[i], TutorialCourse.PrisonLap[i], "Same chunks as set one of the branch");
                Assert.AreEqual(PrisonPhase.Hold, PrisonCourse.PhaseOf(TutorialCourse.PrisonCellChunk, PrisonCourse.DoorZ, out _));
            }
            finally { TutorialCourse.Script = null; }
        }

        [Test]
        public void TheTutorialWallMonkeyStandsInTheRescueChunk()
        {
            Assert.AreEqual(TutorialHazard.Rescue, TutorialCourse.HazardAt(TutorialCourse.RescueChunk));
            Assert.AreEqual(TutorialCourse.RescueChunk, TutorialCourse.ChunkOf(TutorialStep.Rescue));
            Assert.Greater(MineRescuePlan.RouteZ(TutorialCourse.RescueChunk), TutorialCourse.HoldZ(TutorialChapter.Basics, TutorialStep.Rescue), "The runner waits in front of the monkey");
            var judge = new MineRescueJudge(); judge.Reach(1); Assert.IsTrue(judge.Reaching(1)); judge.Advance(.5f); Assert.IsFalse(judge.Reaching(1), "A reach expires once the clock runs");
        }
    }
}
