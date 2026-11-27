using System;
using System.Collections.Generic;
using Mongscape.Domain;
using UnityEngine;
using UnityEngine.UIElements;

namespace Mongscape.Presentation
{
    // The result screen in UI Toolkit (plan 6-5, style guide 8 and 9).
    //   0 - 1.5 s: the score alone in the middle, counting up (1.0 s, fast then slow), a 1.1x pop, "신기록" when beaten.
    //   then: the score rises to the head (rank and score apart, no separator), today's ranking on the left with my row
    //   rising from the bottom into its place (centred by scrolling, pinned under "…" when out of the list), this run on
    //   the right (distance, calories with the food line and (i), the weekly bar or a guest's minutes, 더보기),
    //   and three buttons: 다시 하기 (primary), 보정, 메인. The two panels share their top and bottom lines.
    public sealed class ResultView
    {
        public const float ScoreSeconds = 1.5f, CountSeconds = 1f, RiseSeconds = .6f;
        const float PanelTop = 282, PanelBottom = 860, LeftX = 60, LeftW = 1120, RightX = 1240, RightW = 620;
        const float RowPitch = 76, RowH = 68, MineH = 100;
        public readonly UiRoot Root;
        public readonly GestureButtons Gestures;
        readonly VisualElement frame, dim, glow, head, table, viewport, content, rightPanel, more, info, weekBar, weekOld, weekAdded;
        readonly Label bigScore, headRank, headScore, newBest, tableTitle, modeName, distanceValue, kcalValue, kcalNote, weekTitle, weekValue, weekPlus, guestMinutes, upperNote, statusText;
        readonly VisualElement kcalBox, kcalInfo, weekBox;
        readonly FoodLine food;
        readonly UiButton retry, calibrate, menu, moreButton, moreClose, infoClose;
        readonly VisualElement mineRow; readonly Label mineRank, mineName, mineScore, mineDistance;
        readonly List<VisualElement> ghosts = new List<VisualElement>();
        // The ranking rows in list order with their top inside the scrolled content (partly visible rows are hidden).
        readonly List<KeyValuePair<VisualElement, float>> tableRows = new List<KeyValuePair<VisualElement, float>>();
        readonly VisualElement moreBody;
        RankingBoard board; IHudData data; RunSession session;
        ShapeElement modeIcon; PlayStyle modeStyle; Label keyboardNote; UiButton kcalButtonRef;
        float openedAt, now; int finalScore; bool member, camera;
        int shownRowsVersion = -1, rowsCount = -1; RankingStatus shownStatus;
        public Action Retry, Calibrate, Main;
        public bool Open { get; private set; }
        public bool MoreOpen => more.style.display == DisplayStyle.Flex;
        public bool InfoOpen => info.style.display == DisplayStyle.Flex;
        public bool ShowingRanking => Open && now - openedAt >= ScoreSeconds;
        // For tests and the review capture: what the screen shows.
        public string FoodText => food.Shown;
        public string HeadRankText => headRank.text;

        public ResultView(GameObject host)
        {
            Root = new UiRoot(host, "Result", 6);
            frame = Root.Frame;
            Gestures = new GestureButtons(frame);
            dim = new VisualElement { pickingMode = PickingMode.Ignore }; UiTokens.Fill(dim); dim.style.backgroundColor = UiTokens.Dim(UiTokens.Deep, .72f);
            frame.Add(dim);
            // A soft round glow (one radial gradient texture) behind the count-up.
            glow = UiTokens.Box(960 - 520, 540 - 520, 1040, 1040);
            glow.style.backgroundImage = RadialTexture(); glow.style.unityBackgroundImageTintColor = UiTokens.A(UiTokens.Focus, .45f);
            frame.Add(glow);

            // Head: rank (accent) and score (text), placed apart.
            head = UiTokens.Box(0, 36, 1920, 150);
            head.style.flexDirection = FlexDirection.Row; head.style.justifyContent = Justify.Center; head.style.alignItems = Align.Center;
            headRank = UiTokens.Label("", 120, UiTokens.Accent); headRank.style.marginRight = 200;
            headScore = UiTokens.Label("", 120, UiTokens.Text);
            head.Add(headRank); head.Add(headScore); frame.Add(head);
            bigScore = UiTokens.Label("", 260, UiTokens.Text, TextAnchor.MiddleCenter); UiTokens.Absolute(bigScore, 0, 380, 1920, 320);
            bigScore.style.transformOrigin = new TransformOrigin(Length.Percent(50), Length.Percent(50));
            frame.Add(bigScore);
            newBest = UiTokens.Label("신기록", 64, UiTokens.Panel, TextAnchor.MiddleCenter, false); UiTokens.Absolute(newBest, 960 - 170, 250, 340, 110);
            newBest.style.backgroundColor = UiTokens.Warn; UiTokens.Radius(newBest, 55); UiTokens.Border(newBest, 5, UiTokens.WarnHi);
            frame.Add(newBest);

            // Left: today's ranking.
            table = UiTokens.Box(0, 0, 1920, 1080);
            tableTitle = UiTokens.Label("오늘 랭킹", 52, UiTokens.TextDim); UiTokens.Absolute(tableTitle, LeftX + 20, 196, 300, 70);
            modeIcon = UiIcons.Make(LeftX + 300, 200, 64, (p, r) => ModeIcon(p, r, modeStyle));
            var person = modeIcon;
            modeName = UiTokens.Label("", 52, UiTokens.TextDim); UiTokens.Absolute(modeName, LeftX + 372, 196, 300, 70);
            table.Add(tableTitle); table.Add(person); table.Add(modeName);
            var leftPanel = Panel(LeftX, PanelTop, LeftW, PanelBottom - PanelTop);
            viewport = UiTokens.Box(18, 18, LeftW - 36, PanelBottom - PanelTop - 36); viewport.style.overflow = Overflow.Hidden;
            content = UiTokens.Box(0, 0, LeftW - 36, 10);
            viewport.Add(content); leftPanel.Add(viewport); table.Add(leftPanel);
            statusText = UiTokens.Label("", 52, UiTokens.TextDim, TextAnchor.MiddleCenter); UiTokens.Absolute(statusText, 0, 200, LeftW - 36, 80);
            viewport.Add(statusText);
            mineRow = UiTokens.Box(LeftX + 8, 0, LeftW - 16, MineH);
            mineRow.style.backgroundColor = UiTokens.Accent; UiTokens.Radius(mineRow, 24);
            mineRank = UiTokens.Label("", 64, UiTokens.Panel, TextAnchor.MiddleLeft, false);
            mineName = UiTokens.Label("나", 60, UiTokens.Panel, TextAnchor.MiddleLeft, false);
            mineScore = UiTokens.Label("", 64, UiTokens.Panel, TextAnchor.MiddleRight, false);
            mineDistance = UiTokens.Label("", 52, UiTokens.Panel, TextAnchor.MiddleRight, false);
            RowLayout(mineRow, mineRank, mineName, mineScore, mineDistance, MineH, UiIcons.Monkey());
            for (int i = 0; i < 3; i++) { var g = UiTokens.Box(LeftX + 8, 0, LeftW - 16, MineH); g.style.backgroundColor = UiTokens.A(UiTokens.Accent, .18f - i * .05f); UiTokens.Radius(g, 24); ghosts.Add(g); table.Add(g); }
            table.Add(mineRow);

            // Right: this run.
            rightPanel = Panel(RightX, PanelTop, RightW, PanelBottom - PanelTop);
            var distLabel = UiTokens.Label("거리", 44, UiTokens.TextDim); UiTokens.Absolute(distLabel, 36, 26, 200, 80);
            distanceValue = UiTokens.Label("", 72, UiTokens.Text, TextAnchor.MiddleRight); UiTokens.Absolute(distanceValue, 200, 22, RightW - 236, 90);
            rightPanel.Add(distLabel); rightPanel.Add(distanceValue);
            // The calorie box is one button: it opens (i).
            kcalBox = UiTokens.Box(22, 116, RightW - 44, 210); UiTokens.Radius(kcalBox, 28); UiTokens.Border(kcalBox, 5, UiTokens.PanelEdge);
            kcalBox.style.backgroundColor = UiTokens.A(UiTokens.Panel, .6f);
            var kcalLabel = UiTokens.Label("칼로리", 44, UiTokens.TextDim); UiTokens.Absolute(kcalLabel, 26, 18, 180, 60);
            kcalNote = UiTokens.Label("추정", 36, UiTokens.TextDim); UiTokens.Absolute(kcalNote, 26, 74, 180, 50);
            kcalValue = UiTokens.Label("", 72, UiTokens.Text, TextAnchor.MiddleRight); UiTokens.Absolute(kcalValue, 150, 12, RightW - 44 - 150 - 104, 96);
            kcalInfo = UiIcons.Make(RightW - 44 - 96, 22, 76, (p, r) =>
            {
                UiIcons.Ring(p, r.center, r.width * .44f, r.width * .08f, UiTokens.Accent);
                UiIcons.Circle(p, r.center + new Vector2(0, -r.height * .22f), r.width * .07f, UiTokens.Accent);
                UiIcons.Line(p, r.center + new Vector2(0, -r.height * .06f), r.center + new Vector2(0, r.height * .24f), r.width * .12f, UiTokens.Accent);
            });
            food = new FoodLine(); UiTokens.Absolute(food, 150, 112, RightW - 44 - 170, 84); food.style.justifyContent = Justify.FlexEnd;
            kcalBox.Add(kcalLabel); kcalBox.Add(kcalNote); kcalBox.Add(kcalValue); kcalBox.Add(kcalInfo); kcalBox.Add(food);
            keyboardNote = UiTokens.Label("키보드 판은 운동 기록이 없어요", 44, UiTokens.TextDim); UiTokens.Absolute(keyboardNote, 36, 130, RightW - 72, 70);
            rightPanel.Add(keyboardNote);
            var kcalButton = new UiButton("", false, () => ShowInfo(true)); kcalButtonRef = kcalButton; UiTokens.Absolute(kcalButton, 22, 116, RightW - 44, 210);
            kcalButton.style.backgroundColor = Color.clear; UiTokens.Border(kcalButton, 0, Color.clear); kcalButton.style.opacity = .01f; kcalButton.name = "button-kcal";
            rightPanel.Add(kcalBox); rightPanel.Add(kcalButton);
            Gestures.Add(kcalButton);
            // This week (members): the minutes already done in focus, this run's in cool. A guest sees this run's minutes.
            weekBox = UiTokens.Box(36, 350, RightW - 72, 220);
            weekTitle = UiTokens.Label("이번 주 운동", 44, UiTokens.TextDim); UiTokens.Absolute(weekTitle, 0, 0, 360, 60);
            upperNote = UiTokens.Label("상체 기준", 36, UiTokens.TextDim, TextAnchor.MiddleRight); UiTokens.Absolute(upperNote, 300, 4, RightW - 372, 56);
            weekBar = UiTokens.Box(0, 70, RightW - 72 - 150, 48); weekBar.style.backgroundColor = UiTokens.Deep; UiTokens.Radius(weekBar, 24); weekBar.style.overflow = Overflow.Hidden;
            weekOld = UiTokens.Box(0, 0, 0, 48); weekOld.style.backgroundColor = UiTokens.Focus;
            weekAdded = UiTokens.Box(0, 0, 0, 48); weekAdded.style.backgroundColor = UiTokens.Cool;
            weekBar.Add(weekAdded); weekBar.Add(weekOld);
            weekPlus = UiTokens.Label("", 44, UiTokens.Cool, TextAnchor.MiddleRight); UiTokens.Absolute(weekPlus, RightW - 72 - 150, 64, 150, 60);
            weekValue = UiTokens.Label("", 52, UiTokens.Text); UiTokens.Absolute(weekValue, 0, 134, 400, 70);
            guestMinutes = UiTokens.Label("", 52, UiTokens.Text); UiTokens.Absolute(guestMinutes, 0, 40, RightW - 72, 80);
            weekBox.Add(weekTitle); weekBox.Add(upperNote); weekBox.Add(weekBar); weekBox.Add(weekPlus); weekBox.Add(weekValue); weekBox.Add(guestMinutes);
            rightPanel.Add(weekBox);
            moreButton = Gestures.Add(new UiButton("더보기", false, () => ShowMore(true), 56)); UiTokens.Absolute(moreButton, RightW - 22 - 300, PanelBottom - PanelTop - 22 - 120, 300, 120);
            rightPanel.Add(moreButton);
            table.Add(rightPanel);

            // Buttons: 다시 하기 (primary), 보정, 메인, aligned to the panels' outer edges.
            retry = Gestures.Add(new UiButton("다시 하기", true, () => Retry?.Invoke(), 64, Retry_Icon));
            calibrate = Gestures.Add(new UiButton("보정", false, () => Calibrate?.Invoke(), 64, Camera_Icon));
            menu = Gestures.Add(new UiButton("메인", false, () => Main?.Invoke(), 64, Home_Icon));
            table.Add(retry); table.Add(calibrate); table.Add(menu);
            frame.Add(table);

            more = Overlay("더보기", out moreBody, out moreClose, "접기", () => ShowMore(false));
            info = Overlay("칼로리는 이렇게 세요", out var infoBody, out infoClose, "닫기", () => ShowInfo(false));
            string[] lines =
            {
                "몸동작을 매초 읽어 운동 강도(MET)를 정해요",
                "칼로리 = MET × 3.5 × 몸무게(kg) ÷ 200 × 분",
                "3 MET 이상 움직인 시간이 주간 목표에 쌓여요",
                "웹캠으로 추정한 값이라 실제와 다를 수 있어요",
                "자세한 건 도움말에",
            };
            // A page to read at arm's length: smaller type than the buttons (title 52, text 34, airy lines).
            ((Label)info[0][0]).style.fontSize = 52; infoBody.style.top = 150;
            foreach (var line in lines) { var l = UiTokens.Label(line, 34, UiTokens.Text); l.style.marginBottom = 8; l.style.height = 48; infoBody.Add(l); }
            frame.Add(more); frame.Add(info);
            Gestures.Add(moreClose); Gestures.Add(infoClose);
            Root.Visible = false;
        }

        static Texture2D radial;
        static Texture2D RadialTexture()
        {
            if (radial != null) return radial;
            const int n = 128; radial = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
            for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
            {
                float d = Vector2.Distance(new Vector2(x + .5f, y + .5f), new Vector2(n / 2f, n / 2f)) / (n / 2f);
                float a = Mathf.Clamp01(1 - d); a = a * a * (3 - 2 * a);
                radial.SetPixel(x, y, new Color(1, 1, 1, a));
            }
            radial.Apply(); return radial;
        }
        // Play style icons (style guide 5): full body = standing person, upper body = seated with raised arms, keyboard.
        public static void ModeIcon(Painter2D p, Rect r, PlayStyle style) => ModeIconColored(p, r, style, UiTokens.TextDim);
        public static void ModeIconColored(Painter2D p, Rect r, PlayStyle style, Color c)
        {
            if (style == PlayStyle.Keyboard)
            {
                UiIcons.RoundRect(p, new Rect(r.x, r.y + r.height * .28f, r.width, r.height * .48f), r.width * .08f, c);
                for (int row = 0; row < 2; row++) for (int k = 0; k < 5; k++)
                    UiIcons.RoundRect(p, new Rect(r.x + r.width * (.08f + k * .175f), r.y + r.height * (.35f + row * .14f), r.width * .12f, r.height * .09f), 2, c == UiTokens.Panel ? UiTokens.Accent : UiTokens.Panel);
                UiIcons.RoundRect(p, new Rect(r.x + r.width * .25f, r.y + r.height * .63f, r.width * .5f, r.height * .07f), 2, c == UiTokens.Panel ? UiTokens.Accent : UiTokens.Panel);
                return;
            }
            if (style == PlayStyle.UpperBody)
            {
                UiIcons.Circle(p, new Vector2(r.center.x, r.y + r.height * .2f), r.height * .11f, c);
                UiIcons.RoundRect(p, new Rect(r.center.x - r.width * .13f, r.y + r.height * .34f, r.width * .26f, r.height * .34f), 4, c);
                UiIcons.Line(p, new Vector2(r.center.x - r.width * .13f, r.y + r.height * .38f), new Vector2(r.x + r.width * .12f, r.y + r.height * .1f), r.width * .08f, c);
                UiIcons.Line(p, new Vector2(r.center.x + r.width * .13f, r.y + r.height * .38f), new Vector2(r.xMax - r.width * .12f, r.y + r.height * .1f), r.width * .08f, c);
                UiIcons.RoundRect(p, new Rect(r.x + r.width * .2f, r.y + r.height * .7f, r.width * .6f, r.height * .08f), 2, c);
                UiIcons.Line(p, new Vector2(r.x + r.width * .26f, r.y + r.height * .76f), new Vector2(r.x + r.width * .26f, r.yMax), r.width * .06f, c);
                UiIcons.Line(p, new Vector2(r.xMax - r.width * .26f, r.y + r.height * .76f), new Vector2(r.xMax - r.width * .26f, r.yMax), r.width * .06f, c);
                return;
            }
            UiIcons.Person(() => c)(p, r);
        }
        static VisualElement Panel(float x, float y, float w, float h)
        {
            var p = UiTokens.Box(x, y, w, h); p.style.backgroundColor = UiTokens.A(UiTokens.Panel, .96f); UiTokens.Radius(p, 32); UiTokens.Border(p, 5, UiTokens.PanelEdge);
            return p;
        }
        VisualElement Overlay(string title, out VisualElement body, out UiButton close, string closeText, Action onClose)
        {
            var o = new VisualElement(); UiTokens.Fill(o); o.style.backgroundColor = UiTokens.Dim(UiTokens.Deep, .8f);
            var p = Panel(260, 90, 1400, 900); p.style.backgroundColor = UiTokens.Panel; o.Add(p);
            var t = UiTokens.Label(title, 88, UiTokens.Accent); UiTokens.Absolute(t, 60, 30, 1280, 120); p.Add(t);
            body = UiTokens.Box(60, 170, 1280, 560); p.Add(body);
            close = new UiButton(closeText, true, onClose); UiTokens.Absolute(close, 1400 - 60 - 300, 900 - 50 - 120, 300, 120); p.Add(close);
            o.style.display = DisplayStyle.None;
            return o;
        }

        static void RowLayout(VisualElement row, Label rank, Label name, Label score, Label dist, float h, Action<Painter2D, Rect> icon = null)
        {
            row.style.flexDirection = FlexDirection.Row; row.style.alignItems = Align.Center; row.style.paddingLeft = 36; row.style.paddingRight = 36;
            rank.style.width = 110; row.Add(rank);
            if (icon != null) { var i = new ShapeElement(icon); i.style.width = 72; i.style.height = 72; i.style.marginRight = 16; row.Add(i); }
            name.style.flexGrow = 1; name.style.flexShrink = 1; name.style.overflow = Overflow.Hidden; name.style.textOverflow = TextOverflow.Ellipsis; row.Add(name);
            score.style.width = 340; row.Add(score);
            dist.style.width = 250; row.Add(dist);
        }

        static void Retry_Icon(Painter2D p, Rect r)
        {
            UiIcons.Ring(p, r.center, r.width * .3f, r.width * .12f, UiTokens.Panel, -60, 220);
            var tip = r.center + new Vector2(Mathf.Cos(-60 * Mathf.Deg2Rad), Mathf.Sin(-60 * Mathf.Deg2Rad)) * r.width * .3f;
            UiIcons.Polygon(p, UiTokens.Panel, tip + new Vector2(-r.width * .2f, -r.width * .08f), tip + new Vector2(r.width * .12f, -r.width * .2f), tip + new Vector2(r.width * .06f, r.width * .16f));
        }
        static void Camera_Icon(Painter2D p, Rect r)
        {
            UiIcons.RoundRect(p, new Rect(r.x + r.width * .06f, r.y + r.height * .28f, r.width * .6f, r.height * .44f), r.width * .06f, UiTokens.Text);
            UiIcons.Polygon(p, UiTokens.Text, new Vector2(r.x + r.width * .68f, r.center.y), new Vector2(r.xMax - r.width * .04f, r.y + r.height * .28f), new Vector2(r.xMax - r.width * .04f, r.y + r.height * .72f));
        }
        static void Home_Icon(Painter2D p, Rect r)
        {
            UiIcons.Polygon(p, UiTokens.Text, new Vector2(r.center.x, r.y + r.height * .1f), new Vector2(r.xMax - r.width * .06f, r.y + r.height * .5f), new Vector2(r.x + r.width * .06f, r.y + r.height * .5f));
            UiIcons.RoundRect(p, new Rect(r.x + r.width * .66f, r.y + r.height * .14f, r.width * .1f, r.height * .22f), 0, UiTokens.Text);
            UiIcons.RoundRect(p, new Rect(r.x + r.width * .18f, r.y + r.height * .48f, r.width * .64f, r.height * .42f), 0, UiTokens.Text);
            UiIcons.RoundRect(p, new Rect(r.x + r.width * .42f, r.y + r.height * .62f, r.width * .16f, r.height * .28f), 0, UiTokens.Panel);
        }

        public void Show(IHudData hud, RankingBoard ranking, RunSession run, bool calibrationAvailable, float time)
        {
            data = hud; board = ranking; session = run; openedAt = now = time; Open = true;
            finalScore = run?.Score.Score ?? 0;
            member = ranking != null && ranking.MemberResult;
            var style = hud?.Player?.Style ?? PlayStyle.Keyboard;
            camera = PlayStyleInfo.UsesCamera(style);
            modeName.text = style == PlayStyle.FullBody ? "전신" : style == PlayStyle.UpperBody ? "상체" : "키보드";
            headScore.text = UiTokens.N(finalScore) + "점";
            distanceValue.text = UiTokens.N(Mathf.FloorToInt(run?.Score.Distance ?? 0)) + " m";
            mineScore.text = UiTokens.Points(finalScore);
            mineDistance.text = UiTokens.N(Mathf.FloorToInt(run?.Score.Distance ?? 0)) + " m";
            double kcal = camera && run != null ? run.Workout.EstimatedKcal : 0;
            kcalValue.text = UiTokens.N((int)Math.Round(kcal)) + " kcal";
            // A keyboard run has no exercise: one plain line instead of the calorie box and its (i).
            UiTokens.Show(kcalBox, camera); UiTokens.Show(kcalButtonRef, camera); UiTokens.Show(keyboardNote, !camera);
            modeStyle = style; modeIcon.MarkDirtyRepaint();
            if (camera) food.Set(kcal); else UiTokens.Show(food, false);
            UiTokens.Show(calibrate, calibrationAvailable);
            LayoutButtons(calibrationAvailable);
            FillMore();
            ShowMore(false); ShowInfo(false);
            shownRowsVersion = -1; rowsCount = -1;
            Root.Visible = true;
            Render(time);
        }
        public void Hide() { Open = false; Root.Visible = false; }

        void LayoutButtons(bool withCalibrate)
        {
            // Left edge of the left panel to the right edge of the right panel; gaps of 90.
            const float y = 932, h = 120;
            if (withCalibrate)
            {
                float w = (RightX + RightW - LeftX - 2 * 90) / 3f;
                UiTokens.Absolute(retry, LeftX, y, w, h); UiTokens.Absolute(calibrate, LeftX + w + 90, y, w, h); UiTokens.Absolute(menu, LeftX + 2 * (w + 90), y, w, h);
            }
            else
            {
                float w = 480;
                UiTokens.Absolute(retry, 960 - 45 - w, y, w, h); UiTokens.Absolute(menu, 960 + 45, y, w, h);
            }
        }

        public void ShowMore(bool on) { UiTokens.Show(more, on); }
        public void ShowInfo(bool on) { UiTokens.Show(info, on); }
        // Esc: closes an open window first; returns false when nothing was open.
        public bool CloseWindow() { if (MoreOpen) { ShowMore(false); return true; } if (InfoOpen) { ShowInfo(false); return true; } return false; }

        // 더보기: intensity times, move counts, rescued monkeys, last week to the same day (members), the WHO note.
        void FillMore()
        {
            moreBody.Clear();
            // A keyboard run has only the monkeys and one line: a shorter window (graphics review).
            SizeOverlay(more, moreClose, camera ? 900 : 560);
            var left = UiTokens.Box(0, 0, 620, 560); var right = UiTokens.Box(680, 0, 600, 560);
            moreBody.Add(left); moreBody.Add(right);
            var workout = session?.Workout;
            int n = session?.MonkeysEarned ?? 0;
            if (!camera)
            {
                Section(left, "구한 원숭이"); left.Add(MonkeyRow(n));
                var note = UiTokens.Label("키보드 판은 운동 기록이 없어요", 44, UiTokens.TextDim); note.style.marginTop = 24; left.Add(note);
                return;
            }
            Section(left, "강도별 시간");
            if (workout != null)
            {
                Pair(left, "저강도", WorkoutReport.Clock(workout.SecondsAt(Intensity.Light)));
                Pair(left, "중강도", WorkoutReport.Clock(workout.SecondsAt(Intensity.Moderate)));
                Pair(left, "고강도", WorkoutReport.Clock(workout.SecondsAt(Intensity.Vigorous)));
            }
            Section(right, "동작 횟수");
            foreach (var row in WorkoutReport.Moves(workout)) Pair(right, row.Key, row.Value);
            Section(left, "구한 원숭이"); left.Add(MonkeyRow(n));
            if (member && data?.LastStats != null)
            {
                Section(right, "지난주 같은 요일까지");
                var change = WorkoutReport.WeekChange(data.LastStats);
                if (change.Length > 0) right.Add(UiTokens.Label(change, 44, UiTokens.Text));
            }
            var who = UiTokens.Label("WHO 권장 주 150분", 36, UiTokens.TextDim); who.style.marginTop = 24; right.Add(who);
        }
        static void SizeOverlay(VisualElement overlay, UiButton close, float height)
        {
            var panel = overlay[0];
            panel.style.height = height; panel.style.top = (1080 - height) / 2;
            close.style.top = height - 50 - 120;
        }
        static VisualElement MonkeyRow(int n)
        {
            var monkeys = new VisualElement(); monkeys.style.flexDirection = FlexDirection.Row; monkeys.style.alignItems = Align.Center; monkeys.style.height = 80;
            for (int i = 0; i < Mathf.Min(n, 5); i++) { var m = new ShapeElement(UiIcons.Monkey()); m.style.width = 64; m.style.height = 64; m.style.marginRight = 8; monkeys.Add(m); }
            monkeys.Add(UiTokens.Label(n > 5 ? "+" + (n - 5) : n == 0 ? "0" : "", 48, UiTokens.Text));
            return monkeys;
        }
        static void Section(VisualElement parent, string title) { var t = UiTokens.Label(title, 52, UiTokens.Accent); t.style.marginTop = 10; t.style.marginBottom = 6; parent.Add(t); }
        static void Pair(VisualElement parent, string label, string value)
        {
            var row = new VisualElement(); row.style.flexDirection = FlexDirection.Row; row.style.justifyContent = Justify.SpaceBetween; row.style.height = 60;
            row.Add(UiTokens.Label(label, 44, UiTokens.TextDim)); row.Add(UiTokens.Label(value, 44, UiTokens.Text, TextAnchor.MiddleRight));
            parent.Add(row);
        }

        public void Render(float time)
        {
            if (!Open) return;
            now = time; Root.Layout();
            float t = now - openedAt;
            bool ranking = t >= ScoreSeconds;
            // Phase one: the count-up in the middle.
            float k = Mathf.Clamp01(t / CountSeconds);
            int shown = Mathf.RoundToInt(finalScore * UiTokens.EaseOut(k));
            bigScore.text = UiTokens.N(shown) + "점";
            float pop = UiTokens.Pop((t - CountSeconds) / .2f, .1f);
            bigScore.style.scale = new Scale(new Vector3(pop, pop, 1));
            bool best = data != null && data.LastNewBest;
            float drop = UiTokens.EaseOut((t - CountSeconds) / .25f);
            UiTokens.Show(newBest, best && t >= CountSeconds);
            newBest.style.translate = new Translate(0, ranking ? -120 : (1 - drop) * -160);
            if (ranking) { newBest.style.left = 1920 - 60 - 340; newBest.style.top = 56; }
            else { newBest.style.left = 960 - 170; newBest.style.top = 250; }
            // Phase two: the score moves up into the head.
            float up = UiTokens.EaseOut((t - ScoreSeconds) / .35f);
            UiTokens.Show(bigScore, up < 1);
            UiTokens.Show(glow, !ranking);
            if (ranking && up < 1) { bigScore.style.top = Mathf.Lerp(380, 0, up); bigScore.style.opacity = 1 - up; }
            else if (!ranking) { bigScore.style.top = 380; bigScore.style.opacity = 1; }
            UiTokens.Show(head, ranking);
            UiTokens.Show(table, ranking);
            head.style.opacity = up;
            table.style.opacity = Mathf.Clamp01(up * 1.5f);
            if (!ranking) return;
            RenderTable(t - ScoreSeconds);
            RenderWeek(t - ScoreSeconds);
        }

        void RenderTable(float t)
        {
            var status = board?.Status ?? RankingStatus.Failed;
            int count = board?.Rows.Count ?? 0;
            if (count != rowsCount || status != shownStatus) { rowsCount = count; shownStatus = status; BuildRows(); }
            // My place: the row in the list, or pinned below "…".
            int index = MineIndex();
            int virtualAt = VirtualIndex();
            int rank = board?.Mine?.Rank ?? (virtualAt >= 0 ? virtualAt + 1 : data?.LastTodayRank ?? 0);
            headRank.text = rank > 0 ? rank + "위" : "";
            mineRank.text = rank > 0 ? rank.ToString() : "-";
            float viewH = PanelBottom - PanelTop - 36;
            float target;
            if (index >= 0)
            {
                float rowY = index * RowPitch;
                float scroll = SnapScroll(rowY + MineH / 2 - viewH / 2, count * RowPitch + (MineH - RowH) - viewH);
                content.style.top = -scroll;
                ShowWholeRows(scroll, viewH);
                // My taller row fills its own slot (the rows below are shifted by the difference), so it covers no neighbour.
                target = PanelTop + 18 + rowY - scroll;
            }
            else
            {
                content.style.top = 0;
                ShowWholeRows(0, viewH);
                target = PanelBottom - 18 - MineH;
            }
            target = Mathf.Clamp(target, PanelTop + 8, PanelBottom - 8 - MineH);
            // My row rises from below the screen into its place (0.6 s) with three fading trails.
            float k = UiTokens.EaseOut(t / RiseSeconds);
            float y = Mathf.Lerp(1100, target, k);
            mineRow.style.top = y;
            for (int i = 0; i < ghosts.Count; i++)
            {
                bool on = t < RiseSeconds + .1f;
                UiTokens.Show(ghosts[i], on);
                ghosts[i].style.top = Mathf.Lerp(1100, target, UiTokens.EaseOut((t - .06f * (i + 1)) / RiseSeconds));
            }
        }

        // The table scrolls by whole rows, so the top row is never cut; rows that do not fit whole are not drawn.
        public static float SnapScroll(float wanted, float max)
        {
            if (max <= 0) return 0;
            return Mathf.Clamp(Mathf.Round(wanted / RowPitch), 0, Mathf.Ceil(max / RowPitch)) * RowPitch;
        }
        void ShowWholeRows(float scroll, float viewH)
        {
            foreach (var row in tableRows)
                row.Key.style.visibility = row.Value - scroll >= -.5f && row.Value - scroll + RowH <= viewH + .5f ? Visibility.Visible : Visibility.Hidden;
        }

        // The run is not on the board yet (still saving, or this PC's list did not mark it): it is placed by its score,
        // after every row at or above it, and the rows below show one rank lower.
        int VirtualIndex()
        {
            if (board == null || board.Mine != null || board.Status != RankingStatus.Ready) return -1;
            int at = 0; while (at < board.Rows.Count && board.Rows[at].Score >= finalScore) at++;
            return at;
        }
        int MineIndex()
        {
            if (board == null) return -1;
            if (board.Mine == null) return VirtualIndex();
            int i = board.Rows.IndexOf(board.Mine);
            if (i >= 0) return i;
            for (int r = 0; r < board.Rows.Count; r++) if (board.Rows[r].Rank == board.Mine.Rank && board.Rows[r].Score == board.Mine.Score) return r;
            return -1;
        }

        void BuildRows()
        {
            content.Clear(); tableRows.Clear();
            statusText.text = board == null ? "" : board.StatusText;
            UiTokens.Show(statusText, statusText.text.Length > 0 && rowsCount == 0);
            if (board == null) return;
            int mine = MineIndex();
            int pinned = mine < 0 && board.Mine != null ? 1 : 0;
            float viewH = PanelBottom - PanelTop - 36;
            int fit = Mathf.FloorToInt((viewH - MineH) / RowPitch);
            int virtualAt = VirtualIndex();
            int rows = pinned == 1 ? Mathf.Min(board.Rows.Count, fit) : board.Rows.Count + (virtualAt >= 0 ? 1 : 0);
            for (int i = 0; i < rows; i++)
            {
                bool me = i == mine;
                int source = virtualAt >= 0 && i > virtualAt ? i - 1 : i;
                var r = me && virtualAt >= 0 ? null : board.Rows[source];
                float y = i * RowPitch + (me ? 0 : (mine >= 0 && i > mine ? MineH - RowH : 0));
                var row = UiTokens.Box(0, y, LeftW - 36, RowH);
                row.style.backgroundColor = i % 2 == 0 ? UiTokens.A(UiTokens.Deep, .55f) : UiTokens.A(UiTokens.Panel, .4f); UiTokens.Radius(row, 18);
                // My row is drawn by mineRow on top; leave its place empty.
                if (me) { row.style.height = MineH; row.style.backgroundColor = Color.clear; }
                else
                {
                    var rank = UiTokens.Label((r.Rank + (virtualAt >= 0 && i > virtualAt ? 1 : 0)).ToString(), 52, UiTokens.TextDim); var name = UiTokens.Label(r.Name, 48, UiTokens.Text);
                    var score = UiTokens.Label(UiTokens.Points(r.Score), 52, UiTokens.Text, TextAnchor.MiddleRight); var dist = UiTokens.Label(UiTokens.N(Mathf.FloorToInt(r.Distance)) + " m", 44, UiTokens.TextDim, TextAnchor.MiddleRight);
                    RowLayout(row, rank, name, score, dist, RowH);
                }
                content.Add(row); tableRows.Add(new KeyValuePair<VisualElement, float>(row, y));
            }
            if (pinned == 1)
            {
                var dots = UiTokens.Label("…", 52, UiTokens.TextDim, TextAnchor.MiddleCenter); UiTokens.Absolute(dots, 0, viewH - MineH - RowPitch + 8, LeftW - 36, RowH);
                content.Add(dots);
            }
            content.style.height = rows * RowPitch + MineH;
        }

        void RenderWeek(float t)
        {
            bool upper = data?.Player?.Style == PlayStyle.UpperBody;
            // Moderate plus vigorous counted twice (Workout.GoalSeconds).
            double runMinutes = (session?.Workout.GoalSeconds ?? 0) / 60.0;
            UiTokens.Show(weekBox, camera);
            if (!camera) return;
            // Guests share one PC, so the weekly total is for members; a guest sees this run's minutes (owner 02:20).
            UiTokens.Show(weekTitle, member); UiTokens.Show(weekBar, member); UiTokens.Show(weekPlus, member); UiTokens.Show(weekValue, member);
            UiTokens.Show(upperNote, upper && member);
            UiTokens.Show(guestMinutes, !member);
            if (!member) { guestMinutes.text = "이번 판 운동 " + Math.Floor(runMinutes) + "분"; return; }
            double week = Math.Max(0, data?.WeekMinutes ?? 0);
            double before = Math.Max(0, week - runMinutes);
            const double goal = 150;
            float barW = RightW - 72 - 150;
            float grow = UiTokens.Ease((t - .3f) / .8f);
            weekOld.style.width = barW * (float)Math.Min(1, before / goal);
            weekAdded.style.width = barW * (float)Math.Min(1, (before + runMinutes * grow) / goal);
            int added = (int)Math.Floor(runMinutes);
            weekPlus.text = added > 0 ? "+" + added + "분" : "";
            weekValue.text = WorkoutReport.WeekGoalValue(week);
        }
    }
}
