using System;
using Mongscape.Domain;
using UnityEngine;
using UnityEngine.UIElements;

namespace Mongscape.Presentation
{
    // Help: "운동은 이렇게 계산해요" (plan 5-4, 5-5; mockup 16): the formula, the intensity rules, every activity with its
    // MET and 2024 Adult Compendium code (IntensityMet), the WHO goal and the sources.
    public sealed class HelpView
    {
        public readonly UiRoot Root;
        public readonly GestureButtons Gestures;
        public Action Back;
        public static readonly MetActivity[] Rows =
        {
            MetActivity.MineGeneral, MetActivity.JogInPlace, MetActivity.Vigorous, MetActivity.RhythmMissed, MetActivity.StairSlow,
            MetActivity.StairFast, MetActivity.HideDeep, MetActivity.HideShallow, MetActivity.UpperGeneral, MetActivity.UpperPull,
        };
        public static string Label(MetActivity a)
        {
            switch (a)
            {
                case MetActivity.MineGeneral: return "광산 달리기와 몸동작";
                case MetActivity.JogInPlace: return "제자리 달리기 (분당 100~150걸음)";
                case MetActivity.Vigorous: return "빠른 제자리 달리기, 동물원 리듬";
                case MetActivity.RhythmMissed: return "동물원 (박자를 자주 놓칠 때)";
                case MetActivity.StairSlow: return "감옥 계단 (천천히)";
                case MetActivity.StairFast: return "감옥 계단 (빠르게, 간수 추격)";
                case MetActivity.HideDeep: return "깊게 숙여 숨기";
                case MetActivity.HideShallow: return "얕게 숙여 숨기";
                case MetActivity.UpperGeneral: return "상체 팔 흔들기";
                case MetActivity.UpperPull: return "상체 숙이기와 당겨 내리기";
                default: return "가만히 서 있기";
            }
        }
        public static readonly string[] Lines =
        {
            "칼로리 = MET × 3.5 × 몸무게(kg) ÷ 200 × 분",
            "3 MET 이상은 중강도, 6 MET 이상은 고강도 (고강도 1분은 2분으로 세요)",
            "주간 목표: 중강도 이상 주 150분 (WHO 권장)",
            "웹캠으로 추정한 값이라 실제와 다를 수 있어요",
        };
        public const string Sources = "출처: 2024 Adult Compendium of Physical Activities, WHO 신체활동 지침(2020)";

        public HelpView(GameObject host)
        {
            Root = new UiRoot(host, "Help", 8);
            var frame = Root.Frame;
            Gestures = new GestureButtons(frame);
            var bg = new VisualElement(); UiTokens.Fill(bg); bg.style.backgroundColor = UiTokens.Dim(UiTokens.Deep, .85f); frame.Add(bg);
            var title = UiTokens.Label("운동은 이렇게 계산해요", 88, UiTokens.Accent); UiTokens.Absolute(title, 80, 40, 1400, 120); frame.Add(title);
            var back = Gestures.Add(new UiButton("뒤로", false, () => Back?.Invoke())); UiTokens.Absolute(back, 1920 - 80 - 300, 40, 300, 120); frame.Add(back);
            for (int i = 0; i < Lines.Length; i++) { var l = UiTokens.Label(Lines[i], 44, UiTokens.Text); UiTokens.Absolute(l, 80, 180 + i * 58, 1760, 56); frame.Add(l); }
            var panel = UiTokens.Box(80, 430, 1760, 560); panel.style.backgroundColor = UiTokens.A(UiTokens.Panel, .9f); UiTokens.Radius(panel, 28); UiTokens.Border(panel, 5, UiTokens.PanelEdge);
            frame.Add(panel);
            Row(panel, 0, "하는 것", "MET", "Compendium 코드", UiTokens.Accent);
            for (int i = 0; i < Rows.Length; i++)
            {
                var a = Rows[i];
                Row(panel, i + 1, Label(a), IntensityMet.Of(a).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture), IntensityMet.Code(a), i % 2 == 0 ? UiTokens.Text : UiTokens.TextDim);
            }
            var src = UiTokens.Label(Sources, 36, UiTokens.TextDim); UiTokens.Absolute(src, 80, 1006, 1760, 50); frame.Add(src);
            Root.Visible = false;
        }
        static void Row(VisualElement panel, int i, string what, string met, string code, Color color)
        {
            float y = 14 + i * 48;
            var a = UiTokens.Label(what, 38, color); UiTokens.Absolute(a, 40, y, 1100, 48);
            var b = UiTokens.Label(met, 38, color, TextAnchor.MiddleRight); UiTokens.Absolute(b, 1140, y, 160, 48);
            var c = UiTokens.Label(code, 38, color, TextAnchor.MiddleRight); UiTokens.Absolute(c, 1360, y, 360, 48);
            panel.Add(a); panel.Add(b); panel.Add(c);
        }
    }
}
