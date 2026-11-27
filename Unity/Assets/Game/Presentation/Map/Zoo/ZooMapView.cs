using System.Collections.Generic;
using Mongscape.Domain;
using UnityEngine;

namespace Mongscape.Presentation.Map
{
    // The zoo map on screen and in the speakers (owner 2026-11-27: run away from the lion). ZooChase holds the rules; this view
    // starts it when the runner enters the zoo branch, sets the zoo base speed (the cadence pace multiplies it like in the
    // mine), loops the song in the background, places the fences, puddles and squeeze bars at distances, moves the lion
    // behind the runner where only the round rear-view mirror shows it (when it comes close), and plays the entry cut
    // (rear view, the lion breaks out) and the one line "도망치세요!".
    public sealed class ZooMapView : MonoBehaviour
    {
        public readonly ZooChase Chase = new ZooChase();
        // Zoo base speed in m/s and the zoo branch (RoutePlan.SetBranchLayout).
        public const float BaseSpeed = 9f;
        public static void Register(RoutePlan plan) { if (plan != null && plan.Extended) plan.SetBranchLayout(SectionKind.Zoo, lap => ZooChase.Branch()); }
        // The lion lives on this layer: the main camera never draws it, only the rear view (the entry cut and the mirror).
        public const int LionLayer = 31;

        private RunSession run;
        private GameAudio audioPlayer;
        private ZooContent content;
        private readonly System.Random random = new System.Random();
        private AudioSource song;
        private int lastJumps, lastArms;
        private bool wasPaused, ownsMusic, gateSounded;
        private Camera hidFrom; private int hidMask;
        private readonly List<GameObject> props = new List<GameObject>();
        private readonly List<Transform> noteViews = new List<Transform>();
        private GameObject lion, cage, gate;
        private Vector3 cagePosition;
        private LionRig rig;
        private int lastStep;
        private float peekAt = -1f;
        // The lion's distance behind the runner: far away, and at the heels (roar and pounce).
        public const float FarGap = 22f, NearGap = 3.6f;
        // Sized against the cage and the other chaser (the art model is a head taller).
        private const float LionScale = .85f;
        public LionRig Rig => rig;
        private Camera rear;
        private RenderTexture mirrorTexture, cutTexture;
        private Material mirrorMaterial;
        private GUIStyle shoutStyle;

        public void Configure(RunSession session, GameAudio audio, PauseFlow pause)
        {
            run = session; audioPlayer = audio; content = ZooContent.Load();
            // The song is only background now (the zoo is no rhythm game): a pause resumes with the usual count.
            song = gameObject.AddComponent<AudioSource>(); song.playOnAwake = false; song.loop = true; song.spatialBlend = 0f;
            run.Sections.Entered += Entered;
            run.Sections.Exited += Exited;
            Chase.Bit += () => run.Warn(WarningCause.Gauge);
            Chase.RoarStarted += () => { if (audioPlayer != null) audioPlayer.ChaserCue(ChaserSound.Roar); };
            Chase.Judged += Judged;
        }

        private void Entered(SectionSpan span)
        {
            if (span.Kind != SectionKind.Zoo || !span.Branch || run.IsTutorial) return;
            int visit = run.Sections.Visits(SectionKind.Zoo);
            Chase.Practice = false; Chase.LionPace = ZooChase.LionPaceFor(visit);
            Begin(ZooChase.Layout(ZooChase.CourseLength, visit, random), ZooChase.CourseLength, true);
        }
        // The zoo tutorial: a short course of fences and bars on the tutorial's zoo map, no iron gate. The argument is unused
        // (the old sync offset).
        public bool StartPractice(float unused, IList<ZooObstacle> layout = null)
        {
            if (run == null) return false;
            StopZoo(); Chase.Practice = true; Chase.LionPace = ZooChase.LionPaceFor(1);
            Begin(layout != null ? new List<ZooObstacle>(layout) : ZooChase.Layout(ZooPractice.Length, 1, random, false), ZooPractice.Length, false);
            return true;
        }
        public void StopPractice() { if (Chase.Practice) StopZoo(); }
        // Every practice fence and bar has been passed or missed; enough of them were passed.
        public bool VerseOver => Chase.Practice && Chase.Active && Chase.Phase == ZooPhase.Song && Chase.Hits + Chase.Misses >= Chase.Obstacles.Count;
        public bool VersePassed => Chase.Hits >= ZooPractice.PassHits(Chase.Obstacles.Count);
        // The tutorial's one look in the mirror: the lion comes close and roars.
        public void ShowLion()
        {
            if (!Chase.Practice || !Chase.Active) return;
            Chase.ShowLion(); peekAt = Chase.Clock;
            if (audioPlayer != null) audioPlayer.ChaserCue(ChaserSound.Roar);
        }
        private void Begin(List<ZooObstacle> obstacles, float length, bool gate)
        {
            Chase.BaseSpeed = BaseSpeed; peekAt = -1f; lastStep = -1;
            Chase.Start(obstacles, run.Runner.Z, length, gate);
            run.Runner.CenterLane(); run.SectionSpeed = BaseSpeed;
            lastJumps = run.Runner.JumpCommands; lastArms = run.Runner.ArmsUpCommands; gateSounded = false;
            Chase.RoarLead = ZooChase.RoarLeadFor(run.WarningCueSeconds);
            if (content != null && content.Music != null) { song.clip = content.Music; song.time = 0f; song.volume = Volume; song.Play(); }
            if (audioPlayer != null) { audioPlayer.MapMusicPlaying = true; ownsMusic = true; }
            BuildProps();
        }
        private void Exited(SectionSpan span) { if (span.Kind == SectionKind.Zoo) StopZoo(); }
        private void StopZoo()
        {
            Chase.Stop(); Chase.Reset(); Chase.Practice = false; peekAt = -1f; rig = null;
            if (song != null) song.Stop();
            if (ownsMusic && audioPlayer != null) audioPlayer.MapMusicPlaying = false;
            ownsMusic = false;
            if (run != null) { var hints = run.Workout.Hints; hints.MissingBeats = false; run.Workout.Hints = hints; }
            ShowLionLayer();
            foreach (var prop in props) if (prop != null) Destroy(prop);
            props.Clear(); noteViews.Clear(); lion = cage = gate = null;
            if (rear != null) rear.enabled = false;
        }
        // The zoo song follows the music volume and ducks with it (docs/AUDIO.md).
        private float Volume => audioPlayer != null ? Mathf.Clamp01(audioPlayer.MusicVolume / .35f) * audioPlayer.MusicDuck : 1f;

        // Called by the bootstrap each running frame before RunSession.Tick. pace: the runner's cadence pace (CadencePace.Scale),
        // null without a cadence (the lion then only comes on missed fences and bars).
        public void Tick(float dt, float? pace)
        {
            if (run == null) return;
            if (!Chase.Active)
            {
                if (Chase.Phase == ZooPhase.Done && run.Section.Kind != SectionKind.Zoo) StopZoo();
                return;
            }
            if (run.Section.Kind != SectionKind.Zoo) { StopZoo(); return; }
            int jumps = run.Runner.JumpCommands, arms = run.Runner.ArmsUpCommands;
            if (jumps > lastJumps) Chase.Input(ZooNoteKind.Jump);
            if (arms > lastArms) Chase.Input(ZooNoteKind.ArmsUp);
            lastJumps = jumps; lastArms = arms;
            Chase.Tick(dt, run.Runner.Z - Chase.EntryZ, pace);
            // The zoo base speed; RunSession multiplies it by the cadence pace like in the mine.
            run.SectionSpeed = BaseSpeed;
            // The lion's distance is the chaser gauge the HUD reads (a bite comes from ZooChase after the roar, not from the gauge).
            run.Approach(Mathf.Min(Chase.Lion, .99f) - run.ChaserGauge);
            PlaySteps();
            if (song.clip != null && !song.isPlaying && !run.Paused) song.Play();
            song.volume = Volume;
        }
        private void Judged(int index, bool hit)
        {
            var obstacle = Chase.Obstacles[index];
            // A fence trip (plan 6-8): the runner falls and the lion closes in (ZooChase), no warning.
            if (!hit && obstacle.Kind == ZooNoteKind.Jump) run.Runner.Stumble();
            if (hit && obstacle.Kind == ZooNoteKind.ArmsUp && index < noteViews.Count && noteViews[index] != null) noteViews[index].localScale = new Vector3(1.25f, 1f, 1f);
        }
        // The lion's paws (about 2.7 strides a second) once it is out of the cage, louder the closer it is.
        private void PlaySteps()
        {
            int step = Mathf.FloorToInt(Chase.Clock / .375f);
            if (step == lastStep) return;
            lastStep = step;
            if (audioPlayer != null && Chase.Clock >= ZooChase.CutSeconds) audioPlayer.LionStep(Mathf.InverseLerp(FarGap, NearGap, Gap()));
        }
        private float PounceSeconds => content != null && content.LionPounce != null ? content.LionPounce.length : .9f;
        private float Gap()
        {
            float gap = Mathf.Lerp(FarGap, NearGap + 1f, Chase.Lion);
            if (Chase.Roaring) return Mathf.Lerp(gap, NearGap, 1f - Chase.RoarLeft / Mathf.Max(ZooChase.RoarSeconds, Chase.RoarLead));
            // The pounce lands at the heels, then the lion drops back.
            if (Chase.SinceBite < PounceSeconds) return Mathf.Lerp(NearGap, gap, Mathf.InverseLerp(.7f, 1f, Chase.SinceBite / PounceSeconds));
            return gap;
        }

        private void Update()
        {
            if (run == null) return;
            if (Chase.Active && run.Flow.State != GameFlowState.Running) { StopZoo(); return; }
            // Pause stops the song; the next running frame starts the four-beat count-in (plan 6-7a).
            if (run.Paused && !wasPaused && Chase.Active) song.Pause();
            if (!run.Paused && wasPaused && Chase.Active) song.UnPause();
            wasPaused = run.Paused;
        }

        private void LateUpdate()
        {
            if (!Chase.Active || run == null) { if (rear != null) rear.enabled = false; return; }
            HideLionLayer();
            PlaceLion();
            PlaceRear();
            AnimateGate();
            AnimateBars();
        }

        // ---- props at distances -------------------------------------------------------------------------------------------
        private void BuildProps()
        {
            foreach (var prop in props) if (prop != null) Destroy(prop);
            props.Clear(); noteViews.Clear();
            var obstacles = Chase.Obstacles; int jumps = 0;
            for (int i = 0; i < obstacles.Count; i++)
            {
                bool jump = obstacles[i].Kind == ZooNoteKind.Jump;
                // Every third jump is a puddle, the others fences (plan 1-2c).
                bool hasContent = content != null;
                GameObject prefab = !hasContent ? null : jump ? (jumps++ % 3 == 2 ? content.Puddle : content.Fence) : content.SqueezeBars;
                var view = Spawn(prefab, jump ? new Vector3(3f, .9f, .25f) : new Vector3(.3f, 2.6f, .3f), jump ? new Color(.55f, .38f, .2f) : new Color(.35f, .36f, .4f), Chase.ObstacleZ(i), 0f);
                noteViews.Add(view != null ? view.transform : null);
            }
            if (Chase.HasGate && !Chase.Practice)
            {
                gate = Spawn(content != null ? content.IronGate : null, new Vector3(4f, 3.5f, .3f), new Color(.3f, .3f, .33f), Chase.EntryZ + Chase.GateEnd, 0f);
                Spawn(content != null ? content.Exit : null, new Vector3(5f, 4f, .5f), new Color(.4f, .6f, .3f), Chase.EntryZ + Chase.Length - 4f, 0f);
            }
            float cageZ = Chase.EntryZ + 8f;
            cage = Spawn(content != null ? content.LionCage : null, new Vector3(3f, 2.5f, 3f), new Color(.45f, .4f, .3f), cageZ, 4.2f);
            cagePosition = cage != null ? cage.transform.position : Vector3.zero;
            lion = Spawn(content != null ? content.Lion : null, new Vector3(.9f, 1.1f, 2f), new Color(.85f, .6f, .25f), cageZ, 4.2f);
            rig = null;
            if (content != null && content.Lion != null)
            {
                // The model keeps its own import transform under a holder that the chase moves.
                var model = lion;
                var holder = new GameObject(model.name); holder.transform.position = model.transform.position;
                model.transform.SetParent(holder.transform, true);
                props[props.Count - 1] = lion = holder;
                rig = model.AddComponent<LionRig>();
                rig.Configure(content.LionBreakout, content.LionRun, content.LionRoar, content.LionPounce);
                holder.transform.localScale = Vector3.one * LionScale;
                // A warm key light in front of the face, so the lion reads in the small mirror at night.
                var glow = new GameObject("LionLight").AddComponent<Light>();
                glow.transform.SetParent(holder.transform, false); glow.transform.localPosition = new Vector3(0f, 2.6f, 3f);
                glow.type = LightType.Point; glow.color = new Color(1f, .82f, .6f); glow.range = 8f; glow.intensity = 6f;
            }
            if (lion != null) foreach (var t in lion.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = LionLayer;
        }
        private GameObject Spawn(GameObject prefab, Vector3 fallbackSize, Color fallbackColor, float z, float side)
        {
            GameObject item;
            if (prefab != null) item = Instantiate(prefab);
            else
            {
                item = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Destroy(item.GetComponent<Collider>());
                item.transform.localScale = fallbackSize;
                var renderer = item.GetComponent<Renderer>(); if (renderer != null) renderer.material.color = fallbackColor;
            }
            foreach (var collider in item.GetComponentsInChildren<Collider>()) Destroy(collider);
            RouteSurface.Sample(z, out float x, out float y);
            item.transform.position = new Vector3(x + side, y + (prefab == null ? fallbackSize.y * .5f : 0f), z);
            item.name = "Zoo_" + (prefab != null ? prefab.name : "Note");
            props.Add(item);
            return item;
        }
        private void AnimateGate()
        {
            if (gate == null) return;
            if (!gateSounded && Chase.Distance >= Chase.GateStart) { gateSounded = true; if (audioPlayer != null) audioPlayer.GateDown(); }
            // The iron gate comes down through the sprint stretch and closes just after the runner is through.
            float t = Mathf.InverseLerp(Chase.GateStart, Chase.GateEnd + 4f, Chase.Distance);
            RouteSurface.Sample(gate.transform.position.z, out float x, out float y);
            gate.transform.position = new Vector3(x, y + Mathf.Lerp(3.2f, 0f, t * t), gate.transform.position.z);
        }
        private void AnimateBars()
        {
            // Squeeze bars close in as the runner gets near, passed ones stay open.
            for (int i = 0; i < noteViews.Count; i++)
            {
                var view = noteViews[i];
                if (view == null || Chase.Obstacles[i].Kind != ZooNoteKind.ArmsUp || Chase.WasHit(i)) continue;
                float ahead = Chase.Obstacles[i].Distance - Chase.Distance;
                float squeeze = Mathf.Clamp01(1f - ahead / (1.2f * BaseSpeed));
                view.localScale = new Vector3(Mathf.Lerp(1.2f, .8f, squeeze), 1f, 1f);
            }
        }

        // ---- the lion ----------------------------------------------------------------------------------------------------
        private void PlaceLion()
        {
            if (lion == null) return;
            float t = Chase.Clock;
            Animate();
            float z = run.Runner.Z - Gap();
            RouteSurface.Sample(z, out float x, out float y);
            var onPath = new Vector3(x, y, z);
            Vector3 position;
            if (t < 1f) position = cagePosition + Vector3.up * Mathf.Abs(Mathf.Sin(t * 30f)) * .05f;
            else if (t < ZooChase.CutSeconds)
            {
                // The breakout: a leap from the cage onto the path.
                float k = (t - 1f) / (ZooChase.CutSeconds - 1f);
                position = Vector3.Lerp(cagePosition, onPath, k) + Vector3.up * Mathf.Sin(k * Mathf.PI) * 1.6f;
                if (cage != null) cage.transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * 40f) * 3f * (1f - k));
            }
            // The run clip carries the gallop; the box stand-in keeps a bob.
            else position = onPath + Vector3.up * (rig != null ? 0f : Mathf.Abs(Mathf.Sin(t * Mathf.PI * 2f * 160f / 120f)) * .18f);
            lion.transform.position = position;
            var forward = t < 1f ? Vector3.left : Vector3.forward;
            lion.transform.rotation = Quaternion.LookRotation(forward, Vector3.up) * Quaternion.Euler(Chase.Roaring && rig == null ? -12f : 0f, 0f, 0f);
        }
        private void Animate()
        {
            if (rig == null) return;
            if (peekAt >= 0f) { rig.Show(LionAction.Roar, Chase.Clock - peekAt); return; }
            var action = Chase.ActionAt(PounceSeconds);
            float time = Chase.Clock;
            // The breakout fills the entry cut and the roar fills the whole warning.
            if (action == LionAction.CageBreakout && content.LionBreakout != null) time = Chase.Clock / ZooChase.CutSeconds * content.LionBreakout.length;
            else if (action == LionAction.Roar && content.LionRoar != null) time = (1f - Chase.RoarLeft / Mathf.Max(ZooChase.RoarSeconds, Chase.RoarLead)) * content.LionRoar.length;
            else if (action == LionAction.Pounce) time = Chase.SinceBite;
            rig.Show(action, time);
        }
        private void PlaceRear()
        {
            bool cut = Chase.Phase == ZooPhase.Cut;
            if (!cut && !Chase.MirrorVisible) { if (rear != null) rear.enabled = false; return; }
            EnsureRear();
            RouteSurface.Sample(run.Runner.Z, out float x, out float y);
            var eye = new Vector3(x + run.Runner.WorldLaneOffset, y + 1.7f, run.Runner.Z + .4f);
            rear.transform.position = eye;
            Vector3 target = cut ? Vector3.Lerp(cagePosition, lion != null ? lion.transform.position : cagePosition, .5f) + Vector3.up * .8f : lion != null ? lion.transform.position + Vector3.up * 1.1f : eye + Vector3.back * 10f + Vector3.down * 1.2f;
            rear.transform.rotation = Quaternion.LookRotation(target - eye, Vector3.up);
            rear.targetTexture = cut ? cutTexture : mirrorTexture;
            rear.fieldOfView = cut ? 60f : 34f;
            rear.enabled = true;
        }
        private void EnsureRear()
        {
            if (rear != null) return;
            mirrorTexture = new RenderTexture(256, 256, 16) { name = "ZooMirror" };
            cutTexture = new RenderTexture(960, 540, 16) { name = "ZooCut" };
            var go = new GameObject("ZooRearCamera");
            go.transform.SetParent(transform, false);
            rear = go.AddComponent<Camera>(); rear.cullingMask = ~0; rear.nearClipPlane = .1f; rear.farClipPlane = 120f; rear.depth = -10; rear.enabled = false;
            if (content != null && content.Mirror != null) mirrorMaterial = new Material(content.Mirror);
        }

        private void OnGUI()
        {
            if (Event.current.type != EventType.Repaint || run == null || !Chase.Active || run.Paused) return;
            if (Chase.Phase == ZooPhase.Cut && cutTexture != null && rear != null && rear.enabled)
            {
                // The entry cut: the camera looks back at the cage while the lion breaks out.
                float fade = Mathf.Clamp01(Mathf.Min(Chase.Clock, ZooChase.CutSeconds - Chase.Clock) * 5f);
                var old = GUI.color; GUI.color = new Color(1f, 1f, 1f, fade);
                GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), cutTexture, ScaleMode.ScaleAndCrop);
                GUI.color = old;
            }
            if (Chase.Phase == ZooPhase.Shout)
            {
                shoutStyle ??= new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, font = UiFont.Main };
                shoutStyle.fontSize = Mathf.RoundToInt(Screen.height * .085f);
                var rect = new Rect(0, Screen.height * .3f, Screen.width, Screen.height * .2f);
                var old = GUI.color;
                GUI.color = new Color(0f, 0f, 0f, .7f); GUI.Label(new Rect(rect.x + 4, rect.y + 4, rect.width, rect.height), "도망치세요!", shoutStyle);
                GUI.color = new Color(1f, .85f, .2f, 1f); GUI.Label(rect, "도망치세요!", shoutStyle);
                GUI.color = old;
            }
            if (Chase.MirrorVisible && mirrorTexture != null && rear != null && rear.enabled)
            {
                // Bottom left (the pose window is bottom right): bigger and redder as the lion closes in.
                float danger = Chase.Roaring ? 1f : Mathf.InverseLerp(ZooChase.MirrorAt, 1f, Chase.Lion);
                float size = Screen.height * Mathf.Lerp(.17f, .24f, danger), margin = Screen.height * .03f;
                var rect = new Rect(margin, Screen.height - margin - size, size, size);
                if (mirrorMaterial != null) { mirrorMaterial.SetFloat("_Danger", danger); Graphics.DrawTexture(rect, mirrorTexture, mirrorMaterial); }
                else GUI.DrawTexture(rect, mirrorTexture);
            }
        }

        // The main camera never draws the lion (it shows only in the rear view); restored when the zoo ends.
        private void HideLionLayer()
        {
            var main = Camera.main;
            if (main == null || main == rear || main == hidFrom) return;
            ShowLionLayer();
            hidFrom = main; hidMask = main.cullingMask; main.cullingMask &= ~(1 << LionLayer);
        }
        private void ShowLionLayer()
        {
            if (hidFrom != null) hidFrom.cullingMask = hidMask;
            hidFrom = null;
        }

        private void OnDestroy()
        {
            if (mirrorTexture != null) mirrorTexture.Release();
            if (cutTexture != null) cutTexture.Release();
        }
    }
}
