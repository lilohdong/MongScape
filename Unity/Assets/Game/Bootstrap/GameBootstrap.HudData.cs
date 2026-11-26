using Mongscape.Application;
using System.Collections;
using Mongscape.Content;
using Mongscape.Domain;
using Mongscape.Infrastructure;
using Mongscape.Presentation;
using Mongscape.Presentation.Map;
using UnityEngine;
using UnityEngine.Timeline;

namespace Mongscape.Bootstrap
{
    // What the HUD, settings window and account screens read from the game: one-line connections to the session, pose,
    // store, account flow and run results.
    public sealed partial class GameBootstrap
    {
        // IHudData
        PlayerProfile IHudData.Player => player;
        PlayerProfile IHudData.SavedProfile => Account.Profile;
        Texture IHudData.CameraTexture => Pose.CameraTexture;
        // Listed, or already delivering frames (a camera the driver listed late still counts).
        bool IHudData.CameraDetected => cameraDetected || Pose.CameraTexture != null && Pose.CameraTexture.width > 16;
        bool IHudData.BodyStylesAllowed => automated || userSettings == null || ((IHudData)this).CameraDetected;
        // Body styles need a connected camera, except in automated test/tool runs.
        private bool PlayableStyle(PlayStyle style)
        {
            if(!PlayStyleInfo.UsesCamera(style)) return true;
            if(!cameraDetected&&!automated) cameraDetected=WebCamTexture.devices.Length>0;
            return ((IHudData)this).BodyStylesAllowed;
        }
        PoseFrame IHudData.PoseFrame => Pose.LastFrame;
        PoseStatus IHudData.PoseStatus => Pose.Status;
        string IHudData.PoseMessage => Pose.Message;
        string IHudData.PoseDiagnostics => BuildInfo.DevelopmentTools && userSettings != null && userSettings.PoseDiagnostics ? Pose.Diagnostics : "";
        MotionReading IHudData.Motion => Pose.Reading;
        float IHudData.SecondsSinceJump => (float)(Time.realtimeSinceStartupAsDouble - Pose.LastJumpTime);
        float IHudData.SecondsSinceKneeStep => (float)(Time.realtimeSinceStartupAsDouble - Pose.LastKneeStepTime);
        float IHudData.CalibrationProgress => Pose.CalibrationProgress;
        bool IHudData.PoseKneesMissing => Pose.KneesMissing;
        float IHudData.StepsPerMinute => Pose.StepsPerMinute;
        CadenceStage IHudData.Cadence => Pose.CadenceStage;
        bool IHudData.PoseCalibrationMoving => Pose.CalibrationMoving;
        FramingIssue IHudData.PoseFraming => Pose.Framing;
        RunCalibrationState IHudData.RunCalibration => RunCalibration;
        bool IHudData.TutorialRun => Session.IsTutorial;
        bool IHudData.PoseRecording => Pose.Recording;
        bool IHudData.BodyActive => Pose.Tracking && Pose.Reading.Active;
        PlayerWallet IHudData.Wallet => Account.Wallet;
        Leaderboard IHudData.Board => Store.Board;
        PlayerStats IHudData.Stats => Store.Stats;
        RankingEntry IHudData.LastEntry => Results.LastEntry;
        // A guest's run was recorded as "게스트" and can still be named on the result screen.
        bool IHudData.CanNameLastRun => Results.CanNameLastRun;
        bool IHudData.NameLastRun(string name) => Results.NameLastRun(name);
        int IHudData.LastRank => Results.LastRank;
        int IHudData.LastTodayRank => Results.LastTodayRank;
        bool IHudData.LastNewBest => Results.LastNewBest;
        int IHudData.LastBestScore => Results.LastBestScore;
        StatsReply IHudData.LastStats => Results.LastStats;
        bool IHudData.LastStatsFailed => Results.LastStatsFailed;
        int IHudData.BestScore => Accounts.MemberBest(player.Style);
        bool IHudData.RunSaveFailed => Accounts.Runs.LastFailed;
        public float StartBanner => Session.Flow.State == GameFlowState.Running ? Mathf.Max(0, 2.2f - (Time.realtimeSinceStartup - runStartedAt)) : 0;
        float IHudData.CaughtFlash => Session.Flow.State == GameFlowState.Caught ? Mathf.Clamp01(1 - (Time.realtimeSinceStartup - caughtAt) / .6f) : 0;
        void IHudData.PreviewStyle(PlayStyle style) { menuCamera=false; player.Style = style; Pose.SetStyle(style); }
        // The member's nickname stays the server's; a guest's name lives in session memory only.
        void IHudData.SaveProfile(PlayerProfile profile)
        {
            if (profile == null) return;
            if (!Account.IsMember) Account.SetNickname(profile.Name);
            Accounts.SaveSettings(AccountSession.SettingsOf(profile));
            player=Account.Profile.Clone();
        }
        void IHudData.Recalibrate() => Pose.Recalibrate();
        bool IHudData.CadenceSpeed => userSettings == null || userSettings.CadenceSpeed;
        void IHudData.SetCadenceSpeed(bool on) { if (userSettings == null || userSettings.CadenceSpeed == on) return; userSettings.CadenceSpeed = on; UserSettingsStore.Save(userSettings); }
        RankRace IHudData.Race => Session.IsTutorial || !playerRun ? null : Results.Race;
        bool IHudData.HazardNear => Session.Flow.State == GameFlowState.Running && Map.HazardAhead(Session.Runner.Z, Mathf.Max(8f, Session.CurrentSpeed * 1.2f));
        float IHudData.PacePressure => Session.PacePressure;
        // Guests on one PC share an empty owner id, so a guest sees no course tip, no streak, and the minutes of this run only
        // instead of the PC's week (QA 2026-11-25 L3).
        ExerciseKind IHudData.Recommended => Account.IsMember ? Store.Exercise.Recommend(ExerciseOwner, System.DateTime.UtcNow) : ExerciseKind.Warmup;
        double IHudData.WeekMinutes => Account.IsMember ? Store.Exercise.WeekModerateMinutes(ExerciseOwner, System.DateTime.UtcNow) : Session.Workout.GoalSeconds / 60;
        int IHudData.Streak => Account.IsMember ? Store.Exercise.Streak(ExerciseOwner, System.DateTime.UtcNow) : 0;
        int IHudData.LastStreakCoins => Results.LastStreakCoins;
        bool IHudData.JointCompareOpen => Tutorial != null && Tutorial.ComparingJoints;

        // A new member (or back to a guest) replaces the player and the face badge; coin or setting updates do not.
        private void PlayerSwitched()
        {
            tutorialOffered = false;
            faceAvatar.Apply(Accounts.Portrait);
            Results = new RunResults(Accounts, () => Store);
            player = Account.Profile.Clone();
        }
        // After an expired session: the settings window closes and the login gate opens with the notice.
        private void ShowExpiredGate()
        {
            if (settingsWindow != null && settingsWindow.IsOpen) settingsWindow.Close(true);
            hud.OpenLogin();
        }

        // IIdentityData: the account flow does the work.
        void IHudData.Unlock(ItemKind kind, System.Action<bool, string> done) => Accounts.Unlock(kind, done);
        public bool IdentityBusy => Accounts.IdentityBusy;
        public bool SignedIn => Account.IsMember;
        public string IdentityMessage => Accounts.IdentityMessage;
        public Texture Portrait => Accounts.Portrait;
        public bool ServerChecking => Accounts.ServerChecking;
        public string ServerProblem => Accounts.ServerProblem;
        public string LoginNotice => Accounts.LoginNotice;
        public void CheckServer() => Accounts.CheckServer();
        public void CheckNickname(string name, System.Action<bool, string> done) => Accounts.CheckNickname(name, done);
        public void FaceLogin(System.Action<bool> done) => Accounts.FaceLogin(done);
        public void FaceEnroll(string nickname, AccountSettings settings, System.Action<bool> done) => Accounts.FaceEnroll(nickname, settings, done);
        public void FaceLogout(bool delete) => Accounts.FaceLogout(delete);
        // The player follows the saved profile once the server accepted it.
        public void SaveMemberProfile(string nickname, AccountSettings settings, System.Action<bool, string> done)
            => Accounts.SaveMemberProfile(nickname, settings, (ok, reason) => { if (ok) player = Account.Profile.Clone(); done?.Invoke(ok, reason); });
        public void MemberStats(System.Action<StatsReply> done) => Accounts.MemberStats(done);
        public void MemberLeaderboard(PlayStyle mode, bool today, System.Action<LeaderboardReply> done) => Accounts.MemberLeaderboard(mode, today, done);
        public void DeleteAccount(System.Action<bool, string> done) => Accounts.DeleteAccount(done);

        // ISettingsHost
        UserSettings ISettingsHost.Settings => userSettings;
        UserSettings ISettingsHost.Defaults => settingsDefaults;
        System.Collections.Generic.IReadOnlyList<string> ISettingsHost.CameraDevices => PoseTracker.CameraDevices();
        IHudData ISettingsHost.Hud => this;
        void ISettingsHost.RefreshCamera() => Pose.RefreshCamera();
        bool ISettingsHost.MemberSignedIn => Account.IsMember;
    }
}
