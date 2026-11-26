using System;
using System.Collections.Generic;

namespace Mongscape.Domain
{
    // Traits (plan 4-1) replace the shop: rescued monkeys unlock them for good, some in levels. Traits forgive mistakes or
    // help rescue more, never reduce the exercise. Ids match the server's TRAITS; a bought level is stored in the account's
    // unlocked list as id*10+level (0..2 are the old shop items and are ignored).
    public enum TraitId { Shield = 1, SharpEars = 2, ThickFur = 3, Call = 4, Dexterity = 5, Loyalty = 6, Sweat = 7 }
    public enum TraitBranch { Survival, Rescue, Companion, Exercise }

    public sealed class TraitDefinition
    {
        public readonly TraitId Id;
        public readonly TraitBranch Branch;
        public readonly string Name;
        // What each level does, for the trait screen (index 0 = Lv1).
        public readonly string[] LevelTexts;
        // Monkeys each level costs. The server's /traits is the price list; these are its copy, replaced from the
        // server when the list arrives and used as is only offline (a guest without a connection sees them, buys nothing).
        public int[] Costs { get; private set; }
        public int MaxLevel => LevelTexts.Length;
        public TraitDefinition(TraitId id, TraitBranch branch, string name, int[] offlineCosts, params string[] levelTexts)
        { Id = id; Branch = branch; Name = name; Costs = offlineCosts; LevelTexts = levelTexts; }
        // Cost of the given level (1-based), or -1 when there is no such level.
        public int Cost(int level) => level >= 1 && level <= Costs.Length && level <= MaxLevel ? Costs[level - 1] : -1;
        internal void SetCosts(int[] costs) { Costs = costs; }
    }

    public static class TraitCatalog
    {
        // Order of the trait screen: branch by branch, top to bottom.
        public static readonly TraitDefinition[] All =
        {
            new TraitDefinition(TraitId.Shield, TraitBranch.Survival, "보호막", new[] { 5, 15, 30 },
                "판 중에 보호막이 나와요\n경고 1번을 막아요", "보호막이 더 자주 나와요", "보호막 1개를 들고 시작해요"),
            new TraitDefinition(TraitId.SharpEars, TraitBranch.Survival, "예민한 귀", new[] { 10, 20 },
                "경고를 0.3초 먼저 알려 줘요", "경고를 0.6초 먼저 알려 줘요"),
            new TraitDefinition(TraitId.ThickFur, TraitBranch.Survival, "두꺼운 털", new[] { 20 },
                "그 판의 첫 경고에는\n미끄러지지 않아요"),
            new TraitDefinition(TraitId.Call, TraitBranch.Rescue, "부르는 소리", new[] { 5, 15 },
                "판 중에 부르는 소리가 나와요\n잠깐 동안 손을 안 뻗어도 구해요", "부르는 소리가 더 오래가요"),
            new TraitDefinition(TraitId.Dexterity, TraitBranch.Rescue, "손재주", new[] { 10, 20, 30 },
                "자물쇠가 10% 빨리 열려요", "자물쇠가 20% 빨리 열려요", "자물쇠가 30% 빨리 열려요"),
            new TraitDefinition(TraitId.Loyalty, TraitBranch.Companion, "의리", new[] { 25 },
                "동료 원숭이가 경고 1번을 대신 막아요\n막은 동료는 잡혀 가요"),
            new TraitDefinition(TraitId.Sweat, TraitBranch.Exercise, "땀방울", new[] { 5, 15 },
                "한 판에 중강도 5분 이상이면\n원숭이 +1", "10분 이상이면 원숭이 +3"),
        };
        public static TraitDefinition Get(TraitId id) { foreach (var trait in All) if (trait.Id == id) return trait; throw new ArgumentOutOfRangeException(nameof(id)); }
        public static bool IsTrait(int id) => id >= (int)TraitId.Shield && id <= (int)TraitId.Sweat;
        public static int Code(TraitId id, int level) => (int)id * 10 + level;
        // The server's price list replaces the offline copy (a trait the server does not send keeps its copy).
        public static void ApplyServerCosts(int id, int[] costs)
        {
            if (!IsTrait(id) || costs == null || costs.Length == 0) return;
            foreach (int cost in costs) if (cost < 0) return;
            Get((TraitId)id).SetCosts((int[])costs.Clone());
        }
    }

    // The level of each trait an account owns (0 = locked). Built from the account's unlocked codes.
    public sealed class TraitLevels
    {
        private readonly int[] levels = new int[(int)TraitId.Sweat + 1];
        public int Level(TraitId id) => levels[(int)id];
        public bool Has(TraitId id) => levels[(int)id] > 0;
        public bool IsMaxed(TraitId id) => Level(id) >= TraitCatalog.Get(id).MaxLevel;
        public int NextCost(TraitId id) => TraitCatalog.Get(id).Cost(Level(id) + 1);
        public void Clear() { Array.Clear(levels, 0, levels.Length); }
        // Level n counts only when levels 1..n are all there (the server never stores a gap).
        public void Restore(IEnumerable<int> codes)
        {
            Clear();
            if (codes == null) return;
            var owned = new HashSet<int>(codes);
            foreach (var trait in TraitCatalog.All)
            {
                int level = 0;
                while (level < trait.MaxLevel && owned.Contains(TraitCatalog.Code(trait.Id, level + 1))) level++;
                levels[(int)trait.Id] = level;
            }
        }
        public void Set(TraitId id, int level) { levels[(int)id] = Math.Max(0, Math.Min(TraitCatalog.Get(id).MaxLevel, level)); }
        public RunTraits ForRun() => new RunTraits(this);
    }

    // The effects one run plays with, fixed at its start (plan 4-1). Other systems read these values:
    // the warning system (SharpEars lead, ThickFur, Loyalty through RunSession.GuardWarning), the prison lock (LockGaugeBonus),
    // the item spawner (ShieldItems, CallItems, ShieldWeight), the mine rescue (CallSeconds) and the result (SweatMonkeys).
    public sealed class RunTraits
    {
        public static readonly RunTraits None = new RunTraits(null);
        private readonly int[] levels = new int[(int)TraitId.Sweat + 1];
        public RunTraits(TraitLevels owned) { if (owned != null) foreach (var trait in TraitCatalog.All) levels[(int)trait.Id] = owned.Level(trait.Id); }
        public int Level(TraitId id) => levels[(int)id];

        // Shield: Lv1 the item appears, Lv2 twice as often, Lv3 the run starts holding one.
        public bool ShieldItems => Level(TraitId.Shield) >= 1;
        public float ShieldWeight => Level(TraitId.Shield) >= 2 ? 2f : 1f;
        public bool StartsWithShield => Level(TraitId.Shield) >= 3;
        // Sharp ears: the warning cue comes this many seconds earlier.
        public float WarningLeadSeconds => .3f * Level(TraitId.SharpEars);
        // Thick fur: the first warning of the run ends without the slip.
        public bool ThickFur => Level(TraitId.ThickFur) >= 1;
        // Call: Lv1 the item appears; while it lasts a monkey in the runner's lane is rescued without reaching out.
        public bool CallItems => Level(TraitId.Call) >= 1;
        public float CallSeconds => CallSecondsAt(Level(TraitId.Call));
        public static float CallSecondsAt(int level) => level >= 2 ? 10f : level == 1 ? 6f : 0f;
        // Dexterity: the prison lock gauge fills this much faster (.1 = 10%).
        public float LockGaugeBonus => .1f * Level(TraitId.Dexterity);
        // Loyalty: one following monkey takes one warning per run (and is taken away).
        public bool Loyalty => Level(TraitId.Loyalty) >= 1;
        // Sweat: extra monkeys for a run with enough moderate-intensity time.
        public int SweatMonkeys(double moderateSeconds) => SweatMonkeysAt(Level(TraitId.Sweat), moderateSeconds);
        public static int SweatMonkeysAt(int level, double moderateSeconds)
            => level >= 2 && moderateSeconds >= 600 ? 3 : level >= 1 && moderateSeconds >= 300 ? 1 : 0;
    }
}
