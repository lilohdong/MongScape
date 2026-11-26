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
    public sealed partial class GameBootstrap : MonoBehaviour, IHudData, ISettingsHost, IIdentityData
    {
        public GameSettings Settings;
        public MongscapeMapChunk[] ChunkPrefabs;
        public GameObject MonkeyPrefab, PolicePrefab, CartPrefab, PickaxePrefab, IdeaPrefab, DustPrefab, HitPrefab;
        public FirstPersonCameraRig CameraRig;
        public Camera OutputCamera;
        public TimelineAsset IntroTimeline;
        public GameObject HandsPrefab;
        public Texture2D TitleLogo;
        public Texture2D LoadingImage;
        public GameObject CartRidePrefab;
        public bool StartSetupOpen=>hud!=null&&hud.SetupOpen;
        public bool IsLoading=>hud!=null&&hud.Loading;
        public bool IsRevealing=>hud!=null&&hud.Revealing;
        public RunSession Session { get; private set; }
        public ChunkSpawner Map { get; private set; }
        public IntroSequenceDirector Intro { get; private set; }
        public PoseTracker Pose { get; private set; }
        public PlayerProgressStore Store { get; private set; }
        public RunnerView Hero { get; private set; }
        private RunnerView guard;
        private KeyboardGameInput keyboard;
        private PoseGameInput poseInput;
        private InputRouter router;
        private HUDPresenter hud;
        private GameAudio audioPlayer;
        private readonly StairCameraFx stairFx = new StairCameraFx();
        public StairCameraFx StairFx => stairFx;
        private float caughtTime, runFade, runStartedAt = -10, caughtAt = -10;
        private ParticleSystem dust, hit;
        private bool previousStumble;
        // Counts steps, jumps, crouches, knee steps and lane changes of the run for the result report.
        private readonly MoveTally moveTally = new MoveTally();
        // Cadence -> runner speed and police (exercise plan 4); on for camera runs with the setup option on.
        private readonly CadencePace pace = new CadencePace();
        private bool cadenceRun;
        // Big movements (exercise plan 6).
        private int seenBigJumps, seenBigKnees, seenBigLanes;
        private bool wasDeep;
        private double bigUntil = -10, deepSince = -10;
        // Exercise plan 8: active seconds per course this run (for the weekly balance) and the streak coins of the last run.
        private readonly double[] kindSeconds = new double[4];
        // Account and face login (AccountFlow); the account is the only source of the current player.
        public AccountFlow Accounts { get; private set; }
        public AccountSession Account => Accounts?.Account;
        // Run settings of the current or next run, taken from the account.
        // Runs started from the setup screen are recorded on the leaderboard.
        private PlayerProfile player = new PlayerProfile();
        public const string ExpiredNotice = AccountFlow.ExpiredNotice;
        private bool playerRun, bannerSounded, goSounded;
        // The end of a run (ranking, exercise record, member save) and the running ranking strip.
        public RunResults Results { get; private set; }
        private UserSettings userSettings, settingsDefaults;
        private GraphicsSettingsApplier graphics;
        private SettingsWindow settingsWindow;
        private PerformanceOverlay overlay;
        private FaceAvatarView faceAvatar;
        public PoliceCaptureSequence Capture { get; private set; }
        public PolicePursuitCue PursuitCue { get; private set; }
        private float guardDistance=15;
        private bool automated;
        private readonly AdaptivePerformance adaptive = new AdaptivePerformance();
        private UserSettings effectiveGraphics;
        private bool menuCamera;
        private PlayStyle cameraBeforeMenu;
        // Whether any webcam is plugged in; re-read every few seconds outside runs (never during play, enumeration can stall a frame).
        private bool cameraDetected, wasMenu;
        private double nextCameraScan;
        // Tutorial lessons on the ordinary run; offered once per session on "게임 시작".
        public TutorialDirector Tutorial { get; private set; }
        private bool tutorialOffered;
        private void Awake()
        {
            var flags=System.Environment.GetCommandLineArgs();
            automated=System.Array.IndexOf(flags,"--mongscape-smoke")>=0||System.Array.IndexOf(flags,"--mongscape-uicapture")>=0
                ||System.Array.IndexOf(flags,"--mongscape-review")>=0||System.Array.IndexOf(flags,"--mongscape-tests")>=0
                ||System.Array.IndexOf(flags,"--mongscape-capture")>=0||System.Array.IndexOf(flags,"--mongscape-recording")>=0
                ||System.Array.IndexOf(flags,"--mongscape-exercise-review")>=0;
            Session = new RunSession(Settings != null ? JsonUtility.FromJson<RunConfig>(JsonUtility.ToJson(Settings.Run)) : new RunConfig());
            if (ChunkPrefabs == null || ChunkPrefabs.Length < 5 || CameraRig == null || OutputCamera == null)
            { Debug.LogError("GameScene requires content references. Run Mongscape/Build GameScene."); enabled = false; return; }
            keyboard = new KeyboardGameInput(); poseInput = new PoseGameInput(); router = new InputRouter(Session, keyboard, poseInput);
            Pose = new PoseTracker(poseInput, Settings != null ? Settings.Camera : null, Settings != null ? Settings.Motion : null, () => Session.Runner.Lane, () => Session.Runner.Riding&&!(RouteSurface.ActivePlan?.ForkWindow(Session.Runner.Z)??false), () => Session.Runner.Riding, OnOrBeforeStairs);
            Store = new PlayerProgressStore(DataRoot());
            Accounts = new AccountFlow(gameObject.AddComponent<LocalProfileClient>(), () => Pose.CameraTexture, () => Store,
                () => Session.Flow.State == GameFlowState.TitleIdle && !hud.Loading, ShowExpiredGate);
            Results = new RunResults(Accounts, () => Store);
            player = Account.Profile.Clone();
            Hero = CreateActor("CHR_Monkey_Prisoner", MonkeyPrefab);
            guard = CreateActor("CHR_Police_Officer", PolicePrefab);
            Capture=gameObject.AddComponent<PoliceCaptureSequence>();Capture.Configure(Hero,guard);
            PursuitCue=gameObject.AddComponent<PolicePursuitCue>();PursuitCue.Configure(Session);
            faceAvatar=Hero.gameObject.AddComponent<FaceAvatarView>();
            Accounts.PlayerSwitched+=PlayerSwitched;
            gameObject.AddComponent<CartRideView>().Configure(Session,Hero,CartRidePrefab);
            gameObject.AddComponent<PowerUpAuraView>().Configure(Session,Hero);
            OutputCamera.GetComponent<CinematicFocus>()?.Configure(Session,Hero);
            var mapRoot = new GameObject("ChunkPool"); mapRoot.transform.SetParent(transform, false);
            Map = new ChunkSpawner(ChunkPrefabs, mapRoot.transform, Session, Settings != null ? Settings.Render : new RenderBudget());
            Map.Decorator = new TutorialLayout(new RuntimeContent(mapRoot.transform, RunItems, Settings != null ? Settings.ItemChancePerChunk : .35f), Session);
            // Right before a duck row (about 1.2 s ahead) the jump line rises, so a running bounce cannot jump into the bar.
            Pose.DuckAhead = () => Session.Flow.State == GameFlowState.Running && !Session.Runner.Riding
                && Map.ActionAhead(HazardAction.Crouch, Session.Runner.Z, Mathf.Max(6f, Session.CurrentSpeed * 1.2f));
            CameraRig.Configure(OutputCamera);
            if(OutputCamera.GetComponent<GameViewport>()==null) OutputCamera.gameObject.AddComponent<GameViewport>();
            if (HandsPrefab != null)
            {
                var hands = Instantiate(HandsPrefab, OutputCamera.transform);
                hands.GetComponent<FirstPersonHands>().Configure(Session);
            }
            var cart = CreateProp("PROP_MineCart", CartPrefab, transform);
            var tool = CreateProp("PROP_Pickaxe", PickaxePrefab, Hero.ToolAnchor);
            tool.transform.localPosition = Hero.Humanoid ? Vector3.zero : new Vector3(.05f, -.35f, .35f);
            if(Hero.Humanoid) { tool.transform.localRotation=Quaternion.Euler(0,0,-90); tool.transform.localScale=Vector3.one*.75f; }
            var bulb = CreateProp("VFX_LightBulb_Idea", IdeaPrefab, Hero.transform); bulb.transform.localPosition = new Vector3(0, 2.4f, 0);
            if(Hero.Humanoid) bulb.transform.localScale=Vector3.one*.65f;
            dust = CreateProp("VFX_Dust_Run", DustPrefab, Hero.transform).GetComponentInChildren<ParticleSystem>();
            hit = CreateProp("VFX_Hit_Stumble", HitPrefab, Hero.transform).GetComponentInChildren<ParticleSystem>();
            var cartAudio = cart.GetComponent<AudioSource>();
            Intro = gameObject.AddComponent<IntroSequenceDirector>();
            Intro.Configure(Session, Hero, guard, CameraRig, cart.transform, tool.transform, bulb, cartAudio, IntroTimeline);
            hud = gameObject.AddComponent<HUDPresenter>();
            hud.Map=Map; hud.Data=this; hud.PoliceAlert=()=>PursuitCue.Visible;
            hud.Configure(Session, RetrySamePlayer);
            hud.TitleLogo = TitleLogo;
            hud.LoadingImage=LoadingImage; hud.BeginRun=StartPlayerRun; hud.PauseRun=TogglePause; hud.ToMenu=ExitToMenu;
            hud.TextInput=KeyboardGameInput.SetTextInput; hud.Quit=QuitGame;
            hud.StartTutorial=StartTutorial; hud.OfferTutorial=OfferTutorial;
            Tutorial=gameObject.AddComponent<TutorialDirector>();
            Tutorial.Framed=()=>Pose.Framing==FramingIssue.Ok;
            Tutorial.NoCamera=()=>Pose.Status==PoseStatus.Off||Pose.Status==PoseStatus.NoCamera||Pose.Status==PoseStatus.NoModel;
            Tutorial.MyPose=()=>Pose.UsesCamera&&Pose.Tracking?Pose.LastFrame:null; Tutorial.MyBaseline=()=>Pose.Baseline; Tutorial.Tuning=()=>Pose.Tuning;
            Tutorial.StepsPerMinute=()=>Pose.StepsPerMinute; Tutorial.PaceScale=()=>pace.Scale; Tutorial.PacePressure=()=>pace.Pressure; Tutorial.Tracking=()=>Pose.Tracking;
            Tutorial.RunCadence=Settings!=null?Settings.Motion.RunCadence:100f;
            Tutorial.Demo=gameObject.AddComponent<TutorialDemo>(); Tutorial.Demo.Configure(MonkeyPrefab);
            Tutorial.Configure(Session,Map,()=>Pose.Status==PoseStatus.Tracking||Pose.Status==PoseStatus.Off||Pose.Status==PoseStatus.NoCamera||Pose.Status==PoseStatus.NoModel,
                RestartTutorial,FinishTutorial,()=>!hud.Hidden&&!hud.Loading&&!hud.Revealing&&RunCalibration==RunCalibrationState.None);
            audioPlayer = gameObject.AddComponent<GameAudio>();
            audioPlayer.Configure(Session, Settings != null ? Settings.MusicVolume : .35f, Settings != null ? Settings.EffectsVolume : .7f);
            hud.RankCracked = audioPlayer.Crack;
            SetupSettings();
            if(automated)player.Style=PlayStyle.Keyboard;
            Session.Flow.Changed += StateChanged;
            Session.Flow.Transition(GameFlowState.TitleIdle);
        }
        private void SetupSettings()
        {
            settingsDefaults = new UserSettings
            {
                MusicVolume = Settings != null ? Settings.MusicVolume : .35f, EffectsVolume = Settings != null ? Settings.EffectsVolume : .7f,
                LiteLandmarkModel = Settings != null && !Settings.Camera.FullLandmarkModel, CameraDevice = Settings != null ? Settings.Camera.DeviceName : "",
            };
            settingsDefaults.ApplyPreset(QualityPreset.Balanced);
            userSettings = UserSettingsStore.Load(settingsDefaults);
            graphics = new GraphicsSettingsApplier();
            overlay = gameObject.AddComponent<PerformanceOverlay>();
            // Pose diagnostics are a development tool (Editor and development builds); release builds never show them.
            // They also show the cadence and the jump line in use (body units), for checking the step and jump tuning live.
            overlay.PoseLine = () => Pose.UsesCamera && BuildInfo.DevelopmentTools ? Pose.Diagnostics + " · 분당 " + Mathf.RoundToInt(Pose.StepsPerMinute) + "걸음 " + Cadence.Korean(Pose.CadenceStage)
                + " · 들썩임 " + Pose.Bounce.ToString("0.00") + " 점프선 " + Pose.JumpThreshold.ToString("0.00") : "";
            overlay.QualityLine = () => "화질 " + userSettings.Preset + " · 렌더 " + Mathf.RoundToInt((effectiveGraphics?.RenderScale ?? userSettings.RenderScale) * 100) + "% · 그림자 " + (effectiveGraphics?.Shadows ?? userSettings.Shadows) + (userSettings.AutoPerformance ? " · 자동 " + adaptive.Level : "") + (userSettings.FrameCap > 0 ? " · 상한 " + userSettings.FrameCap : "");
            var window = new GameObject("SettingsWindow"); window.transform.SetParent(transform, false);
            settingsWindow = window.AddComponent<SettingsWindow>();
            settingsWindow.Configure(this);
            settingsWindow.Closed += () => hud.Hidden = false;
            hud.OpenSettings = OpenSettings;
            ApplySettings(false);
        }
        public void OpenSettings()
        {
            if (settingsWindow == null || settingsWindow.IsOpen || hud.Loading) return;
            if (Session.Flow.State == GameFlowState.Running && !Session.Paused) TogglePause();
            hud.Hidden = true; settingsWindow.Open();
        }
        public void ApplySettings(bool save)
        {
            adaptive.Reset();
            effectiveGraphics=userSettings.Clone();
            graphics?.Apply(effectiveGraphics);
            if (Map != null) Map.DetailScale = userSettings.DetailDistance;
            audioPlayer?.SetVolumes(userSettings.MusicVolume, userSettings.EffectsVolume);
            Pose.Configure(userSettings.CameraDevice, userSettings.LiteLandmarkModel, userSettings.LaneSensitivity);
            Pose.SetInferenceRate(30);
            if (overlay != null) overlay.Visible = userSettings.PerformanceOverlay;
            HazardMarker.ThickOutline = userSettings.ColorBlind;
            if (hud != null && hud.RunModel != null) hud.RunModel.CameraEnabled = userSettings.InGameCamera;
            if (save) UserSettingsStore.Save(userSettings);
        }
        private RunnerView CreateActor(string id, GameObject prefab)
        {
            var root = new GameObject(id); root.transform.SetParent(transform, false);
            var visual = CreateProp("VisualRoot", prefab, root.transform);
            var view = root.AddComponent<RunnerView>(); view.Configure(visual); return view;
        }
        private static GameObject CreateProp(string id, GameObject prefab, Transform parent)
        {
            GameObject result = prefab != null ? Instantiate(prefab, parent) : GameObject.CreatePrimitive(PrimitiveType.Capsule);
            result.name = id; result.transform.SetParent(parent, false);
            if (prefab == null) { var collider = result.GetComponent<Collider>(); if (collider != null) collider.enabled = false; }
            return result;
        }
        public void StartIntro()
        {
            if (Session.Flow.Transition(GameFlowState.IntroTimeline)) Intro.Begin();
        }
        public bool MenuOpen=>hud!=null&&hud.MenuOpen;
        public void OpenMainMenu()
        {
            if(Session.Flow.State!=GameFlowState.TitleIdle||hud.Loading) return;
            hud.OpenMainMenu();
        }
        // After the title: face login, sign-up or guest play.
        public void OpenLogin()
        {
            if(Session.Flow.State!=GameFlowState.TitleIdle||hud.Loading) return;
            Accounts.ClearLoginNotice(); hud.OpenLogin();
        }
        // Straight to the run setup (tests).
        public void OpenStartSetup()
        {
            if(Session.Flow.State!=GameFlowState.TitleIdle||hud.Loading) return;
            hud.OpenSetup();
        }
        // Direct start (tests, tools): keeps the current play style and does not enter the leaderboard.
        public void BeginRun(string weight)
        {
            if(Session.Flow.State!=GameFlowState.TitleIdle||hud.Loading||!Session.Workout.SetWeight(weight)) return;
            menuCamera=false;
            playerRun=false; cadenceRun=false; Results.ClearRace(); Session.Workout.Met=PlayStyleInfo.Met(player.Style);Session.Workout.StyleScale=IntensityMet.Scale(player.Style);
            Session.Workout.CadenceCounts=true;
            StartCoroutine(PrepareRun(723));
        }
        // Start from the setup screen: the run is recorded on the leaderboard, and the chosen play style
        // becomes the account's default.
        public void StartPlayerRun(PlayerProfile profile)
        {
            if(profile==null||Session.Flow.State!=GameFlowState.TitleIdle||hud.Loading) return;
            // A weight outside 10-350 kg (an old profile) never blocks the run: it counts as the 65 kg default.
            if(!Session.Workout.SetWeight(profile.Weight)) Session.Workout.SetWeight("");
            // No camera: a body style falls back to the keyboard for this run, and the saved default is left alone.
            if(!PlayableStyle(profile.Style)){profile.Style=PlayStyle.Keyboard;}
            else if(profile.Style!=Account.Settings.Style){var saved=Account.Settings;saved.Style=profile.Style;Accounts.SaveSettings(saved);}
            player=profile; playerRun=true; KeyboardGameInput.SetTextInput(false);
            // The best to beat is read before the run (and before its save).
            Accounts.RefreshMemberStats();
            Session.Workout.Met=PlayStyleInfo.Met(profile.Style);Session.Workout.StyleScale=IntensityMet.Scale(profile.Style);
            Session.Workout.SetGoal(profile.CalorieGoal);
            cadenceRun = PlayStyleInfo.UsesCamera(profile.Style) && userSettings.CadenceSpeed;
            // Cadence off: stepping in place earns no calories, only jumps, crouches, knee raises and lane changes do.
            Session.Workout.CadenceCounts = userSettings.CadenceSpeed;
            Results.LoadRace(profile.Style, userSettings.RankStripMine);
            menuCamera=false; Pose.SetStyle(profile.Style);
            StartCoroutine(PrepareRun(Settings != null && Settings.Run.RandomizeLayout ? Random.Range(1, int.MaxValue) : 723));
        }
        // Tutorial (main menu, after sign-up or the "게임 시작" offer): the same scene and map on a fixed lesson course.
        // It never reaches the result screen, so nothing goes to the ranking, coins or calories.
        public void StartTutorial()
        {
            if(Session.Flow.State!=GameFlowState.TitleIdle||hud.Loading) return;
            tutorialOffered=true; playerRun=false; KeyboardGameInput.SetTextInput(false);
            if(!PlayableStyle(player.Style)) player.Style=PlayStyle.Keyboard;
            Session.Workout.SetWeight(""); Session.Workout.Met=PlayStyleInfo.Met(player.Style);Session.Workout.StyleScale=IntensityMet.Scale(player.Style);
            menuCamera=false; Pose.SetStyle(player.Style);
            cadenceRun=PlayStyleInfo.UsesCamera(player.Style)&&userSettings.CadenceSpeed; Results.ClearRace(); Session.Workout.CadenceCounts=userSettings.CadenceSpeed;
            Session.IsTutorial=true; Tutorial.Begin(player.Style,Session.Config.ExtendedRoutes,userSettings.CadenceSpeed,tutorialShort); tutorialShort=false;
            StartCoroutine(PrepareRun(TutorialCourse.Seed));
        }
        // Members who have not finished it and every guest are offered it once per session.
        private bool OfferTutorial()
        {
            if(TutorialScript.ShouldOffer(Account.IsMember,Account.TutorialDone,tutorialOffered)){tutorialOffered=true;tutorialShort=false;return true;}
            // Members who finished the earlier tutorial: the short version of what this update changed, offered once on this PC.
            bool seen=Account.IsMember&&userSettings.UpdatedTutorialSeen.Contains(Account.Id);
            if(!TutorialScript.ShouldOfferUpdate(Account.IsMember,Account.TutorialDone,seen,tutorialOffered)) return false;
            tutorialOffered=true; tutorialShort=true; userSettings.UpdatedTutorialSeen.Add(Account.Id); UserSettingsStore.Save(userSettings); return true;
        }
        // The next tutorial start is the short update version (set by the offer, cleared when the tutorial begins).
        private bool tutorialShort;
        // After a mistake: a fresh course, started at the given route z in front of the step.
        private void RestartTutorial(float z)
        {
            RouteSurface.UsePlan(TutorialCourse.Plan(Session.Config));
            Session.Reset(); Session.Runner.SetStartZ(z); keyboard.Clear(); poseInput.Clear(); Pose.ResetCommands();
            Map.Reset(); Map.Tick(z); Map.AnimateGates(z);
            stairFx.Reset(); guardDistance=15; previousStumble=false; Hero.Apply(Session.Runner); CameraRig.Takeover(Hero.Head);
        }
        // A member's profile remembers the finished tutorial; then back to the main menu.
        private void FinishTutorial()
        {
            Accounts.MarkTutorialDone();
            ExitToMenu();
        }
        private IEnumerator PrepareRun(int seed)
        {
            Session.Flow.Transition(GameFlowState.LoadingIntro);
            hud.BeginLoading(); hud.LoadingProgress=.12f;
            // Every run calibrates again: the player stands still during loading, and the countdown waits for it (WaitForCalibration).
            Pose.Recalibrate(); runCalibrated = false; calibrationDoneAt = -1;
            yield return new WaitForSecondsRealtime(.25f);
            RouteSurface.UsePlan(Session.IsTutorial?TutorialCourse.Plan(Session.Config):new RoutePlan(Session.Config, seed));
            Session.Reset(); moveTally.Reset(); pace.Reset(); ResetBigMoves(); System.Array.Clear(kindSeconds, 0, 4); keyboard.Clear(); poseInput.Clear(); hud.LoadingProgress=.42f;
            yield return new WaitForSecondsRealtime(.25f);
            Map.Reset(seed); Map.AnimateGates(Session.Runner.Z); hud.LoadingProgress=.78f;
            yield return new WaitForSecondsRealtime(.25f);
            Intro.PrepareGameplay(); hud.LoadingProgress=1;
            yield return new WaitForSecondsRealtime(.25f);
            Session.Flow.Transition(GameFlowState.Running); hud.EndLoading();
        }
        public void TogglePause()
        {
            if(Session.Flow.State!=GameFlowState.Running) return;
            Session.Paused=!Session.Paused; Time.timeScale=Session.Paused?0:1;
            keyboard.Clear(); poseInput.Clear();
        }
        private void UpdateAdaptive(bool live)
        {
            if(automated || !userSettings.AutoPerformance)return;
            if(!adaptive.Tick(Time.unscaledDeltaTime,live && !hud.Revealing && UnityEngine.Application.isFocused,userSettings.FrameCap))return;
            adaptive.Apply(userSettings,effectiveGraphics);graphics.Apply(effectiveGraphics);
            Map.DetailScale=effectiveGraphics.DetailDistance;
            // Rate changes reuse the loaded model and do not restart the camera or invalidate calibration.
            Pose.SetInferenceRate(adaptive.Level>=2?20:30);
        }
        private void UpdateMenuCamera(bool menu)
        {
            bool accountCamera=Session.Flow.State==GameFlowState.TitleIdle&&hud.AccountPageOpen;
            // Every menu screen shows the webcam when one is plugged in, keyboard players included; hand pointing still follows the setting.
            bool handCamera=userSettings.HandMenus&&PlayStyleInfo.UsesCamera(player.Style);
            GesturePointer.Enabled=userSettings.HandMenus&&(handCamera||accountCamera);
            // A keyboard run's pause stays without the camera: opening it on every pause and closing it on resume hitches play.
            // The login, sign-up and face capture pages open it even before the device list shows one (some drivers list it late).
            bool keyboardPause=Session.Paused&&!PlayStyleInfo.UsesCamera(player.Style);
            bool needsCamera=!automated && menu && (accountCamera || cameraDetected && !keyboardPause);
            if(needsCamera && (!Pose.UsesCamera||accountCamera&&Pose.Style!=PlayStyle.UpperBody))
            {if(!menuCamera)cameraBeforeMenu=Pose.Style;menuCamera=true;Pose.SetStyle(PlayStyle.UpperBody);}
            else if(menuCamera && !needsCamera){menuCamera=false;Pose.SetStyle(cameraBeforeMenu);}
        }
        private void Update()
        {
            double now = Time.realtimeSinceStartupAsDouble;
            bool live = Session.Flow.State == GameFlowState.Running && !Session.Paused && !hud.Loading;
            bool menu=(Session.Flow.State==GameFlowState.TitleIdle || Session.Flow.State==GameFlowState.Result || Session.Paused || settingsWindow.IsOpen) && !hud.Loading && !hud.Revealing;
            // Re-read the device list on entering a menu, then every 5 s on calm menu screens only (not during play, pause, loading or reveal).
            bool calmMenu=menu&&!Session.Paused;
            if(menu&&!wasMenu || calmMenu&&now>=nextCameraScan){nextCameraScan=now+5;cameraDetected=!automated&&!CameraAccess.Disabled&&WebCamTexture.devices.Length>0;}
            wasMenu=menu;
            UpdateMenuCamera(menu);
            UpdateAdaptive(live);
            Pose.Tick(now, live);
            // The calibration panel takes no hand pointing: the player stands still and it closes by itself.
            // Not gated on calibration: logging out switches to the upper-body camera, which restarts calibration, and a player
            // reaching for a button never stands still long enough to finish it (the pointer checks pose freshness itself).
            GesturePointer.Feed(Pose.LastFrame,menu&&GesturePointer.Enabled&&Pose.CameraLive&&!hud.CalibrationOpen,now);
            OperatorKeys();
            if (Accounts.ExpiryPending && Session.Flow.State == GameFlowState.TitleIdle && !hud.Loading) Accounts.ApplyExpiry();
            TickInput();
            if (Session.Flow.State == GameFlowState.Caught && Time.timeScale < 1 && Time.realtimeSinceStartup - caughtAt > .7f) Time.timeScale = 1;
            if(Session.Paused||hud.Loading||hud.Revealing) return;
            float dt = Mathf.Min(Time.deltaTime, .1f);
            Intro.Tick(dt);
            if (Session.Flow.State == GameFlowState.IntroTimeline) hud.Fade = Mathf.Clamp01(1 - Intro.Elapsed * 3f);
            if (Session.Flow.State == GameFlowState.Running)
            {
                if (WaitForCalibration()) return;
                StartBannerSounds();
                Tutorial.Tick(Time.realtimeSinceStartup);
                // The finished tutorial has already gone back to the menu (Retry); nothing of the run may follow this frame.
                if (Session.Flow.State != GameFlowState.Running) return;
                // The cadence sets the pace on foot; below the target for 3 s the police close in, a full gauge in the mine is a warning.
                var course = MapCourse.KindAt(RouteSurface.ActivePlan, RouteSurface.SequenceAt(Session.Runner.Z));
                // Out of view the pace holds (speed, lag and police stay as they were); on the stairs it rests (QA 2026-11-25 M6, H2).
                pace.Tick(dt, cadenceRun && !Session.Runner.Riding, Pose.StepsPerMinute,
                    Settings != null ? Settings.Motion : null, MapCourse.TargetCadence(course, Settings != null ? Settings.Motion : null),
                    Pose.UsesCamera && Pose.Tracking, OnOrBeforeStairs());
                Session.ApplyPace(pace);
                TickBigMoves();
                float previousZ = Session.Runner.Z;
                Session.Tick(dt); Results.Race.Update(Session.Score.Score); Map.Tick(Session.Runner.Z); Hero.Apply(Session.Runner); Map.Evaluate(previousZ);
                Map.AnimateGates(Session.Runner.Z);
                // Intensity MET of this moment from the cadence; no grace period after the last step (standing counts nothing).
                double clock=Time.realtimeSinceStartupAsDouble;
                int activity=clock-Pose.LastJumpTime<.65?1:Pose.Reading.Crouch?2:clock-Pose.LastKneeStepTime<.7?3:Pose.Reading.Lane!=0?4:0;
                bool tracked=Pose.UsesCamera&&Pose.Tracking;
                if(tracked) moveTally.Observe(clock,Pose.StepCount,Pose.LastJumpTime,Pose.Reading.Crouch,Pose.LastKneeStepTime,Pose.Reading.Lane,Session.Workout);
                double activeBefore=Session.Workout.ActiveSeconds;
                Session.Workout.TickIntensity(dt,Session.Flow.State==GameFlowState.Running,tracked,Pose.CadenceStage,moveTally.Moving(clock),clock<bigUntil,activity);
                kindSeconds[(int)course]+=Session.Workout.ActiveSeconds-activeBefore;
                runFade += dt; hud.Fade = Mathf.Clamp01(runFade * 2f);
                if (dust != null && !dust.isPlaying) dust.Play();
                if (Session.Runner.Stumbling && !previousStumble && hit != null) hit.Play();
                previousStumble = Session.Runner.Stumbling;
                if (Session.Flow.State == GameFlowState.Running)
                {
                    float distance=Session.Runner.Slowing&&Session.SlipStrikes>0||Session.SlipPursuit?1.8f:Mathf.Lerp(15,1.8f,Session.PolicePressure);
                    guardDistance=Mathf.MoveTowards(guardDistance,distance,dt*18);
                    guard.Pose(new Vector3(Session.Runner.X, 0, Session.Runner.Z-guardDistance), !Session.Runner.Riding);
                    guard.Play("AN_Guard_ChaseRun");
                }
            }
            if (Session.Flow.State == GameFlowState.Caught)
            {
                caughtTime += Time.unscaledDeltaTime;
                if(Session.Reason==CaptureReason.BrokenRail)
                {
                    guard.SetVisible(false);
                    if(caughtTime>=1.6f)Session.Flow.Transition(GameFlowState.Result);
                }
                else
                {
                    Capture.Tick(Time.unscaledDeltaTime);
                    if(Capture.Complete)Session.Flow.Transition(GameFlowState.Result);
                }
            }
        }
        // Camera play never starts a run before its own calibration: the calibration screen (CalibrationPanel.DrawRun) shows the
        // camera while the player is found and measured, then "보정 완료" for a second, then the countdown begins.
        // A missing camera or model does not hold the run.
        private bool runCalibrated = true;
        private float calibrationDoneAt = -1;
        private const float CalibrationDoneSeconds = 1f;
        private bool WaitForCalibration()
        {
            if (runCalibrated) return false;
            var status = Pose.Status;
            bool measuring = Pose.UsesCamera && (status == PoseStatus.Starting || status == PoseStatus.Searching || status == PoseStatus.Calibrating);
            runStartedAt = Time.realtimeSinceStartup;
            if (measuring) { calibrationDoneAt = -1; return true; }
            if (Pose.UsesCamera && status == PoseStatus.Tracking)
            {
                if (calibrationDoneAt < 0) calibrationDoneAt = Time.realtimeSinceStartup;
                if (Time.realtimeSinceStartup - calibrationDoneAt < CalibrationDoneSeconds) return true;
            }
            runCalibrated = true; calibrationDoneAt = -1; bannerSounded = goSounded = false; Pose.ResetCommands();
            return false;
        }
        private RunCalibrationState RunCalibration => runCalibrated || Session.Flow.State != GameFlowState.Running || hud.Loading ? RunCalibrationState.None
            : calibrationDoneAt >= 0 ? RunCalibrationState.Done : RunCalibrationState.Measuring;
        private void StartBannerSounds()
        {
            float banner = StartBanner;
            if (banner <= 0) return;
            if (!bannerSounded) { bannerSounded = true; audioPlayer.Countdown(false); }
            if (!goSounded && banner <= 1f && Pose.Status != PoseStatus.Calibrating) { goSounded = true; audioPlayer.Countdown(true); }
        }
        private void OperatorKeys()
        {
            if (KeyboardGameInput.SettingsPressed) { if (settingsWindow != null && settingsWindow.IsOpen) settingsWindow.Close(true); else OpenSettings(); }
            if (KeyboardGameInput.OverlayPressed) { userSettings.PerformanceOverlay = !userSettings.PerformanceOverlay; ApplySettings(true); }
            if (settingsWindow != null && settingsWindow.IsOpen) return;
            if (KeyboardGameInput.RecalibratePressed && !hud.MenuOpen) Pose.Recalibrate();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // F6: record the camera's joint coordinates for the recognition scorecard (never images).
            if (KeyboardGameInput.PoseRecordPressed) TogglePoseRecording();
            // Demo invincibility: the HUD shows a small "무적 모드 켜짐" note in the bottom-left corner while it is on; the log records each switch.
            if (KeyboardGameInput.InvinciblePressed) { Session.Invincible = !Session.Invincible; Debug.Log("Invincible " + (Session.Invincible ? "on" : "off")); }
            // F7: the operator ends the run as a capture now (invincible or not), so a demo run can be closed into the results.
            if (KeyboardGameInput.ForceCatchPressed && Session.ForceCatch()) Debug.Log("Run ended by the operator (F7).");
#endif
        }
        internal HUDPresenter Hud => hud;
        internal RankRace Race => Results.Race;
        // Recordings go to <persistent data>/PoseRecordings; copy them (with filled-in labels) to Tools/QA/PoseRecordings/team.
        public static string PoseRecordingFolder => System.IO.Path.Combine(UnityEngine.Application.persistentDataPath, "PoseRecordings");
        private void TogglePoseRecording()
        {
            if (Pose.Recording) { string path = Pose.StopRecording(); Debug.Log("Pose recording saved: " + path + " (" + Pose.RecordedFrames + " frames)"); return; }
            if (!BuildInfo.DevelopmentTools || !Pose.UsesCamera) return;
            string name = Pose.StartRecording(PoseRecordingFolder);
            if (name != null) Debug.Log("Pose recording started: " + name);
        }
        // Development builds can keep player data in another folder (--mongscape-data <dir>) for captures and tests.
        // On foot on a stair chunk, or within 3 m of one (people start lifting their knees a little early).
        private bool OnOrBeforeStairs()
        {
            var runner = Session.Runner; if (runner.Riding) return false;
            return RouteSurface.IsStair(RouteSurface.TypeAt(RouteSurface.SequenceAt(runner.Z)))
                || RouteSurface.IsStair(RouteSurface.TypeAt(RouteSurface.SequenceAt(runner.Z + 3f)));
        }
        private static string DataRoot()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            var args = System.Environment.GetCommandLineArgs(); int at = System.Array.IndexOf(args, "--mongscape-data");
            if (at >= 0 && at + 1 < args.Length) return args[at + 1];
#endif
            return null;
        }
        public void TickInput()
        {
            keyboard.Poll();
            if(hud.Loading||hud.Revealing||(settingsWindow!=null&&settingsWindow.IsOpen)){keyboard.Clear();poseInput.Clear();return;}
            router.Tick();
            if (Session.Flow.State == GameFlowState.TitleIdle && !hud.BlocksStart && KeyboardGameInput.StartPressed) OpenLogin();
        }
        private void LateUpdate()
        {
            if (Session != null && (Session.Flow.State == GameFlowState.Running || Session.Flow.State == GameFlowState.Caught))
            {
                if(Session.Flow.State==GameFlowState.Caught&&Session.Reason!=CaptureReason.BrokenRail)CameraRig.FollowCapture(Hero.transform.position,Time.unscaledDeltaTime,Capture.ProneBlend);
                else if(Session.Runner.Riding)CameraRig.FollowCart(Hero.Head,Time.deltaTime,-Session.Runner.CartLean*12);
                else
                {
                    stairFx.Tick(Time.deltaTime,StairCameraFx.OnStairChunk(Session.Runner),Session.Runner.KneeSteps);
                    if(stairFx.Stepped&&audioPlayer!=null)audioPlayer.Footstep();
                    CameraRig.Follow(Hero.Head,Session.Runner.Crouching,Time.deltaTime,Session.Runner.Slowing?Mathf.Sin(Time.time*12)*2:0,stairFx.Pitch,stairFx.Bob);
                }
            }
        }
        private void StateChanged(GameFlowState state)
        {
            if (state == GameFlowState.Running) { runFade = 0; poseInput.Clear(); Pose.ResetCommands(); runStartedAt = Time.realtimeSinceStartup; bannerSounded = goSounded = false; }
            if (state == GameFlowState.Caught)
            {
                caughtTime = 0; guard.Play("AN_Guard_Catch"); Hero.Play("AN_Monkey_Caught");
                if(Session.Reason!=CaptureReason.BrokenRail)
                {
                    Capture.Begin();
                }
                if (dust != null) dust.Stop();
                // A short slow motion and red flash make the capture readable.
                caughtAt = Time.realtimeSinceStartup; Time.timeScale = .35f;
            }
            if (state == GameFlowState.Result)
            {
                Time.timeScale = 1;
                var args=System.Environment.GetCommandLineArgs();
                bool tooling=System.Array.IndexOf(args,"--mongscape-capture")>=0||System.Array.IndexOf(args,"--mongscape-smoke")>=0
                    ||System.Array.IndexOf(args,"--mongscape-review")>=0||System.Array.IndexOf(args,"--mongscape-tests")>=0
                    ||System.Array.IndexOf(args,"--mongscape-uicapture")>=0;
                Results.Clear();
                if(!tooling&&playerRun) RecordFinishedRun();
            }
        }
        // The run that just ended: see RunResults.
        public void RecordFinishedRun() => Results.Record(Session, player.Style, kindSeconds);
        private string ExerciseOwner => Account.IsMember ? Account.Id : "";
        private void ResetBigMoves()
        {
            seenBigJumps = Pose.BigJumps; seenBigKnees = Pose.BigKnees; seenBigLanes = Pose.BigLanes;
            wasDeep = false; bigUntil = -10; deepSince = -10;
        }
        // Each new big movement gets its in-game effect once (a coin when the matching hazard is close) and counts +1 MET for a
        // second. Keyboard play never has big movements.
        private void TickBigMoves()
        {
            double clock = Time.realtimeSinceStartupAsDouble;
            bool camera = Pose.UsesCamera && Pose.Tracking;
            var reading = Pose.Reading;
            bool bigJump = Pose.BigJumps > seenBigJumps, bigKnee = Pose.BigKnees > seenBigKnees, bigLane = Pose.BigLanes > seenBigLanes;
            seenBigJumps = Pose.BigJumps; seenBigKnees = Pose.BigKnees; seenBigLanes = Pose.BigLanes;
            bool deep = camera && reading.CrouchDeep, deepStart = deep && !wasDeep;
            wasDeep = deep;
            if (!camera) return;
            if (bigJump && Session.Big(RunSession.BigMove.Jump)) bigUntil = clock + 1;
            // Held deep is resting after a moment: no longer sliding on, no +1 MET (QA 2026-11-25).
            if (deepStart) deepSince = clock;
            if (deep && MoveTally.Fresh(deepSince, clock)) { Session.Runner.DeepSlide(); bigUntil = System.Math.Max(bigUntil, clock + .3); }
            if (deepStart && Session.Big(RunSession.BigMove.Crouch)) bigUntil = clock + 1;
            if (bigKnee && Session.Big(RunSession.BigMove.Knee)) bigUntil = clock + 1;
            if (bigLane && Session.Big(RunSession.BigMove.Lane)) bigUntil = clock + 1;
        }
        public void Retry()
        {
            if (!Session.Flow.Transition(GameFlowState.Retry)) return;
            Capture.ResetSequence();
            guardDistance=15; Session.IsTutorial=false; Tutorial.End();
            Session.Reset(); keyboard.Clear(); poseInput.Clear(); Map.Reset(); Intro.ResetSequence();
            Time.timeScale=1; hud.CloseSetup();
            if (dust != null) dust.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (hit != null) hit.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            previousStumble = false; runFade = caughtTime = 0; hud.Fade = 1;
            Session.Flow.Transition(GameFlowState.TitleIdle);
        }
        // "다시 하기": the same player runs again with the same settings.
        public void RetrySamePlayer()
        {
            bool again = playerRun; var profile = player;
            Retry();
            // An expired member goes to the login gate (next frame) instead of another run.
            if (again && !Accounts.ExpiryPending) StartPlayerRun(profile);
        }
        // "메인으로" from the result or pause screen; a paused run ends without a record.
        public void ExitToMenu()
        {
            if (Session.Paused) { Session.Paused = false; Time.timeScale = 1; }
            playerRun = false;
            Retry();
            Results = new RunResults(Accounts, () => Store);
            player = Account.Profile.Clone();
            OpenMainMenu();
        }
        private void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            UnityEngine.Application.Quit();
#endif
        }
        // Members are signed out when the game closes; the request is not awaited.
        private void OnApplicationQuit() => Accounts?.LogoutOnQuit();
        private void OnDestroy() { GesturePointer.Reset(); Time.timeScale=1; graphics?.Dispose(); Pose?.Dispose(); RouteSurface.UsePlan(null); KeyboardGameInput.SetTextInput(false); Accounts?.ClearPortrait(); }

        // Tests and tools: another account server (or a fake), another PC store and a face capture without a webcam. Call on the title screen.
        public void UseAccountServices(IAccountGateway accountGateway, PlayerProgressStore store = null, System.Action<System.Action<string[]>> faces = null)
        {
            Accounts.UseServices(accountGateway, faces);
            if (store != null) Store = store;
        }
        // Items placed on the map: what the member unlocked (none for guests).
        public System.Collections.Generic.List<ItemKind> RunItems()
        {
            if (Session != null && Session.IsTutorial) return new System.Collections.Generic.List<ItemKind>();
            return Account.RunItems();
        }
    }
}
