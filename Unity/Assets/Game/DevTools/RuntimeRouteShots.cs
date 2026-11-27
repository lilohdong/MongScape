#if UNITY_EDITOR || DEVELOPMENT_BUILD
using Mongscape.Bootstrap;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Mongscape.Domain;
using Mongscape.Presentation;
using Mongscape.Presentation.Map;
using UnityEngine;
namespace Mongscape.DevTools
{
    // --mongscape-route-shots: one keyboard run that steers itself mine -> zoo -> mine -> prison -> mine (demo
    // invincibility on), then the result, its ranking and the ranking page. Captures go to --mongscape-review-output.
    // Use with --mongscape-data <dir>; accounts are the in-memory review gateway, so no server is written to.
    public sealed class RuntimeRouteShots : MonoBehaviour
    {
        private GameBootstrap game; private ZooMapView zoo; private string output; private float began;
        private bool driving, done; private string failure;
        private readonly List<SectionKind> visited = new List<SectionKind>();
        private readonly Dictionary<int, int> lanes = new Dictionary<int, int>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)] private static void Install()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "--mongscape-route-shots") >= 0) new GameObject("Route shots").AddComponent<RuntimeRouteShots>();
        }
        private void Awake()
        {
            began = Time.realtimeSinceStartup; UnityEngine.Application.runInBackground = true;
            var args = Environment.GetCommandLineArgs(); int at = Array.IndexOf(args, "--mongscape-review-output");
            output = at >= 0 && at + 1 < args.Length ? Path.GetFullPath(args[at + 1]) : Path.Combine(UnityEngine.Application.persistentDataPath, "RouteShots");
            Directory.CreateDirectory(output);
            UnityEngine.Application.logMessageReceived += Log;
        }
        private void Log(string message, string stack, LogType type) { if (type == LogType.Exception && failure == null) failure = message; }
        private void OnDestroy() { UnityEngine.Application.logMessageReceived -= Log; }
        private IEnumerator Start()
        {
            yield return null; game = FindFirstObjectByType<GameBootstrap>();
            yield return new WaitUntil(() => game.Session != null && game.Session.Flow.State == GameFlowState.TitleIdle);
            var settings = ((ISettingsHost)game).Settings; settings.Fullscreen = false; settings.PerformanceOverlay = false; settings.AutoPerformance = false;
            settings.MusicVolume = settings.EffectsVolume = 0; game.ApplySettings(false);
            { var a = Environment.GetCommandLineArgs(); int sz = Array.IndexOf(a, "--mongscape-audit-size"); var wh = sz >= 0 && sz + 1 < a.Length ? a[sz + 1].Split('x') : new[] { "1920", "1080" }; Screen.SetResolution(int.Parse(wh[0]), int.Parse(wh[1]), false); }
            game.UseAccountServices(new RuntimeUiReview.ReviewAccountGateway());
            Seed();
            game.KeyStepsOverride = 120f; game.StartPlayerRun(new PlayerProfile { Name = "", Weight = "70", Style = PlayStyle.Keyboard });
            yield return new WaitUntil(() => !game.IsLoading && !game.IsRevealing && game.Session.Flow.State == GameFlowState.Running);
            zoo = game.GetComponent<ZooMapView>();
            game.Session.Invincible = true; driving = true; Time.timeScale = 1.5f;
            StartCoroutine(Moments());
            yield return Wait(5f); yield return Capture("01_mine_start");
            // The warning slots: one handcuff lighting up (the flash), then held.
            RunHudView.PreviewWarnings = 1; yield return Wait(.1f); yield return Capture("01b_warning_flash");
            yield return Wait(1.5f); yield return Capture("01c_warning_one"); RunHudView.PreviewWarnings = null;
            yield return Until(() => Plan() != null && Plan().ForkWindow(game.Session.Runner.Z)); yield return Wait(.6f); yield return Capture("02_fork_first");
            yield return Section(SectionKind.Zoo); yield return Wait(1.2f); yield return Capture("03_zoo_entry_cut");
            yield return Wait(10f); yield return Capture("04_zoo_run");
            yield return Wait(20f); yield return Capture("05_zoo_later");
            yield return Section(SectionKind.Mine); yield return Wait(4f); yield return Capture("06_mine_after_zoo");
            yield return Section(SectionKind.Prison); yield return Wait(2f); yield return Capture("07_prison_entry");
            yield return Wait(8f); yield return Capture("08_prison_run");
            yield return Wait(10f); yield return Capture("09_prison_later");
            yield return Section(SectionKind.Mine); yield return Wait(5f); yield return Capture("10_mine_after_prison");
            // A wall monkey two chunks ahead on each side, so the shot shows one up close even when the route had none.
            for (int ahead = 2; ahead <= 3; ahead++)
            {
                int seq = RouteSurface.SequenceAt(game.Session.Runner.Z) + ahead;
                foreach (var a in game.Map.Active)
                    if (a.Chunk != null && Mathf.Abs(a.Chunk.Entry.position.z - seq * RouteSurface.Length) < 1f) game.Content.PlaceTutorialMonkey(a.Chunk, seq, ahead == 2 ? -1 : 1);
            }
            yield return Wait(12f);
            driving = false; Time.timeScale = 1f;
            game.Session.Invincible = false; game.Session.ForceCatch();
            yield return Until(() => game.Session.Flow.State == GameFlowState.Result);
            yield return new WaitForSecondsRealtime(.9f); yield return Capture("11_result_score");
            yield return new WaitForSecondsRealtime(2.5f); yield return Capture("12_result_ranking");
            var hud = game.Hud;
            hud.Result.ShowMore(true); yield return new WaitForSecondsRealtime(.6f); yield return Capture("13_result_more"); hud.Result.ShowMore(false);
            game.ExitToMenu(); yield return new WaitForSecondsRealtime(1.5f); yield return Capture("14_main_menu");
            hud.ShowPage(MenuPage.Ranking); hud.Ranking.Select(false, PlayStyle.Keyboard, true); yield return new WaitForSecondsRealtime(1.2f); yield return Capture("15_ranking_today");
            hud.Ranking.Select(false, PlayStyle.Keyboard, false); yield return new WaitForSecondsRealtime(1.2f); yield return Capture("16_ranking_all");
            game.OpenMainMenu(); yield return new WaitForSecondsRealtime(.8f);
            Finish(failure);
        }
        // Side shots taken whenever the run meets them: the wall monkey up close, the stair gauge, the squeeze bars closing.
        private bool capturing, shooting;
        private IEnumerator Moments()
        {
            int monkey = 0, stair = 0, squeeze = 0;
            while (!done && driving)
            {
                var run = game.Session; float z = run.Runner.Z;
                if (!capturing && monkey < 2)
                    foreach (var m in game.Content.StuckMonkeys)
                        if (m != null && m.isActiveAndEnabled && !m.Popped && m.transform.position.z - z > 5f && m.transform.position.z - z < 8f)
                        { yield return Moment("m_monkey_" + (++monkey)); break; }
                if (!capturing && stair < 3 && run.InStair && run.StairProgress > .2f + stair * .3f) yield return Moment("m_stair_" + run.Section.Kind + "_" + (++stair));
                if (!capturing && squeeze < 2 && zoo != null && zoo.Chase.Active)
                    for (int i = 0; i < zoo.Chase.Obstacles.Count; i++)
                    {
                        float ahead = zoo.Chase.Obstacles[i].Distance - zoo.Chase.Distance;
                        if (zoo.Chase.Obstacles[i].Kind == ZooNoteKind.ArmsUp && ahead > 2.5f && ahead < 4f && (squeeze == 0 || i > lastSqueeze))
                        { lastSqueeze = i + 6; yield return Moment("m_squeeze_" + (++squeeze)); break; }
                    }
                yield return null;
            }
        }
        private int lastSqueeze;
        private IEnumerator Moment(string name) { capturing = true; yield return Capture(name); capturing = false; }
        // Thirty guests above any run (the result table has to scroll), and one name with several runs.
        private void Seed()
        {
            var now = DateTime.Now;
            for (int i = 0; i < 30; i++) Add("손님 " + (i + 1), 90000 - i * 900, now.AddMinutes(-i - 1));
            for (int i = 0; i < 6; i++) Add("탈옥왕 455", 455 - i * 20, now.AddMinutes(-40 - i));
            for (int i = 0; i < 8; i++) Add("작은 " + (i + 1), 300 - i * 30, now.AddMinutes(-60 - i));
        }
        private void Add(string name, int score, DateTime when) => game.Store.Record(new RankingEntry
        { Name = name, Score = score, Distance = score / 3, Mode = (int)PlayStyle.Keyboard, Style = PlayStyleInfo.Label(PlayStyle.Keyboard), ScoreVersion = RunScore.Version, Ticks = when.Ticks }, 0);
        private static RoutePlan Plan() => RouteSurface.ActivePlan;
        private IEnumerator Section(SectionKind kind)
        {
            yield return Until(() => game.Session.Sections.Current.Kind == kind && (kind != SectionKind.Mine || !game.Session.Sections.Current.Branch || visited.Count > 0));
            visited.Add(kind); Debug.Log("MONGSCAPE_ROUTE section " + game.Session.Sections.Current + " at " + Mathf.RoundToInt(game.Session.Runner.Z) + "m");
        }
        private IEnumerator Wait(float seconds) { float until = Time.time + seconds; while (Time.time < until && !done) yield return null; }
        private IEnumerator Until(Func<bool> condition) { while (!done && !condition()) yield return null; }
        // The demo invincibility note is left out of the picture: off for the captured frame only.
        private IEnumerator Capture(string name)
        {
            while (shooting) yield return null;
            shooting = true;
            bool was = game.Session.Invincible; float scale = Time.timeScale;
            Time.timeScale = 0f; game.Session.Invincible = false;
            yield return null; yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(output, name + ".png"));
            yield return null; yield return null;
            game.Session.Invincible = was; Time.timeScale = scale; shooting = false;
            Debug.Log("MONGSCAPE_ROUTE shot " + name + " at " + Mathf.RoundToInt(game.Session.Runner.Z) + "m " + game.Session.Flow.State);
        }
        private void Update()
        {
            if (done) return;
            if (Time.realtimeSinceStartup - began > 420) { Finish("Route timeout after " + string.Join(",", visited)); return; }
            if (!driving || game.Session.Flow.State != GameFlowState.Running || game.Session.Paused) return;
            var run = game.Session; var runner = run.Runner; var plan = Plan(); float z = runner.Z;
            if (plan != null && plan.ForkWindow(z))
            {
                int sequence = RouteSurface.SequenceAt(z), lap = plan.CycleAt(sequence);
                if (!lanes.TryGetValue(lap, out int lane))
                {
                    var want = lanes.Count == 0 ? SectionKind.Zoo : lanes.Count == 1 ? SectionKind.Prison : SectionKind.Mine;
                    for (int l = -1; l <= 1; l++) if (SectionDef.KindOfDestination(plan.DestinationFor(sequence, l)) == want) lane = l;
                    lanes[lap] = lane; Debug.Log("MONGSCAPE_ROUTE fork lap " + lap + " wants " + want + " lane " + lane);
                }
                if (runner.Lane != lane) runner.Command(lane < 0 ? MotionCommand.Left : lane > 0 ? MotionCommand.Right : MotionCommand.Center);
                return;
            }
            if (zoo != null && zoo.Chase.Active)
                for (int i = 0; i < zoo.Chase.Obstacles.Count; i++)
                {
                    var obstacle = zoo.Chase.Obstacles[i];
                    float ahead = obstacle.Distance - zoo.Chase.Distance;
                    if (!zoo.Chase.IsJudged(i) && ahead > 0f && ahead < 3f)
                        runner.Command(obstacle.Kind == ZooNoteKind.Jump ? MotionCommand.Jump : MotionCommand.ArmsUp);
                }
            if (run.Prison.Phase == PrisonPhase.Hold) runner.Command(MotionCommand.Crouch);
            if (StairJudge.StepDue(runner)) runner.Command(MotionCommand.KneeStep);
            if (runner.Riding)
                foreach (var chunk in game.Map.Active) foreach (var gap in chunk.Chunk.RailGaps)
                    if (gap.gameObject.activeInHierarchy && !gap.Passed && gap.transform.position.z >= z && gap.transform.position.z - z < Mathf.Max(1, run.CurrentSpeed) * .8f)
                        runner.Command(gap.RequiredLean < 0 ? MotionCommand.Left : MotionCommand.Right);
        }
        private void Finish(string error)
        {
            if (done) return; done = true; Time.timeScale = 1f;
            if (error == null) Debug.Log("MONGSCAPE_ROUTE_PASS " + string.Join(",", visited) + " " + output);
            else Debug.LogError("MONGSCAPE_ROUTE_FAIL " + error);
            UnityEngine.Application.Quit(error == null ? 0 : 1);
        }
    }
}
#endif
