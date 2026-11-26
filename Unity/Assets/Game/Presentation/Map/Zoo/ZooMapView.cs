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
        private int lastJumps, lastArms, nextSignal;
        private bool wasPaused, ownsMusic, gateSounded;
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
            if (pause != null)
            {
                // Resume in the zoo (plan 6-7a): four beats of count-in while the song plays its last four beats again,
                // so the song is exactly where the runner is when play goes on.
                pause.ResumeHook = () => Chase.Active ? new ResumePlan(Chase.CountInSeconds, 4) : (ResumePlan?)null;
                pause.Count += n => { if (n == 4 && Chase.Active) LeadIn(); };
            }
            if (content != null && content.Charts != null)
                foreach (var chart in content.Charts) if (chart != null) charts.Add(ZooBeatmap.Parse(chart.text));
            song = gameObject.AddComponent<AudioSource>(); song.playOnAwake = false; song.loop = false; song.spatialBlend = 0f;
            signals = gameObject.AddComponent<AudioSource>(); signals.playOnAwake = false; signals.spatialBlend = 0f;
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
            Chase.Practice = false;
            Begin(chart, SyncOffset);
        }
        // The zoo tutorial: the real song on the easy chart up to the break, on the tutorial's zoo map. syncOffset is the
        // player's calibrated delay. False without a chart.
        public bool StartPractice(float syncOffset)
        {
            var full = charts.Find(c => c.Chart == ZooPractice.Chart) ?? charts.Find(c => c.Tier == ZooTier.Easy);
            if (run == null || full == null) return false;
            StopZoo(); Chase.Practice = true;
            Begin(ZooPractice.Verse(full), syncOffset);
            return true;
        }
        public void StopPractice() { if (Chase.Practice) StopZoo(); }
        // Every verse note has been judged; enough of them were caught.
        public bool VerseOver => Chase.Practice && Chase.Active && Chase.Phase == ZooPhase.Song && Chase.Hits + Chase.Misses >= Chase.Map.Notes.Count;
        public bool VersePassed => Chase.Hits >= ZooPractice.PassHits(Chase.Map.Notes.Count);
        // The tutorial's one look in the mirror: the lion comes close and roars.
        public void ShowLion()
        {
            if (!Chase.Practice || !Chase.Active) return;
            Chase.ShowLion(); peekAt = Chase.SongTime;
            if (audioPlayer != null) audioPlayer.ChaserCue(ChaserSound.Roar);
        }
        private void Begin(ZooBeatmap chart, float syncOffset)
        {
            Chase.SongSpeed = SongSpeed; Chase.SyncOffset = syncOffset; peekAt = -1f; lastStep = -1;
            Chase.Start(chart, run.Runner.Z, Mathf.Max(SongSpeed * .6f, run.CurrentSpeed));
            run.Runner.CenterLane();
            lastJumps = run.Runner.JumpCommands; lastArms = run.Runner.ArmsUpCommands; nextSignal = 0; gateSounded = false;
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
            foreach (var prop in props) if (prop != null) Destroy(prop);
            props.Clear(); noteViews.Clear(); lion = cage = gate = null;
            if (rear != null) rear.enabled = false;
        }
        // The zoo song follows the music volume and ducks with it (docs/AUDIO.md).
        private float Volume => audioPlayer != null ? Mathf.Clamp01(audioPlayer.MusicVolume / .35f) * audioPlayer.MusicDuck : 1f;

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
            PlaySteps();
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
        // The lion's paws on every beat once it is out of the cage, louder the closer it is.
        private void PlaySteps()
        {
            int beat = Mathf.FloorToInt(Chase.SongTime / Chase.Map.BeatSeconds);
            if (beat == lastStep) return;
            lastStep = beat;
            if (audioPlayer != null && Chase.SongTime >= ZooChase.CutSeconds) audioPlayer.LionStep(Mathf.InverseLerp(FarGap, NearGap, Gap()));
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
        private void LeadIn()
        {
            if (song.clip == null) return;
            song.time = Mathf.Clamp(Chase.SongTime - Chase.CountInSeconds, 0f, song.clip.length - .01f);
            if (Chase.SongTime >= Chase.CountInSeconds) { song.UnPause(); if (!song.isPlaying) song.Play(); }
        }
        private void SyncSong()
        {
            if (song.clip == null || run.Paused) return;
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
            if (run.Paused && !wasPaused && Chase.Active) song.Pause();
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
                bool hasContent = content != null;
                GameObject prefab = !hasContent ? null : jump ? (jumps++ % 3 == 2 ? content.Puddle : content.Fence) : content.SqueezeBars;
                var view = Spawn(prefab, jump ? new Vector3(3f, .9f, .25f) : new Vector3(.3f, 2.6f, .3f), jump ? new Color(.55f, .38f, .2f) : new Color(.35f, .36f, .4f), Chase.NoteZ(i), 0f);
                noteViews.Add(view != null ? view.transform : null);
            }
            if (Chase.Map.Length > 0f && !Chase.Practice)
            {
                gate = Spawn(content != null ? content.IronGate : null, new Vector3(4f, 3.5f, .3f), new Color(.3f, .3f, .33f), Chase.EntryZ + Chase.DistanceAt(Chase.Map.BreakEnd), 0f);
                Spawn(content != null ? content.Exit : null, new Vector3(5f, 4f, .5f), new Color(.4f, .6f, .3f), Chase.EntryZ + Chase.DistanceAt(Chase.Map.Length - .5f), 0f);
            }
            float cageZ = Chase.EntryZ + Chase.DistanceAt(.9f);
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
            if (!gateSounded && Chase.SongTime >= Chase.Map.BreakStart) { gateSounded = true; if (audioPlayer != null) audioPlayer.GateDown(); }
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
            float t = Chase.SongTime;
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
            if (peekAt >= 0f) { rig.Show(LionAction.Roar, Chase.SongTime - peekAt); return; }
            var action = Chase.ActionAt(PounceSeconds);
            float time = Chase.SongTime;
            // The breakout fills the entry cut and the roar fills the whole warning.
            if (action == LionAction.CageBreakout && content.LionBreakout != null) time = Chase.SongTime / ZooChase.CutSeconds * content.LionBreakout.length;
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

        private void OnDestroy()
        {
            if (mirrorTexture != null) mirrorTexture.Release();
            if (cutTexture != null) cutTexture.Release();
        }
    }
}
