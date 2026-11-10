using System.Collections.Generic;
using KebabStreet.Core;
using NUnit.Framework;

namespace KebabStreet.Tests.EditMode
{
    /// <summary>
    /// Детермінізм: однаковий seed + однакова послідовність команд і Advance → однакові
    /// знімки на кожному кроці. Сценарій генерується окремим генератором із власним seed
    /// і містить усі команди (зокрема в неправильних станах), такти, кадри різної довжини
    /// і звернення «підсистеми» до генератора зміни. Результат цілі залежить від генератора.
    /// </summary>
    public sealed class ShiftSimulationDeterminismTests
    {
        private const int Steps = 3000;

        private static ShiftSimulation CreateShift(ulong seed)
        {
            ShiftConfig config = ShiftTestKit.Config(durationSec: 5, maxCatchUpTicks: 20, continueBonusSec: 2);
            return new ShiftSimulation(config, seed, new RandomGoal());
        }

        private static void ApplyStep(ShiftSimulation shift, IRandom script)
        {
            switch (script.NextInt(0, 12))
            {
                case 0:
                    shift.Start();
                    break;
                case 1:
                    shift.Pause();
                    break;
                case 2:
                    shift.Resume();
                    break;
                case 3:
                    shift.Continue();
                    break;
                case 4:
                    shift.Tick();
                    break;
                case 5:
                    shift.Random.NextUInt64();
                    break;
                default:
                    shift.Advance(script.NextInt(0, 4000) / 100000d);
                    break;
            }
        }

        private static List<ShiftSnapshot> RunScript(ulong shiftSeed, ulong scriptSeed)
        {
            ShiftSimulation shift = CreateShift(shiftSeed);
            var script = new SplitMix64Random(scriptSeed);
            var snapshots = new List<ShiftSnapshot>(Steps);
            for (int i = 0; i < Steps; i++)
            {
                ApplyStep(shift, script);
                snapshots.Add(shift.CaptureSnapshot());
            }

            return snapshots;
        }

        [TestCase(1UL, 100UL)]
        [TestCase(2026UL, 7UL)]
        [TestCase(6UL, 9UL)]
        [TestCase(ulong.MaxValue, 0UL)]
        public void SameSeedAndSameScript_GiveEqualSnapshotsAtEveryStep(ulong shiftSeed, ulong scriptSeed)
        {
            List<ShiftSnapshot> first = RunScript(shiftSeed, scriptSeed);
            List<ShiftSnapshot> second = RunScript(shiftSeed, scriptSeed);

            Assert.AreEqual(Steps, first.Count);
            for (int i = 0; i < Steps; i++)
            {
                Assert.AreEqual(first[i], second[i], "Розбіжність на кроці " + i);
            }
        }

        [Test]
        public void TwoShiftsSteppedSideBySide_StayEqual_AndDoNotShareState()
        {
            ShiftSimulation first = CreateShift(2026UL);
            ShiftSimulation second = CreateShift(2026UL);
            var firstScript = new SplitMix64Random(8UL);
            var secondScript = new SplitMix64Random(8UL);

            for (int i = 0; i < Steps; i++)
            {
                ApplyStep(first, firstScript);
                ApplyStep(second, secondScript);
                Assert.AreEqual(first.CaptureSnapshot(), second.CaptureSnapshot(), "Розбіжність на кроці " + i);
            }
        }

        [Test]
        public void Script_PassesThroughWholeShiftLifecycle()
        {
            // Захист самого тесту: сценарій справді доходить до кінця зміни, поразки і продовження.
            List<ShiftSnapshot> snapshots = RunScript(1UL, 100UL);
            var seen = new HashSet<ShiftState>();
            foreach (ShiftSnapshot snapshot in snapshots)
            {
                seen.Add(snapshot.State);
            }

            Assert.IsTrue(seen.Contains(ShiftState.Intro));
            Assert.IsTrue(seen.Contains(ShiftState.Running));
            Assert.IsTrue(seen.Contains(ShiftState.Paused));
            Assert.IsTrue(seen.Contains(ShiftState.Lost));
            Assert.IsTrue(seen.Contains(ShiftState.Won));
            Assert.IsTrue(snapshots[Steps - 1].ContinueUsed);
        }

        [Test]
        public void Script_FinalSnapshot_MatchesGoldenValue()
        {
            // Обчислено незалежною реалізацією (Node.js, BigInt). Ловить будь-яку зміну
            // поведінки таймера, Advance чи генератора між рантаймами.
            List<ShiftSnapshot> snapshots = RunScript(2026UL, 7UL);

            Assert.AreEqual(
                new ShiftSnapshot(ShiftState.Lost, 420L, 0L, 0L, true, 0x007DC9D4DACAF707UL),
                snapshots[Steps - 1]);
        }

        [Test]
        public void DifferentSeeds_GiveDifferentRuns()
        {
            List<ShiftSnapshot> first = RunScript(5UL, 9UL);
            List<ShiftSnapshot> second = RunScript(6UL, 9UL);

            Assert.AreNotEqual(first[0], second[0], "Стан генератора різний від першого кроку");

            // Той самий сценарій, інший seed: одна зміна програла й продовжилась, інша виграла одразу.
            Assert.AreEqual(420L, first[Steps - 1].ElapsedTicks);
            Assert.AreEqual(300L, second[Steps - 1].ElapsedTicks);
        }

        [Test]
        public void GoalOutcome_DependsOnlyOnSeed()
        {
            var outcomes = new char[16];
            for (int i = 0; i < outcomes.Length; i++)
            {
                ShiftSimulation shift = CreateShift((ulong)(i + 1));
                shift.Start();
                ShiftTestKit.TickUntilNotRunning(shift);
                outcomes[i] = shift.State == ShiftState.Won ? 'W' : 'L';
            }

            Assert.AreEqual("LWLWWWLWWWLLLWLL", new string(outcomes));
        }

        [Test]
        public void Snapshot_EqualityCoversEveryField()
        {
            var snapshot = new ShiftSnapshot(ShiftState.Running, 10L, 20L, 30L, false, 40UL);
            var same = new ShiftSnapshot(ShiftState.Running, 10L, 20L, 30L, false, 40UL);

            Assert.IsTrue(snapshot.Equals(same));
            Assert.IsTrue(snapshot.Equals((object)same));
            Assert.AreEqual(snapshot.GetHashCode(), same.GetHashCode());

            Assert.IsFalse(snapshot.Equals(new ShiftSnapshot(ShiftState.Paused, 10L, 20L, 30L, false, 40UL)));
            Assert.IsFalse(snapshot.Equals(new ShiftSnapshot(ShiftState.Running, 11L, 20L, 30L, false, 40UL)));
            Assert.IsFalse(snapshot.Equals(new ShiftSnapshot(ShiftState.Running, 10L, 21L, 30L, false, 40UL)));
            Assert.IsFalse(snapshot.Equals(new ShiftSnapshot(ShiftState.Running, 10L, 20L, 31L, false, 40UL)));
            Assert.IsFalse(snapshot.Equals(new ShiftSnapshot(ShiftState.Running, 10L, 20L, 30L, true, 40UL)));
            Assert.IsFalse(snapshot.Equals(new ShiftSnapshot(ShiftState.Running, 10L, 20L, 30L, false, 41UL)));
            Assert.IsFalse(snapshot.Equals("не знімок"));
        }

        [Test]
        public void Snapshot_ToString_IsStableAndCultureIndependent()
        {
            var snapshot = new ShiftSnapshot(ShiftState.Lost, 1234567L, 0L, 500000L, true, 255UL);

            Assert.AreEqual(
                "Lost elapsed=1234567 remaining=0 pending=500000 continueUsed=True random=0x00000000000000FF",
                snapshot.ToString());
        }
    }
}
