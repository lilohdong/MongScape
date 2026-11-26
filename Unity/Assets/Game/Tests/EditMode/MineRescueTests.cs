using System.Collections.Generic;
using Mongscape.Domain;
using NUnit.Framework;

namespace Mongscape.Tests
{
    public sealed class MineRescueTests
    {
        private const float L = RouteSurface.Length;
        // A plan whose forks lead to the mine on even laps and to the zoo on odd ones (the same map twice in a row is closed).
        private static RoutePlan MinePlan(int seed, int laps)
        {
            var plan = new RoutePlan(new RunConfig(), seed);
            for (int lap = 0; lap < laps; lap++)
            {
                int fork = lap * RoutePlan.ExtendedLapChunks + CartFork.FirstChunk, lane = 0, want = lap % 2 == 0 ? 1 : -1;
                for (int l = -1; l <= 1; l++) if (plan.DestinationFor(fork, l) == want) lane = l;
                plan.UpdateChoice(lap * RoutePlan.ExtendedLapChunks * L + CartFork.Decision + 1, lane);
            }
            return plan;
        }
        private static List<int> Monkeys(RoutePlan plan, int from, int to)
        {
            var found = new List<int>();
            for (int s = from; s < to; s++) if (MineRescuePlan.At(plan, s, out _)) found.Add(s);
            return found;
        }

        [Test] public void EveryMineLapHasOneOrTwoMonkeysOnWalkingChunksOnly()
        {
            for (int seed = 1; seed <= 40; seed++)
            {
                var plan = MinePlan(seed, 4);
                for (int lap = 0; lap < 4; lap++)
                {
                    int start = lap * RoutePlan.ExtendedLapChunks;
                    var found = Monkeys(plan, start, start + RoutePlan.ExtendedLapChunks);
                    if (plan.DestinationAt(start + CartFork.DestinationChunk) == 1) Assert.That(found.Count, Is.InRange(1, 2), "seed " + seed + " lap " + lap);
                    else { Assert.That(found.Count, Is.InRange(0, 1)); if (found.Count == 1) Assert.AreEqual(start, found[0], "only the mine start"); }
                    foreach (int s in found)
                    {
                        Assert.AreEqual(0, plan.TypeAt(s), "only mine walks");
                        Assert.AreNotEqual(start + RoutePlan.ExtendedLapChunks - 1, s, "never the return chunk");
                        MineRescuePlan.At(plan, s, out int side); Assert.That(side == -1 || side == 1);
                    }
                }
                Assert.IsFalse(MineRescuePlan.At(plan, 0, out _), "the opening chunk has none");
            }
        }
        [Test] public void OtherBranchesHaveNoMonkeysPastTheFork()
        {
            for (int seed = 1; seed <= 20; seed++)
            {
                var plan = new RoutePlan(new RunConfig(), seed);
                int lane = 0; for (int l = -1; l <= 1; l++) if (plan.DestinationFor(CartFork.FirstChunk, l) == -1) lane = l;
                plan.UpdateChoice(CartFork.Decision + 1, lane);
                Assert.IsEmpty(Monkeys(plan, CartFork.DestinationChunk, RoutePlan.ExtendedLapChunks));
            }
        }
        [Test] public void SameSeedGivesSameMonkeys()
        {
            CollectionAssert.AreEqual(Monkeys(MinePlan(7, 3), 0, 54), Monkeys(MinePlan(7, 3), 0, 54));
        }
        [Test] public void TheMonkeyLaneIsClearForTwoSecondsBeforeIt()
        {
            var slots = new List<ObstacleSlot>();
            foreach (float z in new[] { 8f, 14f, 19f })
                for (int lane = -1; lane <= 1; lane++) slots.Add(new ObstacleSlot(ObstacleKind.Barrier, lane, z, false));
            slots.Add(new ObstacleSlot(ObstacleKind.Crate, -1, 12f, true)); slots.Add(new ObstacleSlot(ObstacleKind.Crate, 0, 12f, true)); slots.Add(new ObstacleSlot(ObstacleKind.Crate, 1, 12f, true));
            var full = new HazardOdds(1, 1, 1, 1);
            for (int seed = 0; seed < 60; seed++)
                foreach (int side in new[] { -1, 1 })
                {
                    var layout = new ObstacleLayout();
                    layout.Plan(new System.Random(seed), 0, 5, slots, new List<LanePlacement>(), full, true, false, speed: 9f, rescueSide: side);
                    Assert.AreEqual(side, layout.RescueSide);
                    float from = ObstacleLayout.RescueClearFrom(9f);
                    Assert.Less(from, 4f, "2 s at 9 m/s reaches back to the chunk start");
                    for (int i = 0; i < slots.Count; i++)
                        if (layout.Slots[i]) Assert.IsFalse(slots[i].FullRow || slots[i].Lane == side, "slot " + i + " seed " + seed);
                    foreach (var b in layout.Blockers) Assert.AreNotEqual(side, b.Lane);
                    foreach (var b in layout.Bananas) Assert.AreNotEqual(side, b.Lane);
                    Assert.IsNull(layout.Violation());
                }
        }
        [Test] public void CartAndStairChunksNeverHoldAMonkey()
        {
            var layout = new ObstacleLayout();
            layout.Plan(new System.Random(1), 1, 5, new List<ObstacleSlot>(), new List<LanePlacement>(), new HazardOdds(0, 0, 0, 0), true, false, rescueSide: 1);
            Assert.AreEqual(0, layout.RescueSide);
            layout.Plan(new System.Random(1), RouteSurface.MineStair, 5, new List<ObstacleSlot>(), new List<LanePlacement>(), new HazardOdds(0, 0, 0, 0), true, false, rescueSide: 1);
            Assert.AreEqual(0, layout.RescueSide);
        }

        // The first monkey of a mine plan and the judge watching it.
        private static (RoutePlan plan, int sequence, int side) FirstMonkey()
        {
            var plan = MinePlan(5, 2);
            var found = Monkeys(plan, 1, 36);
            MineRescuePlan.At(plan, found[0], out int side);
            return (plan, found[0], side);
        }
        [Test] public void ReachingInTheLaneWithinTheLastSecondRescues()
        {
            var (plan, s, side) = FirstMonkey();
            var judge = new MineRescueJudge(); int rescued = 0; judge.RescuedAt += (seq, w) => { rescued++; Assert.AreEqual(s, seq); Assert.AreEqual(side, w); };
            float z = MineRescuePlan.RouteZ(s);
            judge.Tick(.1f, plan, z - 9f * .8f, side, 9f);
            Assert.AreEqual(0, rescued, "no reach yet");
            judge.Reach(side); judge.Tick(.1f, plan, z - 9f * .7f, side, 9f);
            Assert.AreEqual(1, rescued); Assert.AreEqual(1, judge.Rescued);
            judge.Tick(.1f, plan, z + 1f, side, 9f);
            Assert.AreEqual(0, judge.Missed, "a rescued monkey is not missed");
        }
        [Test] public void ReachingTooEarlyOrInTheWrongLaneMisses()
        {
            var (plan, s, side) = FirstMonkey();
            float z = MineRescuePlan.RouteZ(s);
            var early = new MineRescueJudge();
            early.Reach(side); early.Tick(.05f, plan, z - 9f * 1.5f, side, 9f);
            early.Tick(.6f, plan, z - 9f * .5f, side, 9f);
            early.Tick(.5f, plan, z + .5f, side, 9f);
            Assert.AreEqual(0, early.Rescued); Assert.AreEqual(1, early.Missed);
            var wrong = new MineRescueJudge();
            wrong.Reach(side); wrong.Tick(.1f, plan, z - 4f, 0, 9f);
            wrong.Tick(.1f, plan, z + .5f, 0, 9f);
            Assert.AreEqual(0, wrong.Rescued, "reached from the middle lane"); Assert.AreEqual(1, wrong.Missed);
            var away = new MineRescueJudge();
            away.Reach(-side); away.Tick(.1f, plan, z - 3f, side, 9f);
            Assert.AreEqual(0, away.Rescued, "reached away from the wall");
        }
        [Test] public void TheCallRescuesWithoutReachingInTheLane()
        {
            var (plan, s, side) = FirstMonkey();
            float z = MineRescuePlan.RouteZ(s);
            var judge = new MineRescueJudge { CallActive = () => true };
            judge.Tick(.1f, plan, z - 4f, 0, 9f);
            Assert.AreEqual(0, judge.Rescued, "still needs the monkey's lane");
            judge.Tick(.1f, plan, z - 3f, side, 9f);
            Assert.AreEqual(1, judge.Rescued);
        }
        [Test] public void MineArtResolvesEveryPiece()
        {
            var art = UnityEngine.Resources.Load<Mongscape.Presentation.Map.MineArt>(Mongscape.Presentation.Map.MineArt.ResourcePath);
            Assert.IsNotNull(art);
            Assert.IsNotNull(art.Monkey); Assert.IsNotNull(art.WallLeft); Assert.IsNotNull(art.WallRight);
            Assert.IsNotNull(art.WaveLeft); Assert.IsNotNull(art.WaveRight); Assert.IsNotNull(art.RescuePop);
            Assert.IsNotNull(art.SignMine); Assert.IsNotNull(art.SignZoo); Assert.IsNotNull(art.SignPrison); Assert.IsNotNull(art.SignBlocked);
            Assert.IsNotNull(art.Rescue); Assert.IsNotNull(art.ForkBlocked);
        }
        [Test] public void KeyboardAndSessionRouteTheReach()
        {
            var run = new RunSession(new RunConfig());
            run.Command(MotionCommand.ReachLeft);
            Assert.IsTrue(run.MineRescue.Reaching(-1)); Assert.IsFalse(run.MineRescue.Reaching(1));
            run.Command(MotionCommand.Left);
            Assert.AreEqual(-1, run.Runner.Lane);
        }

        // Standing body (image units, aspect 4:3) with the wrists moved: shoulders at x .44/.56, y .35.
        private static PoseFrame Body(double time, float wristLx, float wristLy, float wristRx, float wristRy, float roll = 0)
        {
            var frame = new PoseFrame { HasPose = true, Time = time, Aspect = 4f / 3f };
            void Set(int i, float x, float y, float v = .95f) => frame.Points[i] = new PoseLandmark { X = x, Y = y, Visibility = v };
            for (int i = 0; i < PoseFrame.Count; i++) Set(i, .5f, .5f, .2f);
            Set(0, .5f, .25f); Set(11, .44f, .35f - roll); Set(12, .56f, .35f + roll);
            Set(23, .46f, .62f); Set(24, .54f, .62f); Set(25, .46f, .78f); Set(26, .54f, .78f); Set(27, .46f, .93f); Set(28, .54f, .93f);
            Set(15, wristLx, wristLy); Set(16, wristRx, wristRy);
            return frame;
        }
        private static MotionClassifier Calibrated(PlayStyle style)
        {
            var classifier = new MotionClassifier(new MotionTuning()); classifier.SetStyle(style);
            for (int i = 0; i < 30; i++) classifier.Update(Body(i / 30.0, .41f, .6f, .59f, .6f));
            Assert.AreEqual(PoseStatus.Tracking, classifier.Status);
            return classifier;
        }
        [TestCase(PlayStyle.FullBody)][TestCase(PlayStyle.UpperBody)]
        public void OneArmOutAtShoulderHeightIsAReachToThatSide(PlayStyle style)
        {
            var classifier = Calibrated(style);
            Assert.AreEqual(0, classifier.Update(Body(2, .41f, .6f, .59f, .6f)).Reach, "arms down");
            Assert.AreEqual(1, classifier.Update(Body(2.1, .41f, .6f, .8f, .36f)).Reach, "right arm out");
            Assert.AreEqual(0, classifier.Update(Body(2.2, .41f, .6f, .59f, .6f)).Reach);
            Assert.AreEqual(-1, classifier.Update(Body(2.3, .2f, .34f, .59f, .6f)).Reach, "left arm out");
            Assert.AreEqual(0, classifier.Update(Body(2.4, .2f, .34f, .8f, .34f)).Reach, "both arms out is a T, not a reach");
            Assert.AreEqual(0, classifier.Update(Body(2.5, .41f, .6f, .8f, .1f)).Reach, "arm up high is not a reach");
        }
        [Test] public void UpperBodyLaneHoldsWhileTheArmIsOut()
        {
            var classifier = Calibrated(PlayStyle.UpperBody);
            int lane = 0;
            // Reaching right tilts the shoulders (right side drops); the lane must not follow while the arm is out.
            for (double t = 0; t < 1; t += 1 / 30.0) lane = classifier.Update(Body(2 + t, .41f, .6f, .82f, .37f, .05f)).Lane;
            Assert.AreEqual(0, lane);
        }
    }
}
