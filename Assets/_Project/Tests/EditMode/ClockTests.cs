using System;
using KebabStreet.Core;
using KebabStreet.Services;
using NUnit.Framework;

namespace KebabStreet.Tests.EditMode
{
    /// <summary>Годинник і захист від переведення часу назад (E0-07).</summary>
    public sealed class ClockTests
    {
        private const long Day = TimeGuard.SecondsPerDay;
        private const long Noon = (19700 * Day) + (12 * 3600);

        [Test]
        public void EffectiveNow_ClockForward_FollowsClock()
        {
            Assert.AreEqual(Noon + 1, TimeGuard.EffectiveNow(Noon, Noon + 1));
            Assert.AreEqual(Noon + (3 * Day), TimeGuard.EffectiveNow(Noon, Noon + (3 * Day)));
        }

        [Test]
        public void EffectiveNow_ClockBackward_DoesNotDecrease()
        {
            Assert.AreEqual(Noon, TimeGuard.EffectiveNow(Noon, Noon - 1));
            Assert.AreEqual(Noon, TimeGuard.EffectiveNow(Noon, Noon - (400 * Day)));
            Assert.AreEqual(Noon, TimeGuard.EffectiveNow(Noon, 0));
        }

        [Test]
        public void EffectiveNow_SameTime_StaysTheSame()
        {
            Assert.AreEqual(Noon, TimeGuard.EffectiveNow(Noon, Noon));
        }

        [Test]
        public void Observe_UpdatesLastKnownTimeOnlyForward()
        {
            var data = new SaveData { LastKnownUnixSec = Noon };

            Assert.AreEqual(Noon + 30, TimeGuard.Observe(data, Noon + 30));
            Assert.AreEqual(Noon + 30, data.LastKnownUnixSec);

            Assert.AreEqual(Noon + 30, TimeGuard.Observe(data, Noon - Day));
            Assert.AreEqual(Noon + 30, data.LastKnownUnixSec);

            Assert.AreEqual(Noon + 31, TimeGuard.Observe(data, Noon + 31));
            Assert.AreEqual(Noon + 31, data.LastKnownUnixSec);
        }

        [Test]
        public void Observe_NullData_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => TimeGuard.Observe(null, Noon));
        }

        [TestCase(0L, 0L)]
        [TestCase(1L, 0L)]
        [TestCase(86399L, 0L)]
        [TestCase(86400L, 1L)]
        [TestCase(86401L, 1L)]
        [TestCase(1700000000L, 19675L)]
        [TestCase(-1L, -1L)]
        [TestCase(-86400L, -1L)]
        [TestCase(-86401L, -2L)]
        public void DayIndex_RoundsDown(long unixSec, long expectedDay)
        {
            Assert.AreEqual(expectedDay, TimeGuard.DayIndex(unixSec));
        }

        [Test]
        public void ClockSetBackAfterNewDay_DoesNotGiveThatDayAgain()
        {
            var clock = new ManualClock(Noon);
            var data = new SaveData();
            long dayOfFirstVisit = TimeGuard.DayIndex(TimeGuard.Observe(data, clock.UtcNowUnixSec));

            // Гравець перевів годинник на добу вперед: настав «новий день».
            clock.Advance(Day);
            long nextDay = TimeGuard.DayIndex(TimeGuard.Observe(data, clock.UtcNowUnixSec));
            Assert.AreEqual(dayOfFirstVisit + 1, nextDay);

            // Перевів на дві доби назад і знову пішов уперед: ефективний день не повертається і не настає вдруге.
            clock.Advance(-2 * Day);
            Assert.AreEqual(nextDay, TimeGuard.DayIndex(TimeGuard.Observe(data, clock.UtcNowUnixSec)));
            clock.Advance(Day);
            Assert.AreEqual(nextDay, TimeGuard.DayIndex(TimeGuard.Observe(data, clock.UtcNowUnixSec)));
            clock.Advance(Day);
            Assert.AreEqual(nextDay, TimeGuard.DayIndex(TimeGuard.Observe(data, clock.UtcNowUnixSec)));

            // Справжній час наздогнав і пішов далі: звичайний хід.
            clock.Advance(Day);
            Assert.AreEqual(nextDay + 1, TimeGuard.DayIndex(TimeGuard.Observe(data, clock.UtcNowUnixSec)));
        }

        [Test]
        public void ClockGuard_SurvivesSaveAndLoad()
        {
            var storage = new InMemorySaveStorage();
            var clock = new ManualClock(Noon);
            var service = new SaveService(storage, clock);
            Assert.AreEqual(SaveWriteResult.Saved, service.Save(new SaveData()));

            // Гру закрито, годинник переведено на тиждень назад, гру відкрито знову.
            clock.Advance(-7 * Day);
            SaveData loaded = new SaveService(storage, clock).Load().Data;

            long effective = TimeGuard.Observe(loaded, clock.UtcNowUnixSec);
            Assert.AreEqual(Noon, effective);
            Assert.AreEqual(TimeGuard.DayIndex(Noon), TimeGuard.DayIndex(effective));
        }

        [Test]
        public void ManualClock_ChangesOnlyExplicitly()
        {
            var clock = new ManualClock(1000);
            Assert.AreEqual(1000, clock.UtcNowUnixSec);
            Assert.AreEqual(1000, clock.UtcNowUnixSec);

            clock.Advance(50);
            Assert.AreEqual(1050, clock.UtcNowUnixSec);

            clock.Advance(-200);
            Assert.AreEqual(850, clock.UtcNowUnixSec);

            clock.UtcNowUnixSec = 5;
            Assert.AreEqual(5, ((IClock)clock).UtcNowUnixSec);
        }

        [Test]
        public void SystemClock_ReturnsCurrentUtcUnixSeconds()
        {
            IClock clock = new SystemClock();

            long before = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            long value = clock.UtcNowUnixSec;
            long after = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            Assert.GreaterOrEqual(value, before);
            Assert.LessOrEqual(value, after);
            Assert.Greater(value, 1700000000L, "Час має бути в секундах Unix, а не в мілісекундах чи тактах");
            Assert.Less(value, 4102444800L, "Час має бути в секундах Unix (до 2100 року)");
        }
    }
}
