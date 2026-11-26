using System;
using System.Globalization;
using Mongscape.Domain;
using Mongscape.Presentation.Map;
using UnityEngine;

namespace Mongscape.Presentation
{
    internal sealed class PauseScreen
    {
        private static readonly CultureInfo Num = CultureInfo.InvariantCulture;
        private readonly HudSkin skin;
        public PauseScreen(HudSkin hudSkin) { skin = hudSkin; }
        // Leaving to the menu ends the run without a record.
        public void Draw(RunSession session, IHudData data, Action resume, Action recalibrate, Action toMenu)
        {
            var flow = data?.Pause;
            // Count-in (3, 2, 1, or the rhythm beats): only the number. Out of view: only the silhouette (no text, plan 6-7a).
            if (flow != null && flow.Counting) { DrawCount(flow.CountdownNumber); return; }
            if (flow != null && flow.ShowSilhouette) { DrawSilhouette(); return; }
            skin.Card(new Rect(0, 0, 1280, 720), Palette.A(Palette.Ground, .75f));
            skin.Panel(new Rect(285, 140, 710, 440), .97f);
            GUI.color = Color.white; GUI.Label(new Rect(325, 165, 620, 60), "잠깐 쉬어가기", skin.Style(34, FontStyle.Bold));
            // Label and value columns, one fact per row, left of the buttons at x 710.
            int row = 0;
            Row(ref row, "점수", RunHudModel.Points(session.Score.Score), true);
            Row(ref row, "시간", RunHudModel.Clock(session.Elapsed));
            Row(ref row, "거리", session.Score.Distance.ToString("N0", Num) + " m");
            // A member's best for this play style; guests have none.
            int best = data?.BestScore ?? -1;
            if (best >= 0) Row(ref row, "최고", RunHudModel.Points(Mathf.Max(best, session.Score.Score)));
            Row(ref row, "원숭이", session.Troop.Kept.ToString(Num));
            Row(ref row, "위치", RouteSurface.AreaAt(session.Runner.Z));
            bool workout = PlayStyleInfo.UsesCamera(data?.Player?.Style ?? PlayStyle.Keyboard);
            Row(ref row, "칼로리", workout ? session.Workout.EstimatedKcal.ToString("F1", Num) + " / " + session.Workout.GoalKcal.ToString("F0", Num) + " kcal" : RunHudModel.KeyboardKcal);
            if (workout) Row(ref row, WorkoutReport.BigMovesLabel, WorkoutReport.BigMoves(session));
            if (skin.PrimaryButton(new Rect(710, 247, 240, 58), "계속하기", 26)) resume?.Invoke();
            bool camera = PlayStyleInfo.UsesCamera(data?.Player?.Style ?? PlayStyle.Keyboard);
            if (camera && skin.FlatButton(new Rect(710, 325, 240, 48), "보정 다시", false)) recalibrate?.Invoke();
            if (skin.FlatButton(new Rect(710, 391, 240, 48), "메인으로", false)) toMenu?.Invoke();
            GUI.color = new Color(1, 1, 1, .75f);
            GUI.Label(new Rect(325, 534, 625, 30), "메인으로 나가면 이번 플레이 기록은 저장하지 않습니다.", skin.Style(17, FontStyle.Normal));
            GUI.color = Color.white;
        }
        private void DrawCount(int number)
        {
            skin.Card(new Rect(0, 0, 1280, 720), Palette.A(Palette.Ground, .35f));
            GUI.color = Color.white; GUI.Label(new Rect(440, 220, 400, 260), number.ToString(Num), skin.Centered(150, FontStyle.Bold)); GUI.color = Color.white;
        }
        // A person shape in the middle: step back into view.
        private void DrawSilhouette()
        {
            skin.Card(new Rect(0, 0, 1280, 720), Palette.A(Palette.Ground, .75f));
            GUI.color = Palette.A(Color.white, .85f);
            Dot(new Vector2(640, 250), 46);
            GUI.DrawTexture(new Rect(590, 305, 100, 150), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(552, 312, 34, 120), Texture2D.whiteTexture); GUI.DrawTexture(new Rect(694, 312, 34, 120), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(596, 460, 38, 120), Texture2D.whiteTexture); GUI.DrawTexture(new Rect(646, 460, 38, 120), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }
        // While the X is held: a ring of dots fills clockwise around the centre top of the screen.
        public void DrawGesture(PauseFlow flow)
        {
            if (flow == null || flow.GestureProgress <= 0) return;
            const int dots = 24; int lit = Mathf.CeilToInt(flow.GestureProgress * dots);
            for (int i = 0; i < dots; i++)
            {
                float a = i / (float)dots * Mathf.PI * 2;
                GUI.color = i < lit ? Color.white : Palette.A(Color.white, .25f);
                Dot(new Vector2(640 + Mathf.Sin(a) * 34, 120 - Mathf.Cos(a) * 34), 5);
            }
            GUI.color = Color.white;
        }
        private static void Dot(Vector2 centre, float radius)
        {
            // A rounded blob from three overlapping rectangles (IMGUI has no circle primitive).
            float r = radius, s = r * .7f;
            GUI.DrawTexture(new Rect(centre.x - s, centre.y - r, s * 2, r * 2), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(centre.x - r, centre.y - s, r * 2, s * 2), Texture2D.whiteTexture);
        }
        private void Row(ref int row, string label, string value, bool large = false)
        {
            float y = 238 + row++ * 34;
            GUI.color = Palette.Sand; GUI.Label(new Rect(325, y, 110, 32), label, skin.Style(18, FontStyle.Normal));
            GUI.color = Color.white; GUI.Label(new Rect(440, y - (large ? 5 : 0), 250, 36), value, skin.Style(large ? 26 : 20, large ? FontStyle.Bold : FontStyle.Normal));
        }
    }
}
