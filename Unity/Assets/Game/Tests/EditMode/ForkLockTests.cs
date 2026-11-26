using Mongscape.Domain;
using NUnit.Framework;

namespace Mongscape.Tests
{
    // Plan 3-2: the map of the previous lap is closed at the next fork.
    public sealed class ForkLockTests
    {
        private const float L = RouteSurface.Length;
        private static float Decision(int lap) => lap * RoutePlan.ExtendedLapChunks * L + CartFork.Decision + .5f;
        private static int Fork(int lap) => lap * RoutePlan.ExtendedLapChunks + CartFork.FirstChunk;

        [Test] public void TheFirstForkIsOpenAndThePreviousMapIsClosedNext()
        {
            var plan = new RoutePlan(new RunConfig(), 4);
            Assert.AreEqual(RoutePlan.NoBlockedLane, plan.BlockedLane(Fork(0)));
            plan.UpdateChoice(Decision(0), -1);
            Assert.AreEqual(-1, plan.BlockedLane(Fork(1)), "the zoo was taken, so the zoo rail is closed");
            plan.UpdateChoice(Decision(1), 1);
            Assert.AreEqual(1, plan.BlockedLane(Fork(2)));
        }
        [Test] public void StayingOnTheClosedRailPicksOneOfTheOpenOnes()
        {
            var picked = new System.Collections.Generic.HashSet<int>();
            for (int seed = 1; seed <= 30; seed++)
            {
                var plan = new RoutePlan(new RunConfig(), seed);
                plan.UpdateChoice(Decision(0), 0);
                plan.UpdateChoice(Decision(1), 0);
                int choice = plan.ChoiceAt(Fork(1));
                Assert.AreNotEqual(0, choice, "the prison was last lap");
                picked.Add(choice);
                var again = new RoutePlan(new RunConfig(), seed); again.UpdateChoice(Decision(0), 0); again.UpdateChoice(Decision(1), 0);
                Assert.AreEqual(choice, again.ChoiceAt(Fork(1)), "seeded");
            }
            CollectionAssert.AreEquivalent(new[] { -1, 1 }, picked, "both open rails come up");
        }
        [Test] public void TheCartDoesNotLeanIntoTheClosedRail()
        {
            var config = new RunConfig();
            var plan = new RoutePlan(config, 2);
            RouteSurface.UsePlan(plan);
            try
            {
                plan.UpdateChoice(Decision(0), 1);
                var runner = new RunnerController(config);
                runner.SetStartZ(RoutePlan.ExtendedLapChunks * L + CartFork.Start + 4); runner.Tick(.01f, 0);
                Assert.IsTrue(runner.Riding); Assert.IsTrue(plan.ForkWindow(runner.Z));
                runner.Command(MotionCommand.Right);
                Assert.AreEqual(0, runner.Lane, "the mine was last lap"); Assert.AreEqual(1, runner.BlockedLeans);
                runner.Command(MotionCommand.Left);
                Assert.AreEqual(-1, runner.Lane);
            }
            finally { RouteSurface.UsePlan(null); }
        }
        [Test] public void LeaningAcrossAClosedMiddleRailReachesTheFarOne()
        {
            var config = new RunConfig();
            var plan = new RoutePlan(config, 2);
            RouteSurface.UsePlan(plan);
            try
            {
                plan.UpdateChoice(Decision(0), 0);
                var runner = new RunnerController(config);
                runner.SetStartZ(RoutePlan.ExtendedLapChunks * L + CartFork.Start + 4); runner.Tick(.01f, 0);
                runner.Command(MotionCommand.Right); Assert.AreEqual(1, runner.Lane);
                runner.Command(MotionCommand.Left); Assert.AreEqual(-1, runner.Lane, "skips the closed prison rail");
                runner.Command(MotionCommand.Center); Assert.AreEqual(-1, runner.Lane); Assert.AreEqual(1, runner.BlockedLeans);
            }
            finally { RouteSurface.UsePlan(null); }
        }
    }
}
