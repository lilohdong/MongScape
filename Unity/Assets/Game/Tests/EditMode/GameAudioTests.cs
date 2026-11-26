using Mongscape.Domain;
using Mongscape.Presentation;
using NUnit.Framework;

namespace Mongscape.Tests
{
    public sealed class GameAudioTests
    {
        // Kenney CC0 effects that replace the synthesised tones (Content/Audio/README.md lists the sources).
        [TestCase("jump")] [TestCase("land_0")] [TestCase("land_1")] [TestCase("land_2")] [TestCase("hit")]
        [TestCase("bonk_0")] [TestCase("bonk_1")] [TestCase("bonk_2")] [TestCase("cue")] [TestCase("count_beep")] [TestCase("count_go")]
        [TestCase("footstep_0")] [TestCase("footstep_1")] [TestCase("footstep_2")] [TestCase("footstep_3")] [TestCase("footstep_4")]
        [TestCase("slip")] [TestCase("item")] [TestCase("shield")] [TestCase("lion_roar")] [TestCase("alarm_bell")]
        [TestCase("warning")] [TestCase("caught")] [TestCase("new_record")] [TestCase("fork_blocked")] [TestCase("gate_down")]
        [TestCase("monkey_rescue")] [TestCase("lock_click")] [TestCase("lock_open")]
        public void EffectFileIsPresent(string name) { Assert.IsTrue(GameAudio.HasEffectFile(name), name); }

        [Test] public void MusicLibraryHasEveryLoop() { Assert.IsTrue(MusicPlayer.Complete(MusicLibrary.Load())); }

        private static MusicTarget Decide(GameFlowState state, SectionKind section = SectionKind.Mine, PrisonPhase phase = PrisonPhase.None,
            bool warden = false, float pressure = 0f, bool mapMusic = false, bool wasHiding = false, bool paused = false)
            => MusicPlayer.Decide(state, paused, section, phase, warden, pressure, mapMusic, wasHiding);

        [Test] public void ScreensPlayMenuAndResult()
        {
            Assert.AreEqual(MusicSong.Menu, Decide(GameFlowState.TitleIdle).Song);
            var result = Decide(GameFlowState.Result);
            Assert.AreEqual(MusicSong.Result, result.Song); Assert.AreEqual(1f, result.L0); Assert.AreEqual(1f, result.L1);
        }

        [Test] public void MineTensionFollowsThePoliceGauge()
        {
            Assert.AreEqual(0f, Decide(GameFlowState.Running, pressure: .2f).L1);
            Assert.AreEqual(.5f, Decide(GameFlowState.Running, pressure: .55f).L1, .01f);
            var chase = Decide(GameFlowState.Running, pressure: 1f);
            Assert.AreEqual(MusicSong.Mine, chase.Song); Assert.AreEqual(1f, chase.L0); Assert.AreEqual(1f, chase.L1);
        }

        [Test] public void PrisonLayersFollowTheSetPhases()
        {
            var stairs = Decide(GameFlowState.Running, SectionKind.Prison, PrisonPhase.Stairs);
            Assert.AreEqual(MusicSong.Prison, stairs.Song);
            Assert.AreEqual(new[] { 1f, 1f, 0f, 0f }, new[] { stairs.L0, stairs.L1, stairs.L2, stairs.L3 });
            var chase = Decide(GameFlowState.Running, SectionKind.Prison, PrisonPhase.Stairs, warden: true);
            Assert.AreEqual(new[] { 1f, 1f, 1f, 0f }, new[] { chase.L0, chase.L1, chase.L2, chase.L3 });
            var hide = Decide(GameFlowState.Running, SectionKind.Prison, PrisonPhase.Hold);
            Assert.AreEqual(new[] { 0f, 0f, 0f, 1f }, new[] { hide.L0, hide.L1, hide.L2, hide.L3 });
            Assert.AreEqual(MusicPlayer.HideIn, hide.Fade);
            var flat = Decide(GameFlowState.Running, SectionKind.Prison, PrisonPhase.Flat, wasHiding: true);
            Assert.AreEqual(new[] { 1f, 0f, 0f, 0f }, new[] { flat.L0, flat.L1, flat.L2, flat.L3 });
            Assert.AreEqual(MusicPlayer.HideOut, flat.Fade);
        }

        [Test] public void ZooSongSilencesTheLoops() { Assert.AreEqual(MusicSong.None, Decide(GameFlowState.Running, SectionKind.Zoo, mapMusic: true).Song); }

        [Test] public void MissingEffectFallsBackToSynthesis() { Assert.IsFalse(GameAudio.HasEffectFile("no_such_effect")); }

        [Test] public void SlipRemembersItsSource()
        {
            RouteSurface.UsePlan(null);
            var run = new RunSession(new RunConfig());
            run.Flow.Transition(GameFlowState.TitleIdle); run.Flow.Transition(GameFlowState.IntroTimeline);
            run.Flow.Transition(GameFlowState.FirstPersonTakeover); run.Flow.Transition(GameFlowState.Running);
            Assert.IsTrue(run.Slip(SlipSource.KneeMiss)); Assert.AreEqual(SlipSource.KneeMiss, run.LastSlipSource);
            Assert.IsTrue(run.Slip(SlipSource.Banana)); Assert.AreEqual(SlipSource.Banana, run.LastSlipSource);
        }
    }
}
