using System;
using System.Collections.Generic;
using System.Globalization;

namespace Mongscape.Domain
{
    // Expresses a run's estimated kcal as "how many of a familiar food", for the result screen.
    // Per-serving kcal are rounded everyday approximations (Korean food composition tables), not exact nutrition data.
    public static class FoodEquivalent
    {
        public readonly struct Food
        {
            public readonly string Name, Unit; public readonly double Kcal;
            public Food(string name, string unit, double kcal) { Name = name; Unit = unit; Kcal = kcal; }
        }

        public readonly struct Portion
        {
            public readonly Food Food; public readonly double Count;
            public Portion(Food food, double count) { Food = food; Count = count; }
            // One decimal place (owner 2026-11-27): "1.0개", "2.2개".
            public string CountText => Count.ToString("F1", CultureInfo.InvariantCulture) + Food.Unit;
            // The result screen line: "바나나 1.0개만큼".
            public string Phrase => Food.Name + " " + CountText + "만큼";
        }

        // Lowest kcal first; selection keeps this order.
        public static readonly Food[] Table =
        {
            new Food("방울토마토", "개", 3),
            new Food("사탕", "개", 20),
            new Food("귤", "개", 40),
            new Food("삶은 달걀", "개", 75),
            new Food("바나나", "개", 90),
            new Food("콜라 250ml", "캔", 110),
            new Food("우유 200ml", "잔", 130),
            new Food("밥", "공기", 300),
            new Food("라면", "봉지", 500),
        };

        // Counts outside this range read badly ("0.03공기", "45개"), so those foods are skipped.
        public const double MinCount = 0.5, MaxCount = 10;
        public const int DefaultMax = 5;

        // Up to max foods whose count falls in [MinCount, MaxCount], the count closest to one first (the result shows the
        // first: "바나나 1.0개만큼"). When none does, the single closest food (smallest for a tiny run, largest for a huge one).
        // Nothing for zero or invalid kcal.
        public static List<Portion> For(double kcal, int max = DefaultMax)
        {
            var result = new List<Portion>();
            if (double.IsNaN(kcal) || double.IsInfinity(kcal) || kcal <= 0 || max <= 0) return result;
            foreach (var food in Table)
            {
                double count = kcal / food.Kcal;
                if (count >= MinCount && count <= MaxCount) result.Add(new Portion(food, count));
            }
            if (result.Count > 0)
            {
                // Stable: equal distances keep the table order.
                var ordered = new List<Portion>(result);
                ordered.Sort((a, b) => { int c = Math.Abs(a.Count - 1).CompareTo(Math.Abs(b.Count - 1)); return c != 0 ? c : Array.IndexOf(Table, a.Food).CompareTo(Array.IndexOf(Table, b.Food)); });
                if (ordered.Count > max) ordered.RemoveRange(max, ordered.Count - max);
                return ordered;
            }
            var nearest = kcal / Table[0].Kcal < MinCount ? Table[0] : Table[Table.Length - 1];
            result.Add(new Portion(nearest, kcal / nearest.Kcal));
            return result;
        }
        // The one food for the result screen, or null when there is nothing to compare.
        public static Portion? Closest(double kcal) { var list = For(kcal, 1); return list.Count > 0 ? list[0] : (Portion?)null; }
    }
}
