using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Mongscape.Presentation
{
    // A big button (style guide 4): panel 94% with a 5 px wood border, or the one primary button in accent.
    // Mouse clicks it; the hand pointer selects it by resting on it 1.2 s (GestureButtons.Pass from an IMGUI pass).
    public sealed class UiButton : VisualElement
    {
        public readonly Label Text;
        public Action Pressed;
        // A button with its own colours (tutorial chapter cards): called instead of the default look, with the hover state.
        public Action<bool> Look;
        readonly bool primary;
        bool hover;
        public UiButton(string text, bool primaryButton, Action pressed, int size = 64, Action<Painter2D, Rect> icon = null)
        {
            primary = primaryButton; Pressed = pressed; name = "button-" + text;
            style.flexDirection = FlexDirection.Row; style.alignItems = Align.Center; style.justifyContent = Justify.Center;
            UiTokens.Radius(this, 32);
            if (icon != null) { var i = new ShapeElement(icon); i.style.width = 64; i.style.height = 64; i.style.marginRight = 16; Add(i); }
            Text = UiTokens.Label(text, size, UiTokens.Text, TextAnchor.MiddleCenter, false);
            Add(Text);
            this.AddManipulator(new Clickable(() => Pressed?.Invoke()));
            RegisterCallback<PointerEnterEvent>(_ => { hover = true; Paint(); });
            RegisterCallback<PointerLeaveEvent>(_ => { hover = false; Paint(); });
            Paint();
        }
        public void SetLabel(string text) => Text.text = text;
        public void Paint()
        {
            if (Look != null) { Look(hover); return; }
            bool lit = primary || hover;
            style.backgroundColor = lit ? UiTokens.Accent : UiTokens.A(UiTokens.Panel, .94f);
            UiTokens.Border(this, 5, lit ? UiTokens.WarnHi : UiTokens.PanelEdge);
            Text.style.color = lit ? UiTokens.Panel : UiTokens.Text;
        }
    }

    // Hand-pointer support for UI Toolkit buttons inside a UiRoot frame (1920x1080), checked from an IMGUI pass in
    // the 1280x720 GUI space that GesturePointer uses.
    public sealed class GestureButtons
    {
        readonly List<UiButton> buttons = new List<UiButton>();
        readonly VisualElement frame;
        public GestureButtons(VisualElement frameElement) { frame = frameElement; }
        public UiButton Add(UiButton b) { buttons.Add(b); return b; }
        static bool Shown(VisualElement e)
        {
            for (var x = e; x != null; x = x.parent) if (x.resolvedStyle.display == DisplayStyle.None || x.resolvedStyle.opacity < .05f) return false;
            return e.enabledInHierarchy;
        }
        public Rect GuiRect(VisualElement e)
        {
            var r = frame.WorldToLocal(e.worldBound);
            const float k = 1280f / 1920f;
            return new Rect(r.x * k, r.y * k, r.width * k, r.height * k);
        }
        // Call inside an IMGUI pass with GUI.matrix = ViewportLayout.GuiMatrix (after GesturePointer.BeginPass).
        public void Pass()
        {
            foreach (var b in buttons)
                if (Shown(b) && GesturePointer.Activate(GuiRect(b), b.name)) { b.Pressed?.Invoke(); break; }
        }
    }
}
