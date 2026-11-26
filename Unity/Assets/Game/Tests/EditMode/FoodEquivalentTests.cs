using Mongscape.Domain;
using NUnit.Framework;

namespace Mongscape.Tests
{
    public sealed class FoodEquivalentTests
    {
        [TestCase(0)][TestCase(-5)][TestCase(double.NaN)][TestCase(double.PositiveInfinity)]
        public void NoFoodsForZeroOrInvalidKcal(double kcal) => Assert.AreEqual(0, FoodEquivalent.For(kcal).Count);

        [Test] public void ThreeMinuteRunShowsTheFoodClosestToOneWithOneDecimal()
        {
            // 3 minutes at 4 MET, 65 kg = 13.65 kcal.
            var foods = FoodEquivalent.For(13.65);
            Assert.AreEqual(2, foods.Count, "candy 0.68 and tomato 4.55; 귤 0.34 is below the minimum");
            Assert.AreEqual("사탕", foods[0].Food.Name); Assert.AreEqual("0.7개", foods[0].CountText);
            Assert.AreEqual("방울토마토", foods[1].Food.Name); Assert.AreEqual("4.6개", foods[1].CountText);
            Assert.AreEqual("사탕 0.7개만큼", FoodEquivalent.Closest(13.65).Value.Phrase);
        }

        [Test] public void OnlyFoodsInReadableRangeClosestToOneFirstAndAtMostFive()
        {
            var foods = FoodEquivalent.For(100);
            Assert.AreEqual(5, foods.Count);
            string[] names = { "콜라 250ml", "바나나", "우유 200ml", "삶은 달걀", "귤" };
            for (int i = 0; i < 5; i++)
            {
                Assert.AreEqual(names[i], foods[i].Food.Name);
                Assert.That(foods[i].Count, Is.InRange(FoodEquivalent.MinCount, FoodEquivalent.MaxCount));
            }
            Assert.AreEqual("0.9캔", foods[0].CountText);
            Assert.AreEqual("1.1개", foods[1].CountText);
            Assert.AreEqual("바나나 1.0개만큼", FoodEquivalent.Closest(90).Value.Phrase);
        }

        [Test] public void FallsBackToNearestFoodOutsideTheTable()
        {
            var tiny = FoodEquivalent.For(1);
            Assert.AreEqual(1, tiny.Count); Assert.AreEqual("방울토마토", tiny[0].Food.Name); Assert.AreEqual("0.3개", tiny[0].CountText);
            var huge = FoodEquivalent.For(6000);
            Assert.AreEqual(1, huge.Count); Assert.AreEqual("라면", huge[0].Food.Name); Assert.AreEqual("12.0봉지", huge[0].CountText);
            Assert.IsNull(FoodEquivalent.Closest(0));
        }

        [Test] public void TableIsSortedByKcal()
        {
            for (int i = 1; i < FoodEquivalent.Table.Length; i++)
                Assert.Greater(FoodEquivalent.Table[i].Kcal, FoodEquivalent.Table[i - 1].Kcal);
        }
    }
}
