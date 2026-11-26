using Mongscape.Domain;
using UnityEngine;

namespace Mongscape.Presentation
{
    // The drawings of the tutorial stations and picture steps (plan 6-1b): the stuck monkey, the sync beats, the warning rule,
    // the searchlight with the lock gauge and the alarm rule. Plain shapes on the 1280x720 HUD plane;
    // the caller has set the GUI matrix. No sentences, only pictures and one symbol each.
    internal static class TutorialPictures
    {
        private static Texture2D discTexture;
        private static readonly Color Gold = new Color(1f, .82f, .25f), Red = new Color(.9f, .22f, .2f), Green = new Color(.45f, 1f, .6f), Fur = new Color(.55f, .36f, .2f),
            Ink = new Color(.1f, .08f, .07f), Wall = new Color(.3f, .27f, .25f), Steel = new Color(.7f, .72f, .75f), Mane = new Color(.85f, .5f, .15f), Face = new Color(.95f, .75f, .45f);

        public static void Dispose() { if (discTexture != null) { Object.Destroy(discTexture); discTexture = null; } }
        private static Texture2D Disc()
        {
            if (discTexture != null) return discTexture;
            const int n = 64; discTexture = new Texture2D(n, n, TextureFormat.RGBA32, false) { hideFlags = HideFlags.DontSave, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
            {
                float d = Vector2.Distance(new Vector2(x + .5f, y + .5f), new Vector2(n * .5f, n * .5f));
                discTexture.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp01(n * .5f - d)));
            }
            discTexture.Apply(false, true); return discTexture;
        }
        private static void Round(Vector2 centre, float radius, Color color) { GUI.color = color; GUI.DrawTexture(new Rect(centre.x - radius, centre.y - radius, radius * 2, radius * 2), Disc()); }
        private static void Box(Rect r, Color color) { GUI.color = color; GUI.DrawTexture(r, Texture2D.whiteTexture); }
        private static void Mark(HudSkin skin, Rect r, string text, int size, Color color) { GUI.color = color; GUI.Label(r, text, skin.Centered(size, FontStyle.Bold)); }
        private static void Monkey(Vector2 c, float r, bool scared)
        {
            Round(c + new Vector2(-r * .95f, 0), r * .38f, Fur); Round(c + new Vector2(r * .95f, 0), r * .38f, Fur);
            Round(c, r, Fur); Round(c + new Vector2(0, r * .25f), r * .62f, Face);
            Round(c + new Vector2(-r * .32f, -r * .12f), r * (scared ? .2f : .14f), Ink); Round(c + new Vector2(r * .32f, -r * .12f), r * (scared ? .2f : .14f), Ink);
        }
        private static void Panel(Rect r, float alpha) { Box(r, new Color(.07f, .06f, .05f, .8f * alpha)); }


        // How long the reach has been held (the wall monkey itself is the real StuckMonkeyView).
        public static void Reach(HudSkin skin, float progress)
        {
            Box(new Rect(540, 336, 200, 14), new Color(1, 1, 1, .2f)); Box(new Rect(540, 336, 200 * progress, 14), Green);
        }

        // Four beats to jump on: the discs light up in time and turn green when caught; a pulse rings at each tick.
        public static void Beats(HudSkin skin, float elapsed, float interval, int lead, int beats, int hits)
        {
            float start = lead * interval;
            float phase = interval > 0 ? Mathf.Repeat(elapsed / interval, 1) : 0;
            Round(new Vector2(640, 380), 30 + 50 * phase, new Color(Gold.r, Gold.g, Gold.b, .5f * (1 - phase)));
            for (int i = 0; i < beats; i++)
            {
                var c = new Vector2(640 + (i - (beats - 1) * .5f) * 130, 500);
                bool caught = i < hits, now = elapsed >= start + i * interval;
                Round(c, 44, new Color(0, 0, 0, .55f));
                Round(c, 38, caught ? Green : now ? Gold : new Color(1, 1, 1, .25f));
                if (caught) Mark(skin, new Rect(c.x - 40, c.y - 22, 80, 44), "↑", 34, Ink);
            }
        }

        // Notes caught in the zoo verse, against the number that passes.
        public static void Count(HudSkin skin, int hits, int pass)
        {
            Box(new Rect(580, 330, 120, 36), new Color(.07f, .06f, .05f, .75f));
            Mark(skin, new Rect(580, 330, 120, 36), hits + " / " + pass, 22, hits >= pass ? Green : Color.white);
        }

        // 1 yellow warning stays until the map ends, the second one means the cuffs (shown as four small pictures in a row).
        public static void WarnRule(HudSkin skin, float t)
        {
            float a(int i) => Mathf.Clamp01((t - i * .16f) * 6);
            for (int i = 0; i < 4; i++)
            {
                var r = new Rect(190 + i * 240, 360, 200, 200); Panel(r, a(i));
                var c = r.center;
                if (i == 0) { Round(c, 52, Red * new Color(1, 1, 1, a(i))); Round(c + new Vector2(0, 0), 26, new Color(1, .85f, .3f, a(i))); }
                else if (i == 1) Warn(skin, c, a(i));
                else if (i == 2) { Warn(skin, c + new Vector2(-46, 0), a(i)); Warn(skin, c + new Vector2(46, 0), a(i)); }
                else Cuffs(c, a(i));
                if (i < 3 && a(i + 1) > 0) Mark(skin, new Rect(r.xMax, 430, 40, 60), "→", 34, new Color(1, 1, 1, a(i + 1)));
            }
        }
        private static void Warn(HudSkin skin, Vector2 c, float alpha) { Round(c, 44, new Color(Gold.r, Gold.g, Gold.b, alpha)); Mark(skin, new Rect(c.x - 30, c.y - 34, 60, 68), "!", 52, new Color(Ink.r, Ink.g, Ink.b, alpha)); }
        private static void Cuffs(Vector2 c, float alpha)
        {
            var steel = new Color(Steel.r, Steel.g, Steel.b, alpha);
            Round(c + new Vector2(-44, 0), 38, steel); Round(c + new Vector2(-44, 0), 20, new Color(.07f, .06f, .05f, 1));
            Round(c + new Vector2(44, 0), 38, steel); Round(c + new Vector2(44, 0), 20, new Color(.07f, .06f, .05f, 1));
            Box(new Rect(c.x - 30, c.y - 5, 60, 10), steel);
        }

        // The searchlight finds the monkey, the bell rings, a warning appears; the second one is the cuffs.
        public static void AlarmRule(HudSkin skin, float t)
        {
            float a(int i) => Mathf.Clamp01((t - i * .16f) * 6);
            for (int i = 0; i < 4; i++)
            {
                var r = new Rect(190 + i * 240, 360, 200, 200); Panel(r, a(i));
                var c = r.center;
                if (i == 0)
                {
                    for (int k = 0; k < 8; k++) Box(new Rect(c.x - (14 + k * 8), r.y + 8 + k * 10, 28 + k * 16, 11), new Color(1f, .95f, .6f, .2f * a(i)));
                    Monkey(c + new Vector2(0, 52), 28, true);
                }
                else if (i == 1)
                {
                    float ring = Mathf.Sin(t * 60) * 6;
                    Round(c + new Vector2(0, -10), 46, new Color(Gold.r, Gold.g, Gold.b, a(i))); Box(new Rect(c.x - 58 + ring, c.y + 22, 116, 10), new Color(Gold.r, Gold.g, Gold.b, a(i)));
                    Round(c + new Vector2(0, 44), 9, new Color(Ink.r, Ink.g, Ink.b, a(i)));
                }
                else if (i == 2) Warn(skin, c, a(i));
                else Cuffs(c, a(i));
                if (i < 3 && a(i + 1) > 0) Mark(skin, new Rect(r.xMax, 430, 40, 60), "→", 34, new Color(1, 1, 1, a(i + 1)));
            }
        }
    }
}
