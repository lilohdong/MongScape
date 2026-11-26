using Mongscape.Domain;
using UnityEngine;

namespace Mongscape.Presentation.Map
{
    // The prison's cell door scene (plan 2-3a), one reusable set placed at the next cell of the prison branch: the cell door
    // on the right wall, the lock gauge over it (in the world, no screen text), the searchlight on the left wall and its
    // warm spot sweeping toward the door and across it while the runner hides. Alarm: the spot turns red.
    public sealed class PrisonStage : MonoBehaviour
    {
        private const float WallX = 3.3f, GaugeWidth = 1.3f, GaugeY = 3.05f, LampY = 3.4f;
        private static readonly Color Warm = new Color(1f, .906f, .64f), Alarm = new Color(1f, .22f, .16f);
        private static readonly Color GaugeFill = new Color(.95f, .72f, .25f), GaugeOpen = new Color(.45f, .9f, .45f), GaugeBack = new Color(.08f, .08f, .1f);
        private RunSession session;
        private GameAudio sounds;
        private PrisonArt art;
        private Transform root, door, lamp, gauge, fill;
        private Animator doorMotion;
        private Light spot;
        private Material fillMaterial;
        private int cellSequence = -1, lastSet = -1, lastAlarms;
        private float open, alarmFlash, clickAt;
        private bool wasOpen;

        public bool Visible => root != null && root.gameObject.activeSelf;
        public int CellSequence => cellSequence;
        public float GaugeShown => fill != null ? fill.localScale.x / GaugeWidth : 0f;
        public bool SpotOn => spot != null && spot.enabled;

        public void Configure(RunSession run, GameAudio audio)
        {
            session = run; sounds = audio; art = PrisonArt.Load();
            root = new GameObject("PrisonStage").transform; root.SetParent(transform, false);
            door = Place(art != null ? art.CellDoor : null, "CellDoor", root, new Vector3(WallX, 0, 0), Quaternion.Euler(0, -90, 0));
            if (door != null && art != null && art.CellDoorMotion != null)
            {
                doorMotion = door.GetComponentInChildren<Animator>() ?? door.gameObject.AddComponent<Animator>();
                doorMotion.runtimeAnimatorController = art.CellDoorMotion; doorMotion.speed = 0f;
            }
            lamp = Place(art != null ? art.Searchlight : null, "Searchlight", root, new Vector3(-WallX, LampY, 0), Quaternion.Euler(0, 90, 0));
            if (lamp != null && art != null && art.SearchlightMotion != null)
            {
                var motion = lamp.GetComponentInChildren<Animator>() ?? lamp.gameObject.AddComponent<Animator>();
                motion.runtimeAnimatorController = art.SearchlightMotion;
            }
            var light = new GameObject("SearchlightSpot"); light.transform.SetParent(root, false);
            light.transform.localPosition = new Vector3(-WallX + .4f, LampY + .3f, 0);
            spot = light.AddComponent<Light>(); spot.type = LightType.Spot; spot.range = 14f; spot.spotAngle = 34f; spot.intensity = 0f;
            spot.color = Warm; spot.shadows = LightShadows.None; spot.enabled = false;
            gauge = Bar("LockGauge", root, new Vector3(WallX - .15f, GaugeY, 0), GaugeBack, out _);
            gauge.localScale = new Vector3(.06f, .2f, GaugeWidth + .08f);
            fill = Bar("LockFill", root, new Vector3(WallX - .2f, GaugeY, 0), GaugeFill, out fillMaterial);
            root.gameObject.SetActive(false);
        }

        private static Transform Place(GameObject prefab, string name, Transform parent, Vector3 position, Quaternion rotation)
        {
            if (prefab == null) return null;
            var item = Instantiate(prefab, parent).transform; item.name = name;
            item.localPosition = position; item.localRotation = rotation;
            return item;
        }
        private static Transform Bar(string name, Transform parent, Vector3 position, Color color, out Material material)
        {
            var bar = GameObject.CreatePrimitive(PrimitiveType.Cube); bar.name = name;
            Destroy(bar.GetComponent<Collider>());
            bar.transform.SetParent(parent, false); bar.transform.localPosition = position;
            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            material = new Material(shader) { color = color };
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            var renderer = bar.GetComponent<Renderer>(); renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return bar.transform;
        }

        private void LateUpdate()
        {
            if (session == null || root == null) return;
            var plan = RouteSurface.ActivePlan;
            int runner = RouteSurface.SequenceAt(session.Runner.Z);
            int start = PrisonCourse.BranchStart(plan, runner);
            var course = session.Prison;
            // The next door still ahead (or the one the runner holds at); none after the third set.
            int set = -1;
            if (start >= 0)
                for (int s = 0; s < PrisonCourse.Sets && set < 0; s++)
                {
                    int cell = start + s * 3 + 1;
                    if (runner < cell || runner == cell && (course.Holding || session.Runner.Z - cell * RouteSurface.Length < PrisonCourse.DoorZ + 4f)) set = s;
                }
            bool show = set >= 0 && session.Flow.State != GameFlowState.Result;
            if (root.gameObject.activeSelf != show) root.gameObject.SetActive(show);
            if (!show) { cellSequence = -1; return; }
            int sequence = start + set * 3 + 1;
            if (sequence != cellSequence || set != lastSet) { cellSequence = sequence; lastSet = set; open = 0f; wasOpen = false; alarmFlash = 0f; }
            float z = sequence * RouteSurface.Length + PrisonCourse.DoorZ;
            RouteSurface.Sample(z, out float x, out float y);
            root.position = new Vector3(x, y, z);

            // Lock gauge and door: only this visit's state counts, and only once the runner is in the branch.
            bool current = PrisonCourse.BranchIndex(plan, runner) >= 0 && course.Set == set;
            bool opened = current && course.Opened[set], spotted = current && course.Spotted[set];
            // Seen: the whole bar turns red (the cell stays shut).
            float shown = opened || spotted ? 1f : current && course.Holding ? course.LockGauge : 0f;
            fill.localScale = new Vector3(.08f, .14f, Mathf.Max(.001f, GaugeWidth * shown));
            fill.localPosition = new Vector3(WallX - .2f, GaugeY, -GaugeWidth * .5f + GaugeWidth * shown * .5f);
            var color = spotted ? Alarm : opened ? GaugeOpen : GaugeFill;
            fillMaterial.color = color; if (fillMaterial.HasProperty("_BaseColor")) fillMaterial.SetColor("_BaseColor", color);
            if (opened && !wasOpen) { sounds?.PlayEffect(art != null ? art.LockOpen : null, .9f); sounds?.PlayEffect(art != null ? art.MonkeyRescue : null, .8f); }
            wasOpen = opened;
            // Picking clicks while the gauge fills.
            if (current && course.Holding && course.Hidden && !opened && !spotted && Time.time >= clickAt) { clickAt = Time.time + .45f; sounds?.PlayEffect(art != null ? art.LockClick : null, .7f); }
            open = Mathf.MoveTowards(open, opened ? 1f : 0f, Time.deltaTime / .6f);
            if (doorMotion != null && doorMotion.isActiveAndEnabled) doorMotion.Play("AN_Prison_CellDoor", 0, open);

            // The searchlight spot: off until the hold, then it comes up the corridor (lead) and sweeps across the door.
            if (course.Alarms > lastAlarms) alarmFlash = 1.2f;
            lastAlarms = course.Alarms;
            alarmFlash = Mathf.Max(0f, alarmFlash - Time.deltaTime);
            bool lit = current && course.Holding || alarmFlash > 0f;
            spot.enabled = lit;
            if (!lit) return;
            float lead = Mathf.Clamp01(course.HoldClock / PrisonCourse.LeadSeconds);
            // -8 m (far up the corridor) -> door during the lead; then across the door to +3 m over the sweep.
            float aimZ = course.LightOn ? Mathf.Lerp(-1.5f, 3f, course.LightProgress) : Mathf.Lerp(-8f, -1.5f, lead);
            var target = root.TransformPoint(new Vector3(WallX - 1.2f, .6f, aimZ));
            spot.transform.rotation = Quaternion.LookRotation(target - spot.transform.position);
            spot.color = alarmFlash > 0f && Mathf.Repeat(alarmFlash * 6f, 1f) < .5f ? Alarm : Warm;
            spot.intensity = Mathf.MoveTowards(spot.intensity, course.LightOn || alarmFlash > 0f ? 22f : 10f, Time.deltaTime * 40f);
        }
    }
}
