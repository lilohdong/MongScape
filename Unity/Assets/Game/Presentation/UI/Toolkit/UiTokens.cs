using System.Globalization;
using UnityEngine;
using UnityEngine.UIElements;

namespace Mongscape.Presentation
{
    // Colours and helpers from the v2 UI style guide (ui-review/UI_STYLE_GUIDE.md section 3), for the UI Toolkit screens.
    // Every layout number is in 1920x1080 pixels: UiRoot scales the whole frame to the letterboxed game view.
    public static class UiTokens
    {
        public static readonly Color Panel = Hex(0x2A1D14), PanelEdge = Hex(0x97693D), Accent = Hex(0xFFD36B), Text = Hex(0xFAF5DE),
            TextDim = Hex(0xC7B08F), Focus = Hex(0xF0940F), Cool = Hex(0x7FC0D8), Warn = Hex(0xFFC70F), WarnHi = Hex(0xFFDF38),
            Danger = Hex(0xFF0904), Alarm = Hex(0xCC160B), Deep = Hex(0x0E171F), Iron = Hex(0x333D45),
            MonkeyFur = Hex(0x7A4A2A), MonkeyFace = Hex(0xF2C28E), PrisonerOrange = Hex(0xE83806), HatNavy = Hex(0x1F2B44), HatBrim = Hex(0x5E7A9E);
        public static Color Hex(int rgb) => new Color32((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb, 255);
        public static Color A(Color c, float a) { c.a = a; return c; }
        static readonly CultureInfo Num = CultureInfo.InvariantCulture;
        // 12,480 (style guide 1: thousands separator).
        public static string N(int value) => value.ToString("N0", Num);

        public static Label Label(string text, int size, Color color, TextAnchor align = TextAnchor.MiddleLeft, bool shadow = true)
        {
            var l = new Label(text) { pickingMode = PickingMode.Ignore };
            l.style.fontSize = size; l.style.color = color; l.style.unityTextAlign = align;
            l.style.marginLeft = l.style.marginRight = l.style.marginTop = l.style.marginBottom = 0;
            l.style.paddingLeft = l.style.paddingRight = l.style.paddingTop = l.style.paddingBottom = 0;
            l.style.whiteSpace = WhiteSpace.NoWrap;
            if (shadow) Shadow(l, size);
            return l;
        }
        // Light text gets a shadow to the bottom right, 1/22 of the size, black 60% (style guide 1).
        public static void Shadow(VisualElement e, int size)
        {
            float o = Mathf.Max(1, size / 22f);
            e.style.textShadow = new TextShadow { offset = new Vector2(o, o), blurRadius = 0, color = new Color(0, 0, 0, .6f) };
        }
        public static void NoShadow(VisualElement e) => e.style.textShadow = new TextShadow { offset = Vector2.zero, color = Color.clear };
        public static VisualElement Box(float x, float y, float w, float h)
        {
            var e = new VisualElement { pickingMode = PickingMode.Ignore };
            e.style.position = Position.Absolute; e.style.left = x; e.style.top = y; e.style.width = w; e.style.height = h;
            return e;
        }
        public static void Absolute(VisualElement e, float x, float y, float w, float h)
        {
            e.style.position = Position.Absolute; e.style.left = x; e.style.top = y; e.style.width = w; e.style.height = h;
        }
        public static void Radius(VisualElement e, float r) { e.style.borderTopLeftRadius = e.style.borderTopRightRadius = e.style.borderBottomLeftRadius = e.style.borderBottomRightRadius = r; }
        public static void Border(VisualElement e, float width, Color color)
        {
            e.style.borderLeftWidth = e.style.borderRightWidth = e.style.borderTopWidth = e.style.borderBottomWidth = width;
            e.style.borderLeftColor = e.style.borderRightColor = e.style.borderTopColor = e.style.borderBottomColor = color;
        }
        public static void Fill(VisualElement e) { e.style.position = Position.Absolute; e.style.left = 0; e.style.top = 0; e.style.right = 0; e.style.bottom = 0; }
        public static void Show(VisualElement e, bool on) { var d = on ? DisplayStyle.Flex : DisplayStyle.None; if (e.style.display != d) e.style.display = d; }
        public static float Ease(float t) { t = Mathf.Clamp01(t); return t * t * (3 - 2 * t); }
        public static float EaseOut(float t) { t = Mathf.Clamp01(t); return 1 - (1 - t) * (1 - t) * (1 - t); }
        // 0 -> 1 -> 0 bump: an overshoot of `amount` at the middle of the window.
        public static float Pop(float t, float amount) => t < 0 || t > 1 ? 1 : 1 + amount * Mathf.Sin(t * Mathf.PI);
    }

    // A full-screen UI Toolkit panel whose 1920x1080 frame follows the letterboxed 16:9 game view (ViewportLayout).
    public sealed class UiRoot
    {
        public readonly UIDocument Document;
        public readonly VisualElement Frame;
        public UiRoot(GameObject host, string name, int sortingOrder)
        {
            var panel = ScriptableObject.CreateInstance<PanelSettings>();
            panel.name = name;
            panel.themeStyleSheet = Resources.Load<ThemeStyleSheet>("MuhanokUI/MuhanokRuntimeTheme");
            panel.scaleMode = PanelScaleMode.ConstantPixelSize; panel.scale = 1;
            panel.sortingOrder = sortingOrder;
            panel.clearColor = false;
            var go = new GameObject(name); go.transform.SetParent(host.transform, false);
            Document = go.AddComponent<UIDocument>();
            Document.panelSettings = panel;
            var root = Document.rootVisualElement;
            root.pickingMode = PickingMode.Ignore;
            root.style.unityFontDefinition = FontDefinition.FromFont(UiFont.Main);
            UiTokens.Fill(root);
            Frame = new VisualElement { name = name + "-frame", pickingMode = PickingMode.Ignore };
            Frame.style.position = Position.Absolute; Frame.style.width = 1920; Frame.style.height = 1080;
            Frame.style.transformOrigin = new TransformOrigin(0, 0);
            Frame.style.overflow = Overflow.Hidden;
            root.Add(Frame);
            Layout();
        }
        float lastW = -1, lastH = -1;
        // Called every frame: keeps the frame on the game view when the window size changes.
        public void Layout()
        {
            if (Screen.width == lastW && Screen.height == lastH) return;
            lastW = Screen.width; lastH = Screen.height;
            var r = ViewportLayout.Current;
            Frame.style.left = r.x; Frame.style.top = Screen.height - r.yMax;
            float s = r.width / 1920f;
            Frame.style.scale = new Scale(new Vector3(s, s, 1));
        }
        public bool Visible
        {
            get => Document.rootVisualElement.style.display != DisplayStyle.None;
            set => UiTokens.Show(Document.rootVisualElement, value);
        }
    }
}
