using System;

namespace Mongscape.Domain
{
    // Game states with their 2024 Adult Compendium activity (plan 5-5). The MET of each second comes from the closest code.
    public enum MetActivity
    {
        Standing,      // 07040 Standing quietly (1.3) -> 0 in the game, not exercise time
        MineGeneral,   // 22240 Active video game, total body, moderate (4.0): mine play, slow stepping, motion moments
        JogInPlace,    // 12025 Jogging, in place (4.8): 100-150 steps a minute
        Vigorous,      // 22320 Active video game, total body, exergames, vigorous (7.5): 150+ steps, the zoo rhythm
        RhythmMissed,  // 22280 Active video game, total body, moderate-to-vigorous (5.0): the zoo with many missed beats
        StairSlow,     // 17133 Stair climbing, slow pace (4.5): prison stairs (approximation)
        StairFast,     // 17131 Stair climbing, general (6.8): fast prison stairs, warden chase (approximation)
        HideDeep,      // 02022 Calisthenics, moderate effort (3.8): deep hiding crouch (approximation)
        HideShallow,   // 02024 Calisthenics, light effort (2.8): shallow hiding crouch
        UpperGeneral,  // 22200 Active video game, upper body, light to moderate (3.0): upper body arm swings
        UpperPull,     // 02115 Upper body exercise, arm ergometer, light (2.8): upper body crouch and pull-down (approximation)
    }

    public enum Intensity { Light, Moderate, Vigorous }

    // Extra facts the maps report about the current moment (set by the zoo and prison scenes; all false by default).
    public struct MetHints
    {
        public bool DeepCrouch;    // the hiding crouch is at a half squat or deeper
        public bool FastStairs;    // the stair beat is fast or the warden is chasing
        public bool MissingBeats;  // the zoo rhythm is being missed often
    }

    // Plan 5-4, 5-5: moderate = 3.0 MET or more, vigorous = 6.0 or more (WHO 2020). A vigorous minute counts two toward
    // the weekly 150 minutes. The upper body uses its own codes (no 0.6 scale); keyboard play counts nothing.
    public static class IntensityMet
    {
        public const double Moderate = 3.0, Vigorous = 6.0;
        public static bool IsModerate(double met) => met >= Moderate - 1e-9;
        public static bool IsVigorous(double met) => met >= Vigorous - 1e-9;
        public static Intensity Level(double met) => IsVigorous(met) ? Intensity.Vigorous : IsModerate(met) ? Intensity.Moderate : Intensity.Light;
        // Weekly goal weight of one second: vigorous 2, moderate 1, light 0.
        public static double GoalWeight(double met) => IsVigorous(met) ? 2 : IsModerate(met) ? 1 : 0;

        public static double Of(MetActivity activity)
        {
            switch (activity)
            {
                case MetActivity.MineGeneral: return 4.0;
                case MetActivity.JogInPlace: return 4.8;
                case MetActivity.Vigorous: return 7.5;
                case MetActivity.RhythmMissed: return 5.0;
                case MetActivity.StairSlow: return 4.5;
                case MetActivity.StairFast: return 6.8;
                case MetActivity.HideDeep: return 3.8;
                case MetActivity.HideShallow: return 2.8;
                case MetActivity.UpperGeneral: return 3.0;
                case MetActivity.UpperPull: return 2.8;
                default: return 0;
            }
        }
        // Compendium code of the activity (for the help page "운동은 이렇게 계산해요").
        public static string Code(MetActivity activity)
        {
            switch (activity)
            {
                case MetActivity.MineGeneral: return "22240";
                case MetActivity.JogInPlace: return "12025";
                case MetActivity.Vigorous: return "22320";
                case MetActivity.RhythmMissed: return "22280";
                case MetActivity.StairSlow: return "17133";
                case MetActivity.StairFast: return "17131";
                case MetActivity.HideDeep: return "02022";
                case MetActivity.HideShallow: return "02024";
                case MetActivity.UpperGeneral: return "22200";
                case MetActivity.UpperPull: return "02115";
                default: return "07040";
            }
        }
        // The Compendium's own wording.
        public static string Source(MetActivity activity)
        {
            switch (activity)
            {
                case MetActivity.MineGeneral: return "Active video game, total body, moderate";
                case MetActivity.JogInPlace: return "Jogging, in place";
                case MetActivity.Vigorous: return "Active video game, total body, exergames, vigorous";
                case MetActivity.RhythmMissed: return "Active video game, total body, moderate-to-vigorous";
                case MetActivity.StairSlow: return "Stair climbing, slow pace";
                case MetActivity.StairFast: return "Stair climbing, general";
                case MetActivity.HideDeep: return "Calisthenics, moderate effort";
                case MetActivity.HideShallow: return "Calisthenics, light effort";
                case MetActivity.UpperGeneral: return "Active video game, upper body, light to moderate";
                case MetActivity.UpperPull: return "Upper body exercise, arm ergometer, light";
                default: return "Standing quietly";
            }
        }

        // The activity of one moment. stage: the cadence stage (steps, or arm swings for the upper body). moving: a jump,
        // crouch, knee or lane moment just happened. motion: 1 jump, 2 crouch, 3 knee step, 4 lane change, 0 none.
        // cadenceCounts false ("달리기 속도 조절" off): stepping counts nothing, only the motion moments do.
        public static MetActivity Classify(PlayStyle style, SectionKind section, CadenceStage stage, bool cadenceCounts, bool moving, int motion, MetHints hints)
        {
            if (style == PlayStyle.Keyboard) return MetActivity.Standing;
            bool stepping = cadenceCounts && stage != CadenceStage.Standing;
            if (!stepping && !moving) return MetActivity.Standing;
            if (style == PlayStyle.UpperBody) return motion == 2 || motion == 3 ? MetActivity.UpperPull : MetActivity.UpperGeneral;
            var pace = stepping ? stage : CadenceStage.Walk;
            if (section == SectionKind.Prison)
            {
                if (motion == 2) return hints.DeepCrouch ? MetActivity.HideDeep : MetActivity.HideShallow;
                if (motion == 3) return hints.FastStairs ? MetActivity.StairFast : MetActivity.StairSlow;
            }
            if (section == SectionKind.Zoo && pace != CadenceStage.Walk) return hints.MissingBeats ? MetActivity.RhythmMissed : MetActivity.Vigorous;
            return pace == CadenceStage.Sprint ? MetActivity.Vigorous : pace == CadenceStage.Run ? MetActivity.JogInPlace : MetActivity.MineGeneral;
        }
    }
}
