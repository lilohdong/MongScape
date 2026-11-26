using System.Collections;
using Mongscape.Bootstrap;
using Mongscape.Domain;
using Mongscape.Infrastructure;
using Mongscape.Presentation.Map;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Mongscape.Tests
{
    public sealed class ZooPlayModeTests
    {
        // Entering the zoo branch starts the chase: cut, then the run; the runner runs at the zoo base speed, the chunks carry
        // no random hazards, the fences and bars, the gate and the lion are placed, and the main camera never draws the lion.
        [UnityTest] public IEnumerator EnteringTheZooStartsTheChase()
        {
            yield return SceneManager.LoadSceneAsync("GameScene"); yield return null;
            var game = Object.FindFirstObjectByType<GameBootstrap>();
            game.Session.Config.ExtendedRoutes = true; game.Pose.SetStyle(PlayStyle.Keyboard); game.BeginRun("70");
            yield return new WaitUntil(() => !game.IsLoading && !game.IsRevealing);
            var plan = new RoutePlan(game.Session.Config, 723); ZooMapView.Register(plan);
            int lane = 0; for (int l = -1; l <= 1; l++) if (SectionDef.KindOfDestination(plan.DestinationFor(CartFork.FirstChunk, l)) == SectionKind.Zoo) lane = l;
            plan.UpdateChoice(CartFork.Decision + 1, lane);
            RouteSurface.UsePlan(plan); game.Session.Reset();
            float start = CartFork.DestinationChunk * RouteSurface.Length - 3f;
            game.Session.Runner.SetStartZ(start); game.Map.Reset(); game.Map.Tick(start);
            var zoo = game.GetComponent<ZooMapView>();
            Assert.IsNotNull(zoo);
            Assert.IsNotNull(ZooContent.Load().Music, "The zoo song is referenced");
            yield return new WaitUntil(() => zoo.Chase.Active || game.Session.Flow.State != GameFlowState.Running);
            Assert.IsTrue(zoo.Chase.Active, "The zoo chase starts at the zoo entrance");
            Assert.Greater(zoo.Chase.Obstacles.Count, 5, "Fences and bars are laid out");
            Assert.AreEqual(ZooChase.SpacingFor(1), zoo.Chase.Obstacles[1].Distance - zoo.Chase.Obstacles[0].Distance, ZooChase.SpacingFor(1) * .2f, "First visit is the widest spacing");
            Assert.IsTrue(GameObject.Find("Zoo_Lion") != null || GameObject.Find("Zoo_Note") != null, "The lion is placed");
            yield return new WaitUntil(() => zoo.Chase.Phase == ZooPhase.Song || !zoo.Chase.Active);
            Assert.AreEqual(ZooPhase.Song, zoo.Chase.Phase);
            // The art clips drive the lion's bones.
            yield return null;
            Assert.IsNotNull(zoo.Rig, "The lion is animated");
            Assert.AreEqual(LionAction.Run, zoo.Rig.Current);
            var bones = zoo.Rig.GetComponentsInChildren<Transform>(); var before = new Quaternion[bones.Length];
            for (int i = 0; i < bones.Length; i++) before[i] = bones[i].localRotation;
            yield return new WaitForSeconds(.2f);
            int moved = 0; for (int i = 1; i < bones.Length; i++) if (Quaternion.Angle(before[i], bones[i].localRotation) > 1f) moved++;
            Assert.Greater(moved, 3, "The run clip binds to the lion's bones");
            Assert.AreEqual(ZooMapView.BaseSpeed, game.Session.CurrentSpeed, 1f, "The runner runs at the zoo base speed");
            Assert.AreEqual(0, game.OutputCamera.cullingMask & (1 << ZooMapView.LionLayer), "The main camera never draws the lion");
            Assert.AreEqual(0, game.Session.Runner.Lane);
            foreach (var active in game.Map.Active)
            {
                var span = SectionSpan.At(plan, active.Chunk.Sequence);
                if (span.Kind != SectionKind.Zoo || !span.Branch) continue;
                foreach (var obstacle in active.Chunk.Obstacles) Assert.IsFalse(obstacle.gameObject.activeSelf, "No random hazards in the zoo");
            }
            Assert.AreEqual(1, game.Session.Sections.Visits(SectionKind.Zoo));
        }
    }
}
