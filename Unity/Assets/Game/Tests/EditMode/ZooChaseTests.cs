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
        private static ZooBeatmap Two()
        {
            var map = new ZooBeatmap();
            map.Notes.Add(new ZooNote(12, 4.5f, ZooNoteKind.Jump)); map.Notes.Add(new ZooNote(16, 6f, ZooNoteKind.ArmsUp));
            return map;
        }
        private static ZooChase Started(ZooBeatmap map = null) { var c = new ZooChase(); c.Start(map ?? Two(), 100f, 9f); return c; }
        private static void Run(ZooChase c, float seconds, bool? behind = null) { for (float t = 0; t < seconds - 1e-4f; t += .02f) c.Tick(.02f, behind); }
        private static RoutePlan ZooPlan(RunConfig config)
        {
            var plan = new RoutePlan(config, 5);
            plan.SetBranchLayout(SectionKind.Zoo, lap => ZooChase.Branch(9f));
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
            var c = Started(new ZooBeatmap());
            Assert.AreEqual(LionAction.CageBreakout, c.ActionAt(.9f));
            Run(c, 2.1f); Assert.AreEqual(LionAction.Run, c.ActionAt(.9f));
            for (int i = 0; i < 2000 && !c.Roaring; i++) c.Tick(.02f, true);
            Assert.AreEqual(LionAction.Roar, c.ActionAt(.9f));
            for (int i = 0; i < 400 && c.Bites == 0; i++) c.Tick(.02f, true);
            Assert.AreEqual(LionAction.Pounce, c.ActionAt(.9f));
            Run(c, 1f); Assert.AreEqual(LionAction.Run, c.ActionAt(.9f));
        }
        [Test] public void ThePracticeVerseIsTheEasyChartUpToTheBreakAndTheLionNeverRoars()
        {
            var full = Chart("easy_a"); var verse = ZooPractice.Verse(full);
            Assert.AreEqual(ZooPractice.Chart, verse.Chart);
            Assert.Greater(verse.Notes.Count, 0); Assert.Less(verse.Notes.Count, full.Notes.Count);
            Assert.IsTrue(verse.Notes.Exists(n => n.Kind == ZooNoteKind.Jump) && verse.Notes.Exists(n => n.Kind == ZooNoteKind.ArmsUp), "Both note kinds");
            foreach (var note in verse.Notes) Assert.Less(note.Time, full.BreakStart);
            Assert.AreEqual(5, ZooPractice.PassHits(8));
            var c = new ZooChase { Practice = true }; c.Start(verse, 0f, 9f);
            Run(c, full.BreakStart + .5f, true);
            Assert.AreEqual(verse.Notes.Count, c.Hits + c.Misses);
            Assert.AreEqual(ZooPractice.LionCap, c.Lion, 1e-4f); Assert.IsFalse(c.Roaring); Assert.AreEqual(0, c.Bites);
            Assert.IsTrue(c.MirrorVisible);
        }
        [Test] public void NothingIsJudgedDuringTheCutAndTheShout()
        {
            var c = Started(); Assert.AreEqual(ZooPhase.Cut, c.Phase);
            Assert.AreEqual(-1, c.Input(ZooNoteKind.Jump));
            Run(c, 2.1f, true); Assert.AreEqual(ZooPhase.Shout, c.Phase); Assert.AreEqual(0f, c.Lion);
            Run(c, 1f, true); Assert.AreEqual(ZooPhase.Song, c.Phase);
            Assert.AreEqual(0f, c.Lion, .001f, "One second of grace after the cut");
            Run(c, 1f, true); Assert.Greater(c.Lion, 0f, "Lagging the beat brings the lion");
        }
        [Test] public void NotesHitInsideTheWindowAndMissAfterIt()
        {
            var c = Started();
            Run(c, 4.5f - .2f);
            Assert.AreEqual(-1, c.Input(ZooNoteKind.ArmsUp), "Wrong kind");
            Assert.AreEqual(0, c.Input(ZooNoteKind.Jump)); Assert.AreEqual(1, c.Hits);
            Run(c, 2f);
            Assert.AreEqual(1, c.Misses, "The arms-up was never made");
            Assert.AreEqual(ZooChase.ArmsUpMiss, c.Lion, .001f);
            var late = Started(); late.SyncOffset = .12f; Run(late, 4.5f + .3f);
            Assert.AreEqual(0, late.Input(ZooNoteKind.Jump), "The sync offset shifts the window for a late camera");
        }
        [Test] public void AMissedJumpBringsTheLionMostAndAHitPushesItBack()
        {
            var c = Started(); Run(c, 5f);
            Assert.AreEqual(1, c.Misses); Assert.AreEqual(ZooChase.JumpMiss, c.Lion, .001f);
            Run(c, .9f); c.Input(ZooNoteKind.ArmsUp);
            Assert.AreEqual(ZooChase.JumpMiss - ZooChase.HitRelief, c.Lion, .001f);
        }
        [Test] public void TheLionRoarsForOneAndAHalfSecondsBeforeItBites()
        {
            var c = Started(new ZooBeatmap()); int roars = 0, bites = 0; c.RoarStarted += () => roars++; c.Bit += () => bites++;
            Run(c, 3f); Run(c, 18f, true);
            Assert.AreEqual(1, roars); Assert.AreEqual(1, bites); Assert.AreEqual(1, c.Bites);
            var d = Started(new ZooBeatmap()); Run(d, 4f);
            while (!d.Roaring) d.Tick(.02f, true);
            Run(d, 1.4f, true); Assert.AreEqual(0, d.Bites, "No bite before the roar is over");
            Run(d, .2f, true); Assert.AreEqual(1, d.Bites); Assert.AreEqual(ZooChase.AfterBite, d.Lion, .03f);
            Assert.IsTrue(d.MirrorVisible);
        }
        [Test] public void SpeedingUpDuringTheRoarCancelsTheBite()
        {
            var c = Started(new ZooBeatmap()); Run(c, 4f);
            while (!c.Roaring) c.Tick(.02f, true);
            Run(c, 5f, false);
            Assert.AreEqual(0, c.Bites); Assert.IsFalse(c.Roaring);
        }
        [Test] public void TheKeyboardOnlyBringsTheLionOnMissedNotes()
        {
            var c = Started(new ZooBeatmap()); Run(c, 20f, null);
            Assert.AreEqual(0f, c.Lion);
        }
        [Test] public void SpeedEasesToTheSongSpeedDuringTheCutAndDistanceFollows()
        {
            var c = new ZooChase(); c.Start(new ZooBeatmap(), 50f, 15f);
            Assert.AreEqual(15f, c.Speed(0f), .001f); Assert.AreEqual(12f, c.Speed(1f), .001f); Assert.AreEqual(9f, c.Speed(5f), .001f);
            Assert.AreEqual(24f, c.DistanceAt(2f), .001f);
            Assert.AreEqual(24f + 27f, c.DistanceAt(5f), .001f);
            float sum = 0; for (float t = 0; t < 10f - 1e-4f; t += .001f) sum += c.Speed(t + .0005f) * .001f;
            Assert.AreEqual(c.DistanceAt(10f), sum, .05f);
        }
        [Test] public void TheSongEndsTheChase()
        {
            var c = Started(new ZooBeatmap()); Run(c, 60.1f);
            Assert.AreEqual(ZooPhase.Done, c.Phase); Assert.IsFalse(c.MirrorVisible);
        }
        [Test] public void TheZooBranchFitsTheSongAndHasOneLaneWithoutStairs()
        {
            var config = new RunConfig { ExtendedRoutes = true };
            var plan = ZooPlan(config);
            int zooChunks = 0; for (int i = CartFork.DestinationChunk; i < plan.LapChunks(0); i++) if (plan.TypeAt(i) == 12) zooChunks++;
            Assert.GreaterOrEqual(zooChunks * RouteSurface.Length, 9f * ZooChase.SongSeconds);
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
        [Test] public void TheSongSpeedIgnoresTheCadenceInTheZoo()
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
                float z = run.Runner.Z; run.Tick(.5f);
                Assert.AreEqual(4.5f, run.Runner.Z - z, .01f);
            }
            finally { RouteSurface.UsePlan(null); }
        }
    }
}
