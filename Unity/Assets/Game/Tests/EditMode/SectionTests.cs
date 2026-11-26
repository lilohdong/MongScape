using System.Collections.Generic;
using Mongscape.Domain;
using NUnit.Framework;

namespace Mongscape.Tests
{
    public sealed class SectionTests
    {
        private const float L = RouteSurface.Length;
        private static RunSession Running(RunConfig config = null)
        {
            var run = new RunSession(config ?? new RunConfig());
            run.Flow.Transition(GameFlowState.TitleIdle); run.Flow.Transition(GameFlowState.IntroTimeline);
            run.Flow.Transition(GameFlowState.FirstPersonTakeover); run.Flow.Transition(GameFlowState.Running); return run;
        }
        // A plan whose first fork is decided toward the given map.
        private static RoutePlan Choose(SectionKind kind, int seed = 3)
        {
            var plan = new RoutePlan(new RunConfig(), seed);
            int forkSequence = CartFork.FirstChunk;
            int lane = -1;
            for (int l = -1; l <= 1; l++) if (SectionDef.KindOfDestination(plan.DestinationFor(forkSequence, l)) == kind) lane = l;
            plan.UpdateChoice(CartFork.Decision + 1, lane);
            return plan;
        }

        [TestCase(SectionKind.Zoo, 25)][TestCase(SectionKind.Prison, 6)]
        public void BranchLayoutHookSetsTheBranchLength(SectionKind kind, int branchChunks)
        {
            var plan = new RoutePlan(new RunConfig(), 3);
            int type = kind == SectionKind.Zoo ? 12 : 4;
            plan.SetBranchLayout(kind, lap => { var b = new int[branchChunks]; for (int i = 0; i < b.Length; i++) b[i] = type; b[b.Length - 1] = kind == SectionKind.Zoo ? 3 : RouteSurface.PrisonExit; return b; });
            // Generate the next lap first so the splice has to move it.
            Assert.AreEqual(0, plan.CycleAt(RoutePlan.ExtendedLapChunks - 1)); plan.TypeAt(RoutePlan.ExtendedLapChunks * 2);
            int lane = 0; for (int l = -1; l <= 1; l++) if (SectionDef.KindOfDestination(plan.DestinationFor(CartFork.FirstChunk, l)) == kind) lane = l;
            plan.UpdateChoice(CartFork.Decision + 1, lane);
            int length = CartFork.DestinationChunk + branchChunks;
            Assert.AreEqual(length, plan.LapChunks(0)); Assert.AreEqual(length, plan.LapStart(1));
            for (int i = CartFork.DestinationChunk; i < length - 1; i++) Assert.AreEqual(type, plan.TypeAt(i));
            Assert.AreEqual(0, plan.CycleAt(length - 1)); Assert.AreEqual(1, plan.CycleAt(length));
            Assert.AreEqual(kind, SectionSpan.At(plan, length - 2).Kind);
            Assert.AreEqual(SectionKind.Mine, SectionSpan.At(plan, length).Kind);
            Assert.AreEqual(0, plan.LocalSequence(length));
            for (int i = 0; i < length * 2; i++) Assert.AreEqual(plan.BaseHeight(i) + RouteSurface.Rise(plan.TypeAt(i)), plan.BaseHeight(i + 1), .001f);
        }
        [Test] public void EachMapHasItsChaserAndRules()
        {
            Assert.AreEqual(ChaserKind.Police, SectionDef.Mine.Chaser.Kind);
            Assert.AreEqual(ChaserKind.Lion, SectionDef.Zoo.Chaser.Kind);
            Assert.AreEqual(ChaserKind.Warden, SectionDef.Prison.Chaser.Kind);
            Assert.AreEqual(ChaserSound.Siren, ChaserDef.Police.Sound);
            Assert.AreEqual(ChaserSound.Roar, ChaserDef.Lion.Sound);
            Assert.AreEqual(ChaserSound.AlarmBell, ChaserDef.Warden.Sound);
            Assert.IsTrue(SectionDef.Mine.UsesDistanceCurve);
            Assert.IsFalse(SectionDef.Zoo.UsesDistanceCurve); Assert.AreEqual(SpeedRule.SongFixed, SectionDef.Zoo.Speed);
            Assert.IsFalse(SectionDef.Prison.UsesDistanceCurve); Assert.AreEqual(SpeedRule.SetFixed, SectionDef.Prison.Speed);
            Assert.AreEqual(1.2f, ChaserDef.Police.CatchSeconds, .001f);
        }
        [TestCase(SectionKind.Zoo)][TestCase(SectionKind.Prison)][TestCase(SectionKind.Mine)]
        public void TheLapStartIsMineAndTheBranchIsTheChosenMap(SectionKind kind)
        {
            var plan = Choose(kind);
            var start = SectionSpan.At(plan, 0); Assert.AreEqual(SectionKind.Mine, start.Kind); Assert.IsFalse(start.Branch);
            Assert.AreEqual(SectionKind.Mine, SectionSpan.At(plan, CartFork.ConnectorChunk).Kind, "The fork belongs to the mine start");
            var branch = SectionSpan.At(plan, CartFork.DestinationChunk); Assert.AreEqual(kind, branch.Kind); Assert.IsTrue(branch.Branch);
            var next = SectionSpan.At(plan, RoutePlan.ExtendedLapChunks); Assert.AreEqual(SectionKind.Mine, next.Kind); Assert.AreEqual(1, next.Lap);
        }
        [Test] public void TrackerRaisesExitThenEnterAtEachBoundaryAndCountsVisits()
        {
            var plan = Choose(SectionKind.Zoo);
            var tracker = new SectionTracker(); var log = new List<string>();
            tracker.Exited += s => log.Add("out " + s.Kind); tracker.Entered += s => log.Add("in " + s.Kind + s.Visit);
            for (float z = 0; z < RoutePlan.ExtendedLapChunks * L + 10; z += 4) tracker.Update(plan, z);
            CollectionAssert.AreEqual(new[] { "out Mine", "in Zoo1", "out Zoo", "in Mine2" }, log);
            Assert.AreEqual(1, tracker.Visits(SectionKind.Zoo)); Assert.AreEqual(2, tracker.Visits(SectionKind.Mine));
            tracker.Reset(); Assert.AreEqual(SectionKind.Mine, tracker.Current.Kind); Assert.AreEqual(0, tracker.Visits(SectionKind.Zoo));
        }
        [Test] public void LeavingTheMineStartForTheMineBranchIsABoundary()
        {
            var plan = Choose(SectionKind.Mine); var tracker = new SectionTracker(); int entered = 0;
            tracker.Entered += s => entered++;
            tracker.Update(plan, (CartFork.DestinationChunk - 1) * L); Assert.AreEqual(0, entered);
            tracker.Update(plan, CartFork.DestinationChunk * L + 1); Assert.AreEqual(1, entered);
            Assert.IsTrue(tracker.Current.Branch); Assert.AreEqual(ChaserKind.Police, tracker.Chaser.Kind);
        }
        [Test] public void WithoutAnExtendedPlanTheRouteIsOneMineSection()
        {
            Assert.AreEqual(SectionKind.Mine, SectionSpan.At(null, 9).Kind);
            Assert.AreEqual(SectionKind.Mine, SectionSpan.At(RoutePlan.Authored(), 9).Kind);
        }
        [Test] public void OnlyTheMineFollowsTheDistanceCurve()
        {
            var plan = Choose(SectionKind.Zoo);
            try
            {
                RouteSurface.UsePlan(plan);
                var run = Running(); run.Runner.SetStartZ(CartFork.DestinationChunk * L + 1);
                run.Score.Advance(1500); run.Tick(.01f);
                Assert.AreEqual(SectionKind.Zoo, run.Section.Kind); Assert.AreEqual(ChaserKind.Lion, run.Chaser.Kind);
                Assert.AreEqual(run.Config.BaseSpeed, run.TargetSpeed, .001f, "Distance does not speed up the zoo");
                run.SectionSpeed = 11f; Assert.AreEqual(11f, run.TargetSpeed, .001f, "The song sets the zoo speed");
                var zooOdds = run.OddsAt(CartFork.DestinationChunk * L + L);
                Assert.AreEqual(run.Config.ObstacleChanceStart, zooOdds.Row, .001f, "Zoo hazard odds stay at the start value");
                var mineOdds = run.OddsAt(RoutePlan.ExtendedLapChunks * L * 40);
                Assert.Greater(mineOdds.Row, zooOdds.Row, "The mine still ramps with distance");
                run.Runner.SetStartZ(RoutePlan.ExtendedLapChunks * L + 1); run.Tick(.01f);
                Assert.AreEqual(SectionKind.Mine, run.Section.Kind); Assert.IsNull(run.SectionSpeed, "The map speed clears at the boundary");
                Assert.Greater(run.TargetSpeed, run.Config.BaseSpeed, "Back in the mine the distance curve applies again");
            }
            finally { RouteSurface.UsePlan(null); }
        }
    }
}
