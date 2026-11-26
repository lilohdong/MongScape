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
            game.StartPlayerRun(new PlayerProfile { Name = "Zoo QA", Weight = "70", Style = PlayStyle.Keyboard });
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
            // Notes are hit until the normal shot is taken, then missed so the lion closes in.
            yield return At(7.2f); yield return Capture("zoo_normal");
            keepUp = false;
            yield return new WaitUntil(() => zoo.Chase.MirrorVisible); yield return new WaitForSeconds(.6f); yield return Capture("zoo_mirror");
            yield return new WaitUntil(() => zoo.Chase.Roaring); yield return new WaitForSeconds(1.2f); yield return Capture("zoo_roar");
            yield return new WaitUntil(() => zoo.Chase.Bites > 0); yield return new WaitForSeconds(.35f); yield return Capture("zoo_pounce");
            // Side view of the lion beside the next props, for sizes.
            var brain = game.OutputCamera.GetComponent<CinemachineBrain>(); brain.enabled = false;
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
        private IEnumerator At(float songTime) { yield return new WaitUntil(() => zoo.Chase.SongTime >= songTime); }
        private IEnumerator Capture(string name) { yield return new WaitForEndOfFrame(); ScreenCapture.CaptureScreenshot(Path.Combine(output, name + ".png")); yield return null; yield return null; }
        private void Update()
        {
            if (done) return;
            if (keepUp && zoo != null && zoo.Chase.Active)
                for (int i = 0; i < zoo.Chase.Map.Notes.Count; i++)
                {
                    var note = zoo.Chase.Map.Notes[i];
                    if (!zoo.Chase.IsJudged(i) && Mathf.Abs(note.Time - zoo.Chase.SongTime) < .08f)
                        game.Session.Runner.Command(note.Kind == ZooNoteKind.Jump ? MotionCommand.Jump : MotionCommand.ArmsUp);
                }
            if (Time.realtimeSinceStartup - began > 120) Finish();
        }
        private void Finish() { if (done) return; done = true; Debug.Log("MONGSCAPE_ZOO_SHOTS_DONE"); UnityEngine.Application.Quit(0); }
    }
}
#endif
