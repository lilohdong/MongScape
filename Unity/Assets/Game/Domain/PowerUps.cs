using System;
using System.Collections.Generic;

namespace Mongscape.Domain
{
    // Items picked up on the course (plan 4-1). They appear only once their trait is unlocked: the call (Call trait) and
    // the shield (Shield trait). The coin magnet and double coins are gone with the coins.
    public enum ItemKind { Call, Shield }

    public sealed class ItemDefinition
    {
        public readonly ItemKind Kind; public readonly string Name; public readonly TraitId Trait;
        public ItemDefinition(ItemKind kind, string name, TraitId trait) { Kind = kind; Name = name; Trait = trait; }
    }

    public static class ItemCatalog
    {
        public static readonly ItemDefinition[] All =
        {
            new ItemDefinition(ItemKind.Call, "부르는 소리", TraitId.Call),
            new ItemDefinition(ItemKind.Shield, "보호막", TraitId.Shield),
        };
        public static ItemDefinition Get(ItemKind kind) { foreach (var item in All) if (item.Kind == kind) return item; return All[0]; }
        // The items a run may place, and how often each comes (relative weight), from the run's traits.
        public static List<ItemKind> Available(RunTraits traits)
        {
            var list = new List<ItemKind>();
            if (traits == null) return list;
            if (traits.CallItems) list.Add(ItemKind.Call);
            if (traits.ShieldItems) { list.Add(ItemKind.Shield); if (traits.ShieldWeight >= 2f) list.Add(ItemKind.Shield); }
            return list;
        }
    }

    // Items of the running run. The call lasts its trait's seconds; the shield is held until it blocks one warning.
    public sealed class PowerUpState
    {
        private float callRemaining;
        private bool shield;
        public int Collected { get; private set; }
        public RunTraits Traits { get; set; } = RunTraits.None;
        public bool IsActive(ItemKind kind) => kind == ItemKind.Shield ? shield : callRemaining > 0f;
        // Seconds left of a timed item (0 for the shield, which has no timer).
        public float Remaining(ItemKind kind) => kind == ItemKind.Call ? callRemaining : 0f;
        public void Activate(ItemKind kind)
        {
            if (kind == ItemKind.Shield) shield = true;
            else callRemaining = Math.Max(callRemaining, Math.Max(RunTraits.CallSecondsAt(1), Traits.CallSeconds));
            Collected++;
        }
        // The run starts holding a shield (Shield Lv3); not counted as a pickup.
        public void GiveStartingShield() { shield = true; }
        public bool ConsumeShield()
        {
            if (!shield) return false;
            shield = false; return true;
        }
        public void Tick(float dt) { callRemaining = Math.Max(0f, callRemaining - Math.Max(0f, dt)); }
        public void Reset() { callRemaining = 0f; shield = false; Collected = 0; }
    }
}
