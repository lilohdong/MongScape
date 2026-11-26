using System.Collections.Generic;
using Mongscape.Domain;
using NUnit.Framework;

namespace Mongscape.Tests
{
    public sealed class PauseFlowTests
    {
        private static RunSession Running()
        {
            var run = new RunSession(new RunConfig());
            run.Flow.Transition(GameFlowState.TitleIdle); run.Flow.Transition(GameFlowState.LoadingIntro); run.Flow.Transition(GameFlowState.Running);
            return run;
        }
        private static void Ticks(PauseFlow flow, RunSession run, double seconds, bool camera, bool tracked, bool crossed, bool stepping)
        { for (double t = 0; t < seconds - 1e-9; t += .05) flow.Tick(run, .05, camera, tracked, crossed, stepping); }

        [Test] public void LosingThePlayerPausesWithTheSilhouetteAndRegainingCountsBackIn()
        {
            var run = Running(); var flow = new PauseFlow(); var counts = new List<int>(); flow.Count += counts.Add;
            Ticks(flow, run, .5, true, false, false, false);
            Assert.IsTrue(run.Paused); Assert.AreEqual(PauseReason.LostTracking, flow.Reason); Assert.IsTrue(flow.ShowSilhouette);
            Ticks(flow, run, 1, true, true, false, false);
            Assert.IsTrue(flow.Counting); Assert.IsTrue(run.Paused);
            Ticks(flow, run, 3.1, true, true, false, false);
            Assert.IsFalse(run.Paused); CollectionAssert.AreEqual(new[] { 3, 2, 1 }, counts);
        }
        [Test] public void TheXCountsOnlyHeldForASecondWithoutRunning()
        {
            var run = Running(); var flow = new PauseFlow();
            Ticks(flow, run, 2, true, true, true, true);
            Assert.IsFalse(run.Paused, "Arms crossing while running in place is a swing, not the X");
            Ticks(flow, run, .5, true, true, true, false);
            Assert.IsFalse(run.Paused); Assert.AreEqual(.5f, flow.GestureProgress, .06f);
            Ticks(flow, run, .55, true, true, true, false);
            Assert.IsTrue(run.Paused); Assert.AreEqual(PauseReason.Gesture, flow.Reason); Assert.AreEqual(0, flow.GestureProgress);
            Ticks(flow, run, 5, true, true, false, false);
            Assert.IsTrue(run.Paused, "A gesture pause waits for the menu");
        }
        [Test] public void KeyPauseTogglesThroughACountIn()
        {
            var run = Running(); var flow = new PauseFlow(); int resumed = 0; flow.Resumed += () => resumed++;
            flow.Toggle(run); Assert.IsTrue(run.Paused); Assert.AreEqual(PauseReason.Key, flow.Reason);
            Ticks(flow, run, 2, false, false, false, false); Assert.IsTrue(run.Paused);
            flow.Toggle(run); Assert.IsTrue(flow.Counting); Assert.AreEqual(3, flow.CountdownNumber);
            Ticks(flow, run, 3.05, false, false, false, false); Assert.IsFalse(run.Paused); Assert.AreEqual(1, resumed);
        }
        [Test] public void ARhythmSectionCanCountInOnTheBar()
        {
            var run = Running(); var flow = new PauseFlow { ResumeHook = () => new ResumePlan(2.5, 4) }; var counts = new List<int>(); flow.Count += counts.Add;
            flow.Pause(run, PauseReason.Key); flow.Resume(run);
            Ticks(flow, run, 2.55, false, false, false, false);
            Assert.IsFalse(run.Paused); CollectionAssert.AreEqual(new[] { 4, 3, 2, 1 }, counts);
        }
        [Test] public void KeyboardRunsNeverAutoPause()
        {
            var run = Running(); var flow = new PauseFlow();
            Ticks(flow, run, 3, false, false, true, false);
            Assert.IsFalse(run.Paused);
        }
        [Test] public void LosingThePlayerDuringTheCountInStopsIt()
        {
            var run = Running(); var flow = new PauseFlow();
            Ticks(flow, run, .5, true, false, false, false); Ticks(flow, run, 1, true, true, false, false);
            Assert.IsTrue(flow.Counting);
            Ticks(flow, run, .1, true, false, false, false);
            Assert.IsFalse(flow.Counting); Assert.IsTrue(flow.ShowSilhouette);
        }
    }
}
