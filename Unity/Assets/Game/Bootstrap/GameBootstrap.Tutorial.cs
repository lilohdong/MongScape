using Mongscape.Domain;
using Mongscape.Infrastructure;
using Mongscape.Presentation;
using UnityEngine;

namespace Mongscape.Bootstrap
{
    // The tutorial's connection to the game (plan 6): which chapter runs, who has finished which chapter, and the offers.
    public sealed partial class GameBootstrap
    {
        private bool tutorialOffered;
        private TutorialChapter tutorialChapter;
        // Finished chapters are saved on this PC per owner (the member's account id, or "guest") and on the member's account;
        // a new PC reads them from the account.
        private string TutorialOwner => Account.IsMember ? Account.Id : TutorialProgress.GuestOwner;
        private int TutorialMask
        {
            get
            {
                int mask = Store != null ? Store.TutorialChapters(TutorialOwner) : 0;
                // A member's account carries the chapters done on any PC (chapter n done means 1..n are).
                return Account.IsMember ? mask | ((1 << Account.TutorialChapters) - 1) : mask;
            }
        }
        private PoseFrame TrackedFrame() => Pose.UsesCamera && Pose.Tracking ? Pose.LastFrame : null;
        private void SetupTutorial()
        {
            Tutorial = gameObject.AddComponent<TutorialDirector>();
            Tutorial.Framed = () => Pose.Framing == FramingIssue.Ok;
            Tutorial.NoCamera = () => Pose.Status == PoseStatus.Off || Pose.Status == PoseStatus.NoCamera || Pose.Status == PoseStatus.NoModel;
            Tutorial.MyPose = TrackedFrame; Tutorial.MyBaseline = () => Pose.Baseline; Tutorial.Tuning = () => Pose.Tuning;
            Tutorial.StepsPerMinute = () => Pose.StepsPerMinute; Tutorial.PaceScale = () => pace.Scale;
            Tutorial.RunCadence = Settings != null ? Settings.Motion.RunCadence : 100f;
            Tutorial.SyncMs = () => userSettings != null ? Mathf.RoundToInt(userSettings.ZooSyncSeconds(Pose.UsesCamera) * 1000f) : 0;
            Tutorial.SaveSync = ms => { if (userSettings == null) return; userSettings.SyncOffsetMs = ms; userSettings.SyncMeasured = true; UserSettingsStore.Save(userSettings); };
            Tutorial.Demo = gameObject.AddComponent<TutorialDemo>(); Tutorial.Demo.Configure(MonkeyPrefab);
            Tutorial.Configure(Session, Map, () => Pose.Status == PoseStatus.Tracking || Pose.Status == PoseStatus.Off || Pose.Status == PoseStatus.NoCamera || Pose.Status == PoseStatus.NoModel,
                RestartTutorial, FinishTutorial, () => !hud.Hidden && !hud.Loading && !hud.Revealing && RunCalibration == RunCalibrationState.None);
        }
        // Wired after the audio exists: the beat tick and the cleared-station sound.
        private void SetupTutorialAudio() { Tutorial.Beat = () => audioPlayer?.Countdown(false); Tutorial.Cleared = () => audioPlayer?.Cue(); }

        // One chapter (chosen on the chapter screen, in the player's play style): the same scene and map on a fixed lesson course.
        // It never reaches the result screen, so nothing goes to the ranking, coins or calories. A visit is one chapter.
        public void StartTutorial(TutorialChapter chapter, bool shortVersion = false)
        {
            if (Session.Flow.State != GameFlowState.TitleIdle || hud.Loading) return;
            tutorialOffered = true; playerRun = false; tutorialChapter = chapter; KeyboardGameInput.SetTextInput(false);
            if (!PlayableStyle(player.Style)) player.Style = PlayStyle.Keyboard;
            Session.Workout.SetWeight(""); Session.Workout.Met = PlayStyleInfo.Met(player.Style); Session.Workout.Style = player.Style;
            menuCamera = false; Pose.SetStyle(player.Style);
            cadenceRun = PlayStyleInfo.UsesCamera(player.Style) && userSettings.CadenceSpeed; Results.ClearRace(); Session.Workout.CadenceCounts = userSettings.CadenceSpeed;
            Session.IsTutorial = true; Tutorial.Begin(chapter, player.Style, Session.Config.ExtendedRoutes, userSettings.CadenceSpeed, shortVersion);
            StartCoroutine(PrepareRun(TutorialCourse.Seed));
        }
        // Everyone who has not finished the basics chapter is asked once per session (guests: "튜토리얼 할까요?", can skip).
        // Members who finished the earlier tutorial are offered the short basics chapter once, remembered on this PC.
        private TutorialOffer OfferTutorial()
        {
            int mask = TutorialMask;
            if (TutorialScript.ShouldOffer(TutorialProgress.IsDone(mask, TutorialChapter.Basics), tutorialOffered)) { tutorialOffered = true; return TutorialOffer.Full; }
            bool seen = Account.IsMember && userSettings.UpdatedTutorialSeen.Contains(Account.Id);
            if (!TutorialScript.ShouldOfferUpdate(Account.IsMember, Account.TutorialDone, seen, tutorialOffered)) return TutorialOffer.None;
            tutorialOffered = true; userSettings.UpdatedTutorialSeen.Add(Account.Id); UserSettingsStore.Save(userSettings); return TutorialOffer.Short;
        }
        // After a mistake: a fresh course, started at the given route z in front of the step.
        private void RestartTutorial(float z)
        {
            RouteSurface.UsePlan(TutorialCourse.Plan(Session.Config, tutorialChapter));
            Session.Reset(); Session.Runner.SetStartZ(z); keyboard.Clear(); poseInput.Clear(); Pose.ResetCommands();
            Map.Reset(); Map.Tick(z); Map.AnimateGates(z);
            stairFx.Reset(); guardDistance = 15; previousStumble = false; Hero.Apply(Session.Runner); CameraRig.Takeover(Hero.Head);
        }
        // The chapter is saved (this PC, and the account for a member's basics); then back to the main menu.
        private void FinishTutorial()
        {
            Store.MarkTutorial(TutorialOwner, tutorialChapter);
            Accounts.MarkTutorialChapter(tutorialChapter);
            ExitToMenu();
        }
    }
}
