using System.Collections.Generic;
using Mongscape.Domain;
using UnityEngine;

namespace Mongscape.Presentation
{
    public enum MusicSong { None, Menu, Mine, Prison, Result }

    // What should play: a song and its layer volumes (0..1), plus how fast the layers move (seconds for a full change).
    public struct MusicTarget
    {
        public MusicSong Song;
        public float L0, L1, L2, L3;
        public float Fade;
        public float Dim;
        public float this[int i] => i == 0 ? L0 : i == 1 ? L1 : i == 2 ? L2 : L3;
    }

    // Layered loop music (Tools/Music/README.md). Every layer of a song starts with PlayScheduled at the same dspTime and
    // loops; game states only move layer volumes. Layers: menu and result base, rhythm; mine base, tension; prison base,
    // steps, tension, hide. Songs crossfade over SongFade seconds.
    public sealed class MusicPlayer
    {
        public const float SongFade = 1f;
        // Plan 6-6 and the sound review: into the prison hide 0.3 s, out of it 1.0 s, other prison changes 0.5 s;
        // mine tension rises in 0.5 s and falls in 1.5 s; menu and result 1.0 s.
        public const float HideIn = .3f, HideOut = 1f, PrisonFade = .5f, MineRise = .5f, MineFall = 1.5f, ScreenFade = 1f;
        // Mine tension follows the police gauge between these values.
        public const float TensionFrom = .3f, TensionFull = .8f;

        // Picks the song and layer volumes for the game state. mapMusic: a map plays its own song (the zoo), so the loops stop.
        public static MusicTarget Decide(GameFlowState state, bool paused, SectionKind section, PrisonPhase phase, bool wardenChasing,
            float policePressure, bool mapMusic, bool wasHiding)
        {
            var t = new MusicTarget { Dim = state == GameFlowState.Caught ? .4f : paused ? .5f : 1f, Fade = ScreenFade };
            bool inRun = state == GameFlowState.Running || state == GameFlowState.Caught || state == GameFlowState.FirstPersonTakeover;
            if (state == GameFlowState.Result) { t.Song = MusicSong.Result; t.L0 = t.L1 = 1f; return t; }
            if (!inRun) { t.Song = MusicSong.Menu; t.L0 = t.L1 = 1f; return t; }
            if (mapMusic) { t.Song = MusicSong.None; return t; }
            if (section == SectionKind.Prison && phase != PrisonPhase.None)
            {
                t.Song = MusicSong.Prison;
                if (phase == PrisonPhase.Hold) { t.L3 = 1f; t.Fade = HideIn; return t; }
                t.L0 = 1f;
                if (phase == PrisonPhase.Stairs) { t.L1 = 1f; t.L2 = wardenChasing ? 1f : 0f; }
                t.Fade = wasHiding ? HideOut : PrisonFade;
                return t;
            }
            t.Song = MusicSong.Mine; t.L0 = 1f;
            t.L1 = Mathf.Clamp01(Mathf.InverseLerp(TensionFrom, TensionFull, policePressure));
            t.Fade = MineRise;
            return t;
        }

        private sealed class Track
        {
            public AudioSource[] Layers;
            public float[] Volume;
            public float Gain;
            public bool Playing;
        }

        private readonly MusicLibrary library;
        private readonly Transform parent;
        private readonly Dictionary<MusicSong, Track> tracks = new Dictionary<MusicSong, Track>();
        public MusicSong Current { get; private set; }
        // True while the prison hide layer is audible (also while it fades out, so leaving the hide uses HideOut).
        public bool Hiding => tracks.TryGetValue(MusicSong.Prison, out var prison) && prison.Playing && prison.Volume[3] > .001f;

        public MusicPlayer(MusicLibrary library, Transform parent) { this.library = library; this.parent = parent; }

        // True when every loop clip is assigned (otherwise GameAudio keeps its synthesised music).
        public static bool Complete(MusicLibrary l) => l != null && l.MenuBase && l.MenuRhythm && l.MineBase && l.MineTension && l.PrisonBase
            && l.PrisonSteps && l.PrisonTension && l.PrisonHide && l.ResultBase && l.ResultRhythm;

        private AudioClip[] Clips(MusicSong song) => song switch
        {
            MusicSong.Menu => new[] { library.MenuBase, library.MenuRhythm },
            MusicSong.Mine => new[] { library.MineBase, library.MineTension },
            MusicSong.Prison => new[] { library.PrisonBase, library.PrisonSteps, library.PrisonTension, library.PrisonHide },
            MusicSong.Result => new[] { library.ResultBase, library.ResultRhythm },
            _ => new AudioClip[0],
        };

        private Track Get(MusicSong song)
        {
            if (tracks.TryGetValue(song, out var track)) return track;
            var clips = Clips(song);
            track = new Track { Layers = new AudioSource[clips.Length], Volume = new float[clips.Length] };
            for (int i = 0; i < clips.Length; i++)
            {
                var node = new GameObject("Music " + song + " " + i); node.transform.SetParent(parent, false);
                var source = node.AddComponent<AudioSource>();
                source.playOnAwake = false; source.loop = true; source.spatialBlend = 0; source.clip = clips[i]; source.volume = 0;
                track.Layers[i] = source;
            }
            tracks[song] = track;
            return track;
        }

        // master: music volume setting times ducking. dt in unscaled seconds.
        public void Tick(MusicTarget target, float master, float dt)
        {
            if (target.Song != MusicSong.None && target.Song != Current)
            {
                var next = Get(target.Song);
                if (!next.Playing)
                {
                    double at = AudioSettings.dspTime + .1;
                    for (int i = 0; i < next.Layers.Length; i++) { next.Volume[i] = target[i]; next.Layers[i].PlayScheduled(at); }
                    next.Playing = true; next.Gain = 0f;
                }
            }
            Current = target.Song;
            foreach (var pair in tracks)
            {
                var track = pair.Value;
                if (!track.Playing) continue;
                bool current = pair.Key == Current;
                track.Gain = Mathf.MoveTowards(track.Gain, current ? 1f : 0f, dt / SongFade);
                if (!current && track.Gain <= 0f)
                {
                    foreach (var source in track.Layers) source.Stop();
                    track.Playing = false; continue;
                }
                for (int i = 0; i < track.Layers.Length; i++)
                {
                    if (current)
                    {
                        float goal = target[i];
                        // Mine tension falls slower than it rises.
                        float fade = pair.Key == MusicSong.Mine && goal < track.Volume[i] ? MineFall : target.Fade;
                        track.Volume[i] = Mathf.MoveTowards(track.Volume[i], goal, dt / Mathf.Max(.01f, fade));
                    }
                    track.Layers[i].volume = master * target.Dim * track.Gain * track.Volume[i];
                }
            }
        }

        public void StopAll()
        {
            foreach (var track in tracks.Values) { foreach (var source in track.Layers) source.Stop(); track.Playing = false; track.Gain = 0f; }
            Current = MusicSong.None;
        }
    }
}
