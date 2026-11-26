using Mongscape.Domain;
using UnityEngine;
using UnityEngine.UIElements;

namespace Mongscape.Presentation
{
    // The running HUD in UI Toolkit (style guide 6 and 7). No text while running: numbers, shapes, colour and sound only.
    //   top left: score, distance, two warning slots (handcuffs)      top middle: rescued monkeys, police chase bar (mine)
    //   top right: the rank strip (row above with its name, my row) and the break-through effect
    //   bottom left: the zoo rear-view mirror slot (render texture set by the zoo)   bottom right: the pose window
    //   middle: the tracking-lost silhouette; screen edges: the warning flash and the chaser-close pulse.
    public sealed class RunHudView
    {
        public readonly UiRoot Root;
        readonly VisualElement frame;
        Label score, distance, monkeys, countdown;
        VisualElement scorePanel, chase, chaseFill, chaseCap, flash, lost, lostPulse, poseWindow, mirror, mirrorRing;
        Image mirrorImage;
        Image poseImage;
        ShapeElement skeleton, lostDrawing, shield;
        readonly VisualElement[] slots = new VisualElement[2], slotRings = new VisualElement[2];
        readonly ShapeElement[] slotIcons = new ShapeElement[2];
        readonly bool[] slotFull = new bool[2];
        readonly VisualElement[] edges = new VisualElement[4];
        public readonly RankStripElement Strip;

        int shownScore = -1, shownMeters = -1, shownMonkeys = -1, warnings, shields;
        float scorePopAt = -10, scoreTintUntil = -10, flashAt = -10, clearAt = -10, shieldAt = -10;
        readonly float[] slotFilledAt = { -10, -10 };
        Color flashColor = UiTokens.Danger;
        float now;
        IHudData data;

        public RunHudView(GameObject host)
        {
            Root = new UiRoot(host, "RunHud", 5);
            frame = Root.Frame;
            Build();
            Strip = new RankStripElement(); frame.Add(Strip);
            Root.Visible = false;
        }

        void Build()
        {
            // Edge flash (under everything else).
            var ramp = Ramp();
            for (int i = 0; i < 4; i++)
            {
                var e = new VisualElement { pickingMode = PickingMode.Ignore };
                e.style.position = Position.Absolute; e.style.backgroundImage = ramp;
                e.style.unityBackgroundImageTintColor = UiTokens.Danger;
                const float w = 96; // 60 px band + 36 px blur
                if (i == 0) { e.style.left = 0; e.style.right = 0; e.style.top = 0; e.style.height = w; e.style.rotate = new Rotate(0); }
                edges[i] = e; frame.Add(e);
            }
            // Rotated copies of the same ramp: top band (texture fades downward), bottom, left, right.
            Place(edges[1], 0, 1080 - 96, 1920, 96, 180);
            Place(edges[2], -492, 492, 1080, 96, 270);
            Place(edges[3], 1920 - 588, 492, 1080, 96, 90);
            Place(edges[0], 0, 0, 1920, 96, 0);

            // Score panel (36,32) to (520,258).
            scorePanel = UiTokens.Box(36, 32, 484, 226);
            scorePanel.style.backgroundColor = UiTokens.A(UiTokens.Panel, .82f); UiTokens.Radius(scorePanel, 28);
            UiTokens.Border(scorePanel, 4, UiTokens.A(UiTokens.PanelEdge, .9f));
            frame.Add(scorePanel);
            score = UiTokens.Label("0", 84, UiTokens.Text); UiTokens.Absolute(score, 28, 6, 430, 96); score.style.transformOrigin = new TransformOrigin(0, Length.Percent(50));
            distance = UiTokens.Label("0 m", 44, UiTokens.TextDim, TextAnchor.MiddleRight); UiTokens.Absolute(distance, 230, 108, 226, 92);
            scorePanel.Add(score); scorePanel.Add(distance);
            for (int i = 0; i < 2; i++)
            {
                int k = i;
                var slot = UiTokens.Box(28 + i * 116, 108, 100, 92); UiTokens.Radius(slot, 22);
                slot.style.transformOrigin = new TransformOrigin(Length.Percent(50), Length.Percent(50));
                var ring = UiTokens.Box(-12, -12, 124, 116); UiTokens.Radius(ring, 30); UiTokens.Border(ring, 8, UiTokens.WarnHi); ring.style.display = DisplayStyle.None;
                var icon = UiIcons.Make(8, 4, 84, UiIcons.Handcuffs(() => slotFull[k] ? UiTokens.Panel : UiTokens.A(UiTokens.Text, .42f)));
                slot.Add(icon); slot.Add(ring);
                slots[i] = slot; slotRings[i] = ring; slotIcons[i] = icon;
                scorePanel.Add(slot);
                SetSlot(i, false);
            }
            shield = UiIcons.Make(56, 60, 120, UiIcons.Shield); shield.style.display = DisplayStyle.None; scorePanel.Add(shield);

            // Top middle: monkeys, then the chase bar.
            var monkeyRow = UiTokens.Box(760, 28, 400, 84);
            monkeyRow.style.flexDirection = FlexDirection.Row; monkeyRow.style.justifyContent = Justify.Center; monkeyRow.style.alignItems = Align.Center;
            var monkeyIcon = new ShapeElement(UiIcons.Monkey()); monkeyIcon.style.width = 72; monkeyIcon.style.height = 72; monkeyIcon.style.marginRight = 14;
            monkeys = UiTokens.Label("0", 72, UiTokens.Text);
            monkeyRow.Add(monkeyIcon); monkeyRow.Add(monkeys); frame.Add(monkeyRow);

            chase = UiTokens.Box(680, 128, 560, 64);
            var track = UiTokens.Box(0, 16, 560, 32); track.style.backgroundColor = UiTokens.A(UiTokens.Deep, .8f); UiTokens.Radius(track, 16);
            UiTokens.Border(track, 3, UiTokens.A(UiTokens.PanelEdge, .9f));
            chaseFill = UiTokens.Box(0, 0, 100, 26); chaseFill.style.backgroundColor = UiTokens.Warn; UiTokens.Radius(chaseFill, 13);
            track.Add(chaseFill);
            chaseCap = UiIcons.Make(0, -4, 72, UiIcons.PoliceCap);
            var runner = UiIcons.Make(560 - 30, -6, 76, UiIcons.Monkey());
            chase.Add(track); chase.Add(runner); chase.Add(chaseCap);
            frame.Add(chase);

            // Bottom left: the rear-view mirror slot (the zoo sets the texture and the stage).
            mirror = UiTokens.Box(230 - 140, 830 - 140, 280, 280); UiTokens.Radius(mirror, 999); mirror.style.overflow = Overflow.Hidden;
            mirror.style.backgroundColor = UiTokens.Deep;
            mirrorImage = new Image { pickingMode = PickingMode.Ignore, scaleMode = ScaleMode.ScaleAndCrop }; UiTokens.Fill(mirrorImage);
            mirror.Add(mirrorImage);
            var glint = new ShapeElement((p, r) => UiIcons.Ring(p, r.center, r.width * .38f, r.width * .05f, UiTokens.A(Color.white, .35f), 200, 250)); UiTokens.Fill(glint);
            mirror.Add(glint);
            mirrorRing = new VisualElement { pickingMode = PickingMode.Ignore }; UiTokens.Fill(mirrorRing); UiTokens.Radius(mirrorRing, 999);
            mirror.Add(mirrorRing);
            mirror.style.display = DisplayStyle.None;
            frame.Add(mirror);

            // Bottom right: the pose window (294 x 218).
            poseWindow = UiTokens.Box(1920 - 36 - 294, 1080 - 36 - 218, 294, 218);
            poseWindow.style.backgroundColor = UiTokens.A(UiTokens.Panel, .9f); UiTokens.Radius(poseWindow, 18); poseWindow.style.overflow = Overflow.Hidden;
            UiTokens.Border(poseWindow, 4, UiTokens.PanelEdge);
            poseImage = new Image { pickingMode = PickingMode.Ignore, scaleMode = ScaleMode.ScaleAndCrop }; UiTokens.Fill(poseImage);
            // Mirrored like a mirror.
            poseImage.style.scale = new Scale(new Vector3(-1, 1, 1));
            skeleton = new ShapeElement(DrawSkeleton); UiTokens.Fill(skeleton);
            poseWindow.Add(poseImage); poseWindow.Add(skeleton);
            frame.Add(poseWindow);

            // Middle: tracking lost (style guide 6-4).
            lost = new VisualElement { pickingMode = PickingMode.Ignore }; UiTokens.Fill(lost); lost.style.backgroundColor = UiTokens.A(UiTokens.Deep, .62f);
            lostPulse = new ShapeElement((p, r) =>
            {
                float t = Mathf.Repeat(now, 1f);
                UiIcons.Ring(p, new Vector2(960, 560), 330 + 40 * t, 18, UiTokens.A(UiTokens.Accent, .3f * (1 - t)));
            });
            UiTokens.Fill(lostPulse);
            lostDrawing = new ShapeElement((p, r) =>
            {
                UiIcons.DashedEllipse(p, new Vector2(960, 560), 250, 330, 10, UiTokens.Accent);
                UiIcons.Person(() => UiTokens.A(UiTokens.Text, .92f))(p, new Rect(960 - 200, 560 - 280, 400, 560));
            });
            UiTokens.Fill(lostDrawing);
            lost.Add(lostPulse); lost.Add(lostDrawing); lost.style.display = DisplayStyle.None;
            frame.Add(lost);

            countdown = UiTokens.Label("", 260, UiTokens.Accent, TextAnchor.MiddleCenter); UiTokens.Absolute(countdown, 660, 340, 600, 320);
            countdown.style.transformOrigin = new TransformOrigin(Length.Percent(50), Length.Percent(50));
            frame.Add(countdown);

            flash = new VisualElement { pickingMode = PickingMode.Ignore }; UiTokens.Fill(flash); flash.style.backgroundColor = UiTokens.Alarm; flash.style.opacity = 0;
            frame.Add(flash);
        }

        static void Place(VisualElement e, float x, float y, float w, float h, float deg)
        {
            e.style.left = x; e.style.top = y; e.style.width = w; e.style.height = h; e.style.right = StyleKeyword.Auto;
            e.style.transformOrigin = new TransformOrigin(Length.Percent(50), Length.Percent(50));
            e.style.rotate = new Rotate(Angle.Degrees(deg));
        }

        static Texture2D ramp;
        // Solid at the top row, fading to clear downward (the band of light along a screen edge).
        static Texture2D Ramp()
        {
            if (ramp != null) return ramp;
            ramp = new Texture2D(1, 64, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
            for (int i = 0; i < 64; i++) { float d = 1 - i / 63f; ramp.SetPixel(0, i, new Color(1, 1, 1, i >= 24 ? 1 : Mathf.Pow(i / 24f, 1.6f))); }
            ramp.Apply(); return ramp;
        }

        void SetSlot(int i, bool full)
        {
            slotFull[i] = full;
            slots[i].style.backgroundColor = full ? UiTokens.Warn : UiTokens.A(UiTokens.Deep, .55f);
            UiTokens.Border(slots[i], 4, full ? UiTokens.WarnHi : UiTokens.TextDim);
            slotIcons[i].MarkDirtyRepaint();
        }

        public void ResetRun()
        {
            shownScore = shownMeters = shownMonkeys = -1; warnings = 0; shields = 0;
            flashAt = clearAt = shieldAt = scorePopAt = scoreTintUntil = -10; slotFilledAt[0] = slotFilledAt[1] = -10;
            SetSlot(0, false); SetSlot(1, false);
            Strip.ResetRun();
        }

        // The zoo's rear-view mirror (style guide 6-2): stage 0 hidden, 1 coming in, 2 close, 3 about to bite.
        public void SetRearMirror(Texture texture, int stage)
        {
            mirrorStage = Mathf.Clamp(stage, 0, 3);
            mirrorImage.image = texture;
        }
        int mirrorStage; float mirrorSize;

        public void Render(RunSession session, IHudData hudData, RunHudModel model, float time)
        {
            now = time; data = hudData;
            Root.Layout();
            bool running = session.Flow.State == GameFlowState.Running;
            // Score: changes at once, pops 1.08x for 0.15 s; a big gain tints it for 0.3 s.
            int s = session.Score.Score;
            if (s != shownScore)
            {
                if (shownScore >= 0 && s > shownScore) { scorePopAt = now; if (s - shownScore >= 100) scoreTintUntil = now + .3f; }
                shownScore = s; score.text = UiTokens.N(s);
            }
            float pop = UiTokens.Pop((now - scorePopAt) / .15f, .08f);
            score.style.scale = new Scale(new Vector3(pop, pop, 1));
            score.style.color = now < scoreTintUntil ? UiTokens.Accent : UiTokens.Text;
            int m = Mathf.FloorToInt(session.Score.Distance);
            if (m != shownMeters) { shownMeters = m; distance.text = UiTokens.N(m) + " m"; }
            int mk = session.Score.Coins;
            if (mk != shownMonkeys) { shownMonkeys = mk; monkeys.text = UiTokens.N(mk); }

            UpdateWarnings(session);
            UpdateChase(session, running);
            UpdateEdges(session, running);
            UpdateMirror();
            UpdatePose(model);
            UiTokens.Show(lost, running && (model.ShowPoseRecovery || PreviewLost));
            if (lost.style.display == DisplayStyle.Flex) lostPulse.MarkDirtyRepaint();

            // 3, 2, 1: numbers only.
            float banner = data?.StartBanner ?? 0;
            if (running && banner > 0 && !model.ShowPoseRecovery)
            {
                int n = Mathf.CeilToInt(banner);
                countdown.text = n.ToString();
                float f = banner - Mathf.Floor(banner);
                float sc = 1 + .1f * Mathf.Clamp01((f - .8f) / .2f);
                countdown.style.scale = new Scale(new Vector3(sc, sc, 1));
                countdown.style.opacity = Mathf.Clamp01(f * 4 + .2f);
                UiTokens.Show(countdown, true);
            }
            else UiTokens.Show(countdown, false);
            flash.style.opacity = (data?.CaughtFlash ?? 0) * .45f;

            if (running && !session.Paused && data != null && !session.IsTutorial) Strip.Tick(data.Race, data.HazardNear, now);
            Strip.Render(running && data != null && !session.IsTutorial ? data.Race : null, data != null && data.HazardNear, now);
        }

        void UpdateWarnings(RunSession session)
        {
            int w = Mathf.Clamp(WarningCount(session), 0, 2);
            if (w > warnings) { for (int i = warnings; i < w; i++) slotFilledAt[i] = now; flashAt = now; flashColor = UiTokens.Danger; }
            else if (w < warnings) clearAt = now;
            warnings = w;
            for (int i = 0; i < 2; i++)
            {
                // Leaving the map empties the full slots from the left over 0.4 s.
                bool full = i < w || (now - clearAt < .4f && now - clearAt < .2f * (i + 1) && slotFull[i]);
                if (full != slotFull[i]) SetSlot(i, full);
                float t = (now - slotFilledAt[i]) / .25f;
                float sc = t >= 0 && t <= 1 ? 1 + .3f * Mathf.Sin(t * Mathf.PI) : 1;
                slots[i].style.scale = new Scale(new Vector3(sc, sc, 1));
                float b = now - slotFilledAt[i];
                UiTokens.Show(slotRings[i], b >= 0 && b < .72f && Mathf.Repeat(b, .24f) < .12f);
            }
            int saved = session.ShieldSaves;
            if (saved > shields) shieldAt = now;
            shields = saved;
            UiTokens.Show(shield, now - shieldAt < .6f);
        }

        // Warnings this map (two slots). The v2 warning rules count slips and police catches; the HUD only reads the number.
        public static int WarningCount(RunSession session) => PreviewWarnings ?? session.SlipStrikes;
        // Review captures only (DevTools): show a state the synthetic run cannot reach on its own.
        public static int? PreviewWarnings; public static bool PreviewLost; public static float? PreviewChase;

        void UpdateChase(RunSession session, bool running)
        {
            // Mine only (the zoo uses the mirror, the prison guard chase is drawn the same way once it exists).
            var plan = RouteSurface.ActivePlan;
            var kind = MapCourse.KindAt(plan, RouteSurface.SequenceAt(session.Runner.Z));
            bool mine = kind == ExerciseKind.Warmup || kind == ExerciseKind.Balance;
            UiTokens.Show(chase, running && mine);
            if (!running || !mine) return;
            float v = ChaseFraction(session);
            float shown = Mathf.Max(.12f, v);
            chaseFill.style.width = 554 * shown;
            chaseFill.style.backgroundColor = v > .7f ? UiTokens.Danger : UiTokens.Warn;
            chaseCap.style.left = Mathf.Clamp(554 * shown - 40, -8, 470);
        }
        public static float ChaseFraction(RunSession session) => PreviewChase ?? Mathf.Clamp01(session.PolicePressure);

        void UpdateEdges(RunSession session, bool running)
        {
            float a = 0; Color c = flashColor;
            float t = now - flashAt;
            // Warning: three 0.12 s flashes at 55% (never faster than 3 per second).
            if (t >= 0 && t < .72f) a = Mathf.Repeat(t, .24f) < .12f ? .55f : 0;
            else if (running && (ChaseFraction(session) >= .7f || mirrorStage >= 2))
            {
                // The chaser is close: a weak 1 s pulse, at most 24%.
                a = .24f * (.5f + .5f * Mathf.Sin(now * Mathf.PI * 2)); c = UiTokens.Danger;
            }
            for (int i = 0; i < 4; i++) { edges[i].style.opacity = a; edges[i].style.unityBackgroundImageTintColor = c; }
        }

        void UpdateMirror()
        {
            float target = mirrorStage == 0 ? 0 : mirrorStage == 1 ? 280 : mirrorStage == 2 ? 320 : 360;
            float speed = target > mirrorSize ? 360 / .25f : 360 / .4f;
            mirrorSize = Mathf.MoveTowards(mirrorSize, target, speed * Time.unscaledDeltaTime);
            UiTokens.Show(mirror, mirrorSize > 4);
            if (mirrorSize <= 4) return;
            mirror.style.left = 230 - mirrorSize / 2; mirror.style.top = 830 - mirrorSize / 2; mirror.style.width = mirrorSize; mirror.style.height = mirrorSize;
            float border = mirrorStage == 3 ? 18 : mirrorStage == 2 ? 14 : 12;
            Color col = mirrorStage == 3 ? UiTokens.Danger : mirrorStage == 2 ? Color.Lerp(UiTokens.Warn, UiTokens.Danger, .5f) : UiTokens.Warn;
            UiTokens.Border(mirrorRing, border, col);
        }

        void UpdatePose(RunHudModel model)
        {
            bool recovering = model.ShowPoseRecovery;
            bool show = model.ShowCamera && data != null && data.CameraDetected && (recovering || PosePreview.Live(data));
            UiTokens.Show(poseWindow, show);
            if (!show) return;
            poseImage.image = data.CameraTexture;
            poseImage.tintColor = recovering ? new Color(.35f, .35f, .35f, 1) : Color.white;
            UiTokens.Border(poseWindow, recovering ? 10 : 4, recovering ? UiTokens.Danger : UiTokens.PanelEdge);
            skeleton.MarkDirtyRepaint();
        }

        void DrawSkeleton(Painter2D p, Rect r)
        {
            var frame = data?.PoseFrame;
            if (frame == null || !frame.HasPose || data.PoseStatus != PoseStatus.Tracking) return;
            // Same crop as the image (ScaleAndCrop), mirrored.
            float aspect = frame.Aspect > 0 ? frame.Aspect : 4f / 3f;
            float w = r.width, h = r.height; float vw = Mathf.Max(w, h * aspect), vh = vw / aspect; if (vh < h) { vh = h; vw = h * aspect; }
            Vector2 P(PoseLandmark l) => new Vector2(r.center.x + (.5f - l.X) * vw, r.center.y + (l.Y - .5f) * vh);
            var bone = new Color(.3f, 1f, .55f);
            for (int b = 0; b < PoseJoint.Bones.GetLength(0); b++)
            {
                var a = frame.Points[PoseJoint.Bones[b, 0]]; var c = frame.Points[PoseJoint.Bones[b, 1]];
                if (a.Visibility < .5f || c.Visibility < .5f) continue;
                UiIcons.Line(p, P(a), P(c), 4, bone);
            }
        }
    }
}
