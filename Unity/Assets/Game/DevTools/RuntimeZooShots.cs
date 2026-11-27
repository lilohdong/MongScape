#if UNITY_EDITOR || DEVELOPMENT_BUILD
using Mongscape.Bootstrap;
using System;
using System.Collections;
using System.IO;
using Mongscape.Domain;
using Mongscape.Presentation;
using Mongscape.Presentation.Map;
using Unity.Cinemachine;
using UnityEngine;
namespace Mongscape.DevTools
{
    // --mongscape-zoo-shots: keyboard captures of the song-paced zoo (entry cut, normal run, mirror, roar, pounce and a side
    // view for sizes). No real camera, saved profiles or leaderboard writes.
    public sealed class RuntimeZooShots : MonoBehaviour
    {
        private GameBootstrap game; private ZooMapView zoo; private string output; private float began; private bool done, keepUp = true;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)] private static void Install()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "--mongscape-zoo-shots") >= 0) new GameObject("Zoo shots").AddComponent<RuntimeZooShots>();
        }
        private void Awake()
        {
            began = Time.realtimeSinceStartup; UnityEngine.Application.runInBackground = true;
            var args = Environment.GetCommandLineArgs(); int at = Array.IndexOf(args, "--mongscape-review-output");
            output = at >= 0 && at + 1 < args.Length ? Path.GetFullPath(args[at + 1]) : Path.Combine(UnityEngine.Application.persistentDataPath, "ZooShots");
            Directory.CreateDirectory(output);
        }
        private IEnumerator Start()
        {
            yield return null; game = FindFirstObjectByType<GameBootstrap>();
            var settings = ((ISettingsHost)game).Settings; settings.Fullscreen = false; settings.PerformanceOverlay = false; settings.AutoPerformance = false;
            settings.MusicVolume = settings.EffectsVolume = 0; game.ApplySettings(false);
            Screen.SetResolution(1280, 720, false);
            game.Session.Config.ExtendedRoutes = true;
            // F and J held at a running pace until the normal shots are taken, then walking so the lion closes in.
            game.KeyStepsOverride = 120f; game.StartPlayerRun(new PlayerProfile { Name = "Zoo QA", Weight = "70", Style = PlayStyle.Keyboard });
            yield return new WaitUntil(() => !game.IsLoading && !game.IsRevealing);
            zoo = game.GetComponent<ZooMapView>();
            var plan = new RoutePlan(game.Session.Config, 723); ZooMapView.Register(plan);
            int lane = 0; for (int l = -1; l <= 1; l++) if (SectionDef.KindOfDestination(plan.DestinationFor(CartFork.FirstChunk, l)) == SectionKind.Zoo) lane = l;
            plan.UpdateChoice(CartFork.Decision + 1, lane);
            RouteSurface.UsePlan(plan); game.Session.Reset();
            float start = CartFork.DestinationChunk * RouteSurface.Length - 3f;
            game.Session.Runner.SetStartZ(start); game.Map.Reset(); game.Map.Tick(start);
            yield return new WaitUntil(() => zoo.Chase.Active);
            yield return At(1.4f); yield return Capture("zoo_cut");
            yield return At(3.3f); yield return Capture("zoo_shout");
            // Fences and bars are passed until the normal shots are taken, then missed so the lion closes in (the keyboard run
            // without F and J walks, so the lion comes anyway).
            yield return new WaitUntil(() => zoo.Chase.Hits >= 1); yield return Capture("zoo_pass_1");
            yield return new WaitUntil(() => NextAhead(ZooNoteKind.ArmsUp) is float a && a < 6f); yield return Capture("zoo_bars_ahead");
            yield return new WaitUntil(() => NextAhead(ZooNoteKind.Jump) is float b && b < 6f); yield return Capture("zoo_fence_ahead");
            keepUp = false; game.KeyStepsOverride = 60f;
            yield return new WaitUntil(() => zoo.Chase.MirrorVisible); yield return new WaitForSeconds(.6f); yield return Capture("zoo_mirror");
            yield return new WaitUntil(() => zoo.Chase.Roaring); yield return new WaitForSeconds(1.2f); yield return Capture("zoo_roar");
            yield return new WaitUntil(() => zoo.Chase.Bites > 0); yield return new WaitForSeconds(.35f); yield return Capture("zoo_pounce");
            // Side view of the lion beside the next props, for sizes.
            var brain = game.OutputCamera.GetComponent<CinemachineBrain>(); brain.enabled = false; game.OutputCamera.cullingMask = ~0;
            game.GetComponent<HUDPresenter>().Hidden = true;
            yield return new WaitForSeconds(1.2f);
            var lion = GameObject.Find("Zoo_Lion").transform;
            game.OutputCamera.transform.position = lion.position + new Vector3(-7f, 2.2f, 2f);
            game.OutputCamera.transform.LookAt(lion.position + new Vector3(0f, 1f, 2f));
            yield return Capture("zoo_side");
            float ahead = float.MaxValue; Transform fence = null;
            foreach (var t in FindObjectsByType<Transform>(FindObjectsSortMode.None))
                if (t.name.StartsWith("Zoo_MD_Zoo_Fence") && t.position.z > lion.position.z && t.position.z - lion.position.z < ahead) { ahead = t.position.z - lion.position.z; fence = t; }
            Report("lion", lion.gameObject); if (fence != null) Report("fence", fence.gameObject);
            var cage = GameObject.Find("Zoo_MD_Zoo_LionCage"); if (cage != null) Report("cage", cage);
            Report("hero", game.Hero.gameObject);
            Finish();
        }
        private static void Report(string name, GameObject item)
        {
            var renderers = item.GetComponentsInChildren<Renderer>(); if (renderers.Length == 0) return;
            var bounds = renderers[0].bounds; foreach (var r in renderers) bounds.Encapsulate(r.bounds);
            Debug.Log("MONGSCAPE_ZOO_SIZE " + name + " " + bounds.size.ToString("F2"));
        }
        private IEnumerator At(float seconds) { yield return new WaitUntil(() => zoo.Chase.Clock >= seconds); }
        private float? NextAhead(ZooNoteKind kind)
        {
            for (int i = 0; i < zoo.Chase.Obstacles.Count; i++)
                if (!zoo.Chase.IsJudged(i) && zoo.Chase.Obstacles[i].Kind == kind) return zoo.Chase.Obstacles[i].Distance - zoo.Chase.Distance;
            return null;
        }
        private IEnumerator Capture(string name) { yield return new WaitForEndOfFrame(); ScreenCapture.CaptureScreenshot(Path.Combine(output, name + ".png")); yield return null; yield return null; }
        private void Update()
        {
            if (done) return;
            if (keepUp && zoo != null && zoo.Chase.Active)
                for (int i = 0; i < zoo.Chase.Obstacles.Count; i++)
                {
                    var obstacle = zoo.Chase.Obstacles[i];
                    float ahead = obstacle.Distance - zoo.Chase.Distance;
                    if (!zoo.Chase.IsJudged(i) && ahead > 0f && ahead < 2.5f)
                        game.Session.Runner.Command(obstacle.Kind == ZooNoteKind.Jump ? MotionCommand.Jump : MotionCommand.ArmsUp);
                }
            if (Time.realtimeSinceStartup - began > 120) Finish();
        }
        private void Finish() { if (done) return; done = true; Debug.Log("MONGSCAPE_ZOO_SHOTS_DONE"); UnityEngine.Application.Quit(0); }
    }
}
#endif
