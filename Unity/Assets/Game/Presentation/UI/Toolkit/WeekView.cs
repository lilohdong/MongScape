using System;
using Mongscape.Domain;
using UnityEngine;
using UnityEngine.UIElements;

namespace Mongscape.Presentation
{
    // The profile's week card (plan 5-4, mockup 15): this week's goal minutes against 150, a bar per weekday (today in
    // cool), and where the player moved (zoo, prison, mine). Members only.
    public sealed class WeekView
    {
        public readonly UiRoot Root;
        public readonly GestureButtons Gestures;
        public Action Back;
        readonly Label total, upper;
        readonly VisualElement bigFill;
        readonly VisualElement[] dayFill = new VisualElement[7];
        readonly Label[] dayValue = new Label[7], dayName = new Label[7];
        readonly VisualElement[] mapFill = new VisualElement[3];
        readonly Label[] mapValue = new Label[3];
        static readonly string[] Days = { "월", "화", "수", "목", "금", "토", "일" };
        const float BarH = 220, BigW = 1580, MapW = 360;

        public WeekView(GameObject host)
        {
            Root = new UiRoot(host, "Week", 8);
            var frame = Root.Frame;
            Gestures = new GestureButtons(frame);
            var bg = new VisualElement(); UiTokens.Fill(bg); bg.style.backgroundColor = UiTokens.Dim(UiTokens.Deep, .85f); frame.Add(bg);
            var title = UiTokens.Label("이번 주 운동", 88, UiTokens.Accent); UiTokens.Absolute(title, 110, 30, 1200, 120); frame.Add(title);
            var panel = UiTokens.Box(110, 190, 1700, 710); panel.style.backgroundColor = UiTokens.A(UiTokens.Panel, .96f); UiTokens.Radius(panel, 36); UiTokens.Border(panel, 5, UiTokens.PanelEdge);
            frame.Add(panel);
            total = UiTokens.Label("", 120, UiTokens.Text); UiTokens.Absolute(total, 60, 30, 900, 150); panel.Add(total);
            var who = UiTokens.Label("WHO 권장", 44, UiTokens.TextDim, TextAnchor.MiddleRight); UiTokens.Absolute(who, 900, 60, 740, 60); panel.Add(who);
            var moderate = UiTokens.Label("중강도 = 숨이 조금 찰 정도", 40, UiTokens.TextDim, TextAnchor.MiddleRight); UiTokens.Absolute(moderate, 900, 120, 740, 56); panel.Add(moderate);
            upper = UiTokens.Label("상체 기준", 36, UiTokens.TextDim); UiTokens.Absolute(upper, 60, 170, 400, 40); panel.Add(upper);
            var big = UiTokens.Box(60, 210, BigW, 70); big.style.backgroundColor = UiTokens.Deep; UiTokens.Radius(big, 35); big.style.overflow = Overflow.Hidden; panel.Add(big);
            bigFill = UiTokens.Box(0, 0, 0, 70); bigFill.style.backgroundColor = UiTokens.Focus; UiTokens.Radius(bigFill, 35); big.Add(bigFill);
            for (int i = 0; i < 7; i++)
            {
                float x = 90 + i * 130;
                var track = UiTokens.Box(x, 410, 70, BarH); track.style.backgroundColor = UiTokens.Deep; UiTokens.Radius(track, 20); track.style.overflow = Overflow.Hidden; panel.Add(track);
                dayFill[i] = UiTokens.Box(0, BarH, 70, 0); UiTokens.Radius(dayFill[i], 20); track.Add(dayFill[i]);
                dayValue[i] = UiTokens.Label("", 40, UiTokens.Text, TextAnchor.MiddleCenter); UiTokens.Absolute(dayValue[i], x - 30, 350, 130, 56); panel.Add(dayValue[i]);
                dayName[i] = UiTokens.Label(Days[i], 44, UiTokens.TextDim, TextAnchor.MiddleCenter); UiTokens.Absolute(dayName[i], x - 30, 640, 130, 60); panel.Add(dayName[i]);
            }
            var where = UiTokens.Label("어디서 움직였나", 44, UiTokens.TextDim); UiTokens.Absolute(where, 1070, 340, 600, 60); panel.Add(where);
            Action<Painter2D, Rect>[] icons = { MapIcons.Lion, MapIcons.Bars, MapIcons.Pickaxe };
            for (int i = 0; i < 3; i++)
            {
                float y = 420 + i * 90;
                panel.Add(UiIcons.Make(1070, y - 4, 70, icons[i]));
                var track = UiTokens.Box(1160, y + 12, MapW, 40); track.style.backgroundColor = UiTokens.Deep; UiTokens.Radius(track, 20); track.style.overflow = Overflow.Hidden; panel.Add(track);
                mapFill[i] = UiTokens.Box(0, 0, 0, 40); mapFill[i].style.backgroundColor = UiTokens.Focus; UiTokens.Radius(mapFill[i], 20); track.Add(mapFill[i]);
                mapValue[i] = UiTokens.Label("", 48, UiTokens.Text, TextAnchor.MiddleRight); UiTokens.Absolute(mapValue[i], 1520, y, 120, 64); panel.Add(mapValue[i]);
            }
            var back = Gestures.Add(new UiButton("뒤로", false, () => Back?.Invoke())); UiTokens.Absolute(back, 110, 930, 300, 120); frame.Add(back);
            Root.Visible = false;
        }

        // days: goal minutes Monday first; maps: zoo, prison, mine; today: 0 = Monday.
        public void Show(double week, double[] days, double[] maps, int today, bool upperBody)
        {
            total.text = Math.Floor(Math.Max(0, week)) + "분  / " + ExerciseLog.WeeklyGoalMinutes + "분";
            UiTokens.Show(upper, upperBody);
            bigFill.style.width = BigW * Mathf.Clamp01((float)(week / ExerciseLog.WeeklyGoalMinutes));
            double maxDay = 30; if (days != null) foreach (var d in days) maxDay = Math.Max(maxDay, d);
            for (int i = 0; i < 7; i++)
            {
                double v = days != null && i < days.Length ? days[i] : 0;
                float h = BarH * Mathf.Clamp01((float)(v / maxDay));
                dayFill[i].style.top = BarH - h; dayFill[i].style.height = h;
                dayFill[i].style.backgroundColor = i == today ? UiTokens.Cool : UiTokens.Focus;
                dayValue[i].text = v >= 1 ? Math.Floor(v).ToString() : "";
                dayValue[i].style.top = 350 + (BarH - h) - 4;
                dayName[i].style.color = i == today ? UiTokens.Accent : UiTokens.TextDim;
            }
            double maxMap = 1; if (maps != null) foreach (var m in maps) maxMap = Math.Max(maxMap, m);
            for (int i = 0; i < 3; i++)
            {
                double v = maps != null && i < maps.Length ? maps[i] : 0;
                mapFill[i].style.width = MapW * Mathf.Clamp01((float)(v / maxMap));
                mapValue[i].text = Math.Floor(v) + "분";
            }
        }
    }

    // Map icons that match the fork signs (style guide 5): zoo = lion face, prison = barred window, mine = pickaxe.
    public static class MapIcons
    {
        public static void Lion(Painter2D p, Rect r)
        {
            var c = r.center; float s = Mathf.Min(r.width, r.height);
            for (int i = 0; i < 10; i++) { float a = i / 10f * Mathf.PI * 2; UiIcons.Circle(p, c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * s * .32f, s * .15f, UiTokens.Accent); }
            UiIcons.Circle(p, c, s * .3f, UiTokens.Accent);
            UiIcons.Circle(p, c, s * .24f, UiTokens.Hex(0xC9822F));
            UiIcons.Ellipse(p, c + new Vector2(0, s * .08f), s * .12f, s * .08f, UiTokens.Hex(0xF2D3A2));
            UiIcons.Circle(p, c + new Vector2(-s * .09f, -s * .05f), s * .03f, UiTokens.Panel);
            UiIcons.Circle(p, c + new Vector2(s * .09f, -s * .05f), s * .03f, UiTokens.Panel);
        }
        public static void Bars(Painter2D p, Rect r)
        {
            float s = Mathf.Min(r.width, r.height); var o = new Vector2(r.center.x - s / 2, r.center.y - s / 2);
            p.strokeColor = UiTokens.Accent; p.lineWidth = s / 8f; p.lineJoin = LineJoin.Round;
            p.BeginPath(); p.MoveTo(o + new Vector2(s * .15f, s * .1f)); p.LineTo(o + new Vector2(s * .85f, s * .1f)); p.LineTo(o + new Vector2(s * .85f, s * .9f)); p.LineTo(o + new Vector2(s * .15f, s * .9f)); p.ClosePath(); p.Stroke();
            for (int i = 1; i < 4; i++) UiIcons.Line(p, o + new Vector2(s * (.15f + i * .175f), s * .12f), o + new Vector2(s * (.15f + i * .175f), s * .88f), s / 10f, UiTokens.Accent);
        }
        public static void Pickaxe(Painter2D p, Rect r)
        {
            float s = Mathf.Min(r.width, r.height); var o = new Vector2(r.center.x - s / 2, r.center.y - s / 2);
            UiIcons.Line(p, o + new Vector2(s * .15f, s * .88f), o + new Vector2(s * .7f, s * .33f), s / 8f, UiTokens.Accent);
            UiIcons.Ring(p, o + new Vector2(s * .38f, s * .55f), s * .45f, s / 8f, UiTokens.Accent, -100, -10);
        }
    }
}
