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
        [TestCase("slip")] [TestCase("item")] [TestCase("shield")]
        public void EffectFileIsPresent(string name) { Assert.IsTrue(GameAudio.HasEffectFile(name), name); }

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
