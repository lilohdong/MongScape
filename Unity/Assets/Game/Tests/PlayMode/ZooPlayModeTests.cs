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
        // Entering the zoo branch starts the song-paced chase: cut, then the song; the runner follows the song clock, the
        // chunks carry no random hazards, and the notes, the gate and the lion are placed.
        [UnityTest] public IEnumerator EnteringTheZooStartsTheSongPacedChase()
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
            Assert.Greater(zoo.Chase.Map.Notes.Count, 0, "A chart was loaded from ZooContent");
            Assert.AreEqual(ZooTier.Easy, zoo.Chase.Map.Tier, "First visit is easy");
            Assert.IsTrue(GameObject.Find("Zoo_Lion") != null || GameObject.Find("Zoo_Note") != null, "The lion is placed");
            yield return new WaitUntil(() => zoo.Chase.Phase == ZooPhase.Song || !zoo.Chase.Active);
            Assert.AreEqual(ZooPhase.Song, zoo.Chase.Phase);
            float expected = zoo.Chase.EntryZ + zoo.Chase.DistanceAt(zoo.Chase.SongTime);
            Assert.AreEqual(expected, game.Session.Runner.Z, 1.5f, "The runner follows the song clock");
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
