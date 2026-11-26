using System.Collections.Generic;
using Mongscape.Domain;
using UnityEngine;

namespace Mongscape.Presentation.Map
{
    // The zoo map on screen and in the speakers (plan 1-2a, 1-2b, 1-2c, 6-8). ZooChase holds the rules; this view starts it
    // when the runner enters the zoo branch, drives the runner's speed from the song clock, plays the song and the note
    // signals, places the fences, puddles and squeeze bars on the beat, moves the lion behind the runner and shows it in the
    // round rear-view mirror, and plays the entry cut (rear view, the lion breaks out) and the one line "도망치세요!".
    public sealed class ZooMapView : MonoBehaviour
    {
        public readonly ZooChase Chase = new ZooChase();
        // Song speed in m/s and the zoo branch it needs (RoutePlan.SetBranchLayout).
        public const float SongSpeed = 9f;
        public static void Register(RoutePlan plan) { if (plan != null && plan.Extended) plan.SetBranchLayout(SectionKind.Zoo, lap => ZooChase.Branch(SongSpeed)); }
        // Recognition delay of the camera (plan 1-2b: 0.1-0.15 s); the keyboard has none.
        public float SyncOffset { get; set; }

        private RunSession run;
        private GameAudio audioPlayer;
        private ZooContent content;
        private readonly List<ZooBeatmap> charts = new List<ZooBeatmap>();
        private readonly System.Random random = new System.Random();
        private AudioSource song, signals;
        private AudioClip countClick;
        private int lastJumps, lastArms, nextSignal;
        private bool counting, wasPaused, ownsMusic;
        private float countLeft; private int countBeats;
        private readonly List<GameObject> props = new List<GameObject>();
        private readonly List<Transform> noteViews = new List<Transform>();
        private GameObject lion, cage, gate;
        private Vector3 cagePosition;
        private Camera rear;
        private RenderTexture mirrorTexture, cutTexture;
        private Material mirrorMaterial;
        private GUIStyle shoutStyle;

        public void Configure(RunSession session, GameAudio audio)
        {
            run = session; audioPlayer = audio; content = ZooContent.Load();
            if (content != null && content.Charts != null)
                foreach (var chart in content.Charts) if (chart != null) charts.Add(ZooBeatmap.Parse(chart.text));
            song = gameObject.AddComponent<AudioSource>(); song.playOnAwake = false; song.loop = false; song.spatialBlend = 0f;
            signals = gameObject.AddComponent<AudioSource>(); signals.playOnAwake = false; signals.spatialBlend = 0f;
            countClick = Click();
            run.Sections.Entered += Entered;
            run.Sections.Exited += Exited;
            Chase.Bit += () => run.Warn(WarningCause.Gauge);
            Chase.RoarStarted += () => { if (audioPlayer != null) audioPlayer.ChaserCue(ChaserSound.Roar); };
            Chase.Judged += Judged;
        }

        private void Entered(SectionSpan span)
        {
            if (span.Kind != SectionKind.Zoo || !span.Branch || run.IsTutorial) return;
            var chart = ZooBeatmap.Pick(charts, run.Sections.Visits(SectionKind.Zoo), random) ?? new ZooBeatmap();
            Chase.SongSpeed = SongSpeed; Chase.SyncOffset = SyncOffset;
            Chase.Start(chart, run.Runner.Z, Mathf.Max(SongSpeed * .6f, run.CurrentSpeed));
            run.Runner.CenterLane();
            lastJumps = run.Runner.JumpCommands; lastArms = run.Runner.ArmsUpCommands; nextSignal = 0; counting = false;
            if (content != null && content.Music != null) { song.clip = content.Music; song.time = 0f; song.volume = Volume; song.Play(); }
            if (audioPlayer != null) { audioPlayer.MapMusicPlaying = true; ownsMusic = true; }
            BuildProps();
        }
        private void Exited(SectionSpan span) { if (span.Kind == SectionKind.Zoo) StopZoo(); }
        private void StopZoo()
        {
            Chase.Stop(); Chase.Reset(); counting = false;
            if (song != null) song.Stop();
            if (ownsMusic && audioPlayer != null) audioPlayer.MapMusicPlaying = false;
            ownsMusic = false;
            if (run != null) { var hints = run.Workout.Hints; hints.MissingBeats = false; run.Workout.Hints = hints; if (run.SpeedScale == 0f && !run.IsTutorial) run.SpeedScale = 1f; }
            foreach (var prop in props) if (prop != null) Destroy(prop);
            props.Clear(); noteViews.Clear(); lion = cage = gate = null;
            if (rear != null) rear.enabled = false;
        }
        private float Volume => audioPlayer != null ? Mathf.Clamp01(audioPlayer.MusicVolume / .35f) : 1f;

        // Called by the bootstrap each running frame before RunSession.Tick. behind: the running beat lags the music (null for
        // the keyboard or with the cadence option off).
        public void Tick(float dt, bool? behind)
        {
            if (run == null) return;
            if (!Chase.Active)
            {
                if (Chase.Phase == ZooPhase.Done && run.Section.Kind != SectionKind.Zoo) StopZoo();
                return;
            }
            if (run.Section.Kind != SectionKind.Zoo) { StopZoo(); return; }
            if (counting)
            {
                // Resuming after a pause: four beats of count-in on the song tempo, then the song and the runner go on together.
                run.SpeedScale = 0f;
                float beat = Chase.Map.BeatSeconds; countLeft -= dt;
                int due = 4 - Mathf.CeilToInt(Mathf.Max(0f, countLeft) / beat - .001f);
                while (countBeats < Mathf.Min(4, due + 1)) { countBeats++; signals.PlayOneShot(countClick, Volume * .8f); }
                if (countLeft > 0f) return;
                counting = false; run.SpeedScale = 1f;
                if (song.clip != null) { song.time = Mathf.Min(Chase.SongTime, song.clip.length - .01f); song.UnPause(); if (!song.isPlaying) song.Play(); }
            }
            int jumps = run.Runner.JumpCommands, arms = run.Runner.ArmsUpCommands;
            if (jumps > lastJumps) Chase.Input(ZooNoteKind.Jump);
            if (arms > lastArms) Chase.Input(ZooNoteKind.ArmsUp);
            lastJumps = jumps; lastArms = arms;
            Chase.Tick(dt, behind);
            // The song clock places the runner: the speed that lands exactly where the song is (no drift, slips included).
            float expected = Chase.EntryZ + Chase.DistanceAt(Chase.SongTime);
            run.SectionSpeed = Mathf.Clamp((expected - run.Runner.Z) / Mathf.Max(.001f, dt), 0f, Chase.Speed(Chase.SongTime) * 1.5f + 3f);
            // The lion's distance is the chaser gauge the HUD reads (a bite comes from ZooChase after the roar, not from the gauge).
            run.Approach(Mathf.Min(Chase.Lion, .99f) - run.ChaserGauge);
            var hints = run.Workout.Hints; hints.MissingBeats = Chase.Misses >= 3 && Chase.Misses * 2 > Chase.Hits; run.Workout.Hints = hints;
            PlaySignals();
            SyncSong();
        }
        private void Judged(int index, bool hit)
        {
            var note = Chase.Map.Notes[index];
            // A fence trip (plan 6-8): the runner falls and the lion closes in (ZooChase), no warning.
            if (!hit && note.Kind == ZooNoteKind.Jump) run.Runner.Stumble();
            if (hit && note.Kind == ZooNoteKind.ArmsUp && index < noteViews.Count && noteViews[index] != null) noteViews[index].localScale = new Vector3(1.25f, 1f, 1f);
        }
        private void PlaySignals()
        {
            if (content == null) return;
            var notes = Chase.Map.Notes;
            while (nextSignal < notes.Count)
            {
                var note = notes[nextSignal];
                float lead = note.Kind == ZooNoteKind.Jump ? content.JumpLead : content.ArmsUpLead;
                if (Chase.SongTime < note.Time - lead) break;
                var clip = note.Kind == ZooNoteKind.Jump ? content.SignalJump : content.SignalArmsUp;
                if (clip != null && Chase.SongTime - (note.Time - lead) < .1f) signals.PlayOneShot(clip, Volume);
                nextSignal++;
            }
        }
        private void SyncSong()
        {
            if (song.clip == null || counting) return;
            if (Chase.SongTime >= song.clip.length) return;
            if (!song.isPlaying) { song.time = Chase.SongTime; song.Play(); return; }
            if (Mathf.Abs(song.time - Chase.SongTime) > .08f) song.time = Chase.SongTime;
            song.volume = Volume;
        }

        private void Update()
        {
            if (run == null) return;
            if (Chase.Active && run.Flow.State != GameFlowState.Running) { StopZoo(); return; }
            // Pause stops the song; the next running frame starts the four-beat count-in (plan 6-7a).
            if (run.Paused && !wasPaused && Chase.Active) { song.Pause(); counting = true; countLeft = Chase.CountInSeconds; countBeats = 0; }
            wasPaused = run.Paused;
        }

        private void LateUpdate()
        {
            if (!Chase.Active || run == null) { if (rear != null) rear.enabled = false; return; }
            PlaceLion();
            PlaceRear();
            AnimateGate();
            AnimateBars();
        }

        // ---- props on the beat -------------------------------------------------------------------------------------------
        private void BuildProps()
        {
            foreach (var prop in props) if (prop != null) Destroy(prop);
            props.Clear(); noteViews.Clear();
            var notes = Chase.Map.Notes; int jumps = 0;
            for (int i = 0; i < notes.Count; i++)
            {
                bool jump = notes[i].Kind == ZooNoteKind.Jump;
                // Every third jump is a puddle, the others fences (plan 1-2c).
                GameObject prefab = jump ? (jumps++ % 3 == 2 ? content?.Puddle : content?.Fence) : content?.SqueezeBars;
                var view = Spawn(prefab, jump ? new Vector3(3f, .9f, .25f) : new Vector3(.3f, 2.6f, .3f), jump ? new Color(.55f, .38f, .2f) : new Color(.35f, .36f, .4f), Chase.NoteZ(i), 0f);
                noteViews.Add(view != null ? view.transform : null);
            }
            if (Chase.Map.Length > 0f)
            {
                gate = Spawn(content?.IronGate, new Vector3(4f, 3.5f, .3f), new Color(.3f, .3f, .33f), Chase.EntryZ + Chase.DistanceAt(Chase.Map.BreakEnd), 0f);
                Spawn(content?.Exit, new Vector3(5f, 4f, .5f), new Color(.4f, .6f, .3f), Chase.EntryZ + Chase.DistanceAt(Chase.Map.Length - .5f), 0f);
            }
            float cageZ = Chase.EntryZ + Chase.DistanceAt(.9f);
            cage = Spawn(content?.LionCage, new Vector3(3f, 2.5f, 3f), new Color(.45f, .4f, .3f), cageZ, 4.2f);
            cagePosition = cage != null ? cage.transform.position : Vector3.zero;
            lion = Spawn(content?.Lion, new Vector3(.9f, 1.1f, 2f), new Color(.85f, .6f, .25f), cageZ, 4.2f);
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
            // The iron gate comes down through the break and closes just after the runner is through.
            float t = Mathf.InverseLerp(Chase.Map.BreakStart, Chase.Map.BreakEnd + .4f, Chase.SongTime);
            RouteSurface.Sample(gate.transform.position.z, out float x, out float y);
            gate.transform.position = new Vector3(x, y + Mathf.Lerp(3.2f, 0f, t * t), gate.transform.position.z);
        }
        private void AnimateBars()
        {
            // Squeeze bars close in on the beat: narrow as the runner gets near, hit ones stay open.
            for (int i = 0; i < noteViews.Count; i++)
            {
                var view = noteViews[i];
                if (view == null || Chase.Map.Notes[i].Kind != ZooNoteKind.ArmsUp || Chase.WasHit(i)) continue;
                float ahead = Chase.Map.Notes[i].Time - Chase.SongTime;
                float squeeze = Mathf.Clamp01(1f - ahead / 1.2f);
                view.localScale = new Vector3(Mathf.Lerp(1.2f, .8f, squeeze), 1f, 1f);
            }
        }

        // ---- the lion ----------------------------------------------------------------------------------------------------
        private void PlaceLion()
        {
            if (lion == null) return;
            float runnerZ = run.Runner.Z, t = Chase.SongTime;
            float gap = Mathf.Lerp(22f, 2.5f, Chase.Lion);
            if (Chase.Roaring) gap = Mathf.Lerp(gap, 1.6f, 1f - Chase.RoarLeft / ZooChase.RoarSeconds);
            float z = runnerZ - gap;
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
            else position = onPath + Vector3.up * Mathf.Abs(Mathf.Sin(t * Mathf.PI * 2f * 160f / 120f)) * .18f;
            lion.transform.position = position;
            var forward = t < 1f ? Vector3.left : Vector3.forward;
            lion.transform.rotation = Quaternion.LookRotation(forward, Vector3.up) * Quaternion.Euler(Chase.Roaring ? -12f : 0f, 0f, 0f);
        }
        private void PlaceRear()
        {
            bool cut = Chase.Phase == ZooPhase.Cut;
            if (!cut && !Chase.MirrorVisible) { if (rear != null) rear.enabled = false; return; }
            EnsureRear();
            RouteSurface.Sample(run.Runner.Z, out float x, out float y);
            var eye = new Vector3(x + run.Runner.WorldLaneOffset, y + 1.7f, run.Runner.Z + .4f);
            rear.transform.position = eye;
            Vector3 target = cut ? Vector3.Lerp(cagePosition, lion != null ? lion.transform.position : cagePosition, .5f) + Vector3.up * .8f : eye + Vector3.back * 10f + Vector3.down * 1.2f;
            rear.transform.rotation = Quaternion.LookRotation(target - eye, Vector3.up);
            rear.targetTexture = cut ? cutTexture : mirrorTexture;
            rear.fieldOfView = cut ? 60f : 50f;
            rear.enabled = true;
        }
        private void EnsureRear()
        {
            if (rear != null) return;
            mirrorTexture = new RenderTexture(256, 256, 16) { name = "ZooMirror" };
            cutTexture = new RenderTexture(960, 540, 16) { name = "ZooCut" };
            var go = new GameObject("ZooRearCamera");
            go.transform.SetParent(transform, false);
            rear = go.AddComponent<Camera>(); rear.nearClipPlane = .1f; rear.farClipPlane = 120f; rear.depth = -10; rear.enabled = false;
            if (content != null && content.Mirror != null) mirrorMaterial = new Material(content.Mirror);
        }

        private void OnGUI()
        {
            if (Event.current.type != EventType.Repaint || run == null || !Chase.Active || run.Paused) return;
            if (Chase.Phase == ZooPhase.Cut && cutTexture != null && rear != null && rear.enabled)
            {
                // The entry cut: the camera looks back at the cage while the lion breaks out.
                float fade = Mathf.Clamp01(Mathf.Min(Chase.SongTime, ZooChase.CutSeconds - Chase.SongTime) * 5f);
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

        private static AudioClip Click()
        {
            int rate = 44100, n = rate / 20; var data = new float[n];
            for (int i = 0; i < n; i++) data[i] = Mathf.Sin(2f * Mathf.PI * 1500f * i / rate) * Mathf.Exp(-i / (rate * .01f)) * .6f;
            var clip = AudioClip.Create("ZooCountClick", n, 1, rate, false); clip.SetData(data, 0); return clip;
        }
        private void OnDestroy()
        {
            if (mirrorTexture != null) mirrorTexture.Release();
            if (cutTexture != null) cutTexture.Release();
        }
    }
}
