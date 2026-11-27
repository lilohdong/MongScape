using System;

namespace Mongscape.Domain
{
    // Running speed from the cadence (steps a minute over the last 3 s): walking (up to 100) is 70 % of the base speed,
    // running (100-150) 100 %, sprinting (150 and up) 130 %, blended smoothly around the stage lines.
    // Below the target cadence for more than LagSeconds the police close in (PacePressure) and the screen edges redden.
    // In the mine a full gauge is one warning (RunSession.ApplyPace) and the gauge drops to half (plan 6-8).
    public sealed class CadencePace
    {
        public const float Walk = .7f, Run = 1f, Sprint = 1.3f;
        // Half widths of the blends around RunCadence and SprintCadence (steps a minute).
        public const float WalkBlend = 15f, SprintBlend = 10f;
        // Scale change per second (smooths step noise), lag before the police react, pressure rise and fall per second.
        public const float ScaleRate = .6f, LagSeconds = 3f, PressureRise = .22f, PressureFall = .5f;
        // The pace gauge fills to 1 (the police pounce: one warning in the mine). Outside the mine it shows up to LegacyMaxPressure.
        public const float MaxPressure = 1f, LegacyMaxPressure = .75f;
        // Target cadence: the bottom of the running stage, a little under RunCadence so a steady jog never lags.
        public const float TargetBelowRun = 10f;
        public float Scale { get; private set; } = 1f;
        public float LagFor { get; private set; }
        public bool Lagging => LagFor > LagSeconds;
        public float Pressure { get; private set; }
        // After stepping off the cart the pace rests for 3 s: the gauge does not fill and the lag stays at 0 (owner, 2026-11-27).
        public const float OffCartGrace = 3f;
        public float SinceCart { get; private set; } = 1000f;
        // Call once a frame; true while on foot and past the grace after the cart. The run starts on foot with no grace.
        public bool OnFootActive(float dt, bool riding)
        {
            SinceCart = riding ? 0f : Math.Min(1000f, SinceCart + Math.Max(0f, dt));
            return !riding && SinceCart >= OffCartGrace;
        }
        public static float Factor(float stepsPerMinute, MotionTuning tuning)
        {
            tuning ??= new MotionTuning();
            float spm = float.IsNaN(stepsPerMinute) ? 0 : Math.Max(0, stepsPerMinute);
            float run = tuning.RunCadence, sprint = tuning.SprintCadence;
            if (spm <= run - WalkBlend) return Walk;
            if (spm < run + WalkBlend) return Lerp(Walk, Run, (spm - (run - WalkBlend)) / (2 * WalkBlend));
            if (spm <= sprint - SprintBlend) return Run;
            if (spm < sprint + SprintBlend) return Lerp(Run, Sprint, (spm - (sprint - SprintBlend)) / (2 * SprintBlend));
            return Sprint;
        }
        public static float Target(MotionTuning tuning) => (tuning ?? new MotionTuning()).RunCadence - TargetBelowRun;
        // enabled: a camera run with "달리기 속도 조절" on, on foot. Off (keyboard, the option off, riding): base speed, no pace pressure.
        public void Tick(float dt, bool enabled, float stepsPerMinute, MotionTuning tuning, float target) => Tick(dt, enabled, stepsPerMinute, tuning, target, true, false);
        // tracked: the camera sees the player. While it does not, everything holds where it was (the last speed, the lag and the
        // police), so stepping out of view is never better than standing still (QA 2026-11-25 M6).
        // stairs: on or right before a stair chunk the pace rests: no slow-down below the running speed, no lag and the police
        // fall back. The stair exercise (knee raises, the upper body's pull-down) is slower than running in place (QA H2).
        public void Tick(float dt, bool enabled, float stepsPerMinute, MotionTuning tuning, float target, bool tracked, bool stairs)
        {
            if (dt <= 0 || float.IsNaN(dt)) return;
            if (enabled && !tracked) return;
            float goal = !enabled ? Run : stairs ? Math.Max(Run, Factor(stepsPerMinute, tuning)) : Factor(stepsPerMinute, tuning);
            Scale = Scale < goal ? Math.Min(goal, Scale + ScaleRate * dt) : Math.Max(goal, Scale - ScaleRate * dt);
            LagFor = enabled && !stairs && stepsPerMinute < target ? LagFor + dt : 0;
            Pressure = Lagging ? Math.Min(MaxPressure, Pressure + PressureRise * dt) : Math.Max(0, Pressure - PressureFall * dt);
        }
        public void Reset() { Scale = 1f; LagFor = 0; Pressure = 0; SinceCart = 1000f; }
        // After the police pounce the gauge drops to half and the lag starts over.
        public void Halve() { Pressure *= .5f; LagFor = 0; }
        private static float Lerp(float a, float b, float t) { t = t < 0 ? 0 : t > 1 ? 1 : t; t = t * t * (3 - 2 * t); return a + (b - a) * t; }
    }
}
