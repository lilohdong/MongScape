using System;
using Mongscape.Domain;
using NUnit.Framework;

namespace Mongscape.Tests
{
    // The profile's week card: goal minutes per weekday and active minutes per map.
    public sealed class WeekCardTests
    {
        [Test] public void DaysStartOnMondayAndMapsSumThisWeek()
        {
            var log = new ExerciseLog();
            // 2026-11-27 is a Friday (KST); 03:00 UTC is noon in Korea.
            var friday = new DateTime(2026, 11, 27, 3, 0, 0, DateTimeKind.Utc);
            var monday = friday.AddDays(-4);
            log.Add("a", monday, 600, 720, new double[] { 60, 120, 300, 120 });
            log.Add("a", friday, 300, 600, new double[] { 0, 0, 0, 300 });
            log.Add("b", friday, 999, 999, new double[] { 999, 999, 999, 999 });
            var days = log.WeekDayMinutes("a", friday);
            Assert.AreEqual(7, days.Length);
            Assert.AreEqual(12, days[0], 1e-9); Assert.AreEqual(10, days[4], 1e-9); Assert.AreEqual(0, days[6], 1e-9);
            var maps = log.WeekMapMinutes("a", friday);
            Assert.AreEqual(5, maps[0], 1e-9, "zoo = cardio");
            Assert.AreEqual(2, maps[1], 1e-9, "prison = strength");
            Assert.AreEqual(8, maps[2], 1e-9, "mine = warm-up and balance");
        }
    }
}
