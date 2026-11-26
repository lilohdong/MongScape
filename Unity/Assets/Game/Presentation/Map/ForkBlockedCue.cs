using Mongscape.Domain;
using UnityEngine;

namespace Mongscape.Presentation.Map
{
    // Plan 3-2: leaning toward the closed rail at the fork does not move the cart; the cart jolts a little and a short
    // "blocked" sound plays, so the player knows the lean was seen. Runs after the camera rig so the jolt is not overwritten.
    [DefaultExecutionOrder(1000)]
    public sealed class ForkBlockedCue : MonoBehaviour
    {
        public const float JoltSeconds = .18f, JoltMetres = .07f;
        private RunSession session;
        private AudioSource voice;
        private int seen;
        private float joltLeft;
        public int Played { get; private set; }
        public void Configure(RunSession run, float volume = .8f)
        {
            session = run; seen = run.Runner.BlockedLeans;
            var art = MineArt.Load();
            if (art != null && art.ForkBlocked != null)
            {
                voice = gameObject.AddComponent<AudioSource>(); voice.playOnAwake = false; voice.spatialBlend = 0; voice.clip = art.ForkBlocked; voice.volume = volume;
            }
        }
        private void LateUpdate()
        {
            if (session == null) return;
            int leans = session.Runner.BlockedLeans;
            if (leans < seen) seen = leans;
            if (leans > seen) { seen = leans; Played++; joltLeft = JoltSeconds; if (voice != null) voice.Play(); }
            if (joltLeft <= 0) return;
            joltLeft -= Time.deltaTime;
            var eye = Camera.main;
            if (eye != null) eye.transform.position += new Vector3(0, Mathf.Sin(joltLeft / JoltSeconds * Mathf.PI * 3) * JoltMetres, 0);
        }
    }
}
