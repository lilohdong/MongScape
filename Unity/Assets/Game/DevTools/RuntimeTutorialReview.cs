#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.IO;
using Mongscape.Bootstrap;
using Mongscape.Domain;
using Mongscape.Presentation;
using UnityEngine;

namespace Mongscape.DevTools
{
    // Only runs with --mongscape-tutorial-review: plays tutorial chapters 1, 2 and 3 in keyboard style from start to end the way a
    // player presses the keys (one banana missed on purpose to show the retry), with a capture at every call, warning and pass,
    // saved to --mongscape-review-output <dir>. The log ends with MONGSCAPE_TUTORIAL_REVIEW_PASS or _FAIL.
    public sealed class RuntimeTutorialReview : MonoBehaviour
    {
        public const string Flag = "--mongscape-tutorial-review";
        private GameBootstrap game;
        private string destination;
        private int shots;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), Flag) < 0) return;
            new GameObject("Tutorial Review").AddComponent<RuntimeTutorialReview>();
        }
        private void Start()
        {
            UnityEngine.Application.runInBackground = true;
            var args = Environment.GetCommandLineArgs(); int at = Array.IndexOf(args, "--mongscape-review-output");
            destination = at >= 0 && at + 1 < args.Length ? args[at + 1] : Path.Combine(UnityEngine.Application.persistentDataPath, "TutorialReview");
            Directory.CreateDirectory(destination);
            StartCoroutine(Run());
        }
        private IEnumerator Run()
        {
            while ((game = FindFirstObjectByType<GameBootstrap>()) == null || game.Session == null || game.Session.Flow.State != GameFlowState.TitleIdle) yield return null;
            yield return new WaitForSecondsRealtime(2f);
            var hud = game.GetComponent<HUDPresenter>();
            game.OpenMainMenu(); yield return new WaitForSecondsRealtime(.5f);
            hud.SelectMenu(MenuAction.Tutorial); yield return new WaitForSecondsRealtime(1f);
            yield return Shot("00_Chapters");
            bool one = false, two = false, three = false;
            yield return Chapter(TutorialChapter.Basics, ok => one = ok);
            yield return Chapter(TutorialChapter.Zoo, ok => two = ok);
            yield return Chapter(TutorialChapter.Prison, ok => three = ok);
            game.OpenMainMenu(); hud.SelectMenu(MenuAction.Tutorial); yield return new WaitForSecondsRealtime(1f);
            yield return Shot("00_Chapters_After");
            bool all = one && two && three;
            Debug.Log((all ? "MONGSCAPE_TUTORIAL_REVIEW_PASS: " : "MONGSCAPE_TUTORIAL_REVIEW_FAIL: basics " + one + " zoo " + two + " prison " + three + " ") + destination);
            UnityEngine.Application.Quit(all ? 0 : 1);
        }
        private IEnumerator Chapter(TutorialChapter chapter, Action<bool> done)
        {
            var hud = game.GetComponent<HUDPresenter>();
            while (game.Session.Flow.State != GameFlowState.TitleIdle || game.IsLoading) yield return null;
            game.OpenMainMenu(); hud.SelectMenu(MenuAction.Tutorial); yield return null;
            hud.ChooseTutorial(TutorialChoice.Start, PlayStyle.Keyboard, chapter);
            float wait = Time.realtimeSinceStartup;
            while (game.IsLoading || game.IsRevealing || game.Session.Flow.State != GameFlowState.Running || !game.Tutorial.Active)
            { if (Time.realtimeSinceStartup - wait > 60) { done(false); yield break; } yield return null; }
            var t = game.Tutorial; var run = game.Session.Runner;
            string tag = chapter == TutorialChapter.Basics ? "1" : chapter == TutorialChapter.Zoo ? "2" : "3";
            float deadline = Time.realtimeSinceStartup + 240;
            int index = -1; bool missedBanana = false, called = false, warned = false, retried = false;
            TutorialStep shown = TutorialStep.Done;
            while (t.Active && Time.realtimeSinceStartup < deadline)
            {
                var step = t.Script.Current;
                if (t.Script.Index != index)
                {
                    if (index >= 0) yield return Shot(tag + "_" + Two(index + 1) + "_Pass_" + shown);
                    index = t.Script.Index; called = warned = false; shown = step;
                    if (t.Script.Finished) { yield return new WaitForSecondsRealtime(.5f); yield return Shot(tag + "_99_Done"); continue; }
                }
                if (t.Retrying && !retried) { retried = true; yield return Shot(tag + "_" + Two(index + 1) + "_Retry_" + step); continue; }
                if (!t.Retrying) retried = false;
                if (t.Waiting && !called && t.Cue != null) { called = true; yield return Shot(tag + "_" + Two(index + 1) + "_Call_" + step); continue; }
                if ((step == TutorialStep.WarnRule || step == TutorialStep.AlarmRule) && !warned && game.Session.Warnings >= 1) { warned = true; yield return Shot(tag + "_" + Two(index + 1) + "_Warning_" + step); continue; }
                if (game.Session.Paused) { if (!game.PauseControl.Counting) game.TogglePause(); yield return null; continue; }
                if (step == TutorialStep.Rescue && t.Waiting) game.Session.Command(MotionCommand.ReachRight);
                if (!t.Retrying && game.Session.Flow.State == GameFlowState.Running)
                {
                    bool near = t.LessonDistance <= TutorialDirector.ShowDistance;
                    if (step == TutorialStep.Lane && near && run.Lane == 0) run.Command(MotionCommand.Left);
                    else if (step == TutorialStep.Jump && t.Cue == ActionCuePresenter.JumpCue && run.Y <= .001f) run.Command(MotionCommand.Jump);
                    else if (step == TutorialStep.Crouch && t.Cue == ActionCuePresenter.CrouchCue) run.Command(MotionCommand.Crouch);
                    else if (step == TutorialStep.WarnRule && near && run.Lane != TutorialCourse.BlockedLane) run.Command(run.Lane < 0 ? MotionCommand.Right : MotionCommand.Left);
                    else if (step == TutorialStep.Banana && near)
                    {
                        // The first try stays on the peel (one more warning would be the capture): the retry; then step aside.
                        if (!missedBanana && t.LessonDistance < 6f) { yield return Shot(tag + "_" + Two(index + 1) + "_Blink_" + step); missedBanana = true; }
                        else if (missedBanana && run.Lane == TutorialCourse.BlockedLane && t.Script.Retries > 0) run.Command(MotionCommand.Left);
                    }
                    else if (step == TutorialStep.PauseX && t.Waiting && run.Z >= TutorialCourse.HoldZ(TutorialChapter.Basics, step) - .5f) game.TogglePause();
                    else if (step == TutorialStep.Fork && (RouteSurface.ActivePlan?.ForkWindow(run.Z) ?? false) && run.Lane == 0) run.Command(MotionCommand.Left);
                    else if (step == TutorialStep.Stair && run.SinceKneeStep >= .3f && (t.LessonDistance <= 3.6f || StairJudge.StepDue(run))) run.Command(MotionCommand.KneeStep);
                    else if (step == TutorialStep.Hide && game.Session.Prison.Holding) run.Command(MotionCommand.Crouch);
                    // F and J in turn at a running pace (the key steps a minute), Space at the fence, W under the bars.
                    else if (step == TutorialStep.Fence && t.LessonDistance < 4f && run.Y <= .001f) run.Command(MotionCommand.Jump);
                    else if (step == TutorialStep.Bars && t.LessonDistance < 4f) run.Command(MotionCommand.ArmsUp);
                    game.KeyStepsOverride = chapter == TutorialChapter.Zoo ? 160f : (float?)null;
                }
                yield return null;
            }
            game.KeyStepsOverride = null;
            while (game.Session.Flow.State != GameFlowState.TitleIdle && Time.realtimeSinceStartup < deadline + 10) yield return null;
            Debug.Log("TUTORIAL_REVIEW: chapter " + chapter + " finished " + !t.Active);
            done(!t.Active);
        }
        private static string Two(int n) => n.ToString("00");
        private IEnumerator Shot(string name)
        {
            yield return new WaitForEndOfFrame();
            string path = Path.Combine(destination, (++shots).ToString("000") + "_" + name + ".png");
            ScreenCapture.CaptureScreenshot(path);
            yield return null;
            Debug.Log("TUTORIAL_CAPTURE: " + name);
        }
    }
}
#endif
