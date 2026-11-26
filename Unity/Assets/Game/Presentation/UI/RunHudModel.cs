using System;
using Mongscape.Domain;
using Mongscape.Presentation.Map;
using UnityEngine;

namespace Mongscape.Presentation
{
    // During a run the only text line left is the tracking-lost notice and the fork key line (v2: no guidance text while running).
    public enum HudNoticeKind { None, Tracking, Fork }

    // Sample in Update, then render cached values for all of Unity's GUI events.
    public sealed class RunHudModel
    {
        public string Coins { get; private set; } = "0";
        // The double-coin item marks the coin counter with "×2" while it lasts.
        public bool CoinBoost { get; private set; }
        public string Workout { get; private set; } = "";
        public bool ShowWorkout { get; private set; }
        public string GoalLabel { get; private set; } = "";
        public string BurnLabel { get; private set; } = "";
        public string WorkoutHint { get; private set; } = "";
        public bool ShowPoseRecovery { get; private set; }
        public bool ShowSiren { get; private set; }
        // One slip strike is active: a bar shrinks over the clear window. ShowStrikeCleared flashes "경고 해제" afterwards.
        public bool ShowStrike { get; private set; }
        public float StrikeFraction { get; private set; }
        public bool ShowStrikeCleared { get; private set; }
        // Stair chunk: a small "무릎!" gauge (fills as the next step is not yet due) and a thin progress bar along the climb.
        public bool ShowStair { get; private set; }
        public float KneeGauge { get; private set; }
        public bool KneeWarning { get; private set; }
        public float StairProgress { get; private set; }
        public const float KneeWarnUrgency = .7f;
        private int previousStrikes;
        private float strikeClearedUntil;
        // Camera styles keep the webcam corner on screen unless the player turned it off in settings.
        public bool CameraEnabled { get; set; } = true;
        public bool ShowCamera { get; private set; }
        public float GoalProgress { get; private set; }
        public string Notice { get; private set; }
        public HudNoticeKind NoticeKind { get; private set; }
        public readonly string[] ItemLabels = new string[ItemCatalog.All.Length];
        public bool ShowFork {get;private set;}
        public bool ForkCommitted {get;private set;}
        public int ForkLane {get;private set;}
        public float ForkProgress {get;private set;}
        public string ForkCaption {get;private set;}
        // Fork preview (exercise plan 3): about PreviewSeconds before the choice, three cards at the top say where each
        // branch leads; the card the player leans toward lights up.
        public const float PreviewSeconds = 4f;
        public bool ShowForkPreview {get;private set;}
        public readonly int[] PreviewDestinations = new int[3];
        private static readonly string[] ChoiceCaptions={"왼쪽 선택","가운데 선택","오른쪽 선택"};
        private readonly int[] itemSeconds = new int[ItemCatalog.All.Length];
        private int coins = -1, kcal = -1;
        private double goal = -1;
        private int workoutMode = -1;
        private bool goalReached;
        private float lastZ = -1, lastTime = -1, lostSince = -1;

        // Play time without pauses as "플레이 3:12"; display only, not part of the score.
        public static string PlayTime(float seconds)
        {
            int total = float.IsNaN(seconds) || float.IsInfinity(seconds) ? 0 : (int)Math.Max(0, Math.Floor(seconds));
            return "플레이 " + Clock(seconds);
        }
        public static string Clock(float seconds)
        {
            int total = float.IsNaN(seconds) || float.IsInfinity(seconds) ? 0 : (int)Math.Max(0, Math.Floor(seconds));
            return total / 60 + ":" + (total % 60).ToString("00");
        }
        // One wording wherever a keyboard run's calories are shown as not counted.
        public const string KeyboardKcal = "키보드는 칼로리 제외";
        public static string Points(int score) => score.ToString("N0", System.Globalization.CultureInfo.InvariantCulture) + "점";

        public void Tick(RunSession session, ChunkSpawner map, PlayStyle style, PoseStatus pose, bool police, float now)
        {
            bool running = session.Flow.State == GameFlowState.Running && !session.Paused;
            // The beacon must remain visible even when another instruction has higher priority.
            ShowSiren = running && police;
            var plan=RouteSurface.ActivePlan;
            float lap=plan?.LapDistance(session.Runner.Z)??0;
            ShowFork=running && (plan?.Extended??false) && lap>=CartFork.Start && lap<CartFork.End;
            ForkCommitted=ShowFork && plan.ForkLocked(session.Runner.Z);
            ForkLane=ForkCommitted?plan.ChoiceAt(RouteSurface.SequenceAt(session.Runner.Z)):session.Runner.Lane;
            ForkProgress=Mathf.InverseLerp(CartFork.Start,CartFork.Decision,lap);
            int sequenceHere=RouteSurface.SequenceAt(session.Runner.Z);
            float previewFrom=CartFork.Decision-PreviewSeconds*Mathf.Max(1f,session.CurrentSpeed);
            ShowForkPreview=running && (plan?.HasFork(sequenceHere)??false) && !plan.ForkLocked(session.Runner.Z) && lap>=previewFrom && lap<CartFork.Decision;
            if(ShowForkPreview)for(int lane=-1;lane<=1;lane++)PreviewDestinations[lane+1]=plan.DestinationFor(sequenceHere,lane);
            ForkCaption=ForkCommitted?MapCourse.Course(plan.DestinationAt(sequenceHere)):ChoiceCaptions[Mathf.Clamp(ForkLane+1,0,2)];
            if (now < lastTime || session.Runner.Z < lastZ - .01f)
            {
                strikeClearedUntil = 0; previousStrikes = 0; lostSince = -1;
            }
            lastZ = session.Runner.Z; lastTime = now;
            ShowStrike = running && session.SlipStrikes == 1 && !session.SlipPursuit;
            StrikeFraction = ShowStrike ? Mathf.Clamp01(session.StrikeClearRemaining / Mathf.Max(.01f, session.Config.StrikeClearSeconds)) : 0;
            if (running && previousStrikes == 1 && session.SlipStrikes == 0) strikeClearedUntil = now + 1.5f;
            previousStrikes = session.SlipStrikes;
            ShowStair = running && session.InStair;
            KneeGauge = ShowStair ? 1f - Mathf.Clamp01(session.KneeUrgency) : 0f;
            KneeWarning = ShowStair && session.KneeUrgency > KneeWarnUrgency;
            StairProgress = ShowStair ? Mathf.Clamp01(session.StairProgress) : 0f;
            ShowStrikeCleared = running && now < strikeClearedUntil && !ShowStrike;
            if (session.Score.Coins != coins) { coins = session.Score.Coins; Coins = coins.ToString(); }
            CoinBoost = session.Items.IsActive(ItemKind.DoubleScore);
            // Keyboard runs and the tutorial count no calories, so the goal card only shows for camera runs (QA 2026-11-25 L9).
            ShowWorkout = PlayStyleInfo.UsesCamera(style) && !session.IsTutorial;
            int mode = ShowWorkout ? 2 : 0;
            GoalProgress = ShowWorkout ? (float)session.Workout.GoalProgress : 0;
            if (ShowWorkout)
            {
                int nextKcal = (int)Math.Round(session.Workout.EstimatedKcal * 10);
                double nextGoal = session.Workout.GoalKcal;
                bool reached = session.Workout.GoalReached;
                if (kcal != nextKcal || goal != nextGoal || reached != goalReached || workoutMode != mode)
                {
                    kcal = nextKcal; goal = nextGoal; goalReached = reached; workoutMode = mode;
                    GoalLabel = "목표  " + goal.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) + " kcal";
                    BurnLabel = "소모  " + (kcal / 10.0).ToString("F1", System.Globalization.CultureInfo.InvariantCulture) + " kcal";
                    WorkoutHint = reached ? "목표 달성!" : "몸동작으로 추정한 값";
                    Workout = BurnLabel + " / " + GoalLabel;
                }
            }
            else workoutMode = mode;
            for (int i = 0; i < ItemLabels.Length; i++)
            {
                var item = ItemCatalog.All[i];
                int seconds = session.Items.IsActive(item.Kind) ? Mathf.CeilToInt(session.Items.Remaining(item.Kind)) : 0;
                if (seconds == itemSeconds[i]) continue;
                itemSeconds[i] = seconds;
                ItemLabels[i] = seconds > 0 ? item.Name + " " + seconds + "초" : null;
            }
            Notice = null; NoticeKind = HudNoticeKind.None;
            if (!running)
            {
                ShowPoseRecovery = ShowCamera = false; lostSince = -1;
                return;
            }
            bool lost = PlayStyleInfo.UsesCamera(style) && pose != PoseStatus.Tracking;
            if (!lost) lostSince = -1;
            else if (lostSince < 0) lostSince = now;
            ShowPoseRecovery = lost && now - lostSince >= .75f;
            ShowCamera = CameraEnabled && PlayStyleInfo.UsesCamera(style);
            bool fork = RouteSurface.ActivePlan?.ForkWindow(session.Runner.Z) ?? false;
            NoticeKind = SelectNotice(ShowPoseRecovery, fork);
            switch (NoticeKind)
            {
                case HudNoticeKind.Tracking:
                    Notice = pose == PoseStatus.NoCamera || pose == PoseStatus.NoModel ? "카메라 연결을 확인하세요 (F1 설정)" : "몸이 보이게 서 주세요 (R 다시 맞추기)"; break;
                case HudNoticeKind.Fork: Notice = style == PlayStyle.Keyboard ? "A 왼쪽   C 가운데   D 오른쪽" : "← 몸을 옮겨 경로 선택 →"; break;
            }
        }

        public static HudNoticeKind SelectNotice(bool trackingLost, bool fork)
            => trackingLost ? HudNoticeKind.Tracking : fork ? HudNoticeKind.Fork : HudNoticeKind.None;
    }
}
