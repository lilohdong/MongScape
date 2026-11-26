using Mongscape.Domain;
using NUnit.Framework;

namespace Mongscape.Tests
{
    public sealed class PrisonCourseTests
    {
        private const float Dt = .05f;
        // Branch index of set s's cell chunk.
        private static int Cell(int set) => set * 3 + 1;

        // Runs one hold at set s's door with a fixed posture until the hold ends (or maxSeconds).
        private static void Hold(PrisonCourse course, int set, bool hidden, float depth, float maxSeconds = 20f)
        {
            course.Tick(Dt, Cell(set), 5f, false, 0f);
            Assert.AreEqual(PrisonPhase.Approach, course.Phase);
            for (float t = 0; t < maxSeconds; t += Dt)
            {
                course.Tick(Dt, Cell(set), PrisonCourse.DoorZ, hidden, depth);
                if (course.Phase != PrisonPhase.Hold) return;
            }
        }

        [Test] public void BranchFollowsTheFixedSetOrder()
        {
            Assert.AreEqual(11, PrisonCourse.Chunks.Length);
            for (int s = 0; s < PrisonCourse.Sets; s++)
            {
                Assert.IsTrue(RouteSurface.IsStair(PrisonCourse.Chunks[s * 3]), "set " + s + " starts with stairs");
                Assert.AreEqual(PrisonPhase.Stairs, PrisonCourse.PhaseOf(s * 3, 10f, out int set)); Assert.AreEqual(s, set);
                Assert.AreEqual(PrisonPhase.Approach, PrisonCourse.PhaseOf(s * 3 + 1, 3f, out _));
                Assert.AreEqual(PrisonPhase.Hold, PrisonCourse.PhaseOf(s * 3 + 1, PrisonCourse.DoorZ, out _));
                Assert.AreEqual(PrisonPhase.Flat, PrisonCourse.PhaseOf(s * 3 + 2, 3f, out _));
            }
            Assert.AreEqual(PrisonPhase.Roof, PrisonCourse.PhaseOf(9, 0f, out _));
            Assert.AreEqual(PrisonPhase.Roof, PrisonCourse.PhaseOf(10, 0f, out _));
            Assert.AreEqual(PrisonPhase.None, PrisonCourse.PhaseOf(-1, 0f, out _));
            Assert.IsFalse(RouteSurface.IsStair(PrisonCourse.Chunks[1]));
        }

        [Test] public void HideGetsLongerAndStairBeatFasterEachVisit()
        {
            Assert.AreEqual(5f, PrisonCourse.HideSecondsFor(1));
            Assert.AreEqual(6f, PrisonCourse.HideSecondsFor(2));
            Assert.AreEqual(7f, PrisonCourse.HideSecondsFor(3));
            Assert.AreEqual(7f, PrisonCourse.HideSecondsFor(5));
            Assert.Greater(PrisonCourse.StairGapScale(1), PrisonCourse.StairGapScale(2));
            Assert.Greater(PrisonCourse.StairGapScale(2), PrisonCourse.StairGapScale(3));
        }

        [Test] public void RunnerStopsAtTheDoorUntilTheLightHasPassed()
        {
            var course = new PrisonCourse(); course.Begin(1);
            Assert.AreEqual(PrisonCourse.DoorZ, course.Tick(Dt, Cell(0), 4f, false, 0f));
            Assert.AreEqual(PrisonCourse.ApproachSpeed, course.Speed);
            float seconds = 0; int started = 0; course.HoldStarted += _ => started++;
            while (seconds < 30f)
            {
                float limit = course.Tick(Dt, Cell(0), PrisonCourse.DoorZ, true, 1f);
                if (course.Phase != PrisonPhase.Hold) { Assert.AreEqual(float.MaxValue, limit); break; }
                Assert.AreEqual(0f, course.Speed); Assert.AreEqual(PrisonCourse.DoorZ, limit);
                seconds += Dt;
            }
            Assert.AreEqual(1, started);
            Assert.AreEqual(PrisonCourse.LeadSeconds + 5f, seconds, .11f);
            // The rest of the cell chunk is walked, the door does not stop the runner again.
            Assert.AreEqual(float.MaxValue, course.Tick(Dt, Cell(0), 13f, false, 0f));
            Assert.AreEqual(PrisonPhase.Flat, course.Phase);
        }

        [Test] public void DeepSquatOpensTheCellShallowDuckOnlyHides()
        {
            var course = new PrisonCourse(); course.Begin(1);
            int opened = 0, alarms = 0; course.CellOpened += _ => opened++; course.Alarm += _ => alarms++;
            Hold(course, 0, true, 1f);
            Assert.AreEqual(1, opened); Assert.IsTrue(course.Opened[0]); Assert.AreEqual(0, alarms);
            Hold(course, 1, true, .3f);
            Assert.AreEqual(1, opened, "a shallow duck hides but does not pick the lock"); Assert.AreEqual(0, alarms);
            Assert.IsFalse(course.Opened[1]);
            Assert.AreEqual(1, course.Rescued);
        }

        [Test] public void DeepSquatFillsBeforeTheLightHasPassed()
        {
            foreach (int visit in new[] { 1, 2, 3 })
            {
                var course = new PrisonCourse(); course.Begin(visit);
                float openedAt = -1, t = 0; course.CellOpened += _ => openedAt = t;
                course.Tick(Dt, Cell(0), 5f, false, 0f);
                for (; t < 20f && (course.Phase == PrisonPhase.Hold || t == 0); t += Dt) course.Tick(Dt, Cell(0), PrisonCourse.DoorZ, true, 1f);
                Assert.Greater(openedAt, 0f, "visit " + visit);
                Assert.Less(openedAt, course.HideSeconds + PrisonCourse.LeadSeconds - .5f, "visit " + visit);
            }
        }

        [Test] public void DexterityOpensTheLockSooner()
        {
            float OpenedAt(float bonus)
            {
                var course = new PrisonCourse { LockBonus = bonus }; course.Begin(1);
                float openedAt = -1, t = 0; course.CellOpened += _ => openedAt = t;
                course.Tick(Dt, Cell(0), 5f, false, 0f);
                for (; t < 20f && openedAt < 0; t += Dt) course.Tick(Dt, Cell(0), PrisonCourse.DoorZ, true, .5f);
                return openedAt;
            }
            float plain = OpenedAt(0f), skilled = OpenedAt(.3f);
            Assert.Greater(skilled, 0f); Assert.Less(skilled, plain * .8f, "Dexterity Lv3: 30% faster");
        }

        [Test] public void KeyboardFillsAtTheBaseRate()
        {
            var course = new PrisonCourse(); course.Begin(1);
            Hold(course, 0, true, -1f);
            Assert.IsTrue(course.Opened[0]);
        }

        [Test] public void StandingUnderTheLightRingsTheAlarmAndTheWardenChasesTheNextStairs()
        {
            var course = new PrisonCourse(); course.Begin(1);
            int alarmSet = -1; course.Alarm += s => alarmSet = s;
            course.Tick(Dt, Cell(0), 5f, false, 0f);
            // Crouched through the lead, then up while the light is on.
            for (float t = 0; t < PrisonCourse.LeadSeconds + .5f; t += Dt) course.Tick(Dt, Cell(0), PrisonCourse.DoorZ, true, .3f);
            Assert.AreEqual(PrisonPhase.Hold, course.Phase);
            for (int i = 0; i < 20 && alarmSet < 0; i++) course.Tick(Dt, Cell(0), PrisonCourse.DoorZ, false, 0f);
            Assert.AreEqual(0, alarmSet);
            Assert.AreEqual(1, course.Alarms); Assert.IsTrue(course.Spotted[0]); Assert.IsFalse(course.Opened[0]);
            Assert.AreEqual(0f, course.LockGauge, "the lock resets");
            Assert.AreEqual(PrisonPhase.Flat, course.Phase, "the runner walks on");
            Assert.AreEqual(1, course.ChasedSet);
            course.Tick(Dt, 3, 10f, false, 0f);
            Assert.IsTrue(course.WardenChasing);
            course.Tick(Dt, 6, 10f, false, 0f);
            Assert.IsFalse(course.WardenChasing, "only the next stairs");
        }

        [Test] public void StandingBeforeTheLightArrivesIsSafe()
        {
            var course = new PrisonCourse(); course.Begin(1);
            int alarms = 0; course.Alarm += _ => alarms++;
            course.Tick(Dt, Cell(0), 5f, false, 0f);
            for (float t = 0; t < PrisonCourse.LeadSeconds - .1f; t += Dt) course.Tick(Dt, Cell(0), PrisonCourse.DoorZ, false, 0f);
            Assert.AreEqual(0, alarms);
            // A single dropped frame under the light is no alarm either.
            for (float t = 0; t < .5f; t += Dt) course.Tick(Dt, Cell(0), PrisonCourse.DoorZ, true, 1f);
            course.Tick(Dt, Cell(0), PrisonCourse.DoorZ, false, 1f);
            course.Tick(Dt, Cell(0), PrisonCourse.DoorZ, true, 1f);
            Assert.AreEqual(0, alarms);
        }

        [Test] public void PerfectEscapeFreesOneMoreMonkeyOnTheRoof()
        {
            var course = new PrisonCourse(); course.Begin(1);
            int roof = 0; course.RoofRescue += () => roof++;
            for (int s = 0; s < PrisonCourse.Sets; s++) Hold(course, s, true, 1f);
            Assert.AreEqual(3, course.Rescued);
            course.Tick(Dt, 9, 1f, false, 0f); course.Tick(Dt, 10, 1f, false, 0f);
            Assert.AreEqual(1, roof); Assert.AreEqual(4, course.Rescued);
        }

        [Test] public void AnAlarmCostsTheRoofBonus()
        {
            var course = new PrisonCourse(); course.Begin(1);
            int roof = 0; course.RoofRescue += () => roof++;
            Hold(course, 0, true, 1f);
            Hold(course, 1, true, 1f);
            // Third set: the cell opens, then the runner stands up under the light.
            course.Tick(Dt, Cell(2), 5f, false, 0f);
            while (!course.Opened[2]) course.Tick(Dt, Cell(2), PrisonCourse.DoorZ, true, 1f);
            for (int i = 0; i < 20 && course.Phase == PrisonPhase.Hold; i++) course.Tick(Dt, Cell(2), PrisonCourse.DoorZ, false, 0f);
            Assert.AreEqual(1, course.Alarms); Assert.AreEqual(3, course.Rescued);
            Assert.AreEqual(-1, course.ChasedSet, "no stairs after the last set");
            course.Tick(Dt, 9, 1f, false, 0f);
            Assert.AreEqual(0, roof); Assert.AreEqual(3, course.Rescued);
        }

        [Test] public void SpeedsFollowThePhase()
        {
            var course = new PrisonCourse(); course.Begin(1);
            course.Tick(Dt, 0, 5f, false, 0f); Assert.AreEqual(PrisonCourse.StairSpeed, course.Speed);
            course.Tick(Dt, 2, 5f, false, 0f); Assert.AreEqual(PrisonCourse.FlatSpeed, course.Speed);
            course.Tick(Dt, 9, 5f, false, 0f); Assert.IsNull(course.Speed);
            course.Tick(Dt, -1, 5f, false, 0f); Assert.IsNull(course.Speed); Assert.AreEqual(PrisonPhase.None, course.Phase);
        }

        // ---- In a run (RunSession + RoutePlan) ----
        private const float L = RouteSurface.Length;
        private static RunSession PrisonRun()
        {
            var plan = new RoutePlan(new RunConfig(), 3);
            int lane = -1;
            for (int l = -1; l <= 1; l++) if (plan.DestinationFor(CartFork.FirstChunk, l) == 0) lane = l;
            plan.UpdateChoice(CartFork.Decision + 1, lane);
            RouteSurface.UsePlan(plan);
            var run = new RunSession(new RunConfig());
            run.Flow.Transition(GameFlowState.TitleIdle); run.Flow.Transition(GameFlowState.IntroTimeline);
            run.Flow.Transition(GameFlowState.FirstPersonTakeover); run.Flow.Transition(GameFlowState.Running);
            run.Runner.SetStartZ(CartFork.DestinationChunk * L + .5f);
            return run;
        }
        private static float PrisonEnd => (CartFork.DestinationChunk + PrisonCourse.Chunks.Length) * L;
        // Plays the prison with the keyboard: knee steps on the stairs (unless skipped), S at the doors (unless standing).
        private static void Play(RunSession run, System.Func<RunSession, bool> step, System.Func<RunSession, bool> hide, System.Action<RunSession> each = null)
        {
            for (int i = 0; i < 4000 && run.Runner.Z < PrisonEnd && run.Flow.State == GameFlowState.Running; i++)
            {
                if (StairJudge.StepDue(run.Runner) && step(run)) run.Runner.Command(MotionCommand.KneeStep);
                if (run.Prison.Holding && hide(run)) run.Runner.Command(MotionCommand.Crouch);
                run.Tick(Dt);
                each?.Invoke(run);
            }
        }

        [Test] public void KeyboardEscapeOpensEveryCellAndTheRoof()
        {
            try
            {
                var run = PrisonRun();
                int holds = 0; float heldAt = -1; bool moved = false;
                run.Prison.HoldStarted += _ => { holds++; heldAt = run.Runner.Z; };
                Play(run, _ => true, _ => true, r => { if (r.Prison.Holding && heldAt > 0 && System.Math.Abs(r.Runner.Z - heldAt) > .01f) moved = true; });
                Assert.AreEqual(GameFlowState.Running, run.Flow.State);
                Assert.GreaterOrEqual(run.Runner.Z, PrisonEnd);
                Assert.AreEqual(3, holds, "one hold per set");
                Assert.IsFalse(moved, "the runner stands still at the door");
                Assert.AreEqual(4, run.Prison.Rescued, "three cells and the roof");
                Assert.AreEqual(0, run.Warnings);
                // Roughly 3 x (10 s stairs + hold + flat) plus the roof.
                Assert.Greater(run.Elapsed, 45f); Assert.Less(run.Elapsed, 90f);
            }
            finally { RouteSurface.UsePlan(null); }
        }

        [Test] public void AlarmIsAWarningAndAMissedBeatOnTheChasedStairsIsACapture()
        {
            try
            {
                var run = PrisonRun();
                Play(run, r => !(r.Prison.Set == 1 && r.Prison.Phase == PrisonPhase.Stairs), r => r.Prison.Set != 0);
                Assert.AreEqual(1, run.Prison.Alarms);
                Assert.AreEqual(GameFlowState.Caught, run.Flow.State);
                Assert.AreEqual(ChaserKind.Warden, run.CaughtBy);
                Assert.AreEqual(1, run.Prison.Set, "caught on the chased stairs");
            }
            finally { RouteSurface.UsePlan(null); }
        }

        [Test] public void AnOrdinaryMissedBeatOnlySlows()
        {
            try
            {
                var run = PrisonRun();
                bool slowed = false;
                Play(run, r => r.Prison.Set != 0, _ => true, r => slowed |= r.Runner.Slowing && r.Prison.Phase == PrisonPhase.Stairs);
                Assert.IsTrue(slowed);
                Assert.AreEqual(GameFlowState.Running, run.Flow.State);
                Assert.AreEqual(0, run.Warnings);
                Assert.AreEqual(4, run.Prison.Rescued);
            }
            finally { RouteSurface.UsePlan(null); }
        }

        [Test] public void PrisonChunksHaveNoHazards()
        {
            var plan = new RoutePlan(new RunConfig(), 3);
            int lane = -1;
            for (int l = -1; l <= 1; l++) if (plan.DestinationFor(CartFork.FirstChunk, l) == 0) lane = l;
            plan.UpdateChoice(CartFork.Decision + 1, lane);
            for (int i = 0; i < PrisonCourse.Chunks.Length; i++)
            {
                Assert.AreEqual(i, PrisonCourse.BranchIndex(plan, CartFork.DestinationChunk + i));
                Assert.AreEqual(PrisonCourse.Chunks[i], plan.TypeAt(CartFork.DestinationChunk + i));
            }
            Assert.AreEqual(-1, PrisonCourse.BranchIndex(plan, CartFork.DestinationChunk - 1));
            Assert.AreEqual(-1, PrisonCourse.BranchIndex(plan, CartFork.DestinationChunk + PrisonCourse.Chunks.Length));
            var layout = new ObstacleLayout();
            var slots = new System.Collections.Generic.List<ObstacleSlot>();
            for (int i = 0; i < 6; i++) slots.Add(new ObstacleSlot(i < 3 ? ObstacleKind.Crate : ObstacleKind.Pipe, i % 3 - 1, i < 3 ? 7 : 19, true));
            layout.Plan(new System.Random(1), 4, 9, slots, new System.Collections.Generic.List<LanePlacement>(), new HazardOdds(1, 1, 1, 1), true, false);
            layout.ClearHazards();
            Assert.IsFalse(layout.Slots.Contains(true)); Assert.AreEqual(0, layout.Blockers.Count); Assert.AreEqual(0, layout.Bananas.Count); Assert.IsFalse(layout.Animal);
        }

        [Test] public void PrisonArtResolvesEveryPiece()
        {
            var art = Mongscape.Presentation.Map.PrisonArt.Load();
            Assert.IsNotNull(art, "Resources/" + Mongscape.Presentation.Map.PrisonArt.ResourcePath);
            Assert.IsNotNull(art.CellDoor); Assert.IsNotNull(art.Searchlight);
            Assert.IsNotNull(art.CellDoorMotion); Assert.IsNotNull(art.SearchlightMotion);
            Assert.IsNotNull(art.LockClick); Assert.IsNotNull(art.LockOpen); Assert.IsNotNull(art.MonkeyRescue);
        }

        [Test] public void BeginClearsTheVisit()
        {
            var course = new PrisonCourse(); course.Begin(1);
            Hold(course, 0, true, 1f);
            course.Begin(2);
            Assert.AreEqual(0, course.Rescued); Assert.IsFalse(course.Opened[0]); Assert.AreEqual(6f, course.HideSeconds);
            Assert.AreEqual(-1, course.ChasedSet);
        }
    }
}
