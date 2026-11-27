using System;
using Mongscape.Core;

namespace Mongscape.Domain
{
    public enum GameFlowState { Boot, TitleIdle, IntroTimeline, FirstPersonTakeover, Running, Caught, Result, Retry, LoadingIntro }
    // ReachLeft/ReachRight: one arm out to that side of the screen (the mine rescue, plan 3-0), sent again while held.
    public enum MotionCommand { Center, Left, Right, Jump, Crouch, KneeStep, ReachLeft, ReachRight, ArmsUp }
    public enum ObstacleKind { Crate, Pipe, Barrier, Cart, Laser }
    // SlipPursuit: caught after the second warning (by the section's chaser); kept under its old name.
    public enum CaptureReason { Collision, BrokenRail, SlipPursuit }
    // What gave a warning (plan 6-8). Pace: the police gauge filled (cadence too low); Gauge: another chaser's gauge filled.
    public enum WarningCause { Collision, Banana, Pace, Gauge, Spotted }
    // What made the runner slip: a floor hazard or a missed knee step slows, a banana is also a warning, Collision is the fall after a hit.
    public enum SlipSource { Floor, Banana, KneeMiss, Collision }

    public sealed class GameFlowController
    {
        public GameFlowState State { get; private set; } = GameFlowState.Boot;
        public event Action<GameFlowState> Changed;
        public bool Transition(GameFlowState next)
        {
            bool valid = (State == GameFlowState.Boot && next == GameFlowState.TitleIdle)
                || (State == GameFlowState.TitleIdle && next == GameFlowState.IntroTimeline)
                || (State == GameFlowState.TitleIdle && next == GameFlowState.LoadingIntro)
                || (State == GameFlowState.LoadingIntro && next == GameFlowState.Running)
                || (State == GameFlowState.IntroTimeline && next == GameFlowState.FirstPersonTakeover)
                || (State == GameFlowState.FirstPersonTakeover && next == GameFlowState.Running)
                || (State == GameFlowState.Running && next == GameFlowState.Caught)
                || (State == GameFlowState.Caught && next == GameFlowState.Result)
                || (State == GameFlowState.Result && next == GameFlowState.Retry)
                // Leaving a run from the pause menu abandons it without a result.
                || (State == GameFlowState.Running && next == GameFlowState.Retry)
                || (State == GameFlowState.Retry && next == GameFlowState.TitleIdle);
            if (!valid) return false;
            State = next;
            Changed?.Invoke(State);
            return true;
        }
    }

    [Serializable]
    public sealed class RunConfig
    {
        public float LaneSpacing = 2.2f, LaneSeconds = .20f, BaseSpeed = 9f, MaxSpeed = 17f;
        public float JumpHeight = 1.8f, Gravity = 24f, CrouchSeconds = .8f;
        // A knee step keeps the runner climbing for this long; the next alternating step has to land inside it.
        public float KneeStepGap = 1f;
        public float LightHit = .18f, HeavyHit = .35f, Recovery = .018f, StumbleSeconds = .8f;
        public int CleanBonus = 25; // Unused since score v2; kept for saved settings.
        public float SlipSeconds = 2.4f, SlipSpeedMultiplier = .5f, CartSpeed = 9f;
        public float BananaCatchSeconds=1.2f;
        // Big movements (exercise plan 6): a jump goes this much higher, a deep slide lasts this much longer, a high knee step
        // on the stairs takes the police back this far (pressure units), a wide side step crosses in this share of the time.
        public float BigJumpHeight = 1.35f, DeepSlideFactor = 1.5f, BigKneeRelief = .08f, BigLaneTime = .5f;
        // Unused since warnings last until the map is left (plan 6-8); kept for saved settings.
        public float StrikeClearSeconds=8f;
        // After a warning (or a guard that took one) the runner is falling: for this long no new warning lands, so one hit
        // touching two pieces of a row, or a banana right behind it, is one warning.
        public float WarningGraceSeconds=1f;
        public float SpeedRampDistance = 120f;
        // Jump/slide pressed shortly before landing runs on landing. Zero keeps the strict legacy rule.
        public float InputBufferSeconds = 0f;
        // Speed curve (extended routes only): on foot and in the cart the speed follows the difficulty curve from the start
        // to the max value and changes smoothly at SpeedChangeRate; off, BaseSpeed/CartSpeed plus SpeedRampDistance apply.
        public bool SpeedCurve = false;
        public float RunSpeedStart = 6f, RunSpeedMax = 9.5f, CartSpeedStart = 5f, CartSpeedMax = 7f, SpeedChangeRate = 2.5f;
        public bool RandomizeLayout = false;
        public bool ExtendedRoutes = true;
        // Difficulty curve: nothing rises before the grace distance, everything reaches its max at the full distance.
        public float DifficultyGraceDistance = 150f, DifficultyFullDistance = 2000f;
        public float ObstacleChanceStart = .25f, ObstacleChanceMax = .9f;
        // Kept equal: a failed blocker roll gives the chunk full-width rows or an animal instead, so lowering it adds hazards.
        public float BlockerChanceStart = .72f, BlockerChanceMax = .72f;
        public float DoubleBlockerStart = .3f, DoubleBlockerMax = .85f;
        public float BananaChanceStart = .3f, BananaChanceMax = .82f;
    }

    // Per-chunk hazard odds: single-lane rows, a lane-blocker chunk, a blocker on both open lanes, a banana per lane.
    public readonly struct HazardOdds
    {
        public readonly float Row, Blocker, DoubleBlocker, Banana;
        public HazardOdds(float row, float blocker, float doubleBlocker, float banana) { Row = row; Blocker = blocker; DoubleBlocker = doubleBlocker; Banana = banana; }
    }

    public sealed class ChaseSystem
    {
        public float Pressure { get; private set; }
        public bool IsCaught => Pressure >= 1f;
        public void Hit(float amount) { Pressure = Math.Min(1f, Math.Max(0f, Pressure + amount)); }
        public void Recover(float amount) { if (!IsCaught) Hit(-Math.Max(0f, amount)); }
        public void Reset() { Pressure = 0f; }
    }

    public sealed class ScoreSystem
    {
        public float Distance { get; private set; }
        // Distance plus exercise (RunScore); rescued monkeys and clean passes do not score (plan 3-2).
        public int Score => RunScore.From(Distance, workout);
        // Obstacles and rail gaps cleared; kept for the record only.
        public int CleanPasses { get; private set; }
        private readonly WorkoutEstimate workout;
        public ScoreSystem(WorkoutEstimate runWorkout = null) { workout = runWorkout; }
        public void Advance(float metres) { Distance += Math.Max(0f, metres); }
        public void CleanPass() { CleanPasses++; }
        public void Reset() { Distance = 0f; CleanPasses = 0; }
    }

    public sealed class DifficultyDirector
    {
        public float Speed(float distance, RunConfig config, bool riding = false)
            => Math.Min(config.MaxSpeed, (riding ? config.CartSpeed : config.BaseSpeed)
                + Math.Max(0f, distance) / Math.Max(1f, config.SpeedRampDistance));
        // 0 until the grace distance, then a smoothstep that reaches 1 at the full distance.
        public float Progress(float distance, RunConfig config)
        {
            if (distance >= config.DifficultyFullDistance) return 1f;
            if (distance <= config.DifficultyGraceDistance) return 0f;
            float t = (distance - config.DifficultyGraceDistance) / (config.DifficultyFullDistance - config.DifficultyGraceDistance);
            return t * t * (3f - 2f * t);
        }
        public float Ramp(float start, float max, float distance, RunConfig config) => start + (max - start) * Progress(distance, config);
        public HazardOdds Odds(float distance, RunConfig config) => new HazardOdds(
            Ramp(config.ObstacleChanceStart, config.ObstacleChanceMax, distance, config),
            Ramp(config.BlockerChanceStart, config.BlockerChanceMax, distance, config),
            Ramp(config.DoubleBlockerStart, config.DoubleBlockerMax, distance, config),
            Ramp(config.BananaChanceStart, config.BananaChanceMax, distance, config));
    }

    public static class SafePatternValidator
    {
        // A complete row always reserves an empty lane. Rows are separated by 12m.
        public static bool IsSafe(int occupiedLaneMask) => (occupiedLaneMask & 7) != 7;
        public static bool Avoided(ObstacleKind kind, float height, bool crouch)
        {
            switch (kind)
            {
                case ObstacleKind.Crate: return height > .9f;
                case ObstacleKind.Pipe: return crouch;
                case ObstacleKind.Cart: return height > .9f;
                default: return false;
            }
        }
        public static bool IsActionRowSafe(int mask,ObstacleKind kind)=>IsSafe(mask)||kind==ObstacleKind.Crate||kind==ObstacleKind.Pipe;
    }

    public sealed class RunnerController
    {
        private readonly RunConfig config;
        private float laneFrom, laneTime, verticalSpeed, crouchLeft, stumbleLeft, slowLeft, jumpBuffer, crouchBuffer;
        // The runner has no clock of its own: it counts the time it was ticked.
        private float clock, lastKneeStep = NoKneeStep;
        private const float NoKneeStep = -100f;
        public int Lane { get; private set; }
        public float X { get; private set; }
        public float Y { get; private set; }
        public float Z { get; private set; }
        public bool Crouching => crouchLeft > 0f;
        // The slide was lengthened by a deep crouch (the far half of the under-bar coin line needs it).
        public bool DeepSliding => Crouching && deepSlide;
        private bool deepSlide;
        public float SinceKneeStep => clock - lastKneeStep;
        // Counts every accepted KneeStep (feedback such as footsteps and camera bob listens for changes).
        public int KneeSteps { get; private set; }
        public bool KneeStepping => SinceKneeStep < config.KneeStepGap;
        public bool Stumbling => stumbleLeft > 0f;
        public bool Riding => RouteSurface.IsCart(Z);
        public float CartLean=>Riding&&!(RouteSurface.ActivePlan?.RailChoiceLocked(Z)??false)?X/config.LaneSpacing:0;
        public float WorldLaneOffset=>Riding?(RouteSurface.ActivePlan?.CartLaneOffset(Z,X,CartLean*.16f)??CartLean*.16f):X;
        public bool Slowing => slowLeft > 0;
        public float SlowRemaining => slowLeft;
        public float SpeedMultiplier => slowLeft>0?config.SlipSpeedMultiplier:1;
        public RunnerController(RunConfig tuning) { config = tuning; Reset(); }
        // Every jump and arms-up command seen (the zoo judges them against its notes, even in the air).
        public int JumpCommands { get; private set; }
        public int ArmsUpCommands { get; private set; }
        // The zoo branch has one lane (plan 1-1): lane commands are ignored there.
        public bool OneLane => RouteSurface.ActivePlan != null && RouteSurface.ActivePlan.Extended && !Riding
            && SectionSpan.At(RouteSurface.ActivePlan, RouteSurface.SequenceAt(Z)).Kind == SectionKind.Zoo
            && SectionSpan.At(RouteSurface.ActivePlan, RouteSurface.SequenceAt(Z)).Branch;
        public void CenterLane() { if (Lane != 0) { laneFrom = X; laneTime = 0f; Lane = 0; } }
        public void Command(MotionCommand command)
        {
            if(RouteSurface.ActivePlan?.RailChoiceLocked(Z)??false)return;
            if (command == MotionCommand.Jump) JumpCommands++;
            if (command == MotionCommand.ArmsUp) { ArmsUpCommands++; return; }
            if ((command == MotionCommand.Left || command == MotionCommand.Right) && OneLane) return;
            int next = Lane;
            if (command == MotionCommand.Left) next = LaneRules.Move(Lane, -1);
            if (command == MotionCommand.Right) next = LaneRules.Move(Lane, 1);
            if(Riding && !(RouteSurface.ActivePlan?.ForkWindow(Z)??false))
            {
                if(command==MotionCommand.Left)next=-1;
                if(command==MotionCommand.Right)next=1;
                if(command==MotionCommand.Center)next=0;
            }
            else if(Riding&&command==MotionCommand.Center)next=0;
            next = SkipBlockedRail(next, command == MotionCommand.Center);
            if (next != Lane) { laneFrom = X; laneTime = 0f; Lane = next; }
            if (command == MotionCommand.Jump && !Riding)
            {
                if (Y <= .001f) StartJump();
                else jumpBuffer = config.InputBufferSeconds;
            }
            if (command == MotionCommand.Crouch)
            {
                if (Y <= .001f && verticalSpeed <= 0f) crouchLeft = config.CrouchSeconds;
                else crouchBuffer = config.InputBufferSeconds;
            }
            if (command == MotionCommand.KneeStep && !Riding) { lastKneeStep = clock; KneeSteps++; }
        }
        // Leans toward the closed rail of the fork (plan 3-2): the cart does not go; the view jolts and plays the blocked sound.
        public int BlockedLeans { get; private set; }
        // At the fork the previous lap's rail is closed: a lean into it stays put, a lean across a closed middle rail goes on
        // to the far one.
        private int SkipBlockedRail(int next, bool centre)
        {
            var plan = RouteSurface.ActivePlan;
            if (next == Lane || !Riding || plan == null || !plan.ForkWindow(Z)) return next;
            int sequence = RouteSurface.SequenceAt(Z);
            if (!plan.IsBlocked(sequence, next)) return next;
            int beyond = LaneRules.Move(next, Math.Sign(next - Lane));
            if (!centre && beyond != next && !plan.IsBlocked(sequence, beyond)) return beyond;
            BlockedLeans++;
            return Lane;
        }
        public void Stumble() { stumbleLeft = config.StumbleSeconds; }
        // A big jump while still rising: the arc reaches BigJumpHeight times the normal height.
        public bool BoostJump()
        {
            if (Riding || Y <= .001f || verticalSpeed <= 0f) return false;
            float apex = config.JumpHeight * config.BigJumpHeight;
            if (Y >= apex) return false;
            verticalSpeed = Math.Max(verticalSpeed, (float)Math.Sqrt(2f * config.Gravity * (apex - Y))); return true;
        }
        // A deep crouch keeps the slide going longer.
        public bool DeepSlide() { if (!Crouching) return false; crouchLeft = Math.Max(crouchLeft, config.CrouchSeconds * config.DeepSlideFactor); deepSlide = true; return true; }
        // A wide side step finishes the lane change sooner.
        public void HurryLane() { laneTime = Math.Max(laneTime, config.LaneSeconds * (1 - config.BigLaneTime)); }
        public float JumpApex => config.JumpHeight;
        public void Slip() { slowLeft = config.SlipSeconds; }
        public void SetStartZ(float z) { Z = z; }
        public void Tick(float dt, float speed)
        {
            if (dt <= 0f) return;
            if(RouteSurface.ActivePlan?.RailChoiceLocked(Z)??false)
            {
                Lane=RouteSurface.ActivePlan.ChoiceAt(RouteSurface.SequenceAt(Z));laneFrom=X=Lane*config.LaneSpacing;laneTime=1;
            }
            laneTime += dt;
            float t = Math.Min(1f, laneTime / Math.Max(.01f, config.LaneSeconds));
            t = t * t * (3f - 2f * t);
            X = laneFrom + (Lane * config.LaneSpacing - laneFrom) * t;
            // Analytic gravity keeps the arc stable even at low frame rates.
            Y = Math.Max(0f, Y + verticalSpeed * dt - config.Gravity * dt * dt * .5f);
            verticalSpeed = Y <= 0f ? Math.Max(0f, verticalSpeed - config.Gravity * dt) : verticalSpeed - config.Gravity * dt;
            if(Riding){Y=0;verticalSpeed=0;lastKneeStep=NoKneeStep;jumpBuffer=0;}
            if (Y <= .001f && verticalSpeed <= 0f)
            {
                if (jumpBuffer > 0f) { jumpBuffer = 0f; crouchBuffer = 0f; StartJump(); }
                else if (crouchBuffer > 0f) { crouchBuffer = 0f; crouchLeft = config.CrouchSeconds; }
            }
            jumpBuffer = Math.Max(0f, jumpBuffer - dt);
            crouchBuffer = Math.Max(0f, crouchBuffer - dt);
            bool wasRiding=Riding;
            Z += speed * SpeedMultiplier * dt;
            if(wasRiding!=Riding){laneFrom=X;Lane=0;laneTime=0;}
            if(wasRiding&&!Riding&&(RouteSurface.ActivePlan?.Extended??false)){X=laneFrom=0;laneTime=1;}
            crouchLeft = Math.Max(0f, crouchLeft - dt);
            if (crouchLeft <= 0f) deepSlide = false;
            clock += dt;
            stumbleLeft = Math.Max(0f, stumbleLeft - dt);
            slowLeft = Math.Max(0f, slowLeft - dt);
        }
        private void StartJump()
        {
            crouchLeft = 0f; deepSlide = false;
            verticalSpeed = (float)Math.Sqrt(2f * config.Gravity * config.JumpHeight);
        }
        public void Reset() { deepSlide = false; Lane = 0; X = Y = 0f; Z = 2f; laneFrom = verticalSpeed = crouchLeft = stumbleLeft = slowLeft = jumpBuffer = crouchBuffer = 0f; laneTime = 1f; clock = 0f; lastKneeStep = NoKneeStep; KneeSteps = 0; BlockedLeans = 0; }
    }

    public sealed class RunSession
    {
        public readonly GameFlowController Flow = new GameFlowController();
        public readonly ChaseSystem Chase = new ChaseSystem();
        // Score reads Workout for the exercise part, so it is built in the constructor.
        public readonly ScoreSystem Score;
        public readonly DifficultyDirector Difficulty = new DifficultyDirector();
        public readonly RunnerController Runner;
        public readonly StairJudge Stair;
        // For the HUD: inside a stair chunk's judged span, how far along it is, and how close the next step is to being late.
        public bool InStair => Stair.InStair;
        public float StairProgress => Stair.StairProgress;
        public float KneeUrgency => Stair.KneeUrgency;
        public readonly RunConfig Config;
        public readonly WorkoutEstimate Workout=new WorkoutEstimate();
        public readonly PowerUpState Items=new PowerUpState();
        public bool Paused {get; set;}
        // Tutorial runs never end in a capture: a mistake is only counted and the tutorial restarts the step.
        public bool IsTutorial { get => tutorial; set { tutorial = value; TutorialMisses = 0; } }
        public int TutorialMisses {get;private set;}
        // The tutorial holds the runner (0) for its calibration step and a retry, and eases it (.5) at the finish; 1 = normal speed.
        public float SpeedScale {get;set;}=1f;
        private bool tutorial;
        // Demo invincibility (development builds only, toggled by the operator): no capture, fall or slip ends or slows the run.
        // It survives Reset so it stays on across runs until switched off. Tutorial lessons still count mistakes.
        public bool Invincible {get;set;}
        // The tutorial's warning lessons let one warning land for real (the slot fills, the chaser's sound) instead of a miss.
        public bool TutorialWarnings {get;set;}
        // The HUD blinks the empty warning slot: one more is the capture (the tutorial's banana lesson).
        public bool BlinkNextWarning {get;set;}
        private bool TutorialWarn(WarningCause cause)
        {
            SlipStrikes = Math.Min(MaxWarnings - 1, SlipStrikes + 1); lastStrike = clock; lastHit = clock; lastWarnedAt = clock; LastWarning = cause;
            Warned?.Invoke(cause, Chaser);
            return true;
        }
        // Puts back the tutorial's lesson warning after a restart (never the capture).
        public void SetTutorialWarnings(int count) { if (tutorial) SlipStrikes = Math.Max(0, Math.Min(MaxWarnings - 1, count)); }
        // Warnings in the current map (plan 6-8): two slots, the second starts the chaser's pursuit and the capture.
        // They last until the map is left; there is no timed clear any more. SlipStrikes is the old name.
        public const int MaxWarnings = 2;
        public int SlipStrikes {get;private set;}
        public int Warnings => SlipStrikes;
        public WarningCause LastWarning {get;private set;}
        // Raised for every warning that lands (not the ones a shield absorbs), with the chaser of the map: sound and HUD hooks.
        public event Action<WarningCause, ChaserDef> Warned;
        // Who caught the runner (valid in Caught and Result).
        public ChaserKind CaughtBy {get;private set;}
        // A chaser's approach gauge for the maps that fill one from their own events (lion: missed beats, warden: missed
        // knee steps). Full gives a warning and drops back to half. The police in the mine use the pace (ApplyPace).
        public float ChaserGauge {get;private set;}
        // What caused the latest slowdown slip (sound feedback tells a banana from a missed knee step).
        public SlipSource LastSlipSource {get;private set;}
        // Raised when leaving a map empties the warning slots, so the HUD can show the all-clear.
        public event Action StrikeCleared;
        public int ShieldSaves {get;private set;}
        public bool SlipPursuit=>bananaCatchLeft>0;
        private bool Curve => Config.SpeedCurve && Config.ExtendedRoutes;
        // Which map the runner is in (plan 3-3): the mine follows the distance curve, the zoo and the prison set their own speed.
        public readonly SectionTracker Sections = new SectionTracker();
        public SectionDef Section => Sections.Def;
        public ChaserDef Chaser => Sections.Chaser;
        // Base speed of a map that does not follow the distance curve: the zoo sets its base speed, the prison its set.
        // Unset falls back to BaseSpeed. Cleared at every section boundary.
        public float? SectionSpeed { get; set; }
        private bool Smoothed => Curve || !Section.UsesDistanceCurve;
        public float TargetSpeed => !Section.UsesDistanceCurve ? FixedSpeed : Curve ? CurveSpeed(Score.Distance, Runner.Riding) : Difficulty.Speed(Score.Distance, Config, Runner.Riding);
        private float FixedSpeed => Math.Max(0f, SectionSpeed ?? Config.BaseSpeed);
        // Hazard odds for a chunk starting at route position z: the distance curve in the mine, the starting odds elsewhere
        // (the zoo and the prison place their own rows; whatever random rows remain must not get harder with distance).
        public HazardOdds OddsAt(float z)
        {
            var def = SectionSpan.At(RouteSurface.ActivePlan, RouteSurface.SequenceAt(z)).Def;
            return Difficulty.Odds(def.UsesDistanceCurve ? z : 0f, Config);
        }
        public float CurrentSpeed => Flow.State == GameFlowState.Running && !Paused ? MoveSpeed * Runner.SpeedMultiplier * SpeedScale : 0f;
        // On foot the cadence sets the pace (CadencePace: 0.7 walking to 1.3 sprinting); the cart keeps its own speed.
        // Every map on foot (the zoo too, owner 2026-11-27): the map's base speed times the cadence pace (CadencePace: 0.7
        // walking to 1.3 sprinting), at most MaxFootSpeed; the cart keeps its own speed.
        private float MoveSpeed => Runner.Riding ? (Smoothed ? smoothSpeed : TargetSpeed) : Math.Min(MaxFootSpeed, (Smoothed ? smoothSpeed : TargetSpeed) * CadenceScale);
        // The top on-foot speed (m/s): the mine curve tops out at 17, so a sprint at the end never runs away.
        public const float MaxFootSpeed = 18f;
        // Set each frame from CadencePace (1 for the keyboard, the option off, or the tutorial).
        public float CadenceScale { get; set; } = 1f;
        // How close the police come because the player is below the target cadence (0..CadencePace.MaxPressure).
        // Separate from Chase: it moves the police and reddens the screen edges but never makes a capture.
        public float PacePressure { get; set; }
        public float PolicePressure => Math.Max(Chase.Pressure, PacePressure);
        // On-foot speed expected at a route distance with the current cadence (a little faster, so hazards planned now still
        // keep their time gap if the player speeds up before reaching them).
        public float PlannedSpeed(float distance)
        {
            bool curveMap = SectionSpan.At(RouteSurface.ActivePlan, RouteSurface.SequenceAt(distance)).Def.UsesDistanceCurve;
            float speed = !curveMap ? (Section.UsesDistanceCurve ? Config.BaseSpeed : FixedSpeed) : Curve ? CurveSpeed(distance, false) : Difficulty.Speed(distance, Config);
            return speed * Math.Min(CadencePace.Sprint, CadenceScale + .3f);
        }
        public CaptureReason Reason {get;private set;}
        private float bananaCatchLeft, smoothSpeed;
        private ChaserDef pursuer;
        private float lastHit = -100f, clock, lastStrike, lastWarnedAt = -100f;
        public bool InWarningGrace => clock - lastWarnedAt < Config.WarningGraceSeconds;
        // Play time without pauses; shown to the player, not part of the score.
        public float Elapsed => clock;
        // Monkeys stuck in the mine walls (plan 3-0): reach toward them in their lane to rescue them.
        public readonly MineRescueJudge MineRescue = new MineRescueJudge();
        // A reach command from the keys or the pose (the other commands go to the runner).
        public void Command(MotionCommand command)
        {
            if (command == MotionCommand.ReachLeft) MineRescue.Reach(-1);
            else if (command == MotionCommand.ReachRight) MineRescue.Reach(1);
            else Runner.Command(command);
        }
        public RunSession(RunConfig config)
        {
            Config = config; Score = new ScoreSystem(Workout); Runner = new RunnerController(config); Stair = new StairJudge(config); smoothSpeed = TargetSpeed; Sections.Exited += LeaveSection;
            Sections.Entered += span => { if (span.Kind == SectionKind.Prison && span.Branch) Prison.Begin(span.Visit); };
            // A cell door and the roof bonus each free one monkey (plan 2-3a).
            Prison.CellOpened += _ => Rescue(RescueSource.PrisonLock);
            Prison.RoofRescue += () => Rescue(RescueSource.PrisonLock);
            // A mine wall monkey (plan 3-0): reached out to, or in the runner's lane while the call item lasts.
            MineRescue.CallActive = () => CallActive;
            MineRescue.RescuedAt += (sequence, side) => Rescue(CallActive ? RescueSource.Call : RescueSource.MineWall);
            // Seen by the searchlight (plan 2-3a): one warning, rung by the warden's alarm bell (ChaserDef.Warden).
            Prison.Alarm += set => Warn(WarningCause.Spotted);
        }
        // The prison branch (PrisonCourse): fixed sets of stairs, a searchlight hold at a cell door and a flat stretch.
        public readonly PrisonCourse Prison = new PrisonCourse();
        // Fed each frame by the input: crouched under the line (pose), and how deep (0..1). A negative depth is the keyboard:
        // holding S (the runner's crouch) hides and the lock fills at the base rate.
        public bool HidePose { get; set; }
        public float HideDepth { get; set; } = -1f;
        private bool Hiding => HideDepth < 0f ? Runner.Crouching : HidePose;
        // How far the runner may move this tick: up to the prison door while it holds there.
        private float PrisonStep(float dt, float move)
        {
            int sequence = RouteSurface.SequenceAt(Runner.Z);
            int branch = tutorial ? TutorialCourse.PrisonIndex(sequence) : PrisonCourse.BranchIndex(RouteSurface.ActivePlan, sequence);
            float local = Runner.Z - sequence * RouteSurface.Length;
            float stop = Prison.Tick(dt, branch, local, branch >= 0 && Hiding, HideDepth);
            Stair.GapScale = branch >= 0 ? Prison.GapScale : 1f;
            // MET hints (plan 5-5): a half squat while hiding, fast stairs on later visits or with the warden behind.
            var hints = Workout.Hints;
            hints.DeepCrouch = branch >= 0 && Prison.Holding && Hiding && HideDepth >= .9f;
            hints.FastStairs = branch >= 0 && Prison.Phase == PrisonPhase.Stairs && (Prison.WardenChasing || Prison.Visit >= 2);
            Workout.Hints = hints;
            if (branch < 0) return move;
            SectionSpeed = Prison.Speed;
            if (Prison.Holding)
            {
                smoothSpeed = 0f;
                // The body holds the hide; keep the runner's crouch (animation, camera) going with it.
                if (Hiding) Runner.Command(MotionCommand.Crouch);
            }
            if (stop >= float.MaxValue || dt <= 0f) return move;
            float per = dt * Math.Max(.01f, Runner.SpeedMultiplier);
            return Math.Min(move, Math.Max(0f, stop - local) / per);
        }
        private float CurveSpeed(float distance, bool riding) => riding
            ? Difficulty.Ramp(Config.CartSpeedStart, Config.CartSpeedMax, distance, Config)
            : Difficulty.Ramp(Config.RunSpeedStart, Config.RunSpeedMax, distance, Config);
        public void Tick(float dt)
        {
            if (Flow.State != GameFlowState.Running || Paused) return;
            clock += dt;
            float target = TargetSpeed;
            smoothSpeed = smoothSpeed < target ? Math.Min(target, smoothSpeed + Config.SpeedChangeRate * dt) : Math.Max(target, smoothSpeed - Config.SpeedChangeRate * 1.6f * dt);
            Items.Tick(dt);
            float z = Runner.Z;
            Runner.Tick(dt, PrisonStep(dt, MoveSpeed * SpeedScale));
            Score.Advance(Runner.Z - z);
            var before = Sections.Current;
            Sections.Update(RouteSurface.ActivePlan, Runner.Z);
            if (!before.SameStay(Sections.Current)) SectionSpeed = null;
            Workout.Section = Section.Kind;
            if (!tutorial) MineRescue.Tick(dt, RouteSurface.ActivePlan, Runner.Z, Runner.Lane, MoveSpeed * Runner.SpeedMultiplier * SpeedScale);
            else MineRescue.Advance(dt);
            // Stair chunks: a missed knee rhythm counts as a slip strike; in the tutorial it is a counted miss that restarts the step.
            int chunk = RouteSurface.SequenceAt(Runner.Z);
            bool onStair = RouteSurface.IsStair(RouteSurface.TypeAt(chunk));
            if (Stair.Tick(dt, onStair, Runner.Z - chunk * RouteSurface.Length, Runner))
            {
                if (tutorial) { lastHit = clock; TutorialMisses++; }
                else Slip(SlipSource.KneeMiss);
            }
            if(SlipPursuit)
            {
                bananaCatchLeft=Math.Max(0,bananaCatchLeft-dt);
                Chase.Hit(.55f*dt/Math.Max(.1f,Config.BananaCatchSeconds));
                if(bananaCatchLeft<=.0001f)Fail(CaptureReason.SlipPursuit);
            }
            else if (SlipStrikes==0&&clock - lastHit > 2f) Chase.Recover(Config.Recovery * dt);
        }
        // Rescued monkeys of this run (plan 4): the map rescues call Rescue, the troop view reads Troop.
        public readonly MonkeyTroop Troop = new MonkeyTroop();
        // The traits this run plays with (plan 4-1), fixed at its start by ApplyTraits; the tutorial plays without.
        public RunTraits Traits { get; private set; } = RunTraits.None;
        // Call after Reset when a run starts: fixes the traits and hands out the starting shield (Shield Lv3).
        public void ApplyTraits(RunTraits traits)
        {
            Traits = traits ?? RunTraits.None; Items.Traits = Traits; Prison.LockBonus = tutorial ? 0f : Traits.LockGaugeBonus; Prison.LeadBonus = tutorial ? 0f : Traits.WarningLeadSeconds; thickFurLeft = Traits.ThickFur; loyaltyLeft = Traits.Loyalty;
            if (Traits.StartsWithShield && !tutorial) Items.GiveStartingShield();
        }
        // A monkey joins the troop (a mine wall rescue, a prison lock, the call item). Ignored outside a running run and in the tutorial.
        public bool Rescue(RescueSource source, int count = 1)
        {
            if (Flow.State != GameFlowState.Running || tutorial || count <= 0) return false;
            Troop.Rescue(source, count); return true;
        }
        // While the call item lasts, a monkey in the runner's lane is rescued without reaching out (plan 3-0).
        public bool CallActive => Items.IsActive(ItemKind.Call);
        // Monkeys this run brings home: the kept troop plus the sweat trait's bonus for the run's moderate time.
        public int MonkeysEarned => Troop.Kept + (tutorial ? 0 : Traits.SweatMonkeys(Workout.GoalSeconds));
        public enum WarningGuard { None, Shield, Loyalty }
        public int LoyaltySaves { get; private set; }
        private bool thickFurLeft, loyaltyLeft;
        // The warning system asks this before a warning lands: the shield item blocks it, then once per run a following
        // monkey takes it (Loyalty) and is taken away.
        public WarningGuard GuardWarning()
        {
            if (Items.ConsumeShield()) { ShieldSaves++; return WarningGuard.Shield; }
            if (loyaltyLeft && Troop.LoseOne()) { loyaltyLeft = false; LoyaltySaves++; return WarningGuard.Loyalty; }
            return WarningGuard.None;
        }
        // The latest warning landed under the thick fur: no slip with it.
        public bool SoftWarning { get; private set; }
        // Thick fur: true once per run, for the first warning, which then lands without the slip.
        private bool TakeThickFur() { if (!thickFurLeft) return false; thickFurLeft = false; return true; }
        // A solid obstacle (plan 6-8): the runner falls and is slowed for a moment, and it is one warning (a shield takes the
        // warning, the fall stays). In the zoo a fence trip is no warning: the lion closes in instead.
        // Returns whether the hit counted (a warning, or a tutorial miss).
        public bool Hit(bool heavy)
        {
            if (Flow.State != GameFlowState.Running || Paused) return false;
            if (tutorial) { Reason = CaptureReason.Collision; lastHit = clock; TutorialMisses++; return true; }
            if (Invincible) return false;
            Runner.Stumble(); lastHit = clock;
            if (Section.Kind == SectionKind.Zoo) { Runner.Slip(); LastSlipSource = SlipSource.Collision; Approach(.35f); return false; }
            if (!Warn(WarningCause.Collision)) return false;
            if (!SoftWarning) Runner.Slip();
            LastSlipSource = SlipSource.Collision;
            return true;
        }
        // One warning from the map's chaser. The second starts the pursuit (BananaCatchSeconds) that ends in the capture.
        // A shield absorbs the warning, then once per run a loyal monkey (GuardWarning). The first one that lands with the
        // thick fur trait sets SoftWarning: the caller skips the slip. Returns whether it landed (tutorial: a counted miss).
        public bool Warn(WarningCause cause)
        {
            if (Flow.State != GameFlowState.Running || Paused) return false;
            if (tutorial) { if (TutorialWarnings) return TutorialWarn(cause); lastHit = clock; TutorialMisses++; return true; }
            if (Invincible || SlipPursuit) return false;
            // One collision event is one warning: right after one, the fall makes the runner untouchable for a moment.
            if (InWarningGrace) return false;
            SoftWarning = false;
            if (GuardWarning() != WarningGuard.None) { lastHit = clock; lastWarnedAt = clock; return false; }
            lastWarnedAt = clock;
            SoftWarning = TakeThickFur();
            SlipStrikes = Math.Min(MaxWarnings, SlipStrikes + 1); lastStrike = clock; lastHit = clock; LastWarning = cause;
            if (SlipStrikes == 1) Chase.Hit(.45f);
            if (SlipStrikes >= MaxWarnings) { pursuer = Chaser; bananaCatchLeft = Math.Max(.1f, Config.BananaCatchSeconds); }
            Warned?.Invoke(cause, Chaser);
            return true;
        }
        // Fills the map chaser's gauge (lion, warden); full = a warning and the gauge drops to half.
        public bool Approach(float amount)
        {
            if (Flow.State != GameFlowState.Running || Paused || tutorial || Invincible) return false;
            ChaserGauge = Math.Max(0f, Math.Min(1f, ChaserGauge + amount));
            if (ChaserGauge < 1f) return false;
            ChaserGauge = .5f;
            return Warn(WarningCause.Gauge);
        }
        // The cadence each frame (plan 6-8): sets the pace and the police gauge. In the mine a full gauge means the police
        // pounce: one warning, and the gauge drops to half. Elsewhere the pace gauge never warns (stays under .75).
        public void ApplyPace(CadencePace pace)
        {
            CadenceScale = pace.Scale;
            bool warns = Section.Kind == SectionKind.Mine && !tutorial;
            if (warns && pace.Pressure >= 1f && Flow.State == GameFlowState.Running && !Paused && !SlipPursuit)
            {
                pace.Halve();
                Warn(WarningCause.Pace);
            }
            float before = PacePressure;
            PacePressure = warns ? pace.Pressure : Math.Min(CadencePace.LegacyMaxPressure, pace.Pressure);
            paceRising = warns && PacePressure > before;
        }
        // The cue before a chaser warning (plan 6-8): CueSeconds ahead, plus the sharp ears trait (plan 4-1).
        public const float CueSeconds = .6f;
        public float WarningCueSeconds => CueSeconds + Traits.WarningLeadSeconds;
        private bool paceRising;
        // True while a warning is about to land: the mine police gauge will fill within WarningCueSeconds at its rise rate.
        // Map stages with their own chaser cue (the zoo lion's roar, the prison searchlight) add WarningCueSeconds themselves.
        public bool WarningSoon => Flow.State == GameFlowState.Running && !Paused && !tutorial && !SlipPursuit && paceRising
            && (1f - PacePressure) / CadencePace.PressureRise <= WarningCueSeconds;
        // Leaving a map empties the warning slots and the gauge (not during a pursuit: that capture still happens).
        private void LeaveSection(SectionSpan left)
        {
            if (SlipPursuit) return;
            ChaserGauge = 0f;
            if (SlipStrikes == 0) return;
            SlipStrikes = 0; Chase.Reset(); StrikeCleared?.Invoke();
        }
        public bool Fail(CaptureReason reason)
        {
            if (Flow.State != GameFlowState.Running || Paused) return false;
            if (tutorial) { Reason = reason; lastHit = clock; TutorialMisses++; return true; }
            if (Invincible) return false;
            if (reason == CaptureReason.Collision && Items.ConsumeShield())
            {
                // The shield absorbs one solid hit: a stumble instead of a capture.
                ShieldSaves++; Runner.Stumble(); lastHit = clock; return false;
            }
            Catch(reason);
            return true;
        }
        // Operator key (F7): ends the run as a capture right away, even while invincible or shielded. Tutorial lessons are left alone.
        public bool ForceCatch()
        {
            if (Flow.State != GameFlowState.Running || Paused || tutorial) return false;
            Catch(CaptureReason.Collision); return true;
        }
        private void Catch(CaptureReason reason)
        {
            Reason=reason; CaughtBy=(reason==CaptureReason.SlipPursuit&&pursuer!=null?pursuer:Chaser).Kind;
            lastHit = clock;
            Runner.Stumble(); Chase.Hit(1f);
            Flow.Transition(GameFlowState.Caught);
        }
        // Floor hazards slow only. A banana slows and is one warning (a shield takes both). A missed knee step slows; in the
        // prison it also brings the warden closer (half a gauge), elsewhere it only slows (plan 6-8).
        public bool Slip(SlipSource source=SlipSource.Floor)
        {
            if(Flow.State!=GameFlowState.Running||Paused||Runner.Riding||Invincible&&!tutorial)return false;
            // The tutorial's banana lesson: stepping on a peel is a counted miss that restarts the step, never a strike or pursuit.
            if(tutorial&&source==SlipSource.Banana){Runner.Slip();LastSlipSource=source;lastHit=clock;if(TutorialWarnings)TutorialWarn(WarningCause.Banana);else TutorialMisses++;return true;}
            if(source==SlipSource.Banana)
            {
                // The shield (or a loyal monkey) absorbs the peel: no slowdown and no warning. During a pursuit it only slows.
                bool pursuit=SlipPursuit;
                bool landed=Warn(WarningCause.Banana);
                if(!landed&&!pursuit){lastHit=clock;return false;}
                if(!(landed&&SoftWarning))Runner.Slip();
                LastSlipSource=source;return true;
            }
            Runner.Slip();LastSlipSource=source;
            // In the prison a missed knee beat only slows (plan 2-3a), except on the stairs the warden chases after an alarm:
            // there it is a capture on the spot.
            if(source==SlipSource.KneeMiss&&!tutorial&&Section.Kind==SectionKind.Prison&&Prison.WardenChasing)Fail(CaptureReason.SlipPursuit);
            return true;
        }
        public enum BigMove { Jump, Crouch, Knee, Lane }
        // Big movements seen this run, by kind.
        public readonly int[] BigMoves = new int[4];
        // Applies a big movement's in-game effect. Returns whether the effect applied.
        public bool Big(BigMove move)
        {
            if (Flow.State != GameFlowState.Running || Paused) return false;
            bool applied = move == BigMove.Jump ? Runner.BoostJump() : move == BigMove.Crouch ? Runner.DeepSlide() : true;
            if (!applied) return false;
            if (move == BigMove.Knee) { Chase.Recover(Config.BigKneeRelief); PacePressure = Math.Max(0, PacePressure - Config.BigKneeRelief); }
            if (move == BigMove.Lane) Runner.HurryLane();
            BigMoves[(int)move]++;
            return true;
        }
        public void Reset() { Array.Clear(BigMoves, 0, BigMoves.Length); Troop.Reset(); LoyaltySaves = 0; thickFurLeft = Traits.ThickFur; loyaltyLeft = Traits.Loyalty; SoftWarning = false; paceRising = false; Runner.Reset(); Stair.Reset(); Chase.Reset(); Score.Reset(); Workout.Reset(); Items.Reset(); Paused=false; SpeedScale=1f; CadenceScale=1f; PacePressure=0f; clock = 0f; lastHit = -100f; lastWarnedAt = -100f;SlipStrikes=0;lastStrike=0f;ShieldSaves=0;bananaCatchLeft=0;pursuer=null;ChaserGauge=0f;LastWarning=WarningCause.Collision;CaughtBy=ChaserKind.Police;Reason=CaptureReason.Collision; Sections.Reset(); SectionSpeed = null; Prison.Reset(); MineRescue.Reset(); Stair.GapScale = 1f; HidePose = false; HideDepth = -1f; if (Traits.StartsWithShield && !tutorial) Items.GiveStartingShield(); smoothSpeed = TargetSpeed; }
    }
}
