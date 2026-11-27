using System;
using System.Collections.Generic;
using Mongscape.Domain;
using UnityEngine;
using UnityEngine.UIElements;

namespace Mongscape.Presentation
{
    // UI Toolkit trait screen (plan 4-1, UI style guide "특성", mockup 14_traits): the monkeys held at the top right, four
    // branches of cards, each card with its level dots and the cost of the next level at the bottom left. Choosing a card
    // opens a detail box with one unlock button. The look lives in Resources/MuhanokUI/TraitsWindow.uss; prices come from
    // TraitCatalog (the server's list once loaded). Mouse and the body hand pointer (GesturePointer) both work.
    public sealed class TraitsWindow : MonoBehaviour
    {
        private const string Folder = "MuhanokUI/";
        private static readonly string[] BranchNames = { "생존", "구출", "동료", "운동" };
        private IHudData data;
        private UIDocument document;
        private VisualElement root, detail, detailDots;
        private Label monkeys, detailName, detailText, detailCost, detailNote;
        private Button unlock;
        private Image detailCostIcon;
        private TraitId? chosen;
        private bool busy;
        private string note = "";
        private int shownVersion = -1;
        private readonly Dictionary<TraitId, Card> cards = new Dictionary<TraitId, Card>();
        private readonly Dictionary<VisualElement, Action> gestureActions = new Dictionary<VisualElement, Action>();
        public bool IsOpen { get; private set; }
        public event Action Closed;

        private sealed class Card
        {
            public VisualElement Root, Dots, CostRow;
            public Label Cost, Done;
        }

        public void Configure(IHudData hudData)
        {
            data = hudData;
            var sheet = Resources.Load<StyleSheet>(Folder + "TraitsWindow");
            var panel = ScriptableObject.CreateInstance<PanelSettings>();
            panel.name = "Mongscape Traits Panel";
            panel.themeStyleSheet = Resources.Load<ThemeStyleSheet>(Folder + "MuhanokRuntimeTheme");
            panel.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            panel.referenceResolution = new Vector2Int(1920, 1080);
            panel.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            panel.match = .5f;
            panel.sortingOrder = 100;
            document = gameObject.AddComponent<UIDocument>();
            document.panelSettings = panel;
            root = document.rootVisualElement;
            if (sheet != null) root.styleSheets.Add(sheet);
            else Debug.LogError("Mongscape: Resources/MuhanokUI/TraitsWindow.uss is missing.");
            root.style.unityFontDefinition = FontDefinition.FromFont(UiFont.Main);
            root.style.position = Position.Absolute; root.style.left = 0; root.style.top = 0; root.style.right = 0; root.style.bottom = 0;
            Build();
            SetVisible(false);
        }

        private void Build()
        {
            var screen = Add(root, "traits-root");
            var header = Add(screen, "traits-header");
            var lead = Add(header, "traits-lead");
            Text(lead, "특성", "traits-title");
            var back = new Button(Close) { name = "traits-back" }; back.AddToClassList("traits-back");
            Icon(back, "back", "traits-back-icon"); Text(back, "뒤로", "traits-back-label");
            lead.Add(back); gestureActions[back] = Close;
            var held = Add(header, "traits-held");
            Icon(held, "monkey", "traits-held-icon");
            monkeys = Text(held, "0", "traits-held-count");

            var columns = Add(screen, "traits-columns");
            for (int b = 0; b < BranchNames.Length; b++)
            {
                var column = Add(columns, "traits-column");
                Text(column, BranchNames[b], "traits-branch");
                foreach (var trait in TraitCatalog.All) if ((int)trait.Branch == b) BuildCard(column, trait);
            }


            // Detail box: what the next level does, its cost, and the one unlock button.
            detail = Add(root, "traits-detail-shade");
            detail.RegisterCallback<ClickEvent>(e => { if (e.target == detail) Choose(null); });
            var box = Add(detail, "traits-detail");
            detailName = Text(box, "", "traits-detail-name");
            detailDots = Add(box, "traits-dots");
            detailText = Text(box, "", "traits-detail-text");
            var costRow = Add(box, "traits-detail-cost-row");
            detailCostIcon = Icon(costRow, "monkey", "traits-cost-icon");
            detailCost = Text(costRow, "", "traits-detail-cost");
            detailNote = Text(box, "", "traits-detail-note");
            var buttons = Add(box, "traits-detail-buttons");
            var cancel = new Button(() => Choose(null)) { text = "닫기", name = "traits-detail-close" }; cancel.AddToClassList("traits-button");
            unlock = new Button(Unlock) { text = "열기", name = "traits-detail-unlock" }; unlock.AddToClassList("traits-button"); unlock.AddToClassList("traits-button-main");
            buttons.Add(cancel); buttons.Add(unlock);
            gestureActions[cancel] = () => Choose(null); gestureActions[unlock] = Unlock;
        }

        private void BuildCard(VisualElement column, TraitDefinition trait)
        {
            var card = new Card();
            card.Root = new Button(() => Choose(trait.Id)) { name = "trait-" + Key(trait.Id) };
            card.Root.AddToClassList("trait-card"); column.Add(card.Root);
            var top = Add(card.Root, "trait-card-top");
            var badge = Add(top, "trait-icon-badge"); Icon(badge, Key(trait.Id), "trait-icon");
            var side = Add(top, "trait-card-side");
            Text(side, trait.Name, "trait-name");
            card.Dots = Add(side, "traits-dots");
            card.CostRow = Add(card.Root, "trait-cost-row");
            Icon(card.CostRow, "monkey", "traits-cost-icon");
            card.Cost = Text(card.CostRow, "", "trait-cost");
            card.Done = Text(card.Root, "최고 단계", "trait-done");
            cards[trait.Id] = card;
            TraitId id = trait.Id; gestureActions[card.Root] = () => Choose(id);
        }

        public void Open()
        {
            if (root == null || data == null) return;
            data.LoadTraitPrices();
            chosen = null; note = ""; busy = false; shownVersion = -1;
            Refresh(); SetVisible(true); IsOpen = true;
        }
        public void Close()
        {
            if (!IsOpen) return;
            SetVisible(false); IsOpen = false; Closed?.Invoke();
        }
        private void SetVisible(bool visible) { if (root != null) root.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None; }

        private void Choose(TraitId? id) { chosen = id; note = ""; Refresh(); }

        private void Unlock()
        {
            if (busy || chosen == null || data == null) return;
            var id = chosen.Value;
            if (!data.TraitsForSale) { note = "회원가입하면 구한 원숭이로 특성을 열 수 있어요"; Refresh(); return; }
            busy = true; note = ""; Refresh();
            data.UnlockTrait(id, (ok, reason) => { busy = false; note = ok ? "" : reason; if (ok && data.Traits.IsMaxed(id)) chosen = null; Refresh(); });
        }

        // Cheap enough to redo whenever something changes: seven cards and one box.
        private void Refresh()
        {
            if (data == null) return;
            var levels = data.Traits; int held = data.Monkeys;
            monkeys.text = held.ToString("N0");
            foreach (var trait in TraitCatalog.All)
            {
                var card = cards[trait.Id]; int level = levels.Level(trait.Id); bool full = level >= trait.MaxLevel;
                Dots(card.Dots, level, trait.MaxLevel, false);
                card.Root.EnableInClassList("trait-card-full", full);
                card.Root.EnableInClassList("trait-card-chosen", chosen == trait.Id);
                card.CostRow.style.display = full ? DisplayStyle.None : DisplayStyle.Flex;
                card.Done.style.display = full ? DisplayStyle.Flex : DisplayStyle.None;
                int cost = trait.Cost(level + 1);
                card.Cost.text = cost.ToString();
                card.Cost.EnableInClassList("trait-cost-short", !data.TraitsForSale || cost > held);
            }
            bool open = chosen != null;
            detail.style.display = open ? DisplayStyle.Flex : DisplayStyle.None;
            if (!open) return;
            var def = TraitCatalog.Get(chosen.Value); int owned = levels.Level(def.Id); bool maxed = owned >= def.MaxLevel;
            int next = Math.Min(def.MaxLevel, owned + 1), price = def.Cost(next);
            detailName.text = def.Name;
            Dots(detailDots, owned, def.MaxLevel, true);
            detailText.text = def.LevelTexts[(maxed ? def.MaxLevel : next) - 1];
            detailCost.text = maxed ? "" : price.ToString();
            detailCostIcon.style.display = maxed ? DisplayStyle.None : DisplayStyle.Flex;
            detailCost.EnableInClassList("trait-cost-short", !data.TraitsForSale || price > held);
            detailNote.text = busy ? "여는 중" : note;
            bool can = !maxed && !busy;
            unlock.SetEnabled(can);
            unlock.text = maxed ? "최고 단계" : "Lv" + next + " 열기";
        }

        private static void Dots(VisualElement row, int owned, int max, bool large)
        {
            if (row.childCount != max) { row.Clear(); for (int i = 0; i < max; i++) { var dot = new VisualElement(); dot.AddToClassList("trait-dot"); if (large) dot.AddToClassList("trait-dot-large"); row.Add(dot); } }
            for (int i = 0; i < max; i++) row[i].EnableInClassList("trait-dot-full", i < owned);
        }

        private void Update()
        {
            if (!IsOpen || data == null) return;
            // The server's answer (prices, a new balance) arrives later: refresh when the numbers move.
            int version = data.Monkeys * 1000 + TraitVersion();
            if (version != shownVersion) { shownVersion = version; Refresh(); }
        }
        private int TraitVersion()
        {
            int v = 0;
            foreach (var trait in TraitCatalog.All) v = v * 7 + data.Traits.Level(trait.Id) + trait.Cost(1);
            return v;
        }

        // Body hand pointer, as in SettingsWindow: dwell on a card or button to press it.
        private void OnGUI()
        {
            if (!IsOpen || root == null) return;
            // Esc: closes the detail box, then the screen (IMGUI event; the project reads keys through the Input System).
            var e = Event.current;
            if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape) { e.Use(); if (chosen != null) Choose(null); else Close(); return; }
            var old = GUI.matrix; GUI.matrix = ViewportLayout.GuiMatrix; GesturePointer.BeginPass();
            var screen = GUI.matrix.MultiplyPoint3x4(GesturePointer.Position);
            var panelPoint = new Vector2(screen.x / Mathf.Max(1, Screen.width) * root.layout.width, screen.y / Mathf.Max(1, Screen.height) * root.layout.height);
            var picked = root.panel?.Pick(panelPoint);
            foreach (var pair in gestureActions)
                if (Shown(pair.Key) && Reachable(pair.Key) && picked != null && (picked == pair.Key || pair.Key.Contains(picked)) && GesturePointer.Activate(ToGui(pair.Key), pair.Key.name)) { pair.Value(); break; }
            GesturePointer.Draw(); GUI.matrix = old;
        }
        // While the detail box is open only its buttons answer the pointer.
        private bool Reachable(VisualElement element) => chosen == null || detail.Contains(element);
        private Rect ToGui(VisualElement element)
        {
            var b = element.worldBound; float w = Mathf.Max(1, root.layout.width), h = Mathf.Max(1, root.layout.height);
            var viewport = ViewportLayout.Current; float scale = viewport.width / 1280f, top = Screen.height - viewport.yMax;
            return new Rect((b.x / w * Screen.width - viewport.x) / scale, (b.y / h * Screen.height - top) / scale, b.width / w * Screen.width / scale, b.height / h * Screen.height / scale);
        }
        private static bool Shown(VisualElement element)
        {
            for (var e = element; e != null; e = e.parent) if (e.resolvedStyle.display == DisplayStyle.None) return false;
            return element.enabledInHierarchy;
        }

        public static string Key(TraitId id) => id switch
        {
            TraitId.Shield => "shield", TraitId.SharpEars => "sharp_ears", TraitId.ThickFur => "thick_fur", TraitId.Call => "call",
            TraitId.Dexterity => "dexterity", TraitId.Loyalty => "loyalty", _ => "sweat",
        };
        private static VisualElement Add(VisualElement parent, string cls)
        {
            var element = new VisualElement { name = cls }; element.AddToClassList(cls); element.pickingMode = PickingMode.Ignore;
            if (cls == "traits-detail-shade" || cls == "traits-detail") element.pickingMode = PickingMode.Position;
            parent.Add(element); return element;
        }
        private static Label Text(VisualElement parent, string text, string cls)
        {
            var label = new Label(text); label.AddToClassList(cls); label.pickingMode = PickingMode.Ignore; parent.Add(label); return label;
        }
        private static Image Icon(VisualElement parent, string key, string cls)
        {
            var image = new Image { image = Resources.Load<Texture2D>(Folder + "Traits/trait-" + key), scaleMode = ScaleMode.ScaleToFit };
            image.AddToClassList(cls); image.pickingMode = PickingMode.Ignore; parent.Add(image); return image;
        }
        private void OnDestroy() { if (document != null && document.panelSettings != null) Destroy(document.panelSettings); }
    }
}
