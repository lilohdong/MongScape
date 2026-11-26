using System;
using Mongscape.Domain;
using UnityEngine;
using UnityEngine.UIElements;

namespace Mongscape.Presentation
{
    // "바나나 1.0개만큼" with its food icon: the run's calories as the familiar food whose count is closest to one
    // (FoodEquivalent.Closest). A component on its own so the result screen can move it (owner picked plan A: inside the
    // calorie box, under the kcal). Icons: ui-review/mockups/17_food_icons.png, 76 px; text 52 px.
    public sealed class FoodLine : VisualElement
    {
        readonly Image icon;
        readonly Label text;
        static Texture2D[] textures;
        public FoodLine(int textSize = 52, int iconSize = 76)
        {
            pickingMode = PickingMode.Ignore;
            style.flexDirection = FlexDirection.Row; style.alignItems = Align.Center;
            icon = new Image { pickingMode = PickingMode.Ignore, scaleMode = ScaleMode.ScaleToFit };
            icon.style.width = iconSize; icon.style.height = iconSize; icon.style.marginRight = 16; icon.style.flexShrink = 0;
            text = UiTokens.Label("", textSize, UiTokens.Text);
            Add(icon); Add(text);
        }
        public string Shown => text.text;
        public void Set(double kcal)
        {
            var portion = FoodEquivalent.Closest(kcal);
            UiTokens.Show(this, portion.HasValue);
            if (!portion.HasValue) return;
            text.text = portion.Value.Phrase;
            icon.image = Icon(Array.IndexOf(FoodEquivalent.Table, portion.Value.Food));
        }
        public static Texture2D Icon(int index)
        {
            textures ??= new Texture2D[FoodEquivalent.Table.Length];
            if (index < 0 || index >= textures.Length) return null;
            return textures[index] ??= Resources.Load<Texture2D>("MuhanokUI/Food/food_" + index);
        }
    }
}
