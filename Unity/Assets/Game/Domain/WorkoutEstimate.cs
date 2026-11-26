using System;
using System.Globalization;

namespace Mongscape.Domain
{
    // Moments of a run, counted for the result report: steps, jumps, crouches, knee steps (stairs) and lane changes.
    public enum MoveKind { Step, Jump, Crouch, KneeStep, SideStep }

    // Calories: MET x 3.5 x weight / 200 per minute, summed over the moments the camera saw exercise.
    // This estimates gross expenditure; camera recognition does not measure metabolism.
    public sealed class WorkoutEstimate
    {
        // The flat MET used by the plain Tick overloads (tools and older tests). Runs tick by intensity instead.
        public const double AssumedMet=4;
        // An empty weight field uses this, so every camera run gets a calorie estimate.
        public const double DefaultWeightKg=65;
        // Accepted weight range; the account server and AccountSession use the same one.
        public const double MinWeightKg=10, MaxWeightKg=350;
        public static bool ValidWeight(double kg)=>!double.IsNaN(kg)&&!double.IsInfinity(kg)&&kg>=MinWeightKg&&kg<=MaxWeightKg;
        public double WeightKg {get; private set;}=DefaultWeightKg;
        public double ActiveSeconds {get; private set;}
        // MET x seconds over the run: calories and the score both come from it.
        public double MetSeconds {get; private set;}
        public double Met {get;set;}=AssumedMet;
        // Play style and map of the moment pick the Compendium activity (plan 5-5); the run session keeps Section current.
        public PlayStyle Style {get;set;}=PlayStyle.FullBody;
        public SectionKind Section {get;set;}=SectionKind.Mine;
        // Facts the zoo and prison scenes report each frame (deep crouch, fast stairs, missed beats).
        public MetHints Hints {get;set;}
        // The activity of the last counted moment (Standing when the last one counted nothing).
        public MetActivity LastActivity {get; private set;}
        public double EstimatedKcal=>KcalOf(MetSeconds,WeightKg);
        // Average MET over the active time (0 before any).
        public double AverageMet=>ActiveSeconds>0?MetSeconds/ActiveSeconds:0;
        public double GoalKcal {get; private set;}=50;
        private readonly double[] activitySeconds=new double[5], activityMetSeconds=new double[5];
        // Active seconds by cadence stage (index = CadenceStage; Standing stays 0) and those at moderate intensity or more.
        private readonly double[] stageSeconds=new double[4];
        // Seconds at 3 MET or more (vigorous included) and at 6 MET or more.
        public double ModerateSeconds {get; private set;}
        public double VigorousSeconds {get; private set;}
        // Weekly goal seconds (plan 5-5): a vigorous second counts two (WHO).
        public double GoalSeconds=>ModerateSeconds+VigorousSeconds;
        // Result report: seconds by intensity (light under 3, moderate 3 to 6, vigorous 6 and up) and by Compendium activity.
        private readonly double[] intensitySeconds=new double[3];
        private readonly double[] metActivitySeconds=new double[Enum.GetValues(typeof(MetActivity)).Length];
        public double SecondsAt(Intensity level)=>intensitySeconds[(int)level];
        public double SecondsIn(MetActivity activity)=>metActivitySeconds[(int)activity];
        // False when "달리기 속도 조절" is off: walk, run and sprint MET are 0 and only the moments of a jump, crouch,
        // knee raise or lane change count (MoveSeconds), so the result report shows that time instead of the stages.
        public bool CadenceCounts {get;set;}=true;
        public double MoveSeconds {get; private set;}
        // Seconds in which the movement was big (stage 4 feeds it; counted, no extra MET since plan 5-5).
        public double BigSeconds {get; private set;}
        private readonly int[] moves=new int[5];
        public bool GoalReached=>EstimatedKcal>=GoalKcal;
        public double GoalProgress=>Math.Min(1,EstimatedKcal/GoalKcal);
        public bool SetGoal(double kcal) { if(double.IsNaN(kcal)||double.IsInfinity(kcal)||kcal<1||kcal>5000)return false; GoalKcal=kcal;return true; }
        public bool SetGoal(string value) => double.TryParse(value,NumberStyles.AllowDecimalPoint,CultureInfo.InvariantCulture,out double kcal) && SetGoal(kcal);
        public double SecondsFor(int activity)=>activitySeconds[Math.Max(0,Math.Min(4,activity))];
        public double KcalFor(int activity)=>KcalOf(activityMetSeconds[Math.Max(0,Math.Min(4,activity))],WeightKg);
        public double StageSeconds(CadenceStage stage)=>stageSeconds[Math.Max(0,Math.Min(3,(int)stage))];
        public int Moves(MoveKind kind)=>moves[Math.Max(0,Math.Min(4,(int)kind))];
        public void CountMove(MoveKind kind,int count=1){ if(count>0) moves[Math.Max(0,Math.Min(4,(int)kind))]+=count; }
        public static double Kcal(double met,double weightKg,double seconds)=>Math.Max(0,met)*3.5*Math.Max(0,weightKg)/200*Math.Max(0,seconds)/60;
        public static double KcalOf(double metSeconds,double weightKg)=>Kcal(1,weightKg,metSeconds);
        // Allocate one time slice to one activity at the flat MET (older callers and tools).
        public void TickActivity(double seconds,bool playing,bool tracked,bool active,int activity)
        {
            if(!playing||!tracked||!active||!Valid(seconds)) return;
            Add(seconds,Met,true,(int)CadenceStage.Run,false,activity,MetActivity.MineGeneral);
        }
        // One camera frame of a run. stage comes from the cadence; moving says another motion (jump, crouch, knee, lane)
        // happened just now. Standing still or out of view counts nothing (no grace period). The MET is the Compendium
        // activity for the play style, map and motion (IntensityMet.Classify). With the cadence off stepping counts nothing.
        public void TickIntensity(double seconds,bool playing,bool tracked,CadenceStage stage,bool moving,bool big,int activity)
        {
            if(!playing||!tracked||!Valid(seconds)) return;
            var kind=IntensityMet.Classify(Style,Section,stage,CadenceCounts,moving,activity,Hints);
            LastActivity=kind;
            if(kind==MetActivity.Standing) return;
            double met=IntensityMet.Of(kind);
            int bucket=!CadenceCounts?-1:stage==CadenceStage.Standing?(int)CadenceStage.Walk:(int)stage;
            Add(seconds,met,IntensityMet.IsModerate(met),bucket,big,activity,kind);
        }
        // stage -1: a motion moment of a run without cadence (MoveSeconds).
        // moderate: the moment counts toward the weekly moderate minutes (the flat-MET ticks of tools and tests always do).
        private void Add(double seconds,double met,bool moderate,int stage,bool big,int activity,MetActivity kind)
        {
            intensitySeconds[(int)IntensityMet.Level(met)]+=seconds; metActivitySeconds[(int)kind]+=seconds;
            if(moderate&&IntensityMet.IsVigorous(met)) VigorousSeconds+=seconds;
            int a=Math.Max(0,Math.Min(4,activity));
            ActiveSeconds+=seconds; MetSeconds+=met*seconds;
            activitySeconds[a]+=seconds; activityMetSeconds[a]+=met*seconds;
            if(stage>=0) stageSeconds[stage]+=seconds; else MoveSeconds+=seconds;
            if(moderate) ModerateSeconds+=seconds;
            if(big) BigSeconds+=seconds;
        }
        private static bool Valid(double seconds)=>!double.IsNaN(seconds)&&!double.IsInfinity(seconds)&&seconds>0;
        public bool SetWeight(string value)
        {
            if(string.IsNullOrWhiteSpace(value)){WeightKg=DefaultWeightKg; Reset(); return true;}
            if(!double.TryParse(value,NumberStyles.AllowDecimalPoint,CultureInfo.InvariantCulture,out double kg)
                ||!ValidWeight(kg)) return false;
            WeightKg=kg; Reset(); return true;
        }
        public void Tick(double seconds,bool playing,bool poseConnected,MotionCommand recognizedMotion)
        {
            if(!playing||!poseConnected||recognizedMotion==MotionCommand.Center||!Valid(seconds)) return;
            Add(seconds,Met,true,(int)CadenceStage.Run,false,0,MetActivity.MineGeneral);
        }
        // Flat-MET seconds (tools and tests).
        public void Tick(double seconds,bool playing,bool active)
        {
            if(!playing||!active||!Valid(seconds)) return;
            Add(seconds,Met,true,(int)CadenceStage.Run,false,0,MetActivity.MineGeneral);
        }
        public void Reset()
        {
            ActiveSeconds=MetSeconds=ModerateSeconds=VigorousSeconds=BigSeconds=MoveSeconds=0; LastActivity=MetActivity.Standing;
            Array.Clear(intensitySeconds,0,intensitySeconds.Length); Array.Clear(metActivitySeconds,0,metActivitySeconds.Length);
            Array.Clear(activitySeconds,0,activitySeconds.Length); Array.Clear(activityMetSeconds,0,activityMetSeconds.Length);
            Array.Clear(stageSeconds,0,stageSeconds.Length); Array.Clear(moves,0,moves.Length);
        }
    }

    // Run score shared by the HUD, pause, result and ranking: round(distance m + score kcal × 100).
    // Score kcal always uses DefaultWeightKg so a heavier player does not score more for the same exercise.
    // Keyboard runs have no active seconds, so their score is the distance.
    // Version 3: the kcal follow the intensity MET of each moment (version 2 used a flat MET per play style).
    // Ranked records of an older version are kept but not ranked (a new ranking season).
    public static class RunScore
    {
        public const int Version=3, PreviousVersion=2;
        public const double KcalPoints=100;
        public static double ScoreKcal(double met,double activeSeconds)=>WorkoutEstimate.Kcal(met,WorkoutEstimate.DefaultWeightKg,activeSeconds);
        public static int From(double distance,double met,double activeSeconds)=>FromMetSeconds(distance,Math.Max(0,met)*Math.Max(0,activeSeconds));
        public static int FromMetSeconds(double distance,double metSeconds)
        {
            double metres=double.IsNaN(distance)||double.IsInfinity(distance)?0:Math.Max(0,distance);
            double kcal=WorkoutEstimate.KcalOf(metSeconds,WorkoutEstimate.DefaultWeightKg);if(double.IsNaN(kcal)||double.IsInfinity(kcal))kcal=0;
            return (int)Math.Min(int.MaxValue,Math.Round(metres+kcal*KcalPoints,MidpointRounding.AwayFromZero));
        }
        public static int From(double distance,WorkoutEstimate workout)=>workout==null?From(distance,0,0):FromMetSeconds(distance,workout.MetSeconds);
    }
}
