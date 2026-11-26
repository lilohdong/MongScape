using System;

namespace Mongscape.Domain
{
    // Why the run is paused (plan 6-7a): Esc or the pause button, the X gesture, or the camera lost the player.
    public enum PauseReason { None, Key, Gesture, LostTracking }

    // How a resume counts in: seconds until play goes on and how many counts are shown over them.
    public readonly struct ResumePlan
    {
        public readonly double Seconds;
        public readonly int Counts;
        public ResumePlan(double seconds, int counts) { Seconds = Math.Max(.1, seconds); Counts = Math.Max(1, counts); }
        public static readonly ResumePlan Default = new ResumePlan(3, 3);
    }

    // Pausing while playing with the body (plan 6-7a):
    // - the camera loses the player for LostDelay: pause with the silhouette, back in view for RegainHold: count in again;
    // - both arms crossed in front of the chest (X) held GestureHold while not running in place: pause (GestureProgress fills);
    // - Esc or the pause button: pause, and again to resume.
    // A resume counts 3, 2, 1 (ResumeHook replaces it, e.g. the zoo: wait for the next bar, then 4 beats).
    public sealed class PauseFlow
    {
        public const double GestureHold = 1, LostDelay = .4, RegainHold = .6;
        public PauseReason Reason { get; private set; }
        // 0..1 while the X is held (the filling circle); 0 otherwise.
        public float GestureProgress { get; private set; }
        public bool Counting => countdownLeft > 0;
        // The number shown during the count-in (3, 2, 1 or the beats), 0 when not counting.
        public int CountdownNumber { get; private set; }
        public bool ShowSilhouette => Reason == PauseReason.LostTracking && !Counting;
        // Rhythm sections set this: the count-in to use instead of 3, 2, 1 (null keeps the default).
        public Func<ResumePlan?> ResumeHook { get; set; }
        public event Action<PauseReason> Paused;
        public event Action Resumed;
        // Each new count-in number (sound and HUD hook).
        public event Action<int> Count;
        private double crossFor, lostFor, regainFor, countdownLeft;
        private ResumePlan plan = ResumePlan.Default;

        // dt: real (unscaled) seconds. camera: a camera run; tracked: the camera sees the player; crossed: the X reading;
        // stepping: running in place right now (the X counts only while the feet or arm swings have stopped).
        public void Tick(RunSession session, double dt, bool camera, bool tracked, bool crossed, bool stepping)
        {
            if (session == null || session.Flow.State != GameFlowState.Running) { Clear(); return; }
            if (double.IsNaN(dt) || dt <= 0) return;
            if (!session.Paused)
            {
                if (camera && tracked && crossed && !stepping)
                {
                    crossFor += dt; GestureProgress = (float)Math.Min(1, crossFor / GestureHold);
                    if (crossFor >= GestureHold) { Pause(session, PauseReason.Gesture); return; }
                }
                else { crossFor = 0; GestureProgress = 0; }
                if (camera && !tracked) { lostFor += dt; if (lostFor >= LostDelay) Pause(session, PauseReason.LostTracking); }
                else lostFor = 0;
                return;
            }
            if (Counting)
            {
                if (camera && !tracked && Reason == PauseReason.LostTracking) { countdownLeft = 0; CountdownNumber = 0; regainFor = 0; return; }
                countdownLeft -= dt;
                if (countdownLeft <= 0) { Finish(session); return; }
                SetNumber((int)Math.Ceiling(countdownLeft / plan.Seconds * plan.Counts));
                return;
            }
            if (Reason == PauseReason.LostTracking && camera)
            {
                regainFor = tracked ? regainFor + dt : 0;
                if (regainFor >= RegainHold) Resume(session);
            }
        }
        public bool Pause(RunSession session, PauseReason reason)
        {
            if (session == null || session.Flow.State != GameFlowState.Running) return false;
            crossFor = lostFor = regainFor = 0; GestureProgress = 0;
            if (session.Paused) { countdownLeft = 0; CountdownNumber = 0; Reason = reason; return true; }
            session.Paused = true; Reason = reason == PauseReason.None ? PauseReason.Key : reason;
            Paused?.Invoke(Reason);
            return true;
        }
        // Starts the count-in; play goes on when it ends.
        public bool Resume(RunSession session)
        {
            if (session == null || !session.Paused || Counting || session.Flow.State != GameFlowState.Running) return false;
            plan = ResumeHook?.Invoke() ?? ResumePlan.Default;
            countdownLeft = plan.Seconds; regainFor = 0; CountdownNumber = 0;
            SetNumber(plan.Counts);
            return true;
        }
        // Esc and the pause button: pause, or resume when paused.
        public void Toggle(RunSession session) { if (session == null) return; if (session.Paused) Resume(session); else Pause(session, PauseReason.Key); }
        private void Finish(RunSession session)
        {
            countdownLeft = 0; CountdownNumber = 0; Reason = PauseReason.None;
            session.Paused = false;
            Resumed?.Invoke();
        }
        private void SetNumber(int number)
        {
            number = Math.Max(1, Math.Min(plan.Counts, number));
            if (number == CountdownNumber) return;
            CountdownNumber = number; Count?.Invoke(number);
        }
        public void Clear() { crossFor = lostFor = regainFor = countdownLeft = 0; GestureProgress = 0; CountdownNumber = 0; Reason = PauseReason.None; }
    }
}
