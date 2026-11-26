using System;
using Mongscape.Domain;
using UnityEngine;

namespace Mongscape.Presentation
{
    public enum MenuPage { None, Main, Setup, Profile, Ranking, Identity, Login, SignUp, TutorialOffer, Credits, Help, Week }

    // UI root: owns the menu page state and draws exactly one screen for the current game state.
    public sealed class HUDPresenter : MonoBehaviour
    {
        private RunSession session;
        public Mongscape.Presentation.Map.ChunkSpawner Map;
        private Action retry;
        public float Fade { get; set; } = 1f;
        public Texture2D TitleLogo;
        public Texture2D LoadingImage;
        public IHudData Data;
        public Action<PlayerProfile> BeginRun;
        public Action PauseRun, Recalibrate;
        // Result and pause: back to the main menu.
        public Action ToMenu;
        public Action OpenSettings;
        // The trait screen (UI Toolkit, TraitsWindow) opened from the main menu.
        public Action OpenTraits;
        public Action Quit;
        public Func<bool> PoliceAlert;
        // Tutorial: starts it, and decides (and remembers) whether "게임 시작" offers it first.
        public Action<TutorialChapter, bool> StartTutorial;
        public Func<TutorialOffer> OfferTutorial;
        // Finished chapters of the current player (a bit each), for the chapter cards.
        public Func<int> TutorialMask;
        private TutorialOfferScreen tutorialOffer;
        private TutorialOfferScreen.Mode offerMode;
        private IdentityScreen identity;
        private LoginGateScreen gate;
        private SignUpScreen signUp;
        // Sign-up step state; tests and tools can drive it without clicking.
        public SignUpFlow SignUp { get; } = new SignUpFlow();
        // 내 정보 form state (members only).
        public ProfileForm Profile { get; } = new ProfileForm();
        // Ranking tabs, the result best and the calorie panel; tests read what the ranking screen shows.
        public RankingBoard Ranking { get; } = new RankingBoard();
        public bool IdentityOpen=>Page==MenuPage.Identity;
        // Login gate, sign-up and face login keep the upper-body camera on for hand pointing and face capture.
        public bool AccountPageOpen=>Page==MenuPage.Login||Page==MenuPage.SignUp||Page==MenuPage.Identity;
        private IIdentityData Account=>Data as IIdentityData;
        // Text fields need the IME; the bootstrap switches it.
        public Action<bool> TextInput;
        // The UI Toolkit settings window draws over the game; the IMGUI HUD hides meanwhile.
        public bool Hidden { get; set; }
        public MenuPage Page { get; private set; }
        public bool MenuOpen => Page != MenuPage.None;
        public bool SetupOpen => Page == MenuPage.Setup;
        public bool Loading { get; private set; }
        public bool Revealing => revealRemaining > 0;
        public float RevealOpacity => Mathf.SmoothStep(0, 1, revealRemaining / .85f);
        public bool BlocksStart => MenuOpen || Loading || Revealing;
        public float LoadingProgress { get; set; }
        private float revealRemaining;
        private bool resultOpen;
        private HudSkin skin;
        private OverlaySkin overlaySkin;
        private TitleScreen title;
        private MainMenuScreen menu;
        private CreditsScreen credits;
        // Credits opens from the login gate or the main menu and returns there.
        private MenuPage creditsReturn = MenuPage.Main;
        private SetupScreen setup;
        private ProfileScreen profile;
        private LeaderboardScreen board;
        private CalibrationPanel calibration;
        private LoadingScreen loading;
        private RunHudView runView;
        private ResultView resultView;
        private HelpView helpView;
        private MainMenuView mainView;
        private PlayModeView playView;
        private RankingView rankingView;
        public MainMenuView MainView => mainView;
        private WeekView weekView;
        public WeekView WeekCard => weekView;
        public HelpView Help => helpView;
        // The UI Toolkit result screen (score count-up, today's ranking, this run).
        public ResultView Result => resultView;
        private readonly RunHudModel runModel = new RunHudModel();
        // The UI Toolkit running HUD; the zoo sets the rear-view mirror through it.
        public RunHudView RunView => runView;
        private PauseView pauseView;
        public PauseView Pause => pauseView;
        public void Configure(RunSession run, Action again)
        {
            session = run; retry = again;
            skin = new HudSkin();
            overlaySkin = new OverlaySkin();
            title = new TitleScreen(skin); setup = new SetupScreen(skin); loading = new LoadingScreen(skin); identity=new IdentityScreen(skin); signUp=new SignUpScreen(skin,identity);
            menu = new MainMenuScreen(overlaySkin); credits = new CreditsScreen(overlaySkin); tutorialOffer = new TutorialOfferScreen(overlaySkin); gate = new LoginGateScreen(overlaySkin); profile = new ProfileScreen(overlaySkin); board = new LeaderboardScreen(overlaySkin, Ranking);
            calibration = new CalibrationPanel(overlaySkin, skin);
            runView = new RunHudView(gameObject);
            pauseView = new PauseView(gameObject) { Resume = () => PauseRun?.Invoke(), Recalibrate = () => Recalibrate?.Invoke(), Main = () => ToMenu?.Invoke() };
            helpView = new HelpView(gameObject) { Back = OpenMainMenu };
            mainView = new MainMenuView(gameObject) { Select = SelectMenu };
            playView = new PlayModeView(gameObject)
            {
                Back = OpenMainMenu, Choose = s => { setupStyle = s; Data?.PreviewStyle(s); },
                SetCadence = on => Data?.SetCadenceSpeed(on), Start = StartFromPlayMode,
            };
            rankingView = new RankingView(gameObject) { Back = OpenMainMenu, Pick = (today, mode) => Ranking.Select(Ranking.MemberTab, mode, today) };
            weekView = new WeekView(gameObject) { Back = () => ShowPage(MenuPage.Profile) };
            resultView = new ResultView(gameObject) { Retry = () => retry?.Invoke(), Calibrate = OpenCalibration, Main = () => ToMenu?.Invoke() };
        }
        public void ShowPage(MenuPage page)
        {
            // 내 정보 is for members; a guest stays on the main menu.
            if (page == MenuPage.Profile && (Account == null || !Account.SignedIn)) page = MenuPage.Main;
            if (page == MenuPage.Credits && Page != MenuPage.Credits) creditsReturn = Page == MenuPage.Login ? MenuPage.Login : MenuPage.Main;
            Page = page;
            if (page == MenuPage.Setup) { setup.Open(Data); setupStyle = PlayStyleInfo.Playable(Data?.Player?.Style ?? PlayStyle.FullBody, Data == null || Data.BodyStylesAllowed); }
            else if (page == MenuPage.Profile) { Profile.Open(Data, Account); profile.Open(); }
            else if (page == MenuPage.TutorialOffer) tutorialOffer.Open();
            else if (page == MenuPage.Week) weekView.Show(Data?.WeekMinutes ?? 0, Data?.WeekDays, Data?.WeekMaps, ((int)System.DateTime.UtcNow.AddHours(9).DayOfWeek + 6) % 7, Data?.Player?.Style == PlayStyle.UpperBody);
            else if (page == MenuPage.Ranking) board.OpenBoard(Data, Account);
            else if (page == MenuPage.Login) Account?.CheckServer();
            else if (page == MenuPage.SignUp) { SignUp.Open(Account); signUp.Open(); }
            TextInput?.Invoke(page == MenuPage.Setup || page == MenuPage.Profile || page == MenuPage.SignUp);
        }
        public void OpenMainMenu() => ShowPage(MenuPage.Main);
        public void CloseCredits() => ShowPage(creditsReturn);
        public void OpenLogin() => ShowPage(MenuPage.Login);
        // Face login capture screen (from the login gate).
        public void ShowIdentity() => ShowPage(MenuPage.Identity);
        public void OpenSetup() => ShowPage(MenuPage.Setup);
        public void ChooseGuest() => OpenMainMenu();
        // A recognized face goes to the main menu; a failure stays for another try.
        public void LoginWithFace() => Account?.FaceLogin(ok => { if (ok && Page == MenuPage.Identity) OpenMainMenu(); });
        // Sign-up capture: the new member is signed in and is offered the tutorial.
        public void EnrollFace() => SignUp.Capture(ok => { if (ok && Page == MenuPage.SignUp) { OfferTutorial?.Invoke(); ShowTutorialOffer(true); } });
        public void ShowTutorialOffer(bool afterSignUp) { offerMode = afterSignUp ? TutorialOfferScreen.Mode.AfterSignUp : TutorialOfferScreen.Mode.BeforePlay; ShowPage(MenuPage.TutorialOffer); }
        // Members of the earlier tutorial: the short basics chapter, offered once.
        public void ShowTutorialShort() { offerMode = TutorialOfferScreen.Mode.Short; ShowPage(MenuPage.TutorialOffer); }
        // The main menu's "튜토리얼" opens the same choice of play style; skipping returns to the menu.
        public void ShowTutorialChoice() { offerMode = TutorialOfferScreen.Mode.FromMenu; ShowPage(MenuPage.TutorialOffer); }
        // Starting runs it in the chosen style (or the player's current one); skipping goes on to where the player was heading.
        public void ChooseTutorial(TutorialChoice choice, PlayStyle? style = null, TutorialChapter chapter = TutorialChapter.Basics)
        {
            if (choice == TutorialChoice.Start) { if (style.HasValue) Data?.PreviewStyle(style.Value); StartTutorial?.Invoke(chapter, offerMode == TutorialOfferScreen.Mode.Short); }
            else if (choice == TutorialChoice.Skip) { if (offerMode == TutorialOfferScreen.Mode.BeforePlay || offerMode == TutorialOfferScreen.Mode.Short) ShowPage(MenuPage.Setup); else OpenMainMenu(); }
        }
        public void ChooseLogin(LoginAction action)
        {
            if (action == LoginAction.Face) ShowIdentity();
            else if (action == LoginAction.SignUp) ShowPage(MenuPage.SignUp);
            else if (action == LoginAction.Guest) ChooseGuest();
            else if (action == LoginAction.Settings) OpenSettings?.Invoke();
            else if (action == LoginAction.Credits) ShowPage(MenuPage.Credits);
            else if (action == LoginAction.Quit) Quit?.Invoke();
        }
        // Main menu actions. "게임 시작" goes to the run setup, after a one-time tutorial offer; the login entry signs a member out first.
        public void SelectMenu(MenuAction action)
        {
            if (action == MenuAction.Play) { var offer = OfferTutorial?.Invoke() ?? TutorialOffer.None; if (offer == TutorialOffer.Full) ShowTutorialOffer(false); else if (offer == TutorialOffer.Short) ShowTutorialShort(); else ShowPage(MenuPage.Setup); }
            else if (action == MenuAction.Tutorial) ShowTutorialChoice();
            else if (action == MenuAction.Ranking) ShowPage(MenuPage.Ranking);
            else if (action == MenuAction.Traits) OpenTraits?.Invoke();
            else if (action == MenuAction.Profile) ShowPage(MenuPage.Profile);
            else if (action == MenuAction.Identity) Logout();
            else if (action == MenuAction.Settings) OpenSettings?.Invoke();
            else if (action == MenuAction.Credits) ShowPage(MenuPage.Credits);
            else if (action == MenuAction.Help) ShowPage(MenuPage.Help);
            else if (action == MenuAction.Quit) Quit?.Invoke();
        }
        // Main menu "로그아웃" and 내 정보: a member is signed out; both go to the login gate.
        public void Logout() { if (Account != null && Account.SignedIn) Account.FaceLogout(false); OpenLogin(); }
        // 내 정보 "회원 탈퇴": two questions, then the account is deleted and the login gate opens.
        public void DeleteAccount() => Profile.RequestDelete(() => { if (Page == MenuPage.Profile) OpenLogin(); });
        // Result screen: a guest is asked for a name for the run just recorded as "게스트" on this PC's ranking.
        public bool AsksGuestName => board.AsksGuestName;
        public bool SaveGuestName(string name) => board.SaveGuestName(Data, name);
        // Opens the result screen once per result (drawn by OnGUI; tests call it directly).
        public void OpenResult()
        {
            if (resultOpen || session == null || session.Flow.State != GameFlowState.Result) return;
            resultOpen = true; board.OpenResult(Data, Account, session); if (board.AsksGuestName) TextInput?.Invoke(true);
            resultView?.Show(Data, Ranking, session, CalibrationAvailable, Time.unscaledTime);
        }
        // Result screen 웹캠 보정: only after a camera run; the panel blocks the result buttons and hand pointing while open.
        public bool CalibrationAvailable => session != null && session.Flow.State == GameFlowState.Result && Data?.Player != null && PlayStyleInfo.UsesCamera(Data.Player.Style);
        public bool CalibrationOpen => calibration != null && calibration.Open;
        public void OpenCalibration() { if (CalibrationAvailable) calibration.Show(Data); }
        public void CloseCalibration() => calibration?.Close();
        public void CloseSetup() { ShowPage(MenuPage.None); Loading = false; revealRemaining = 0; }
        public void BeginLoading() { ShowPage(MenuPage.None); Loading = true; }
        public void EndLoading() { Loading = false; revealRemaining = .85f; }
        public RunHudModel RunModel => runView != null ? runModel : null;
        private void Update()
        {
            revealRemaining = Mathf.Max(0, revealRemaining - Time.unscaledDeltaTime);
            if (!CalibrationAvailable) calibration?.Close(); else calibration.Tick(Data, Time.realtimeSinceStartup);
            if (runView == null) return;
            helpView.Root.Visible = !Hidden && Page == MenuPage.Help && session != null && session.Flow.State == GameFlowState.TitleIdle;
            bool titleMenu = !Hidden && !Loading && !Revealing && session != null && session.Flow.State == GameFlowState.TitleIdle;
            mainView.Root.Visible = titleMenu && Page == MenuPage.Main;
            if (mainView.Root.Visible) { mainView.Root.Layout(); mainView.Refresh(Data, TitleLogo); }
            playView.Root.Visible = titleMenu && Page == MenuPage.Setup;
            if (playView.Root.Visible) { playView.Root.Layout(); setupStyle = PlayStyleInfo.Playable(setupStyle, Data == null || Data.BodyStylesAllowed); playView.Refresh(setupStyle, Data == null || Data.BodyStylesAllowed, Data?.CadenceSpeed ?? true); }
            rankingView.Root.Visible = titleMenu && Page == MenuPage.Ranking;
            if (rankingView.Root.Visible) { rankingView.Root.Layout(); rankingView.Refresh(Ranking); }
            if (helpView.Root.Visible) helpView.Root.Layout();
            bool menuScreen = !Hidden && session != null && session.Flow.State == GameFlowState.TitleIdle;
            weekView.Root.Visible = menuScreen && Page == MenuPage.Week; if (weekView.Root.Visible) weekView.Root.Layout();
            bool inRun = session != null && (session.Flow.State == GameFlowState.Running || session.Flow.State == GameFlowState.Caught);
            if (inRun && !wasRunning) runView.ResetRun();
            wasRunning = inRun;
            bool showRun = inRun && !Hidden && !Loading && !Revealing && !session.Paused
                && !(Data != null && Data.RunCalibration != RunCalibrationState.None && session.Flow.State == GameFlowState.Running);
            runView.Root.Visible = showRun;
            pauseView.Root.Visible = inRun && session.Paused && !Hidden && !Loading && !Revealing;
            if (pauseView.Root.Visible) { pauseView.Root.Layout(); pauseView.Place(PlayStyleInfo.UsesCamera(Data?.Player?.Style ?? PlayStyle.Keyboard)); pauseView.SetFlow(Data?.Pause, Time.unscaledTime); }
            if (session == null || Hidden || Loading || Revealing) return;
            if (inRun)
                runModel.Tick(session, Map, Data?.Player?.Style ?? PlayStyle.Keyboard, Data?.PoseStatus ?? PoseStatus.Off, PoliceAlert?.Invoke() ?? false, Time.unscaledTime);
            if (showRun) runView.Render(session, Data, runModel, Time.unscaledTime);
            if (resultView != null)
            {
                bool result = session.Flow.State == GameFlowState.Result && resultView.Open;
                if (session.Flow.State != GameFlowState.Result && resultView.Open) resultView.Hide();
                resultView.Root.Visible = result && !CalibrationOpen;
                if (result) resultView.Render(Time.unscaledTime);
            }
        }
        private bool wasRunning;
        // Played when the ranking strip passes a row.
        public Action RankCracked { set { if (runView != null) runView.Strip.Cracked = value; } }
        private void OnGUI()
        {
            if (session == null || Hidden) return;
            // Any style without its own font (IMGUI defaults, text fields) falls back to the game font too.
            if (GUI.skin.font != UiFont.Main) GUI.skin.font = UiFont.Main;
            Matrix4x4 old = GUI.matrix;
            GUI.matrix = ViewportLayout.GuiMatrix;
            GesturePointer.BeginPass();
            var state = session.Flow.State;
            if (state != GameFlowState.Result) resultOpen = false;
            if (Loading || Revealing) loading.Draw(LoadingImage, LoadingProgress, Loading, RevealOpacity);
            else if (state == GameFlowState.TitleIdle) DrawMenu();
            else if (state == GameFlowState.Running || state == GameFlowState.Caught)
            {
                if (session.Paused) { if (pauseView.ShowsButtons) pauseView.Gestures.Pass(); }
                // Camera runs hold on the calibration screen until the player is measured, then the countdown starts.
                else if (Data != null && Data.RunCalibration != RunCalibrationState.None && state == GameFlowState.Running) calibration.DrawRun(Data);
                // The running HUD is the UI Toolkit RunHudView (drawn by its own panel).
            }
            else if (state == GameFlowState.Result)
            {
                OpenResult();
                bool calibrating = calibration.Open;
                // The open calibration panel takes the keys first and leaves the result screen underneath inert.
                calibration.Keys();
                if (!calibrating && resultView != null)
                {
                    resultView.Gestures.Pass();
                    var e = Event.current;
                    if (e.type == EventType.KeyDown && !board.AsksGuestName)
                    {
                        if (e.keyCode == KeyCode.Escape) { e.Use(); if (!resultView.CloseWindow()) ToMenu?.Invoke(); }
                        else if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter || e.keyCode == KeyCode.Space) { e.Use(); if (!resultView.CloseWindow()) retry?.Invoke(); }
                    }
                    // A guest names the run on this PC's ranking (text input stays IMGUI), over the lower ranking rows.
                    if (resultView.ShowingRanking && !resultView.MoreOpen && !resultView.InfoOpen) board.DrawGuestName(Data, new Rect(60, 400, 700, 150));
                }
                if (calibrating) calibration.Draw(Data);
            }
            // F9 demo invincibility (development builds): a small reminder in the bottom-left corner while it is on, with the F7 key that still ends the run.
            if (session.Invincible && (state == GameFlowState.Running || state == GameFlowState.Caught) && Event.current.type == EventType.Repaint)
            {
                skin.Card(new Rect(16, 684, 220, 26), new Color(0, 0, 0, .55f));
                GUI.color = Palette.Gold; GUI.Label(new Rect(16, 684, 220, 26), "무적 모드 켜짐 (F9) · F7 잡히기", skin.Centered(14, FontStyle.Bold)); GUI.color = Color.white;
            }
            // Joint recording (F6, Editor and development builds): a red note at the top of every screen while it runs.
            if (Data != null && Data.PoseRecording && Event.current.type == EventType.Repaint)
            {
                var note = new Rect(530, 4, 220, 26);
                skin.Card(note, new Color(0, 0, 0, .65f));
                GUI.color = new Color(1, .35f, .35f); GUI.Label(note, "● 관절 녹화 중 (F6 끄기)", skin.Centered(14, FontStyle.Bold)); GUI.color = Color.white;
            }
            var cameraSlot = CameraSlot(state);
            if (cameraSlot.width > 0) PosePreview.DrawCorner(skin, cameraSlot, Data);
            GesturePointer.Draw();
            GUI.color = Color.white; GUI.matrix = old;
        }
        // Bottom-left webcam corner, clear of every screen's panels and buttons.
        public static readonly Rect CameraCorner = new Rect(20, 578, 216, 122);
        // Where this screen shows the small webcam view; Rect.zero where the screen draws its own large preview
        // (camera setup, face capture, calibration, the run HUD). Nothing is drawn without a live webcam (PosePreview.DrawCorner).
        public Rect CameraSlot(GameFlowState state)
        {
            if (Data == null) return Rect.zero;
            if (Loading || Revealing) return CameraCorner;
            if (state == GameFlowState.TitleIdle)
            {
                switch (Page)
                {
                    case MenuPage.Identity: return Rect.zero;
                    // Inside the setup panel, under the play style buttons.
                    // Left of the switch panel, under the cards.
                    case MenuPage.Setup: return new Rect(60, 420, 260, 146);
                    // Between the big buttons and the monkey card.
                    case MenuPage.Main: return new Rect(600, 540, 216, 122);
                    case MenuPage.Ranking: return new Rect(1044, 590, 216, 122);
                    // Inside the sign-up panel, right of the step's main button.
                    case MenuPage.SignUp: return SignUp.Step == SignUpStep.Capture ? Rect.zero : new Rect(880, 462, 210, 118);
                    default: return CameraCorner;
                }
            }
            if (state == GameFlowState.Running || state == GameFlowState.Caught) return session.Paused ? CameraCorner : Rect.zero;
            if (state == GameFlowState.Result) return calibration.Open ? Rect.zero : CameraCorner;
            return Rect.zero;
        }
        private PlayStyle setupStyle = PlayStyle.FullBody;
        // 시작 on the play style screen: the saved name and weight with the chosen style (no calorie goal).
        public void StartFromPlayMode()
        {
            var player = Data?.Player;
            BeginRun?.Invoke(new PlayerProfile { Name = player?.Name?.Trim() ?? "", Weight = player?.Weight?.Trim() ?? "", Style = setupStyle });
        }
        private void DrawMenu()
        {
            // Esc is "뒤로" on the pages whose back button returns to the main menu (the profile handles its own keys).
            var e = Event.current;
            if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape && (Page == MenuPage.Ranking || Page == MenuPage.Setup || Page == MenuPage.Help))
            { e.Use(); OpenMainMenu(); return; }
            if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape && Page == MenuPage.Week) { e.Use(); ShowPage(MenuPage.Profile); return; }
            switch (Page)
            {
                case MenuPage.None: title.Draw(TitleLogo, Fade, OpenLogin); break;
                case MenuPage.Setup:
                    playView.Gestures.Pass();
                    if (e.type == EventType.KeyDown && (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter)) { e.Use(); StartFromPlayMode(); }
                    else if (e.type == EventType.KeyDown && e.keyCode >= KeyCode.Alpha1 && e.keyCode <= KeyCode.Alpha3) { e.Use(); var s = (PlayStyle)(e.keyCode - KeyCode.Alpha1); if (PlayStyleInfo.Allowed(s, Data == null || Data.BodyStylesAllowed)) { setupStyle = s; Data?.PreviewStyle(s); } }
                    break;
                case MenuPage.Profile:
                    if (Account == null || !Account.SignedIn) OpenMainMenu();
                    else profile.Draw(Profile, OpenMainMenu, Logout, DeleteAccount, () => ShowPage(MenuPage.Week));
                    break;
                case MenuPage.Ranking: rankingView.Gestures.Pass(); break;
                case MenuPage.Login: ChooseLogin(gate.Draw(Account, TitleLogo)); break;
                case MenuPage.SignUp: if (Account != null) signUp.Draw(Data, Account, SignUp, EnrollFace, OpenLogin); break;
                case MenuPage.Identity:
                    if (Account != null) identity.Draw(Data, Account, "얼굴 로그인", "정면을 보고 틀 안에 얼굴을 맞추면 잠시 뒤 자동으로 로그인됩니다.\n가입한 얼굴이면 이름 입력 없이 로그인됩니다.", "얼굴 로그인", LoginWithFace, OpenLogin);
                    break;
                case MenuPage.Main:
                    mainView.Gestures.Pass();
                    if (e.type == EventType.KeyDown && (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter)) { e.Use(); SelectMenu(MenuAction.Play); }
                    break;
                case MenuPage.TutorialOffer: { var choice = tutorialOffer.Draw(offerMode, TutorialMask?.Invoke() ?? 0, Data == null || Data.BodyStylesAllowed, Data?.Player?.Style ?? PlayStyle.Keyboard, out var style, out var chapter); ChooseTutorial(choice, style, chapter); break; }
                case MenuPage.Credits: if (credits.Draw()) CloseCredits(); break;
                case MenuPage.Help: helpView.Gestures.Pass(); break;
                case MenuPage.Week: weekView.Gestures.Pass(); break;
            }
        }
        private void OnDestroy() { skin?.Dispose(); overlaySkin?.Dispose(); }
    }
}
