using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Mongscape.Presentation
{
    // A vector drawing element: flat filled shapes, at most two colours, no outlines (style guide 5).
    public sealed class ShapeElement : VisualElement
    {
        Action<Painter2D, Rect> draw;
        public ShapeElement(Action<Painter2D, Rect> painter)
        {
            pickingMode = PickingMode.Ignore; draw = painter;
            generateVisualContent += ctx => draw?.Invoke(ctx.painter2D, contentRect);
        }
        public void SetPainter(Action<Painter2D, Rect> painter) { draw = painter; MarkDirtyRepaint(); }
    }

    public static class UiIcons
    {
        static Angle D(float degrees) => Angle.Degrees(degrees);
        public static ShapeElement Make(float x, float y, float size, Action<Painter2D, Rect> painter)
        {
            var e = new ShapeElement(painter);
            UiTokens.Absolute(e, x, y, size, size);
            return e;
        }
        public static void Circle(Painter2D p, Vector2 c, float r, Color color)
        {
            p.fillColor = color; p.BeginPath(); p.Arc(c, r, D(0), D(360)); p.ClosePath(); p.Fill();
        }
        public static void Ellipse(Painter2D p, Vector2 c, float rx, float ry, Color color)
        {
            p.fillColor = color; p.BeginPath();
            for (int i = 0; i <= 32; i++) { float a = i / 32f * Mathf.PI * 2; var v = c + new Vector2(Mathf.Cos(a) * rx, Mathf.Sin(a) * ry); if (i == 0) p.MoveTo(v); else p.LineTo(v); }
            p.ClosePath(); p.Fill();
        }
        public static void Ring(Painter2D p, Vector2 c, float r, float width, Color color, float fromDeg = 0, float toDeg = 360)
        {
            p.strokeColor = color; p.lineWidth = width; p.lineCap = LineCap.Round; p.BeginPath(); p.Arc(c, r, D(fromDeg), D(toDeg)); p.Stroke();
        }
        public static void RoundRect(Painter2D p, Rect r, float radius, Color color)
        {
            radius = Mathf.Min(radius, r.width / 2, r.height / 2);
            p.fillColor = color; p.BeginPath();
            p.MoveTo(new Vector2(r.xMin + radius, r.yMin));
            p.ArcTo(new Vector2(r.xMax, r.yMin), new Vector2(r.xMax, r.yMax), radius);
            p.ArcTo(new Vector2(r.xMax, r.yMax), new Vector2(r.xMin, r.yMax), radius);
            p.ArcTo(new Vector2(r.xMin, r.yMax), new Vector2(r.xMin, r.yMin), radius);
            p.ArcTo(new Vector2(r.xMin, r.yMin), new Vector2(r.xMax, r.yMin), radius);
            p.ClosePath(); p.Fill();
        }
        public static void Line(Painter2D p, Vector2 a, Vector2 b, float width, Color color)
        {
            p.strokeColor = color; p.lineWidth = width; p.lineCap = LineCap.Round; p.BeginPath(); p.MoveTo(a); p.LineTo(b); p.Stroke();
        }
        public static void Polygon(Painter2D p, Color color, params Vector2[] points)
        {
            p.fillColor = color; p.BeginPath(); p.MoveTo(points[0]); for (int i = 1; i < points.Length; i++) p.LineTo(points[i]); p.ClosePath(); p.Fill();
        }

        // Handcuffs for the warning slots (style guide 5-1): two separate rings, a filled lock box on each, two chain links between.
        // The rings stay one ring diameter apart so the icon never reads as glasses or an infinity sign at 3 m.
        public static Action<Painter2D, Rect> Handcuffs(Func<Color> color) => (p, r) =>
        {
            float s = Mathf.Min(r.width, r.height); var o = new Vector2(r.center.x - s / 2, r.center.y - s / 2);
            Color c = color(); float w = s / 8f, ringR = s * .13f;
            var left = o + new Vector2(s * .21f, s * .64f); var right = o + new Vector2(s * .79f, s * .64f);
            Ring(p, left, ringR, w, c); Ring(p, right, ringR, w, c);
            float bw = s * .2f, bh = s * .15f;
            RoundRect(p, new Rect(left.x - bw / 2, left.y - ringR - bh - w * .2f, bw, bh), s * .03f, c);
            RoundRect(p, new Rect(right.x - bw / 2, right.y - ringR - bh - w * .2f, bw, bh), s * .03f, c);
            float cy = left.y - ringR - bh * .6f;
            p.strokeColor = c; p.lineWidth = w * .8f;
            p.BeginPath(); p.Arc(o + new Vector2(s * .41f, cy), s * .075f, D(0), D(360)); p.Stroke();
            p.BeginPath(); p.Arc(o + new Vector2(s * .59f, cy), s * .075f, D(0), D(360)); p.Stroke();
        };

        // The monkey everywhere (rescued monkeys, me, costs): brown head, apricot face plate, an orange prisoner belt.
        public static Action<Painter2D, Rect> Monkey(Func<Color> tint = null) => (p, r) =>
        {
            float s = Mathf.Min(r.width, r.height); var o = new Vector2(r.center.x - s / 2, r.center.y - s / 2);
            Color fur = tint?.Invoke() ?? UiTokens.MonkeyFur;
            bool flat = tint != null;
            // body with the belt
            RoundRect(p, new Rect(o.x + s * .28f, o.y + s * .62f, s * .44f, s * .36f), s * .14f, fur);
            if (!flat) RoundRect(p, new Rect(o.x + s * .28f, o.y + s * .78f, s * .44f, s * .08f), s * .02f, UiTokens.PrisonerOrange);
            // ears, head, face
            Circle(p, o + new Vector2(s * .17f, s * .36f), s * .12f, fur);
            Circle(p, o + new Vector2(s * .83f, s * .36f), s * .12f, fur);
            Circle(p, o + new Vector2(s * .5f, s * .36f), s * .3f, fur);
            if (flat) return;
            Circle(p, o + new Vector2(s * .17f, s * .36f), s * .06f, UiTokens.MonkeyFace);
            Circle(p, o + new Vector2(s * .83f, s * .36f), s * .06f, UiTokens.MonkeyFace);
            Ellipse(p, o + new Vector2(s * .5f, s * .43f), s * .2f, s * .16f, UiTokens.MonkeyFace);
            Ellipse(p, o + new Vector2(s * .41f, s * .29f), s * .09f, s * .08f, UiTokens.MonkeyFace);
            Ellipse(p, o + new Vector2(s * .59f, s * .29f), s * .09f, s * .08f, UiTokens.MonkeyFace);
            Circle(p, o + new Vector2(s * .42f, s * .3f), s * .035f, UiTokens.Panel);
            Circle(p, o + new Vector2(s * .58f, s * .3f), s * .035f, UiTokens.Panel);
        };

        // Police cap for the chase bar: navy crown with a light outline and a light-blue brim so it shows on the dark bar.
        public static Action<Painter2D, Rect> PoliceCap => (p, r) =>
        {
            float s = Mathf.Min(r.width, r.height); var o = new Vector2(r.center.x - s / 2, r.center.y - s / 2);
            var crown = new[] { o + new Vector2(s * .12f, s * .62f), o + new Vector2(s * .06f, s * .3f), o + new Vector2(s * .5f, s * .16f), o + new Vector2(s * .94f, s * .3f), o + new Vector2(s * .88f, s * .62f) };
            p.strokeColor = UiTokens.Text; p.lineWidth = s * .07f; p.lineJoin = LineJoin.Round;
            p.BeginPath(); p.MoveTo(crown[0]); for (int i = 1; i < crown.Length; i++) p.LineTo(crown[i]); p.ClosePath(); p.Stroke();
            Polygon(p, UiTokens.HatNavy, crown);
            RoundRect(p, new Rect(o.x + s * .1f, o.y + s * .6f, s * .8f, s * .2f), s * .08f, UiTokens.HatBrim);
            Circle(p, o + new Vector2(s * .5f, s * .4f), s * .08f, UiTokens.WarnHi);
        };

        // Shield shown for 0.6 s over a warning slot when a shield blocks the warning.
        public static Action<Painter2D, Rect> Shield => (p, r) =>
        {
            float s = Mathf.Min(r.width, r.height); var o = new Vector2(r.center.x - s / 2, r.center.y - s / 2);
            Polygon(p, UiTokens.Cool, o + new Vector2(s * .5f, s * .06f), o + new Vector2(s * .88f, s * .2f), o + new Vector2(s * .8f, s * .62f), o + new Vector2(s * .5f, s * .94f), o + new Vector2(s * .2f, s * .62f), o + new Vector2(s * .12f, s * .2f));
            Polygon(p, UiTokens.Text, o + new Vector2(s * .5f, s * .2f), o + new Vector2(s * .72f, s * .28f), o + new Vector2(s * .66f, s * .58f), o + new Vector2(s * .5f, s * .78f));
        };

        public static Action<Painter2D, Rect> Trophy(Color color) => (p, r) =>
        {
            float s = Mathf.Min(r.width, r.height); var o = new Vector2(r.center.x - s / 2, r.center.y - s / 2);
            Polygon(p, color, o + new Vector2(s * .22f, s * .12f), o + new Vector2(s * .78f, s * .12f), o + new Vector2(s * .7f, s * .5f), o + new Vector2(s * .5f, s * .62f), o + new Vector2(s * .3f, s * .5f));
            Ring(p, o + new Vector2(s * .22f, s * .3f), s * .12f, s * .07f, color, 90, 270);
            Ring(p, o + new Vector2(s * .78f, s * .3f), s * .12f, s * .07f, color, -90, 90);
            RoundRect(p, new Rect(o.x + s * .45f, o.y + s * .6f, s * .1f, s * .16f), 0, color);
            RoundRect(p, new Rect(o.x + s * .3f, o.y + s * .76f, s * .4f, s * .12f), s * .03f, color);
        };

        // The person to stand like on the tracking-lost screen: the calibration READY stance, facing the camera,
        // feet hip width apart and arms hanging a little away from the body.
        public static Action<Painter2D, Rect> Person(Func<Color> color) => (p, r) =>
        {
            float h = r.height, cx = r.center.x, y0 = r.yMin; Color c = color();
            float limb = h * .075f;
            Circle(p, new Vector2(cx, y0 + h * .085f), h * .075f, c);
            RoundRect(p, new Rect(cx - h * .1f, y0 + h * .18f, h * .2f, h * .36f), h * .05f, c);
            Line(p, new Vector2(cx - h * .12f, y0 + h * .22f), new Vector2(cx - h * .19f, y0 + h * .5f), limb, c);
            Line(p, new Vector2(cx + h * .12f, y0 + h * .22f), new Vector2(cx + h * .19f, y0 + h * .5f), limb, c);
            Line(p, new Vector2(cx - h * .06f, y0 + h * .52f), new Vector2(cx - h * .09f, y0 + h * .96f), limb * 1.1f, c);
            Line(p, new Vector2(cx + h * .06f, y0 + h * .52f), new Vector2(cx + h * .09f, y0 + h * .96f), limb * 1.1f, c);
        };

        // Dashed ellipse (where to stand).
        public static void DashedEllipse(Painter2D p, Vector2 c, float rx, float ry, float width, Color color, int dashes = 28)
        {
            p.strokeColor = color; p.lineWidth = width; p.lineCap = LineCap.Round;
            for (int i = 0; i < dashes; i++)
            {
                float a0 = i / (float)dashes * Mathf.PI * 2, a1 = (i + .55f) / dashes * Mathf.PI * 2;
                p.BeginPath();
                for (int k = 0; k <= 4; k++) { float a = Mathf.Lerp(a0, a1, k / 4f); var v = c + new Vector2(Mathf.Cos(a) * rx, Mathf.Sin(a) * ry); if (k == 0) p.MoveTo(v); else p.LineTo(v); }
                p.Stroke();
            }
        }

        // Pause sign (two bars) for the X-arms progress circle.
        public static Action<Painter2D, Rect> PauseBars(Color color) => (p, r) =>
        {
            float s = Mathf.Min(r.width, r.height); var o = new Vector2(r.center.x - s / 2, r.center.y - s / 2);
            RoundRect(p, new Rect(o.x + s * .3f, o.y + s * .25f, s * .14f, s * .5f), s * .03f, color);
            RoundRect(p, new Rect(o.x + s * .56f, o.y + s * .25f, s * .14f, s * .5f), s * .03f, color);
        };
    }
}
