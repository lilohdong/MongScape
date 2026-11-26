using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Mongscape.Presentation
{
    // The pause screen in UI Toolkit (plan 6-7a, style guide mockup 06): a pause sign, three big buttons
    // (계속 primary, 보정 다시, 메인) and a band that is not a button: X arms or Esc = pause, drawn as shapes.
    public sealed class PauseView
    {
        public readonly UiRoot Root;
        public readonly GestureButtons Gestures;
        public Action Resume, Recalibrate, Main;
        UiButton resume, again, main;
        public PauseView(GameObject host)
        {
            Root = new UiRoot(host, "Pause", 7);
            var frame = Root.Frame;
            Gestures = new GestureButtons(frame);
            var dim = new VisualElement { pickingMode = PickingMode.Ignore }; UiTokens.Fill(dim); dim.style.backgroundColor = UiTokens.Dim(UiTokens.Deep, .7f);
            frame.Add(dim);
            frame.Add(UiIcons.Make(960 - 70, 140, 140, UiIcons.PauseBars(UiTokens.Accent)));
            resume = Gestures.Add(new UiButton("계속", true, () => Resume?.Invoke(), 64, Play_Icon));
            again = Gestures.Add(new UiButton("보정 다시", false, () => Recalibrate?.Invoke(), 64, Camera_Icon));
            main = Gestures.Add(new UiButton("메인", false, () => Main?.Invoke(), 64, Home_Icon));
            Place(true);
            frame.Add(resume); frame.Add(again); frame.Add(main);
            // Not a button: no border, deep 50%.
            var band = UiTokens.Box(600, 790, 720, 180); band.style.backgroundColor = UiTokens.Dim(UiTokens.Deep, .7f); UiTokens.Radius(band, 36);
            band.Add(UiIcons.Make(50, 10, 160, (p, r) =>
            {
                UiIcons.Person(() => UiTokens.TextDim)(p, r);
                UiIcons.Line(p, new Vector2(r.x + r.width * .12f, r.y + r.height * .2f), new Vector2(r.xMax - r.width * .12f, r.y + r.height * .55f), r.width * .07f, UiTokens.Accent);
                UiIcons.Line(p, new Vector2(r.xMax - r.width * .12f, r.y + r.height * .2f), new Vector2(r.x + r.width * .12f, r.y + r.height * .55f), r.width * .07f, UiTokens.Accent);
            }));
            var esc = UiTokens.Label("Esc", 64, UiTokens.TextDim, TextAnchor.MiddleCenter); UiTokens.Absolute(esc, 250, 45, 140, 90);
            UiTokens.Border(esc, 4, UiTokens.TextDim); UiTokens.Radius(esc, 12); band.Add(esc);
            band.Add(UiIcons.Make(430, 50, 80, (p, r) =>
            {
                UiIcons.RoundRect(p, new Rect(r.x, r.y + r.height * .22f, r.width, r.height * .18f), r.height * .09f, UiTokens.TextDim);
                UiIcons.RoundRect(p, new Rect(r.x, r.y + r.height * .6f, r.width, r.height * .18f), r.height * .09f, UiTokens.TextDim);
            }));
            band.Add(UiIcons.Make(560, 40, 100, UiIcons.PauseBars(UiTokens.Accent)));
            frame.Add(band);
            Root.Visible = false;
        }
        bool withCalibrate;
        // 보정 다시 only for camera runs; a keyboard run gets two buttons.
        public void Place(bool calibrate)
        {
            if (calibrate == withCalibrate && resume.style.width.value.value > 0) return;
            withCalibrate = calibrate;
            UiTokens.Show(again, calibrate);
            if (calibrate) { UiTokens.Absolute(resume, 150, 470, 480, 200); UiTokens.Absolute(again, 720, 470, 480, 200); UiTokens.Absolute(main, 1290, 470, 480, 200); }
            else { UiTokens.Absolute(resume, 960 - 45 - 520, 470, 520, 200); UiTokens.Absolute(main, 960 + 45, 470, 520, 200); }
        }
        public static void Play_Icon(Painter2D p, Rect r) => UiIcons.Polygon(p, UiTokens.Panel, new Vector2(r.x + r.width * .25f, r.y + r.height * .18f), new Vector2(r.xMax - r.width * .15f, r.center.y), new Vector2(r.x + r.width * .25f, r.yMax - r.height * .18f));
        public static void Camera_Icon(Painter2D p, Rect r)
        {
            UiIcons.RoundRect(p, new Rect(r.x + r.width * .06f, r.y + r.height * .28f, r.width * .6f, r.height * .44f), r.width * .06f, UiTokens.Text);
            UiIcons.Polygon(p, UiTokens.Text, new Vector2(r.x + r.width * .68f, r.center.y), new Vector2(r.xMax - r.width * .04f, r.y + r.height * .28f), new Vector2(r.xMax - r.width * .04f, r.y + r.height * .72f));
        }
        public static void Home_Icon(Painter2D p, Rect r)
        {
            UiIcons.Polygon(p, UiTokens.Text, new Vector2(r.center.x, r.y + r.height * .1f), new Vector2(r.xMax - r.width * .06f, r.y + r.height * .5f), new Vector2(r.x + r.width * .06f, r.y + r.height * .5f));
            UiIcons.RoundRect(p, new Rect(r.x + r.width * .66f, r.y + r.height * .14f, r.width * .1f, r.height * .22f), 0, UiTokens.Text);
            UiIcons.RoundRect(p, new Rect(r.x + r.width * .18f, r.y + r.height * .48f, r.width * .64f, r.height * .42f), 0, UiTokens.Text);
            UiIcons.RoundRect(p, new Rect(r.x + r.width * .42f, r.y + r.height * .62f, r.width * .16f, r.height * .28f), 0, UiTokens.Panel);
        }
    }
}
