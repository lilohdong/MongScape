using Mongscape.Domain;
using UnityEngine;

namespace Mongscape.Presentation
{
    public enum TutorialChoice { None, Start, Skip }

    // The tutorial chapter screen (plan 6-1b, 6-7): three cards (locked, open, done), the play style row and one way out.
    // Opened by the main menu's "튜토리얼", after sign-up, and once before "게임 시작" ("튜토리얼 할까요?", can be skipped).
    // Members who finished the earlier tutorial see the short basics offer instead. A card starts that chapter in the picked style.
    internal sealed class TutorialOfferScreen
    {
        public enum Mode { AfterSignUp, BeforePlay, FromMenu, Short }
        readonly OverlaySkin skin;
        PlayStyle? picked;
        public TutorialOfferScreen(OverlaySkin overlaySkin) { skin = overlaySkin; }
        // A fresh visit starts on the player's own style.
        public void Open() { picked = null; }

        // mask: finished chapters. bodyOk: a camera is available for the full and upper body styles.
        public TutorialChoice Draw(Mode mode, int mask, bool bodyOk, PlayStyle current, out PlayStyle style, out TutorialChapter chapter)
        {
            style = PlayStyleInfo.Playable(picked ?? current, bodyOk); chapter = TutorialChapter.Basics;
            var oldMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.identity;
            skin.Fill(new Rect(0, 0, Screen.width, Screen.height), Palette.A(Palette.Ground, 0.6f));
            GUI.matrix = oldMatrix;

            var panel = new Rect(60, 44, 1160, 620);
            skin.BorderBox(panel, Palette.A(Palette.Panel, 0.96f), Palette.Wood, 3);
            string title = mode == Mode.AfterSignUp ? "가입을 환영합니다" : mode == Mode.BeforePlay ? "튜토리얼 할까요?" : mode == Mode.Short ? "바뀐 것만 해 볼까요?" : "튜토리얼";
            skin.Text(new Rect(100, 64, 1080, 62), title, 44, Palette.Gold, TextAnchor.MiddleCenter);

            var choice = TutorialChoice.None;
            if (mode == Mode.Short)
            {
                skin.Text(new Rect(100, 160, 1080, 40), "보정  구출  바나나", 28, Palette.Cream, TextAnchor.MiddleCenter, false, true);
                if (skin.Button(new Rect(400, 250, 480, 84), "해 보기", Palette.Gold, Color.clear, Palette.Outline, 34)) choice = TutorialChoice.Start;
            }
            else
            {
                for (int i = 0; i < 3; i++)
                {
                    var option = (TutorialChapter)i;
                    var card = new Rect(100 + i * 370, 150, 340, 290);
                    if (Card(card, option, mask, i + 1)) { choice = TutorialChoice.Start; chapter = option; }
                }
            }
            // Play style row: the picked one is lit.
            for (int i = 0; i < 3; i++)
            {
                var option = (PlayStyle)i;
                bool allowed = PlayStyleInfo.Allowed(option, bodyOk), on = option == style;
                var r = new Rect(240 + i * 270, 470, 250, 56);
                bool was = GUI.enabled; GUI.enabled = was && allowed;
                if (skin.Button(r, PlayStyleInfo.Label(option), on ? Palette.Gold : Palette.A(Palette.Ground, .9f), on ? Color.clear : Palette.Wood, on ? Palette.Outline : allowed ? Palette.Cream : Palette.Muted, 24)) picked = option;
                GUI.enabled = was;
            }
            string skip = mode == Mode.AfterSignUp ? "나중에 하기" : mode == Mode.BeforePlay ? "건너뛰기" : mode == Mode.Short ? "건너뛰기" : "돌아가기";
            if (skin.Button(new Rect(440, 560, 400, 62), skip, Palette.A(Palette.Ground, 0.92f), Palette.Wood, Palette.Cream, 26)) choice = TutorialChoice.Skip;
            // Esc skips; the number of an open card starts it.
            var e = Event.current;
            if (e.type == EventType.KeyDown)
            {
                int key = e.keyCode >= KeyCode.Alpha1 && e.keyCode <= KeyCode.Alpha3 ? e.keyCode - KeyCode.Alpha1 : e.keyCode >= KeyCode.Keypad1 && e.keyCode <= KeyCode.Keypad3 ? e.keyCode - KeyCode.Keypad1 : -1;
                if (mode != Mode.Short && key >= 0 && TutorialProgress.IsOpen(mask, (TutorialChapter)key)) { e.Use(); choice = TutorialChoice.Start; chapter = (TutorialChapter)key; }
                else if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter) { e.Use(); choice = TutorialChoice.Start; chapter = TutorialChapter.Basics; }
                else if (e.keyCode == KeyCode.Escape) { e.Use(); choice = TutorialChoice.Skip; }
            }
            return choice;
        }

        // One chapter card: the number, the title, how long it takes and its state. Locked cards do not react.
        bool Card(Rect r, TutorialChapter chapter, int mask, int number)
        {
            bool open = TutorialProgress.IsOpen(mask, chapter), done = TutorialProgress.IsDone(mask, chapter);
            var border = done ? new Color(.45f, 1f, .6f) : open ? Palette.Gold : Palette.Wood;
            bool was = GUI.enabled; GUI.enabled = was && open;
            bool pressed = skin.Button(r, "", Palette.A(open ? Palette.Ground : Palette.Dark, .95f), border, Palette.Cream, 20);
            GUI.enabled = was;
            var dim = open ? Palette.Cream : Palette.Muted;
            skin.Text(new Rect(r.x, r.y + 18, r.width, 96), number.ToString(), 82, open ? Palette.Gold : Palette.Muted, TextAnchor.MiddleCenter);
            skin.Text(new Rect(r.x + 10, r.y + 138, r.width - 20, 50), TutorialScript.ChapterTitle(chapter), 34, dim, TextAnchor.MiddleCenter, false, true);
            var badge = new Rect(r.x + 90, r.y + 224, r.width - 180, 44);
            skin.BorderBox(badge, done ? new Color(.2f, .5f, .3f, .9f) : open ? Palette.A(Palette.Gold, .9f) : Palette.A(Palette.Track, .9f), Color.clear, 0);
            skin.Text(badge, TutorialProgress.State(mask, chapter), 24, done ? Palette.Cream : open ? Palette.Outline : Palette.Muted, TextAnchor.MiddleCenter, false, true);
            return pressed && open;
        }
    }
}
