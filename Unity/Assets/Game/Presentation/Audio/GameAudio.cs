using System;
using Mongscape.Domain;
using UnityEngine;

namespace Mongscape.Presentation
{
    // Effects come from Kenney CC0 files in Resources/MuhanokAudio/Sfx (see Content/Audio/README.md). An effect whose file is
    // missing falls back to the tone synthesised below. Music: a step sequencer whose tempo/energy follows the interval phase.
    // Siren: synthesised (Kenney has none), rises with chase pressure.
    public sealed class GameAudio : MonoBehaviour
    {
        private const int Rate = 44100;
        private RunSession session;
        private AudioSource music, siren, effects;
        private const string SfxFolder = "MuhanokAudio/Sfx/";
        private AudioClip crack, coin;
        private AudioClip[] jump, land, hit, bonk, cue, item, shield, beep, go, step, slip;
        private readonly System.Random pick = new System.Random();
        private float musicVolume = .35f, effectsVolume = .7f;
        // Read on the audio thread.
        private volatile float tempo = 96f, energy = .3f, level;
        private double phase;
        private int lastCoins, lastItems, lastShields;
        private bool wasAirborne, wasSlowing;
        private GameFlowState lastState;
        public void Configure(RunSession run, float musicLevel, float effectsLevel)
        {
            session = run; musicVolume = musicLevel; effectsVolume = effectsLevel;
            music = Source("Music", true); music.clip = AudioClip.Create("Muhanok Music", Rate * 2, 1, Rate, true, FillMusic); music.loop = true; music.Play();
            siren = Source("Siren", true); siren.clip = Siren(); siren.loop = true; siren.volume = 0; siren.Play();
            effects = Source("Effects", false);
            coin = Tone(.12f, f => 1046 + (f > .05f ? 520 : 0), .5f);
            beep = Load(() => Tone(.15f, f => 660, .4f), "count_beep");
            go = Load(() => Tone(.35f, f => 990, .45f), "count_go");
            jump = Load(() => Tone(.18f, f => 300 + f * 2600, .35f), "jump");
            step = Load(() => Tone(.09f, f => 150 - f * 80, .6f), "footstep_0", "footstep_1", "footstep_2", "footstep_3", "footstep_4");
            land = Load(() => Tone(.09f, f => 150 - f * 80, .6f), "land_0", "land_1", "land_2");
            hit = Load(() => Noise(.4f), "hit");
            bonk = Load(() => beep[0], "bonk_0", "bonk_1", "bonk_2");
            cue = Load(() => beep[0], "cue");
            slip = Load(() => Tone(.3f, f => 700 - f * 500, .4f), "slip");
            item = Load(() => Tone(.4f, f => 523 * Mathf.Pow(2, Mathf.Floor(f * 10) / 12f * 4), .4f), "item");
            shield = Load(() => Tone(.35f, f => 880 - f * 900, .45f), "shield");
            crack = Shatter(.16f);
        }
        private AudioSource Source(string id, bool loop)
        {
            var node = new GameObject(id); node.transform.SetParent(transform, false);
            var source = node.AddComponent<AudioSource>(); source.playOnAwake = false; source.loop = loop; source.spatialBlend = 0; return source;
        }
        // Loads the named effect files (variants of one sound); an effect with no file found uses the synthesised fallback.
        private static AudioClip[] Load(Func<AudioClip> fallback, params string[] names)
        {
            var clips = new System.Collections.Generic.List<AudioClip>(names.Length);
            foreach (var name in names) { var clip = Resources.Load<AudioClip>(SfxFolder + name); if (clip != null) clips.Add(clip); }
            if (clips.Count == 0) clips.Add(fallback());
            return clips.ToArray();
        }
        // True when the named effect has a file (it is not the synthesised fallback); tests use it.
        public static bool HasEffectFile(string name) => Resources.Load<AudioClip>(SfxFolder + name) != null;
        private void Play(AudioClip[] clips, float gain)
        {
            if (effects == null || clips == null || clips.Length == 0) return;
            effects.PlayOneShot(clips[clips.Length == 1 ? 0 : pick.Next(clips.Length)], effectsVolume * gain);
        }
        // In-game gain of each effect relative to the effects volume (files are all peak-normalised to -1 dBFS).
        private const float JumpGain = .45f, LandGain = .4f, StepGain = .35f, HitGain = 1f, BonkGain = .75f, CueGain = .5f,
            BeepGain = .6f, GoGain = .7f, SlipGain = .8f, ItemGain = .7f, ShieldGain = .8f;
        public void SetVolumes(float musicLevel, float effectsLevel) { musicVolume = Mathf.Clamp01(musicLevel); effectsVolume = Mathf.Clamp01(effectsLevel); }
        public void Countdown(bool final) { if (final) Play(go, GoGain); else Play(beep, BeepGain); }
        // A short step for a KneeStep on the stairs (the bootstrap calls it with the camera effect), under the music.
        public void Footstep() => Play(step, StepGain);
        // A short glassy break when the ranking strip passes a row.
        public void Crack() { if (effects != null && crack != null) effects.PlayOneShot(crack, effectsVolume * .55f); }
        // The guard's baton hits in the capture scene.
        public void Bonk() => Play(bonk, BonkGain);
        // A coach hint line appears.
        public void Cue() => Play(cue, CueGain);

        // Chaser sound hook (plan 6-8): played when a warning lands. Files MuhanokAudio/Sfx/lion_roar and alarm_bell when
        // present; the police use the siren swell, the others fall back to the hit sound.
        private AudioClip[] roar, alarmBell;
        private int lastWarnings;
        public void ChaserCue(ChaserSound sound)
        {
            if (sound == ChaserSound.Siren) { if (siren != null) siren.volume = Mathf.Max(siren.volume, effectsVolume * .45f); return; }
            if (roar == null) { roar = Load(() => hit[0], "lion_roar"); alarmBell = Load(() => hit[0], "alarm_bell"); }
            Play(sound == ChaserSound.Roar ? roar : alarmBell, HitGain);
        }
        private void Update()
        {
            if (session == null) return;
            if (session.Warnings > lastWarnings && session.Flow.State == GameFlowState.Running) ChaserCue(session.Chaser.Sound);
            lastWarnings = session.Warnings;
            var state = session.Flow.State;
            bool running = state == GameFlowState.Running && !session.Paused;
            // On foot: fast and full, a little faster each lap; the cart: slow and soft; menus: calm.
            bool cart = session.Runner.Riding;
            int lap = RouteSurface.CycleAt(RouteSurface.SequenceAt(session.Runner.Z));
            tempo = running ? (cart ? 92f : 128f + 4f * Math.Min(6, lap)) : 84f;
            energy = running ? (cart ? .35f : 1f) : .3f;
            float target = state == GameFlowState.Result || state == GameFlowState.Caught ? .25f : running ? 1f : .6f;
            level = Mathf.MoveTowards(level, target, Time.unscaledDeltaTime);
            music.volume = musicVolume;
            float danger = running ? Mathf.Max(session.SlipPursuit ? 1f : 0f, Mathf.InverseLerp(.35f, .9f, session.Chase.Pressure)) : 0f;
            // The looping siren is the police; the lion and the warden have their own cue (ChaserCue) until they get loops.
            if (session.Chaser.Sound != ChaserSound.Siren) danger = 0f;
            siren.volume = Mathf.MoveTowards(siren.volume, danger * effectsVolume * .45f, Time.unscaledDeltaTime);
            if (state == GameFlowState.Running)
            {
                if (session.Score.Coins > lastCoins) effects.PlayOneShot(coin, effectsVolume * .6f);
                if (session.Items.Collected > lastItems) Play(item, ItemGain);
                if (session.ShieldSaves > lastShields) Play(shield, ShieldGain);
                bool airborne = session.Runner.Y > .05f;
                if (airborne && !wasAirborne) Play(jump, JumpGain);
                if (!airborne && wasAirborne) Play(land, LandGain);
                wasAirborne = airborne;
                // A missed knee step on the stairs also slows the runner; only a real slip (banana, floor) squelches.
                bool slowing = session.Runner.Slowing;
                if (slowing && !wasSlowing && (session.LastSlipSource == SlipSource.Banana || session.LastSlipSource == SlipSource.Floor)) Play(slip, SlipGain);
                wasSlowing = slowing;
            }
            if (state == GameFlowState.Caught && lastState != GameFlowState.Caught) Play(hit, HitGain);
            lastCoins = session.Score.Coins; lastItems = session.Items.Collected; lastShields = session.ShieldSaves; lastState = state;
        }

        // Audio thread: 16-step pattern in A minor pentatonic with kick, hat, bass and lead.
        private static readonly int[] Lead = { 0, 3, 5, 7, 10, 7, 5, 3, 0, 5, 7, 12, 10, 7, 5, 3 };
        private static readonly int[] Bass = { 0, 0, 7, 0, 5, 5, 3, 3 };
        private uint noise = 22222;
        private void FillMusic(float[] data)
        {
            float bpm = tempo, e = energy, l = level;
            double stepLength = Rate * 60.0 / bpm / 4.0;
            for (int i = 0; i < data.Length; i++)
            {
                int step = (int)(phase / stepLength) % 16;
                double inStep = (phase % stepLength) / stepLength;
                double t = phase / Rate;
                float kick = step % 4 == 0 ? (float)(Math.Sin(2 * Math.PI * (55 + 90 * Math.Exp(-inStep * 18)) * inStep * stepLength / Rate) * Math.Exp(-inStep * 7)) : 0f;
                noise ^= noise << 13; noise ^= noise >> 17; noise ^= noise << 5;
                float hat = step % 2 == 1 ? ((noise & 0xFFFF) / 32768f - 1f) * (float)Math.Exp(-inStep * 30) * .25f * e : 0f;
                double bassFreq = 55 * Math.Pow(2, Bass[step / 2] / 12.0);
                float bass = (Math.Sin(2 * Math.PI * bassFreq * t) > 0 ? .22f : -.22f) * (float)Math.Exp(-inStep * 2.5);
                double leadFreq = 440 * Math.Pow(2, Lead[step] / 12.0);
                float lead = (float)(Math.Sin(2 * Math.PI * leadFreq * t) + .3 * Math.Sin(4 * Math.PI * leadFreq * t)) * (float)Math.Exp(-inStep * 5) * .12f * e;
                data[i] = (kick * .55f * (.4f + .6f * e) + hat + bass + lead) * .55f * l;
                phase += 1;
            }
            if (phase > Rate * 600.0) phase = 0;
        }

        private static AudioClip Tone(float seconds, Func<float, float> frequency, float volume)
        {
            int length = (int)(Rate * seconds); var samples = new float[length]; double phase = 0;
            for (int i = 0; i < length; i++)
            {
                float f = i / (float)length;
                phase += 2 * Math.PI * frequency(f) / Rate;
                samples[i] = (float)Math.Sin(phase) * volume * (1 - f) * Mathf.Clamp01(i / 200f);
            }
            var clip = AudioClip.Create("tone", length, 1, Rate, false); clip.SetData(samples, 0); return clip;
        }
        private static AudioClip Shatter(float seconds)
        {
            int length = (int)(Rate * seconds); var samples = new float[length]; var random = new System.Random(11);
            for (int i = 0; i < length; i++) { float f = i / (float)length; float env = Mathf.Exp(-f * 9); samples[i] = ((float)random.NextDouble() * 2 - 1) * env * .45f + (float)Math.Sin(i * 2 * Math.PI * 2400 / Rate) * env * .25f; }
            var clip = AudioClip.Create("crack", length, 1, Rate, false); clip.SetData(samples, 0); return clip;
        }
        private static AudioClip Noise(float seconds)
        {
            int length = (int)(Rate * seconds); var samples = new float[length]; var random = new System.Random(7);
            for (int i = 0; i < length; i++) { float f = i / (float)length; samples[i] = ((float)random.NextDouble() * 2 - 1) * (1 - f) * (1 - f) * .7f + (float)Math.Sin(i * 2 * Math.PI * 70 / Rate) * (1 - f) * .5f; }
            var clip = AudioClip.Create("hit", length, 1, Rate, false); clip.SetData(samples, 0); return clip;
        }
        private static AudioClip Siren()
        {
            int length = Rate * 2; var samples = new float[length]; double phase = 0;
            for (int i = 0; i < length; i++)
            {
                double f = i < length / 2 ? 650 : 900;
                phase += 2 * Math.PI * f / Rate;
                samples[i] = (float)(Math.Sin(phase) > 0 ? .3 : -.3) * .5f + (float)Math.Sin(phase) * .25f;
            }
            var clip = AudioClip.Create("siren", length, 1, Rate, false); clip.SetData(samples, 0); return clip;
        }
    }
}
