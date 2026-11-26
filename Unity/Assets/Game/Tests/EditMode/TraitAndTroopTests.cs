using System.Linq;
using Mongscape.Application;
using Mongscape.Domain;
using NUnit.Framework;

namespace Mongscape.Tests
{
    // Plan 4 and 4-1: rescued monkeys are the currency, traits replace the shop.
    public sealed class TraitAndTroopTests
    {
        private static RunSession Running(RunTraits traits = null)
        {
            var run = new RunSession(new RunConfig());
            run.Flow.Transition(GameFlowState.TitleIdle); run.Flow.Transition(GameFlowState.LoadingIntro); run.Flow.Transition(GameFlowState.Running);
            if (traits != null) { run.ApplyTraits(traits); run.Reset(); }
            return run;
        }
        private static RunTraits With(params (TraitId id, int level)[] owned)
        {
            var levels = new TraitLevels(); foreach (var (id, level) in owned) levels.Set(id, level); return levels.ForRun();
        }
        [TearDown] public void RestoreOfflinePrices() { TraitCatalog.ApplyServerCosts((int)TraitId.Shield, new[] { 5, 15, 30 }); }

        [Test] public void CatalogMatchesThePlanTable()
        {
            Assert.AreEqual(7, TraitCatalog.All.Length);
            Assert.AreEqual(225, TraitCatalog.All.Sum(t => t.Costs.Sum()), "Plan 4-1: 225 monkeys for everything");
            CollectionAssert.AreEqual(new[] { 1, 2, 3, 4, 5, 6, 7 }, TraitCatalog.All.Select(t => (int)t.Id).ToArray(), "Ids match the server's TRAITS");
            Assert.AreEqual(3, TraitCatalog.Get(TraitId.Dexterity).MaxLevel); Assert.AreEqual(1, TraitCatalog.Get(TraitId.Loyalty).MaxLevel);
            Assert.AreEqual(-1, TraitCatalog.Get(TraitId.ThickFur).Cost(2));
            foreach (var trait in TraitCatalog.All) foreach (var text in trait.LevelTexts) StringAssert.DoesNotContain("·", text);
        }

        [Test] public void ServerPricesReplaceTheOfflineCopy()
        {
            var reply = new TraitsReply { ok = true, traits = new[] { new TraitRow { id = 1, key = "shield", costs = new[] { 7, 17, 37 } }, new TraitRow { id = 42, costs = new[] { 1 } } } };
            Assert.IsTrue(reply.ApplyCosts());
            Assert.AreEqual(7, TraitCatalog.Get(TraitId.Shield).Cost(1)); Assert.AreEqual(37, TraitCatalog.Get(TraitId.Shield).Cost(3));
            Assert.IsFalse(new TraitsReply().ApplyCosts(), "A failed reply keeps the copy");
            TraitCatalog.ApplyServerCosts(1, new[] { -1 }); Assert.AreEqual(7, TraitCatalog.Get(TraitId.Shield).Cost(1), "Negative prices are refused");
        }

        [Test] public void LevelsComeFromUnlockedCodesWithoutGaps()
        {
            var levels = new TraitLevels();
            levels.Restore(new[] { 0, 1, 2, 11, 12, 13, 22, 51, 99, 71, 72 });
            Assert.AreEqual(3, levels.Level(TraitId.Shield)); Assert.IsTrue(levels.IsMaxed(TraitId.Shield));
            Assert.AreEqual(0, levels.Level(TraitId.SharpEars), "Level 2 without level 1 does not count");
            Assert.AreEqual(1, levels.Level(TraitId.Dexterity)); Assert.AreEqual(20, levels.NextCost(TraitId.Dexterity));
            Assert.AreEqual(2, levels.Level(TraitId.Sweat)); Assert.AreEqual(-1, levels.NextCost(TraitId.Sweat));
            Assert.AreEqual(0, levels.Level(TraitId.Loyalty));
        }

        [Test] public void RunTraitEffectValues()
        {
            Assert.IsFalse(RunTraits.None.ShieldItems); Assert.IsFalse(RunTraits.None.CallItems); Assert.AreEqual(0, RunTraits.None.WarningLeadSeconds);
            var all = With((TraitId.Shield, 3), (TraitId.SharpEars, 2), (TraitId.ThickFur, 1), (TraitId.Call, 1), (TraitId.Dexterity, 3), (TraitId.Loyalty, 1), (TraitId.Sweat, 1));
            Assert.IsTrue(all.ShieldItems); Assert.AreEqual(2f, all.ShieldWeight); Assert.IsTrue(all.StartsWithShield);
            Assert.AreEqual(.6f, all.WarningLeadSeconds, 1e-5); Assert.IsTrue(all.ThickFur); Assert.AreEqual(6f, all.CallSeconds);
            Assert.AreEqual(.3f, all.LockGaugeBonus, 1e-5); Assert.IsTrue(all.Loyalty);
            Assert.AreEqual(0, RunTraits.SweatMonkeysAt(1, 299)); Assert.AreEqual(1, RunTraits.SweatMonkeysAt(1, 300)); Assert.AreEqual(1, RunTraits.SweatMonkeysAt(1, 900));
            Assert.AreEqual(1, RunTraits.SweatMonkeysAt(2, 599)); Assert.AreEqual(3, RunTraits.SweatMonkeysAt(2, 600)); Assert.AreEqual(0, RunTraits.SweatMonkeysAt(0, 900));
            CollectionAssert.AreEqual(new[] { ItemKind.Call, ItemKind.Shield, ItemKind.Shield }, ItemCatalog.Available(all));
            CollectionAssert.IsEmpty(ItemCatalog.Available(RunTraits.None));
        }

        [Test] public void TroopShowsFiveAndCountsTheRest()
        {
            var troop = new MonkeyTroop();
            troop.Rescue(RescueSource.MineWall); troop.Rescue(RescueSource.PrisonLock, 6); int version = troop.Version;
            Assert.AreEqual(7, troop.Rescued); Assert.AreEqual(5, troop.Visible); Assert.AreEqual(2, troop.Hidden); Assert.AreEqual(6, troop.FromSource(RescueSource.PrisonLock));
            Assert.IsTrue(troop.LoseOne()); Assert.AreEqual(6, troop.Kept); Assert.AreEqual(1, troop.Hidden); Assert.IsTrue(troop.LastWasLoss); Assert.AreNotEqual(version, troop.Version);
            troop.Reset(); Assert.AreEqual(0, troop.Kept); Assert.IsFalse(troop.LoseOne());
        }

        [Test] public void RescuesCountOnlyInARunningRunAndNotInTheTutorial()
        {
            var run = Running();
            Assert.IsTrue(run.Rescue(RescueSource.MineWall)); Assert.IsTrue(run.Rescue(RescueSource.Call));
            Assert.AreEqual(2, run.MonkeysEarned); Assert.AreEqual(0, run.Score.Score - (int)run.Score.Distance, "Monkeys never score");
            var tutorial = new RunSession(new RunConfig()) { IsTutorial = true };
            tutorial.Flow.Transition(GameFlowState.TitleIdle); tutorial.Flow.Transition(GameFlowState.LoadingIntro); tutorial.Flow.Transition(GameFlowState.Running);
            Assert.IsFalse(tutorial.Rescue(RescueSource.MineWall)); Assert.AreEqual(0, tutorial.MonkeysEarned);
            run.Reset(); Assert.AreEqual(0, run.Troop.Rescued, "A new run starts without a troop");
        }

        [Test] public void LoyaltyGivesUpOneMonkeyForOneWarningPerRun()
        {
            var run = Running(With((TraitId.Loyalty, 1)));
            Assert.IsTrue(run.Warn(WarningCause.Collision), "Nobody follows yet: the warning lands"); Assert.AreEqual(1, run.Warnings);
            run.Reset(); run.Rescue(RescueSource.MineWall, 2);
            Assert.IsFalse(run.Warn(WarningCause.Collision)); Assert.AreEqual(0, run.Warnings); Assert.AreEqual(1, run.Troop.Kept); Assert.AreEqual(1, run.LoyaltySaves);
            Assert.IsTrue(run.Warn(WarningCause.Collision), "Once per run"); Assert.AreEqual(1, run.Troop.Kept);
            Assert.AreEqual(1, run.MonkeysEarned, "The monkey taken away is not brought home");
        }

        [Test] public void ShieldGoesBeforeLoyalty()
        {
            var run = Running(With((TraitId.Loyalty, 1))); run.Rescue(RescueSource.MineWall);
            run.Items.Activate(ItemKind.Shield);
            Assert.IsFalse(run.Warn(WarningCause.Banana)); Assert.AreEqual(1, run.ShieldSaves); Assert.AreEqual(1, run.Troop.Kept);
            Assert.IsFalse(run.Warn(WarningCause.Banana)); Assert.AreEqual(0, run.Troop.Kept);
        }

        [Test] public void ThickFurKeepsTheRunnerOnItsFeetForTheFirstWarning()
        {
            var run = Running(With((TraitId.ThickFur, 1)));
            Assert.IsTrue(run.Slip(SlipSource.Banana)); Assert.AreEqual(1, run.Warnings); Assert.IsTrue(run.SoftWarning); Assert.IsFalse(run.Runner.Slowing);
            var plain = Running(); plain.Slip(SlipSource.Banana); Assert.IsTrue(plain.Runner.Slowing);
        }

        [Test] public void ShieldLevelThreeStartsHoldingOne()
        {
            var run = Running(With((TraitId.Shield, 3)));
            Assert.IsTrue(run.Items.IsActive(ItemKind.Shield)); Assert.AreEqual(0, run.Items.Collected, "Not a pickup");
            run.Warn(WarningCause.Collision); Assert.IsFalse(run.Items.IsActive(ItemKind.Shield));
            run.Reset(); Assert.IsTrue(run.Items.IsActive(ItemKind.Shield), "Every run starts with it");
            Assert.IsFalse(Running(With((TraitId.Shield, 2))).Items.IsActive(ItemKind.Shield));
        }

        [Test] public void SharpEarsCueThePoliceGaugeEarlier()
        {
            var plain = Running(); var eared = Running(With((TraitId.SharpEars, 2)));
            Assert.AreEqual(RunSession.CueSeconds, plain.WarningCueSeconds, 1e-5); Assert.AreEqual(RunSession.CueSeconds + .6f, eared.WarningCueSeconds, 1e-5);
            // The pace gauge left one second from full at its rise rate: inside the eared window, outside the plain one.
            float pressure = 1f - CadencePace.PressureRise * 1f;
            Assert.IsFalse(Soon(plain, pressure)); Assert.IsTrue(Soon(eared, pressure));
        }
        private static bool Soon(RunSession run, float pressure)
        {
            var pace = new CadencePace(); var field = typeof(CadencePace).GetProperty("Pressure");
            field.SetValue(pace, pressure * .9f); run.ApplyPace(pace);
            field.SetValue(pace, pressure); run.ApplyPace(pace);
            return run.WarningSoon;
        }

        [Test] public void SharpEarsLengthenTheSearchlightApproach()
        {
            var course = new PrisonCourse { LeadBonus = .6f };
            Assert.AreEqual(PrisonCourse.LeadSeconds + .6f, course.Lead, 1e-5);
            Assert.AreEqual(course.Lead + course.HideSeconds, course.HoldSeconds, 1e-5);
        }

        [Test] public void SweatAddsMonkeysForModerateTime()
        {
            var run = Running(With((TraitId.Sweat, 2)));
            Assert.AreEqual(0, run.MonkeysEarned);
            run.Rescue(RescueSource.PrisonLock, 2);
            Assert.AreEqual(2, run.MonkeysEarned);
            Assert.AreEqual(3, run.Traits.SweatMonkeys(600));
        }

        [Test] public void RunRecordSendsTheMonkeysAsCoins()
        {
            var run = Running(); run.Rescue(RescueSource.MineWall, 4);
            var record = AccountRun.Create(run, PlayStyle.Keyboard, run.MonkeysEarned);
            Assert.AreEqual(4, record.coins);
            Assert.AreEqual(AccountRun.MaxCoins, AccountRun.Create(run, PlayStyle.Keyboard, 99999).coins);
        }
    }
}
