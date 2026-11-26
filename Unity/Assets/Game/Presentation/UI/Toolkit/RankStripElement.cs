using Mongscape.Domain;
using UnityEngine;
using UnityEngine.UIElements;

namespace Mongscape.Presentation
{
    // The running rank strip, top right (style guide 7, owner decision 02:20: the row above keeps its name).
    // Two rows: the player just above (rank, name, score) and me (rank, monkey, score, accent).
    // Passing the row above plays a 0.8 s break-through: the row flashes, shatters into pieces with light rays,
    // my row rises into its place with the rank rolling one up, and the next row above drops in from the top.
    // While a hazard is close the effect waits (up to MaxHold) so it never covers an obstacle at the wrong time.
    public sealed class RankStripElement : VisualElement
    {
        public const float Duration = .8f, MaxHold = 3f;
        const float X = 1920 - 36 - 520, W = 520, UpperY = 32, UpperH = 80, MineY = 124, MineH = 104;
        readonly VisualElement upper, mine;
        readonly Label upperRank, upperName, upperScore, mineRank, mineScore;
        readonly ShapeElement trophy, fx;
        float effectAt = -10, holdSince = -1;
        bool effect;
        RankRace.Row breaking;
        int breakingRank;
        float now;
        public System.Action Cracked;
        public bool Playing => effect;

        public RankStripElement()
        {
            pickingMode = PickingMode.Ignore; UiTokens.Fill(this);
            upper = UiTokens.Box(X, UpperY, W, UpperH); UiTokens.Radius(upper, 20);
            upper.style.flexDirection = FlexDirection.Row; upper.style.alignItems = Align.Center; upper.style.paddingLeft = 22; upper.style.paddingRight = 24;
            // Two and three digit ranks widen their column instead of running into the name.
            upperRank = UiTokens.Label("", 52, UiTokens.TextDim); upperRank.style.minWidth = 96; upperRank.style.marginRight = 14; upperRank.style.flexShrink = 0;
            upperName = UiTokens.Label("", 44, UiTokens.TextDim); upperName.style.flexGrow = 1; upperName.style.flexShrink = 1; upperName.style.overflow = Overflow.Hidden; upperName.style.textOverflow = TextOverflow.Ellipsis;
            upperScore = UiTokens.Label("", 52, UiTokens.TextDim, TextAnchor.MiddleRight); upperScore.style.marginLeft = 16; upperScore.style.flexShrink = 0;
            trophy = new ShapeElement(UiIcons.Trophy(UiTokens.Accent)); trophy.style.width = 64; trophy.style.height = 64; trophy.style.display = DisplayStyle.None;
            upper.Add(upperRank); upper.Add(trophy); upper.Add(upperName); upper.Add(upperScore);

            mine = UiTokens.Box(X, MineY, W, MineH); UiTokens.Radius(mine, 24); mine.style.backgroundColor = UiTokens.Accent;
            mine.style.flexDirection = FlexDirection.Row; mine.style.alignItems = Align.Center; mine.style.paddingLeft = 22; mine.style.paddingRight = 24;
            mine.style.overflow = Overflow.Hidden;
            mineRank = UiTokens.Label("", 72, UiTokens.Panel, TextAnchor.MiddleLeft, false); mineRank.style.minWidth = 128; mineRank.style.marginRight = 12; mineRank.style.flexShrink = 0;
            var me = new ShapeElement(UiIcons.Monkey()); me.style.width = 64; me.style.height = 64;
            mineScore = UiTokens.Label("", 64, UiTokens.Panel, TextAnchor.MiddleRight, false); mineScore.style.flexGrow = 1;
            mine.Add(mineRank); mine.Add(me); mine.Add(mineScore);

            fx = new ShapeElement(DrawEffect); UiTokens.Fill(fx);
            Add(upper); Add(mine); Add(fx);
            style.display = DisplayStyle.None;
        }

        public void ResetRun() { effect = false; effectAt = -10; holdSince = -1; }

        // Every frame while running: starts the next queued overtake unless a hazard is close (then waits a little).
        public void Tick(RankRace race, bool hazardNear, float time)
        {
            if (race == null || !race.Loaded) return;
            bool busy = effect && time - effectAt < Duration;
            if (busy) return;
            effect = false;
            if (race.Pending == 0) { holdSince = -1; return; }
            if (hazardNear) { if (holdSince < 0) holdSince = time; if (time - holdSince < MaxHold) return; }
            if (race.TakeOvertake(out breaking))
            {
                // The rank the passed row had: my rank before this pass minus one.
                breakingRank = race.Rank + race.Pending;
                effect = true; effectAt = time; holdSince = -1; Cracked?.Invoke();
            }
        }

        public void Render(RankRace race, bool hazardNear, float time)
        {
            now = time;
            bool show = race != null && race.Loaded;
            UiTokens.Show(this, show);
            if (!show) return;
            float alpha = hazardNear && !effect ? .6f : 1f;
            style.opacity = alpha;
            float t = effect ? (now - effectAt) / Duration * Duration : 99;
            // My rank while the row still stands: one lower than the new rank.
            int myRank = effect && t < .15f ? breakingRank + 1 : effect ? breakingRank : race.Rank;
            if (effect && race.Pending > 0 && t >= .15f) myRank = breakingRank;
            mineRank.text = myRank + "위";
            mineScore.text = UiTokens.N(race.Score);

            // The row above: during the break the passed row, afterwards the next one (or the trophy when first).
            bool first = race.First;
            float upperTop = UpperY, mineTop = MineY;
            if (effect)
            {
                if (t < .15f) { ShowRow(breaking.Name, breaking.Score, breakingRank, false); upper.style.backgroundColor = UiTokens.WarnHi; SetUpperText(UiTokens.Panel); upper.style.opacity = 1; }
                else if (t < .6f) { upper.style.opacity = 0; }
                else
                {
                    ShowNext(race, first);
                    float k = UiTokens.EaseOut((t - .6f) / .2f);
                    upperTop = Mathf.Lerp(UpperY - 110, UpperY, k); upper.style.opacity = k;
                }
                // My row rises into the passed row's place, then settles back under the new row above.
                if (t < .15f) mineTop = MineY;
                else if (t < .45f) mineTop = Mathf.Lerp(MineY, UpperY, UiTokens.EaseOut((t - .15f) / .3f));
                else if (t < .6f) mineTop = UpperY;
                else mineTop = Mathf.Lerp(UpperY, MineY, UiTokens.Ease((t - .6f) / .2f));
                // The rank number rolls upward when it changes.
                float roll = t >= .15f && t < .35f ? (1 - UiTokens.EaseOut((t - .15f) / .2f)) * 40 : 0;
                mineRank.style.translate = new Translate(0, roll);
                fx.MarkDirtyRepaint();
            }
            else
            {
                ShowNext(race, first);
                upper.style.opacity = 1;
                mineRank.style.translate = new Translate(0, 0);
                // The row just above trembles when I am close.
                if (race.Close) upper.style.translate = new Translate(Mathf.Sin(now * 60) * 2, 0); else upper.style.translate = new Translate(0, 0);
            }
            upper.style.top = upperTop; mine.style.top = mineTop;
        }

        void ShowNext(RankRace race, bool first)
        {
            if (first) { ShowTrophy(); return; }
            int above = race.Rank - 2;
            if (above < 0 || above >= race.Others.Count) { ShowTrophy(); return; }
            var row = race.Others[above];
            ShowRow(row.Name, row.Score, race.Rank - 1, false);
        }
        void ShowTrophy()
        {
            upper.style.backgroundColor = UiTokens.A(UiTokens.Panel, .78f);
            UiTokens.Show(trophy, true); UiTokens.Show(upperRank, false); upperName.text = ""; upperScore.text = "";
        }
        void ShowRow(string name, int score, int rank, bool flash)
        {
            UiTokens.Show(trophy, false); UiTokens.Show(upperRank, true);
            upper.style.backgroundColor = UiTokens.A(UiTokens.Panel, .78f);
            SetUpperText(UiTokens.TextDim);
            upperRank.text = rank + "위"; upperName.text = name; upperScore.text = UiTokens.N(score);
        }
        void SetUpperText(Color c) { upperRank.style.color = c; upperName.style.color = c; upperScore.style.color = c; }

        // Pieces and wedge rays from the passed row, kept inside the screen (y >= 20, left of the right edge).
        void DrawEffect(Painter2D p, Rect r)
        {
            if (!effect) return;
            float t = now - effectAt;
            if (t < .15f || t > .6f) return;
            float k = (t - .15f) / .45f;
            var c = new Vector2(X + W / 2, UpperY + UpperH / 2);
            // 12 rays, wide at the base (24 px), sharp at the tip.
            float rayLen = Mathf.Lerp(60, 260, UiTokens.EaseOut(k)), rayAlpha = 1 - k;
            for (int i = 0; i < 12; i++)
            {
                float a = i / 12f * Mathf.PI * 2 + .2f; var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a)); var side = new Vector2(-dir.y, dir.x);
                var b0 = c + dir * 40 + side * 12; var b1 = c + dir * 40 - side * 12; var tip = c + dir * rayLen;
                tip.x = Mathf.Min(tip.x, 1900); tip.y = Mathf.Max(tip.y, 20);
                UiIcons.Polygon(p, UiTokens.A(UiTokens.WarnHi, rayAlpha), b0, tip, b1);
            }
            // 24 pieces fly out sideways and up, then fall.
            for (int i = 0; i < 24; i++)
            {
                float u = (i % 8) / 7f, row = i / 8 / 2f;
                float seed = Mathf.Repeat(Mathf.Sin(i * 12.9898f) * 43758.5f, 1f);
                var start = new Vector2(X + 30 + u * (W - 60), UpperY + 16 + row * 48);
                var v = new Vector2((u - .5f) * 900 + (seed - .5f) * 300, -420 - seed * 300);
                float tt = k * .45f;
                var pos = start + v * tt + new Vector2(0, 2600 * tt * tt);
                pos.x = Mathf.Clamp(pos.x, 40, 1880); pos.y = Mathf.Max(pos.y, 40);
                float size = Mathf.Lerp(48, 90, seed);
                float rot = (seed - .5f) * 8 * k;
                var pts = new Vector2[4];
                for (int q = 0; q < 4; q++)
                {
                    float ang = rot + q * Mathf.PI / 2 + seed; float rad = size * (q % 2 == 0 ? .5f : .32f);
                    pts[q] = pos + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * rad;
                }
                float alpha = 1 - Mathf.Clamp01((k - .7f) / .3f);
                UiIcons.Polygon(p, UiTokens.A(UiTokens.Panel, alpha), pts);
                p.strokeColor = UiTokens.A(UiTokens.WarnHi, alpha); p.lineWidth = 4; p.lineJoin = LineJoin.Round;
                p.BeginPath(); p.MoveTo(pts[0]); for (int q = 1; q < 4; q++) p.LineTo(pts[q]); p.ClosePath(); p.Stroke();
            }
        }
    }
}
