using System;
using System.Collections.Generic;
using Mongscape.Domain;
using UnityEngine;
using UnityEngine.UIElements;

namespace Mongscape.Presentation
{
    // Icons for the menu buttons (style guide 5: flat, two colours at most).
    public static class MenuIcons
    {
        public static Action<Painter2D, Rect> Play(Color c) => (p, r) => UiIcons.Polygon(p, c, new Vector2(r.x + r.width * .22f, r.y + r.height * .14f), new Vector2(r.xMax - r.width * .12f, r.center.y), new Vector2(r.x + r.width * .22f, r.yMax - r.height * .14f));
        public static Action<Painter2D, Rect> Book(Color c) => (p, r) =>
        {
            UiIcons.Polygon(p, c, new Vector2(r.x + r.width * .04f, r.y + r.height * .2f), new Vector2(r.center.x - r.width * .03f, r.y + r.height * .27f), new Vector2(r.center.x - r.width * .03f, r.yMax - r.height * .14f), new Vector2(r.x + r.width * .04f, r.yMax - r.height * .2f));
            UiIcons.Polygon(p, c, new Vector2(r.xMax - r.width * .04f, r.y + r.height * .2f), new Vector2(r.center.x + r.width * .03f, r.y + r.height * .27f), new Vector2(r.center.x + r.width * .03f, r.yMax - r.height * .14f), new Vector2(r.xMax - r.width * .04f, r.yMax - r.height * .2f));
        };
        public static Action<Painter2D, Rect> Traits(Color c) => (p, r) =>
        {
            var root = new Vector2(r.center.x, r.yMax - r.height * .06f); var fork = new Vector2(r.center.x, r.y + r.height * .5f);
            UiIcons.Line(p, root, fork, r.width * .09f, c);
            foreach (float dx in new[] { -.3f, 0f, .3f })
            {
                var tip = new Vector2(r.center.x + r.width * dx, r.y + r.height * .22f);
                UiIcons.Line(p, fork, tip, r.width * .07f, c); UiIcons.Circle(p, tip, r.width * .13f, c);
            }
        };
        public static Action<Painter2D, Rect> Person(Color c) => (p, r) =>
        {
            UiIcons.Circle(p, new Vector2(r.center.x, r.y + r.height * .32f), r.width * .2f, c);
            p.fillColor = c; p.BeginPath(); p.Arc(new Vector2(r.center.x, r.yMax - r.height * .08f), r.width * .38f, Angle.Degrees(180), Angle.Degrees(360)); p.ClosePath(); p.Fill();
        };
        public static Action<Painter2D, Rect> Question(Color c) => (p, r) =>
        {
            UiIcons.Circle(p, r.center, r.width * .42f, c);
            UiIcons.Ring(p, r.center + new Vector2(0, -r.height * .1f), r.width * .14f, r.width * .08f, UiTokens.Panel, 180, 420);
            UiIcons.Line(p, r.center + new Vector2(0, r.height * .04f), r.center + new Vector2(0, r.height * .12f), r.width * .08f, UiTokens.Panel);
            UiIcons.Circle(p, r.center + new Vector2(0, r.height * .25f), r.width * .05f, UiTokens.Panel);
        };
        public static Action<Painter2D, Rect> Gear(Color c) => (p, r) =>
        {
            for (int i = 0; i < 8; i++)
            {
                float a = i / 8f * Mathf.PI * 2; var d = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                UiIcons.Line(p, r.center + d * r.width * .2f, r.center + d * r.width * .4f, r.width * .15f, c);
            }
            UiIcons.Circle(p, r.center, r.width * .3f, c);
            UiIcons.Circle(p, r.center, r.width * .12f, UiTokens.Panel);
        };
        public static Action<Painter2D, Rect> Door(Color c) => (p, r) =>
        {
            UiIcons.RoundRect(p, new Rect(r.x + r.width * .2f, r.y + r.height * .1f, r.width * .45f, r.height * .8f), r.width * .04f, c);
            UiIcons.Circle(p, new Vector2(r.x + r.width * .55f, r.center.y), r.width * .05f, UiTokens.Panel);
            UiIcons.Line(p, new Vector2(r.x + r.width * .7f, r.center.y), new Vector2(r.xMax - r.width * .06f, r.center.y), r.width * .08f, c);
            UiIcons.Polygon(p, c, new Vector2(r.xMax, r.center.y), new Vector2(r.xMax - r.width * .16f, r.center.y - r.height * .13f), new Vector2(r.xMax - r.width * .16f, r.center.y + r.height * .13f));
        };
        public static Action<Painter2D, Rect> Back(Color c) => (p, r) =>
        {
            p.strokeColor = c; p.lineWidth = r.width * .12f; p.lineCap = LineCap.Round; p.lineJoin = LineJoin.Round;
            p.BeginPath(); p.MoveTo(new Vector2(r.x + r.width * .62f, r.y + r.height * .2f)); p.LineTo(new Vector2(r.x + r.width * .32f, r.center.y)); p.LineTo(new Vector2(r.x + r.width * .62f, r.yMax - r.height * .2f)); p.Stroke();
        };
    }

    // A square icon button with its word under it (main menu corner, style guide 4: 160 x 160, 72 apart).
    public sealed class IconButton : VisualElement
    {
        public readonly UiButton Button; readonly Label caption;
        public IconButton(string text, Action<Painter2D, Rect> icon, Action pressed)
        {
            pickingMode = PickingMode.Ignore;
            Button = new UiButton("", false, pressed); Button.name = "button-" + text; Button.Text.style.display = DisplayStyle.None;
            UiTokens.Absolute(Button, 0, 0, 160, 160);
            var i = new ShapeElement(icon); i.style.width = 92; i.style.height = 92; Button.Add(i);
            caption = UiTokens.Label(text, 44, UiTokens.Text, TextAnchor.MiddleCenter); UiTokens.Absolute(caption, -40, 172, 240, 60);
            Add(Button); Add(caption);
        }
        public void SetText(string text) => caption.text = text;
    }

    // Main menu (mockup 11): logo, four big buttons on the left (시작 primary, 튜토리얼, 특성, 랭킹), the corner icons
    // (프로필 or 로그인, 도움말, 설정, 나가기) and a card with the monkeys and this week's minutes (not a button).
    public sealed class MainMenuView
    {
        public readonly UiRoot Root; public readonly GestureButtons Gestures;
        public Action<MenuAction> Select;
        readonly Image logo; readonly IconButton account; readonly Label player, monkeys, weekValue, build; readonly VisualElement weekRow, weekFill;
        bool member;
        public MainMenuView(GameObject host)
        {
            Root = new UiRoot(host, "MainMenu", 4); var frame = Root.Frame; Gestures = new GestureButtons(frame);
            var dim = new VisualElement { pickingMode = PickingMode.Ignore }; UiTokens.Fill(dim); dim.style.backgroundColor = UiTokens.Dim(UiTokens.Deep, .35f); frame.Add(dim);
            logo = new Image { pickingMode = PickingMode.Ignore, scaleMode = ScaleMode.ScaleToFit }; UiTokens.Absolute(logo, 100, 30, 470, 220); frame.Add(logo);
            string[] labels = { "시작", "튜토리얼", "특성", "랭킹" };
            MenuAction[] actions = { MenuAction.Play, MenuAction.Tutorial, MenuAction.Traits, MenuAction.Ranking };
            for (int i = 0; i < 4; i++)
            {
                var a = actions[i]; bool primary = i == 0; Color ic = primary ? UiTokens.Panel : UiTokens.Text;
                Action<Painter2D, Rect> icon = i == 0 ? MenuIcons.Play(ic) : i == 1 ? MenuIcons.Book(ic) : i == 2 ? MenuIcons.Traits(ic) : UiIcons.Trophy(ic);
                var b = Gestures.Add(new UiButton(labels[i], primary, () => Select?.Invoke(a), 72, icon));
                UiTokens.Absolute(b, 110, 290 + i * 202, 590, 130); frame.Add(b);
            }
            account = new IconButton("프로필", MenuIcons.Person(UiTokens.Text), () => Select?.Invoke(member ? MenuAction.Profile : MenuAction.Identity));
            var help = new IconButton("도움말", MenuIcons.Question(UiTokens.Text), () => Select?.Invoke(MenuAction.Help));
            var settings = new IconButton("설정", MenuIcons.Gear(UiTokens.Text), () => Select?.Invoke(MenuAction.Settings));
            var quit = new IconButton("나가기", MenuIcons.Door(UiTokens.Text), () => Select?.Invoke(MenuAction.Quit));
            var icons = new[] { account, help, settings, quit };
            for (int i = 0; i < icons.Length; i++) { UiTokens.Absolute(icons[i], 1000 + i * 232, 60, 160, 160); Gestures.Add(icons[i].Button); frame.Add(icons[i]); }
            var card = UiTokens.Box(1290, 760, 550, 220); card.style.backgroundColor = UiTokens.A(UiTokens.Panel, .94f); UiTokens.Radius(card, 32); UiTokens.Border(card, 5, UiTokens.PanelEdge); frame.Add(card);
            card.Add(UiIcons.Make(26, 22, 84, UiIcons.Monkey()));
            monkeys = UiTokens.Label("", 72, UiTokens.Text); UiTokens.Absolute(monkeys, 126, 16, 200, 96); card.Add(monkeys);
            player = UiTokens.Label("", 44, UiTokens.TextDim, TextAnchor.MiddleRight); UiTokens.Absolute(player, 300, 30, 220, 64); card.Add(player);
            weekRow = UiTokens.Box(0, 110, 550, 110); card.Add(weekRow);
            var weekTitle = UiTokens.Label("이번 주", 44, UiTokens.TextDim); UiTokens.Absolute(weekTitle, 36, 0, 160, 60); weekRow.Add(weekTitle);
            var track = UiTokens.Box(190, 12, 320, 40); track.style.backgroundColor = UiTokens.Deep; UiTokens.Radius(track, 20); track.style.overflow = Overflow.Hidden; weekRow.Add(track);
            weekFill = UiTokens.Box(0, 0, 0, 40); weekFill.style.backgroundColor = UiTokens.Focus; UiTokens.Radius(weekFill, 20); track.Add(weekFill);
            weekValue = UiTokens.Label("", 44, UiTokens.Text, TextAnchor.MiddleRight); UiTokens.Absolute(weekValue, 190, 52, 320, 56); weekRow.Add(weekValue);
            build = UiTokens.Label(BuildInfo.Label, 36, UiTokens.TextDim, TextAnchor.MiddleRight, true); UiTokens.Absolute(build, 1290, 1000, 550, 50); frame.Add(build);
            Root.Visible = false;
        }
        public void Refresh(IHudData data, Texture2D titleLogo)
        {
            logo.image = titleLogo;
            member = data is IIdentityData id && id.SignedIn;
            account.SetText(member ? "프로필" : "로그인");
            player.text = MainMenuScreen.PlayerLabel(data);
            monkeys.text = UiTokens.N(data?.Monkeys ?? 0);
            UiTokens.Show(weekRow, member);
            double week = data?.WeekMinutes ?? 0;
            weekFill.style.width = 320 * Mathf.Clamp01((float)(week / ExerciseLog.WeeklyGoalMinutes));
            weekValue.text = WorkoutReport.WeekGoalValue(week);
        }
    }

    // Play style choice (mockup 12, owner: no calorie goal): three cards, the run-speed switch for camera styles,
    // 시작 and 뒤로. A card is chosen by pressing it; the camera styles are locked without a camera.
    public sealed class PlayModeView
    {
        public readonly UiRoot Root; public readonly GestureButtons Gestures;
        public Action<PlayStyle> Choose; public Action Start, Back; public Action<bool> SetCadence;
        readonly UiButton[] cards = new UiButton[3]; readonly Label[] names = new Label[3], subs = new Label[3]; readonly ShapeElement[] icons = new ShapeElement[3];
        readonly VisualElement speedRow, knob, track; readonly Label speedState, noCamera, keyHint;
        static readonly string[] Names = { "전신", "상체", "키보드" }, Subs = { "서서 온몸으로", "앉아서 팔로", "운동 없이" };
        PlayStyle style; bool bodyOk = true, cadence = true;
        public PlayModeView(GameObject host)
        {
            Root = new UiRoot(host, "PlayMode", 4); var frame = Root.Frame; Gestures = new GestureButtons(frame);
            var dim = new VisualElement { pickingMode = PickingMode.Ignore }; UiTokens.Fill(dim); dim.style.backgroundColor = UiTokens.Dim(UiTokens.Deep, .75f); frame.Add(dim);
            var title = UiTokens.Label("어떻게 할까요?", 88, UiTokens.Accent, TextAnchor.MiddleCenter); UiTokens.Absolute(title, 0, 30, 1920, 120); frame.Add(title);
            for (int i = 0; i < 3; i++)
            {
                var s = (PlayStyle)i; int k = i;
                var card = new UiButton("", false, () => { if (PlayStyleInfo.Allowed(s, bodyOk)) Choose?.Invoke(s); }); card.Text.style.display = DisplayStyle.None;
                card.style.flexDirection = FlexDirection.Column; UiTokens.Absolute(card, 120 + i * 590, 180, 500, 420);
                icons[i] = new ShapeElement((p, r) => ResultView.ModeIconColored(p, r, s, CardInk(k)));
                UiTokens.Absolute(icons[i], 150, 40, 200, 220);
                names[i] = UiTokens.Label(Names[i], 76, UiTokens.Text, TextAnchor.MiddleCenter); UiTokens.Absolute(names[i], 0, 255, 490, 90);
                subs[i] = UiTokens.Label(Subs[i], 44, UiTokens.TextDim, TextAnchor.MiddleCenter); UiTokens.Absolute(subs[i], 0, 340, 490, 60);
                card.Add(icons[i]); card.Add(names[i]); card.Add(subs[i]);
                card.Look = hover => PaintCard(k, hover);
                cards[i] = Gestures.Add(card); frame.Add(card);
            }
            noCamera = UiTokens.Label("카메라를 연결하면 전신과 상체를 고를 수 있어요", 44, UiTokens.TextDim, TextAnchor.MiddleCenter); UiTokens.Absolute(noCamera, 0, 615, 1920, 60); frame.Add(noCamera);
            var panel = UiTokens.Box(520, 690, 1310, 170); panel.style.backgroundColor = UiTokens.A(UiTokens.Panel, .94f); UiTokens.Radius(panel, 32); UiTokens.Border(panel, 5, UiTokens.PanelEdge); frame.Add(panel);
            speedRow = UiTokens.Box(0, 0, 1310, 170); panel.Add(speedRow);
            var speedLabel = UiTokens.Label("달리기 속도 조절", 52, UiTokens.Text); UiTokens.Absolute(speedLabel, 50, 50, 460, 70); speedRow.Add(speedLabel);
            var toggle = Gestures.Add(new UiButton("", false, () => SetCadence?.Invoke(!cadence))); toggle.Text.style.display = DisplayStyle.None; toggle.name = "button-cadence";
            toggle.Look = hover => { toggle.style.backgroundColor = Color.clear; UiTokens.Border(toggle, hover ? 8 : 0, UiTokens.Focus); };
            UiTokens.Absolute(toggle, 480, 25, 300, 120); speedRow.Add(toggle);
            track = UiTokens.Box(10, 10, 280, 100); UiTokens.Radius(track, 50); toggle.Add(track);
            knob = UiTokens.Box(0, 8, 84, 84); UiTokens.Radius(knob, 42); knob.style.backgroundColor = UiTokens.Text; track.Add(knob);
            speedState = UiTokens.Label("", 56, UiTokens.Accent); UiTokens.Absolute(speedState, 820, 45, 300, 80); speedRow.Add(speedState);
            keyHint = UiTokens.Label("A D 옆으로    Space 점프    S 숙이기    W 무릎", 44, UiTokens.TextDim, TextAnchor.MiddleCenter); UiTokens.Absolute(keyHint, 0, 50, 1310, 70); panel.Add(keyHint);
            var back = Gestures.Add(new UiButton("뒤로", false, () => Back?.Invoke(), 64, MenuIcons.Back(UiTokens.Text))); UiTokens.Absolute(back, 90, 900, 300, 120); frame.Add(back);
            var go = Gestures.Add(new UiButton("시작", true, () => Start?.Invoke(), 72, MenuIcons.Play(UiTokens.Panel))); UiTokens.Absolute(go, 1830 - 420, 900, 420, 130); frame.Add(go);
            Root.Visible = false;
        }
        Color CardInk(int i) => i == (int)style ? UiTokens.Panel : PlayStyleInfo.Allowed((PlayStyle)i, bodyOk) ? UiTokens.Text : UiTokens.A(UiTokens.TextDim, .5f);
        void PaintCard(int i, bool hover)
        {
            bool chosen = i == (int)style, allowed = PlayStyleInfo.Allowed((PlayStyle)i, bodyOk);
            var card = cards[i];
            card.style.backgroundColor = chosen ? UiTokens.Accent : allowed ? UiTokens.A(UiTokens.Panel, .94f) : UiTokens.Iron;
            UiTokens.Border(card, hover && allowed ? 12 : 5, hover && allowed ? UiTokens.Focus : chosen ? UiTokens.WarnHi : UiTokens.PanelEdge);
            names[i].style.color = chosen ? UiTokens.Panel : allowed ? UiTokens.Text : UiTokens.TextDim;
            subs[i].style.color = chosen ? UiTokens.Panel : UiTokens.TextDim;
            if (chosen) { UiTokens.NoShadow(names[i]); UiTokens.NoShadow(subs[i]); } else { UiTokens.Shadow(names[i], 76); UiTokens.Shadow(subs[i], 44); }
            icons[i].MarkDirtyRepaint();
        }
        public void Refresh(PlayStyle current, bool bodyStyles, bool cadenceOn)
        {
            style = current; bodyOk = bodyStyles; cadence = cadenceOn;
            for (int i = 0; i < 3; i++) cards[i].Paint();
            UiTokens.Show(noCamera, !bodyOk);
            bool camera = PlayStyleInfo.UsesCamera(style);
            UiTokens.Show(speedRow, camera); UiTokens.Show(keyHint, !camera);
            track.style.backgroundColor = cadence ? UiTokens.Focus : UiTokens.Iron;
            knob.style.left = cadence ? 188 : 8;
            speedState.text = cadence ? "켜짐" : "꺼짐"; speedState.style.color = cadence ? UiTokens.Accent : UiTokens.TextDim;
        }
    }

    // Main menu ranking (plan 6-5): 오늘 and 전체 tabs, the three play styles, the board with my row in accent.
    public sealed class RankingView
    {
        public readonly UiRoot Root; public readonly GestureButtons Gestures;
        public Action Back; public Action<bool, PlayStyle> Pick;
        readonly UiButton[] periods = new UiButton[2], modes = new UiButton[3];
        readonly VisualElement rows; readonly Label status;
        const float RowPitch = 76, RowH = 68;
        int shownCount = -1; RankingStatus shownStatus; bool shownToday; PlayStyle shownMode; RankingRow shownMine;
        public RankingView(GameObject host)
        {
            Root = new UiRoot(host, "Ranking", 4); var frame = Root.Frame; Gestures = new GestureButtons(frame);
            var dim = new VisualElement { pickingMode = PickingMode.Ignore }; UiTokens.Fill(dim); dim.style.backgroundColor = UiTokens.Dim(UiTokens.Deep, .8f); frame.Add(dim);
            var title = UiTokens.Label("랭킹", 88, UiTokens.Accent); UiTokens.Absolute(title, 90, 30, 400, 120); frame.Add(title);
            string[] p = { "오늘", "전체" };
            for (int i = 0; i < 2; i++) { bool today = i == 0; var b = new UiButton(p[i], false, () => Pick?.Invoke(today, shownMode), 56); UiTokens.Absolute(b, 500 + i * 330, 30, 300, 120); periods[i] = Gestures.Add(b); frame.Add(b); }
            for (int i = 0; i < 3; i++)
            {
                var s = (PlayStyle)i; var b = new UiButton(PlayStyleInfo.Label(s), false, () => Pick?.Invoke(shownToday, s), 56);
                UiTokens.Absolute(b, 1830 - 3 * 300 - 2 * 30 + i * 330, 30, 300, 120); modes[i] = Gestures.Add(b); frame.Add(b);
            }
            var panel = UiTokens.Box(90, 190, 1740, 680); panel.style.backgroundColor = UiTokens.A(UiTokens.Panel, .96f); UiTokens.Radius(panel, 32); UiTokens.Border(panel, 5, UiTokens.PanelEdge); panel.style.overflow = Overflow.Hidden; frame.Add(panel);
            rows = UiTokens.Box(18, 18, 1704, 644); panel.Add(rows);
            status = UiTokens.Label("", 52, UiTokens.TextDim, TextAnchor.MiddleCenter); UiTokens.Absolute(status, 0, 260, 1704, 80); rows.Add(status);
            var back = Gestures.Add(new UiButton("뒤로", false, () => Back?.Invoke(), 64, MenuIcons.Back(UiTokens.Text))); UiTokens.Absolute(back, 90, 920, 300, 120); frame.Add(back);
            Root.Visible = false;
        }
        static void Tab(UiButton b, bool on)
        {
            b.Look = hover =>
            {
                b.style.backgroundColor = on ? UiTokens.Accent : UiTokens.A(UiTokens.Panel, .94f);
                UiTokens.Border(b, hover ? 12 : 5, hover ? UiTokens.Focus : on ? UiTokens.WarnHi : UiTokens.PanelEdge);
                b.Text.style.color = on ? UiTokens.Panel : UiTokens.Text;
            };
            b.Paint();
        }
        public void Refresh(RankingBoard board)
        {
            if (board == null) return;
            if (board.Rows.Count == shownCount && board.Status == shownStatus && board.TodayOnly == shownToday && board.Mode == shownMode && board.Mine == shownMine) return;
            shownCount = board.Rows.Count; shownStatus = board.Status; shownToday = board.TodayOnly; shownMode = board.Mode; shownMine = board.Mine;
            Tab(periods[0], shownToday); Tab(periods[1], !shownToday);
            for (int i = 0; i < 3; i++) Tab(modes[i], (int)shownMode == i);
            rows.Clear(); rows.Add(status);
            status.text = board.StatusText.Length > 0 ? board.StatusText : board.Rows.Count == 0 ? "아직 기록이 없어요" : "";
            UiTokens.Show(status, board.Rows.Count == 0);
            int fit = Mathf.FloorToInt(644 / RowPitch);
            int mineIndex = board.Mine != null ? board.Rows.IndexOf(board.Mine) : -1;
            bool pinMine = board.Mine != null && (mineIndex < 0 || mineIndex >= fit);
            int count = Mathf.Min(board.Rows.Count, pinMine ? fit - 2 : fit);
            for (int i = 0; i < count; i++) rows.Add(Row(board.Rows[i], i * RowPitch, i == mineIndex, i));
            if (pinMine)
            {
                var dots = UiTokens.Label("…", 52, UiTokens.TextDim, TextAnchor.MiddleCenter); UiTokens.Absolute(dots, 0, count * RowPitch, 1704, RowH); rows.Add(dots);
                rows.Add(Row(board.Mine, (count + 1) * RowPitch, true, 0));
            }
        }
        static VisualElement Row(RankingRow r, float y, bool mine, int i)
        {
            var row = UiTokens.Box(0, y, 1704, RowH); UiTokens.Radius(row, 18);
            row.style.backgroundColor = mine ? UiTokens.Accent : i % 2 == 0 ? UiTokens.A(UiTokens.Deep, .55f) : UiTokens.A(UiTokens.Panel, .4f);
            row.style.flexDirection = FlexDirection.Row; row.style.alignItems = Align.Center; row.style.paddingLeft = 40; row.style.paddingRight = 40;
            Color main = mine ? UiTokens.Panel : UiTokens.Text, dim = mine ? UiTokens.Panel : UiTokens.TextDim;
            var rank = UiTokens.Label(r.Rank.ToString(), 52, dim, TextAnchor.MiddleLeft, !mine); rank.style.width = 140;
            var name = UiTokens.Label(r.Name, 52, main, TextAnchor.MiddleLeft, !mine); name.style.flexGrow = 1; name.style.overflow = Overflow.Hidden; name.style.textOverflow = TextOverflow.Ellipsis;
            var score = UiTokens.Label(UiTokens.N(r.Score), 52, main, TextAnchor.MiddleRight, !mine); score.style.width = 360;
            var dist = UiTokens.Label(UiTokens.N(Mathf.FloorToInt(r.Distance)) + " m", 44, dim, TextAnchor.MiddleRight, !mine); dist.style.width = 300;
            row.Add(rank); row.Add(name); row.Add(score); row.Add(dist);
            return row;
        }
    }
}
