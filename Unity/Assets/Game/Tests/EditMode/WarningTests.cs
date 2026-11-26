using System.Collections.Generic;
using Mongscape.Domain;
using NUnit.Framework;

namespace Mongscape.Tests
{
    public sealed class WarningTests
    {
        private const float L = RouteSurface.Length;
        private static RunSession Running()
        {
            var run = new RunSession(new RunConfig());
            run.Flow.Transition(GameFlowState.TitleIdle); run.Flow.Transition(GameFlowState.LoadingIntro); run.Flow.Transition(GameFlowState.Running);
            return run;
        }
        private static RoutePlan Choose(SectionKind kind)
        {
            var plan = new RoutePlan(new RunConfig(), 5); int lane = 0;
            for (int l = -1; l <= 1; l++) if (SectionDef.KindOfDestination(plan.DestinationFor(CartFork.FirstChunk, l)) == kind) lane = l;
            plan.UpdateChoice(CartFork.Decision + 1, lane); return plan;
        }
        [TearDown] public void ClearPlan() => RouteSurface.UsePlan(null);

        [Test] public void LeavingTheMapEmptiesTheWarningSlots()
        {
            RouteSurface.UsePlan(Choose(SectionKind.Prison));
            var run = Running(); int cleared = 0; run.StrikeCleared += () => cleared++;
            run.Runner.SetStartZ(CartFork.DestinationChunk * L - 3f);
            Assert.IsTrue(run.Slip(SlipSource.Banana)); Assert.AreEqual(1, run.Warnings);
            for (int i = 0; i < 20; i++) run.Tick(.1f);
            Assert.AreEqual(SectionKind.Prison, run.Section.Kind);
            Assert.AreEqual(0, run.Warnings, "The slots empty at the boundary"); Assert.AreEqual(1, cleared); Assert.AreEqual(0, run.Chase.Pressure);
        }
        [Test] public void APursuitStillCatchesAcrossTheBoundary()
        {
            RouteSurface.UsePlan(Choose(SectionKind.Prison));
            var run = Running(); run.Runner.SetStartZ(CartFork.DestinationChunk * L - .5f);
            run.Slip(SlipSource.Banana); run.Runner.SetStartZ(CartFork.DestinationChunk * L - .4f); run.Hit(true);
            Assert.IsTrue(run.SlipPursuit);
            for (int i = 0; i < 20; i++) run.Tick(.1f);
            Assert.AreEqual(GameFlowState.Caught, run.Flow.State); Assert.AreEqual(ChaserKind.Police, run.CaughtBy, "The police started the chase");
        }
        [Test] public void TheZooChaserIsTheLionAndAFenceTripIsNoWarning()
        {
            RouteSurface.UsePlan(Choose(SectionKind.Zoo));
            var run = Running(); run.Runner.SetStartZ(CartFork.DestinationChunk * L + 1f); run.Tick(.01f);
            var heard = new List<ChaserSound>(); run.Warned += (cause, chaser) => heard.Add(chaser.Sound);
            Assert.IsFalse(run.Hit(true)); Assert.AreEqual(0, run.Warnings); Assert.IsTrue(run.Runner.Stumbling);
            Assert.Greater(run.ChaserGauge, 0f, "The lion closes in");
            for (int i = 0; i < 4; i++) run.Approach(.5f);
            Assert.AreEqual(2, run.Warnings); CollectionAssert.AreEqual(new[] { ChaserSound.Roar, ChaserSound.Roar }, heard);
            run.Tick(2f); Assert.AreEqual(GameFlowState.Caught, run.Flow.State); Assert.AreEqual(ChaserKind.Lion, run.CaughtBy);
        }
        [Test] public void AFullGaugeIsOneWarningAndDropsToHalf()
        {
            var run = Running();
            Assert.IsFalse(run.Approach(.6f)); Assert.IsTrue(run.Approach(.6f));
            Assert.AreEqual(1, run.Warnings); Assert.AreEqual(.5f, run.ChaserGauge, .001f); Assert.AreEqual(WarningCause.Gauge, run.LastWarning);
        }
        [Test] public void InThePrisonAMissedKneeStepBringsTheWardenCloser()
        {
            RouteSurface.UsePlan(Choose(SectionKind.Prison));
            var run = Running(); run.Runner.SetStartZ(CartFork.DestinationChunk * L + 1f); run.Tick(.01f);
            run.Slip(SlipSource.KneeMiss); Assert.AreEqual(0, run.Warnings); Assert.AreEqual(.5f, run.ChaserGauge, .001f);
            run.Slip(SlipSource.KneeMiss); Assert.AreEqual(1, run.Warnings); Assert.AreEqual(ChaserKind.Warden, run.Chaser.Kind);
        }
        [Test] public void ThePolicePounceWhenThePaceGaugeFillsInTheMine()
        {
            var run = Running(); var pace = new CadencePace(); var tuning = new MotionTuning();
            for (int i = 0; i < 200; i++) { pace.Tick(.05f, true, 0f, tuning, 100f); run.ApplyPace(pace); }
            Assert.GreaterOrEqual(run.Warnings, 1, "Standing still in the mine is not safe");
            Assert.AreEqual(WarningCause.Pace, run.LastWarning);
            Assert.Less(pace.Pressure, 1f);
            for (int i = 0; i < 400 && run.Flow.State == GameFlowState.Running; i++) { pace.Tick(.05f, true, 0f, tuning, 100f); run.ApplyPace(pace); run.Tick(.05f); }
            Assert.AreEqual(GameFlowState.Caught, run.Flow.State, "Standing still ends in a capture");
        }
        [Test] public void WithThePaceOptionOffThereIsNoPoliceGauge()
        {
            var run = Running(); var pace = new CadencePace();
            for (int i = 0; i < 400; i++) { pace.Tick(.05f, false, 0f, new MotionTuning(), 100f); run.ApplyPace(pace); }
            Assert.AreEqual(0, run.Warnings); Assert.AreEqual(0, run.PacePressure);
        }
        [Test] public void AShieldBlocksOneWarning()
        {
            var run = Running(); run.Items.Activate(ItemKind.Shield); int warned = 0; run.Warned += (c, d) => warned++;
            Assert.IsFalse(run.Warn(WarningCause.Spotted)); Assert.AreEqual(0, run.Warnings); Assert.AreEqual(1, run.ShieldSaves); Assert.AreEqual(0, warned);
            Assert.IsTrue(run.Warn(WarningCause.Spotted)); Assert.AreEqual(1, warned);
        }
    }
}
