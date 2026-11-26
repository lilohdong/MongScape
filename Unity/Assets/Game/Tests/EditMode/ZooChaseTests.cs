using System.Collections.Generic;
using System.IO;
using Mongscape.Domain;
using NUnit.Framework;
using UnityEngine;

namespace Mongscape.Tests
{
    public sealed class ZooChaseTests
    {
        private static readonly string[] ChartNames = { "easy_a", "easy_b", "normal_a", "normal_b", "hard_a", "hard_b" };
        private static ZooBeatmap Chart(string name) =>
            ZooBeatmap.Parse(File.ReadAllText(Path.Combine(UnityEngine.Application.dataPath, "Game/Content/Audio/Music/Zoo/zoo_chase_v1." + name + ".beatmap.json")));
        // A fence at 40 m and a set of bars at 60 m.
        private static List<ZooObstacle> Two() => new List<ZooObstacle> { new ZooObstacle(40f, ZooNoteKind.Jump), new ZooObstacle(60f, ZooNoteKind.ArmsUp) };
        private static ZooChase Started(List<ZooObstacle> course = null) { var c = new ZooChase(); c.Start(course ?? Two(), 100f); return c; }
        // One 0.02 s frame at the zoo base speed times the pace (null: no cadence, the base speed).
        private static void Step(ZooChase c, float? pace) => c.Tick(.02f, c.Distance + c.BaseSpeed * (pace ?? 1f) * .02f, pace);
        private static void Run(ZooChase c, float seconds, float? pace = null) { for (float t = 0; t < seconds - 1e-4f; t += .02f) Step(c, pace); }
        private static void RunTo(ZooChase c, float distance, float? pace = null) { for (int i = 0; i < 100000 && c.Active && c.Distance < distance; i++) Step(c, pace); }
        private static RoutePlan ZooPlan(RunConfig config)
        {
            var plan = new RoutePlan(config, 5);
            plan.SetBranchLayout(SectionKind.Zoo, lap => ZooChase.Branch());
            int lane = 0; for (int l = -1; l <= 1; l++) if (SectionDef.KindOfDestination(plan.DestinationFor(CartFork.FirstChunk, l)) == SectionKind.Zoo) lane = l;
            plan.UpdateChoice(CartFork.Decision + 1, lane);
            return plan;
        }

        [Test] public void AllSixChartsParseWithTheirTierAndNotes()
        {
            foreach (var name in ChartNames)
            {
                var map = Chart(name);
                Assert.AreEqual(160f, map.Bpm, .01f, name);
                Assert.AreEqual(name.StartsWith("easy") ? ZooTier.Easy : name.StartsWith("hard") ? ZooTier.Hard : ZooTier.Normal, map.Tier, name);
                Assert.AreEqual(name.StartsWith("easy") ? 25 : name.StartsWith("hard") ? 47 : 36, map.Notes.Count, name);
                foreach (var note in map.Notes)
                {
                    Assert.AreEqual(note.Beat * .375f, note.Time, .001f);
                    Assert.IsFalse(note.Time < 3f || note.Time >= map.BreakStart && note.Time < map.BreakEnd || note.Time >= 57f, name + " note in intro, break or exit");
                }
            }
        }
        [Test] public void VisitPicksTheTierAndAChartInsideIt()
        {
            var charts = new List<ZooBeatmap>(); foreach (var name in ChartNames) charts.Add(Chart(name));
            Assert.AreEqual(ZooTier.Easy, ZooBeatmap.TierForVisit(1)); Assert.AreEqual(ZooTier.Normal, ZooBeatmap.TierForVisit(2));
            Assert.AreEqual(ZooTier.Hard, ZooBeatmap.TierForVisit(3)); Assert.AreEqual(ZooTier.Hard, ZooBeatmap.TierForVisit(7));
            var seen = new HashSet<string>(); var random = new System.Random(4);
            for (int i = 0; i < 40; i++) { var pick = ZooBeatmap.Pick(charts, 2, random); Assert.AreEqual(ZooTier.Normal, pick.Tier); seen.Add(pick.Chart); }
            Assert.AreEqual(2, seen.Count, "Both normal charts come up");
            Assert.IsNull(ZooBeatmap.Pick(new List<ZooBeatmap>(), 1, random));
        }
        [Test] public void TheRoarWarningAddsTheWholeCueTime()
        {
            var plain = new RunSession(new RunConfig());
            Assert.AreEqual(1.5f + RunSession.CueSeconds, ZooChase.RoarLeadFor(plain.WarningCueSeconds), 1e-4f);
            Assert.AreEqual(2.7f, ZooChase.RoarLeadFor(RunSession.CueSeconds + .6f), 1e-4f);
        }
        [Test] public void TheLionBreaksOutRunsRoarsAndPounces()
        {
            var c = Started(new List<ZooObstacle>());
            Assert.AreEqual(LionAction.CageBreakout, c.ActionAt(.9f));
            Run(c, 2.1f); Assert.AreEqual(LionAction.Run, c.ActionAt(.9f));
            for (int i = 0; i < 4000 && !c.Roaring; i++) Step(c, CadencePace.Walk);
            Assert.AreEqual(LionAction.Roar, c.ActionAt(.9f));
            for (int i = 0; i < 400 && c.Bites == 0; i++) Step(c, CadencePace.Walk);
            Assert.AreEqual(LionAction.Pounce, c.ActionAt(.9f));
            Run(c, 1f, CadencePace.Walk); Assert.AreEqual(LionAction.Run, c.ActionAt(.9f));
        }
        [Test] public void ThePracticeCourseHasBothKindsAndTheLionNeverRoars()
        {
            var course = ZooChase.Layout(ZooPractice.Length, 1, new System.Random(3), false);
            Assert.Greater(course.Count, 3);
            Assert.IsTrue(course.Exists(o => o.Kind == ZooNoteKind.Jump) && course.Exists(o => o.Kind == ZooNoteKind.ArmsUp), "Both kinds");
            Assert.AreEqual(5, ZooPractice.PassHits(8));
            var c = new ZooChase { Practice = true }; c.Start(course, 0f, ZooPractice.Length, false);
            Run(c, 40f, CadencePace.Walk);
            Assert.AreEqual(course.Count, c.Hits + c.Misses);
            Assert.AreEqual(ZooPractice.LionCap, c.Lion, 1e-4f); Assert.IsFalse(c.Roaring); Assert.AreEqual(0, c.Bites);
        }
        [Test] public void NothingIsJudgedDuringTheCutAndTheShout()
        {
            var c = Started(); Assert.AreEqual(ZooPhase.Cut, c.Phase);
            Assert.AreEqual(-1, c.Input(ZooNoteKind.Jump));
            Run(c, 2.1f, CadencePace.Walk); Assert.AreEqual(ZooPhase.Shout, c.Phase); Assert.AreEqual(0f, c.Lion);
            Run(c, 1f, CadencePace.Walk); Assert.AreEqual(ZooPhase.Song, c.Phase);
            Assert.AreEqual(0f, c.Lion, .001f, "One second of grace after the cut");
            Run(c, 1f, CadencePace.Walk); Assert.Greater(c.Lion, 0f, "Walking brings the lion");
        }
        [Test] public void ObstaclesPassByDistanceGenerously()
        {
            var c = Started();
            RunTo(c, 40f - ZooChase.Ahead + .5f);
            Assert.AreEqual(-1, c.Input(ZooNoteKind.ArmsUp), "Wrong kind");
            Assert.AreEqual(0, c.Input(ZooNoteKind.Jump), "Several metres early still passes the fence"); Assert.AreEqual(1, c.Hits);
            RunTo(c, 70f);
            Assert.AreEqual(1, c.Misses, "The arms-up was never made");
            Assert.AreEqual(ZooChase.ArmsUpMiss, c.Lion, .001f);
        }
        [Test] public void AMissedJumpBringsTheLionMostAndAPassPushesItBack()
        {
            var c = Started(); RunTo(c, 45f);
            Assert.AreEqual(1, c.Misses); Assert.AreEqual(ZooChase.JumpMiss, c.Lion, .001f);
            RunTo(c, 59f); c.Input(ZooNoteKind.ArmsUp);
            Assert.AreEqual(ZooChase.JumpMiss - ZooChase.HitRelief, c.Lion, .001f);
        }
        [Test] public void RunningFasterThanTheLionOpensTheGapAndWalkingClosesIt()
        {
            var c = Started(new List<ZooObstacle>()); c.LionPace = ZooChase.LionPaceFor(1);
            Run(c, 4f); Run(c, 5f, CadencePace.Walk);
            float near = c.Lion; Assert.Greater(near, 0f);
            Run(c, 3f, CadencePace.Run); Assert.Less(c.Lion, near, "Running at full pace beats a first-visit lion");
            Assert.Greater(ZooChase.LionPaceFor(3), ZooChase.LionPaceFor(1), "The lion gets a little faster each visit");
            Assert.LessOrEqual(ZooChase.LionPaceFor(9), CadencePace.Run, "Running always keeps up");
        }
        [Test] public void TheGateStretchNeedsASprint()
        {
            var c = Started(new List<ZooObstacle>()); c.LionPace = ZooChase.LionPaceFor(3);
            Run(c, 4f);
            RunTo(c, c.GateStart + 1f, CadencePace.Run);
            float before = c.Lion; Assert.IsTrue(c.InGate);
            Run(c, 2f, CadencePace.Run); Assert.Greater(c.Lion, before, "The lion sprints through the gate stretch");
            float mid = c.Lion; Run(c, 2f, CadencePace.Sprint); Assert.Less(c.Lion, mid, "A sprint outruns it");
            var course = ZooChase.Layout(ZooChase.CourseLength, 2, new System.Random(1));
            foreach (var o in course) Assert.IsFalse(o.Distance > c.GateStart - 1f && o.Distance < c.GateEnd + 1f, "No fence or bar in the gate stretch");
            foreach (var o in course) Assert.IsTrue(o.Distance >= ZooChase.StartClear && o.Distance <= ZooChase.CourseLength - ZooChase.EndClear);
        }
        [Test] public void TheLionRoarsBeforeItBites()
        {
            var c = Started(new List<ZooObstacle>()); int roars = 0, bites = 0; c.RoarStarted += () => roars++; c.Bit += () => bites++;
            Run(c, 3f);
            while (!c.Roaring) Step(c, CadencePace.Walk);
            Run(c, 1.4f, CadencePace.Walk); Assert.AreEqual(0, c.Bites, "No bite before the roar is over");
            Run(c, .2f, CadencePace.Walk); Assert.AreEqual(1, roars); Assert.AreEqual(1, bites); Assert.AreEqual(ZooChase.AfterBite, c.Lion, .03f);
            Assert.IsTrue(c.MirrorVisible);
        }
        [Test] public void SpeedingUpDuringTheRoarCancelsTheBite()
        {
            var c = Started(new List<ZooObstacle>()); Run(c, 4f);
            while (!c.Roaring) Step(c, CadencePace.Walk);
            Run(c, 5f, CadencePace.Sprint);
            Assert.AreEqual(0, c.Bites); Assert.IsFalse(c.Roaring);
        }
        [Test] public void TheLionStaysOutOfTheMirrorWhileFar()
        {
            var c = Started(new List<ZooObstacle>()); Run(c, 4f, CadencePace.Run);
            Assert.IsFalse(c.MirrorVisible);
            while (c.Lion < ZooChase.MirrorAt) Step(c, CadencePace.Walk);
            Assert.IsTrue(c.MirrorVisible);
        }
        [Test] public void WithoutACadenceOnlyMissesBringTheLion()
        {
            var c = Started(new List<ZooObstacle>()); Run(c, 20f, null);
            Assert.AreEqual(0f, c.Lion);
        }
        [Test] public void TheCourseEndEndsTheChase()
        {
            var c = Started(new List<ZooObstacle>()); RunTo(c, ZooChase.CourseLength + 1f);
            Assert.AreEqual(ZooPhase.Done, c.Phase); Assert.IsFalse(c.MirrorVisible);
        }
        [Test] public void TheZooBranchFitsTheCourseAndHasOneLaneWithoutStairs()
        {
            var config = new RunConfig { ExtendedRoutes = true };
            var plan = ZooPlan(config);
            int zooChunks = 0; for (int i = CartFork.DestinationChunk; i < plan.LapChunks(0); i++) if (plan.TypeAt(i) == 12) zooChunks++;
            Assert.GreaterOrEqual(zooChunks * RouteSurface.Length, ZooChase.CourseLength);
            Assert.AreEqual(3, plan.TypeAt(plan.LapChunks(0) - 1));
            for (int i = 0; i < plan.LapChunks(0); i++) Assert.IsFalse(RouteSurface.IsStair(plan.TypeAt(i)));
            RouteSurface.UsePlan(plan);
            try
            {
                var runner = new RunnerController(config);
                runner.SetStartZ((CartFork.DestinationChunk + 2) * RouteSurface.Length);
                Assert.IsTrue(runner.OneLane);
                runner.Command(MotionCommand.Left); runner.Command(MotionCommand.ArmsUp); runner.Command(MotionCommand.Jump);
                Assert.AreEqual(0, runner.Lane); Assert.AreEqual(1, runner.ArmsUpCommands); Assert.AreEqual(1, runner.JumpCommands);
            }
            finally { RouteSurface.UsePlan(null); }
        }
        [Test] public void TheZooSpeedIsTheBaseSpeedTimesTheRunningPace()
        {
            var config = new RunConfig { ExtendedRoutes = true };
            RouteSurface.UsePlan(ZooPlan(config));
            try
            {
                var run = new RunSession(config);
                run.Flow.Transition(GameFlowState.TitleIdle); run.Flow.Transition(GameFlowState.IntroTimeline);
                run.Flow.Transition(GameFlowState.FirstPersonTakeover); run.Flow.Transition(GameFlowState.Running);
                run.Runner.SetStartZ(CartFork.DestinationChunk * RouteSurface.Length + 1); run.Tick(.01f);
                Assert.AreEqual(SectionKind.Zoo, run.Section.Kind);
                run.SectionSpeed = 9f; run.CadenceScale = .7f;
                for (int i = 0; i < 200; i++) run.Tick(.02f);
                Assert.AreEqual(9f * .7f, run.CurrentSpeed, .05f);
                run.CadenceScale = 1.3f;
                for (int i = 0; i < 200; i++) run.Tick(.02f);
                Assert.AreEqual(9f * 1.3f, run.CurrentSpeed, .05f);
            }
            finally { RouteSurface.UsePlan(null); }
        }
        [Test] public void FootSpeedNeverPassesTheCap()
        {
            var run = new RunSession(new RunConfig());
            run.Flow.Transition(GameFlowState.TitleIdle); run.Flow.Transition(GameFlowState.IntroTimeline);
            run.Flow.Transition(GameFlowState.FirstPersonTakeover); run.Flow.Transition(GameFlowState.Running);
            run.Score.Advance(5000f); run.CadenceScale = CadencePace.Sprint;
            for (int i = 0; i < 300; i++) run.Tick(.02f);
            Assert.LessOrEqual(run.CurrentSpeed, RunSession.MaxFootSpeed + .01f);
        }
        [Test] public void FAndJInTurnCountStepsButOneKeyRepeatedDoesNot()
        {
            var keys = new KeyStepCadence();
            double t = 0;
            for (int i = 0; i < 6; i++) { Assert.IsTrue(keys.Press(i % 2, t)); t += .4; }
            Assert.AreEqual(6 * 60f / KeyStepCadence.Window, keys.StepsPerMinute(t - .4), .01f);
            var same = new KeyStepCadence(); Assert.IsTrue(same.Press(0, 0)); Assert.IsFalse(same.Press(0, .2)); Assert.IsFalse(same.Press(0, .4));
            Assert.AreEqual(20f, same.StepsPerMinute(.5), .01f);
            Assert.AreEqual(0f, keys.StepsPerMinute(t + 10), "Old steps drop out");
            // Steady F J at 2 steps a second is a run (about 120 a minute): the same pace as running in place.
            var run = new KeyStepCadence(); double r = 0; for (int i = 0; i < 30; i++) { run.Press(i % 2, r); r += .5; }
            float share = CadencePace.Factor(run.StepsPerMinute(r - .5), new MotionTuning()); Assert.GreaterOrEqual(share, CadencePace.Run - .01f); Assert.Less(share, CadencePace.Sprint);
        }
    }
}
