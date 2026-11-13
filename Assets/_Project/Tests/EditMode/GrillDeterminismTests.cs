using System.Collections.Generic;
using KebabStreet.Core;
using NUnit.Framework;

namespace KebabStreet.Tests.EditMode
{
    /// <summary>
    /// Детермінізм зміни з грилем: однаковий seed + однакова послідовність команд, тактів
    /// і кадрів → однакові знімки на кожному кроці. Сценарій генерується окремим генератором
    /// і містить усі команди гриля (зокрема неможливі й у неправильних станах), скасування,
    /// паузи й продовження. «Золоті» значення обчислено незалежною моделлю на Node.js.
    /// </summary>
    public sealed class GrillDeterminismTests
    {
        private const int Steps = 3000;

        // За 60 тактів на секунду: приготування 30 тактів, пригорання через 42, дія 3 такти.
        private static ShiftSimulation CreateShift(ulong seed)
        {
            var grill = new GrillConfig(3, 500, 700, 200, 4, 50);
            var config = new ShiftConfig(ShiftTestKit.TicksPerSecond, 20, 5, 2, 3, grill);
            return new ShiftSimulation(config, seed, new RandomGoal());
        }

        private static void ApplyStep(ShiftSimulation shift, IRandom script)
        {
            switch (script.NextInt(0, 16))
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
                    shift.PlaceMeat(script.NextInt(-1, 3));
                    break;
                case 5:
                    shift.PlaceMeat();
                    break;
                case 6:
                case 7:
                    // Номер 3 — слота немає: команда має бути відхилена.
                    shift.TakeMeat(script.NextInt(-1, 4));
                    break;
                case 8:
                    shift.DiscardBurntMeat(script.NextInt(-1, 3));
                    break;
                case 9:
                    shift.CancelQueuedAction(script.NextInt(0, 3));
                    break;
                case 10:
                    shift.Random.NextUInt64();
                    break;
                case 11:
                    shift.Tick();
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

        private static ShiftAction Action(ShiftActionType type, int slot)
        {
            return new ShiftAction(type, slot);
        }

        [TestCase(5UL, 9UL)]
        [TestCase(6UL, 9UL)]
        [TestCase(2026UL, 7UL)]
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
            ShiftSimulation first = CreateShift(5UL);
            ShiftSimulation second = CreateShift(5UL);
            var firstScript = new SplitMix64Random(9UL);
            var secondScript = new SplitMix64Random(9UL);

            for (int i = 0; i < Steps; i++)
            {
                ApplyStep(first, firstScript);
                ApplyStep(second, secondScript);
                Assert.AreEqual(first.CaptureSnapshot(), second.CaptureSnapshot(), "Розбіжність на кроці " + i);
            }
        }

        [Test]
        public void Script_CoversEveryMeatState_EveryActionResult_AndWholeShiftLifecycle()
        {
            // Захист самого тесту: сценарій справді доходить до всіх станів і результатів.
            List<ShiftSnapshot> snapshots = RunScript(5UL, 9UL);
            var shiftStates = new HashSet<ShiftState>();
            var meatStates = new HashSet<MeatState>();
            var results = new HashSet<ShiftActionResult>();
            int longestQueue = 0;
            foreach (ShiftSnapshot snapshot in snapshots)
            {
                shiftStates.Add(snapshot.State);
                results.Add(snapshot.Actions.LastResult);
                if (snapshot.Actions.Count > longestQueue)
                {
                    longestQueue = snapshot.Actions.Count;
                }

                for (int slot = 0; slot < snapshot.Grill.SlotCount; slot++)
                {
                    meatStates.Add(snapshot.Grill.GetState(slot));
                }
            }

            Assert.AreEqual(5, shiftStates.Count, "Intro, Running, Paused, Lost, Won");
            Assert.AreEqual(4, meatStates.Count, "Empty, Raw, Done, Burnt");
            Assert.AreEqual(10, results.Count, "Усі значення ShiftActionResult");
            Assert.AreEqual(3, longestQueue, "Черга заповнювалась до межі");
            Assert.IsTrue(snapshots[Steps - 1].ContinueUsed);
        }

        [Test]
        public void Script_SnapshotInTheMiddle_MatchesGoldenValue()
        {
            List<ShiftSnapshot> snapshots = RunScript(5UL, 9UL);

            var grill = new GrillSnapshot(
                new[] { MeatState.Raw, MeatState.Raw, MeatState.Burnt },
                new[] { 10L, 1L, 72L },
                8L, 5L, 1L, 0L, 0L);
            var actions = new ActionQueueSnapshot(
                new[]
                {
                    Action(ShiftActionType.PlaceMeat, 0),
                    Action(ShiftActionType.TakeMeat, GrillSlot.Any),
                    Action(ShiftActionType.TakeMeat, 2),
                },
                1L, 13L, 41L, Action(ShiftActionType.PlaceMeat, GrillSlot.Any), ShiftActionResult.Done);

            Assert.AreEqual(
                new ShiftSnapshot(ShiftState.Running, 181L, 119L, 186200L, false, 0xF8782311CA1CD641UL, grill, actions),
                snapshots[999]);
        }

        [Test]
        public void Script_SnapshotAfterContinue_MatchesGoldenValue()
        {
            List<ShiftSnapshot> snapshots = RunScript(5UL, 9UL);

            var grill = new GrillSnapshot(
                new[] { MeatState.Raw, MeatState.Burnt, MeatState.Empty },
                new[] { 29L, 72L, 0L },
                14L, 10L, 3L, 2L, 8L);
            var actions = new ActionQueueSnapshot(
                new[]
                {
                    Action(ShiftActionType.PlaceMeat, 1),
                    Action(ShiftActionType.DiscardBurntMeat, 0),
                    Action(ShiftActionType.PlaceMeat, GrillSlot.Any),
                },
                0L, 26L, 74L, Action(ShiftActionType.TakeMeat, GrillSlot.Any), ShiftActionResult.Done);

            Assert.AreEqual(
                new ShiftSnapshot(ShiftState.Running, 339L, 81L, 789400L, true, 0x1BBCDCBFA53E0A85UL, grill, actions),
                snapshots[1999]);
        }

        [Test]
        public void Script_FinalSnapshot_MatchesGoldenValue()
        {
            List<ShiftSnapshot> snapshots = RunScript(5UL, 9UL);

            var grill = new GrillSnapshot(
                new[] { MeatState.Raw, MeatState.Raw, MeatState.Raw },
                new[] { 1L, 19L, 13L },
                20L, 14L, 3L, 3L, 12L);
            var actions = new ActionQueueSnapshot(
                new[]
                {
                    Action(ShiftActionType.DiscardBurntMeat, 2),
                    Action(ShiftActionType.PlaceMeat, GrillSlot.Any),
                    Action(ShiftActionType.DiscardBurntMeat, 0),
                },
                1L, 37L, 89L, Action(ShiftActionType.PlaceMeat, GrillSlot.Any), ShiftActionResult.Done);

            Assert.AreEqual(
                new ShiftSnapshot(ShiftState.Won, 420L, 0L, 0L, true, 0x10D6D52E877665F7UL, grill, actions),
                snapshots[Steps - 1]);
        }

        [Test]
        public void DifferentSeeds_GiveDifferentRuns_ButGrillItselfDoesNotDependOnSeed()
        {
            List<ShiftSnapshot> first = RunScript(5UL, 9UL);
            List<ShiftSnapshot> second = RunScript(6UL, 9UL);

            Assert.AreNotEqual(first[0], second[0], "Стан генератора різний від першого кроку");

            // Гриль не бере випадковості: до кінця першого таймера зміни (де ціль уперше
            // звертається до генератора) гриль і черга в обох змінах однакові.
            Assert.AreEqual(first[999].Grill, second[999].Grill);
            Assert.AreEqual(first[999].Actions, second[999].Actions);

            // Далі той самий сценарій розходиться: одна зміна програла й продовжилась, інша виграла.
            Assert.AreEqual(420L, first[Steps - 1].ElapsedTicks);
            Assert.AreEqual(300L, second[Steps - 1].ElapsedTicks);
            Assert.AreEqual(20L, first[Steps - 1].Grill.PlacedCount);
            Assert.AreEqual(13L, second[Steps - 1].Grill.PlacedCount);
        }

        [Test]
        public void GrillSnapshot_EqualityCoversEveryField()
        {
            MeatState[] states = { MeatState.Raw, MeatState.Done };
            long[] ages = { 5L, 40L };
            var snapshot = new GrillSnapshot(states, ages, 1L, 2L, 3L, 4L, 5L);
            var same = new GrillSnapshot(
                new[] { MeatState.Raw, MeatState.Done }, new[] { 5L, 40L }, 1L, 2L, 3L, 4L, 5L);

            Assert.IsTrue(snapshot.Equals(same));
            Assert.IsTrue(snapshot.Equals((object)same));
            Assert.AreEqual(snapshot.GetHashCode(), same.GetHashCode());

            Assert.IsFalse(snapshot.Equals(new GrillSnapshot(
                new[] { MeatState.Raw, MeatState.Burnt }, ages, 1L, 2L, 3L, 4L, 5L)));
            Assert.IsFalse(snapshot.Equals(new GrillSnapshot(states, new[] { 5L, 41L }, 1L, 2L, 3L, 4L, 5L)));
            Assert.IsFalse(snapshot.Equals(new GrillSnapshot(
                new[] { MeatState.Raw }, new[] { 5L }, 1L, 2L, 3L, 4L, 5L)));
            Assert.IsFalse(snapshot.Equals(new GrillSnapshot(states, ages, 9L, 2L, 3L, 4L, 5L)));
            Assert.IsFalse(snapshot.Equals(new GrillSnapshot(states, ages, 1L, 9L, 3L, 4L, 5L)));
            Assert.IsFalse(snapshot.Equals(new GrillSnapshot(states, ages, 1L, 2L, 9L, 4L, 5L)));
            Assert.IsFalse(snapshot.Equals(new GrillSnapshot(states, ages, 1L, 2L, 3L, 9L, 5L)));
            Assert.IsFalse(snapshot.Equals(new GrillSnapshot(states, ages, 1L, 2L, 3L, 4L, 9L)));
            Assert.IsFalse(snapshot.Equals(default(GrillSnapshot)));
            Assert.IsFalse(snapshot.Equals("не знімок"));
        }

        [Test]
        public void GrillSnapshot_CopiesItsArrays_AndRejectsMismatchedOnes()
        {
            MeatState[] states = { MeatState.Done };
            long[] ages = { 40L };
            var snapshot = new GrillSnapshot(states, ages, 0L, 0L, 0L, 0L, 0L);

            states[0] = MeatState.Burnt;
            ages[0] = 99L;

            Assert.AreEqual(MeatState.Done, snapshot.GetState(0));
            Assert.AreEqual(40L, snapshot.GetAgeTicks(0));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => snapshot.GetState(1));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => snapshot.GetAgeTicks(-1));
            Assert.Throws<System.ArgumentException>(
                () => new GrillSnapshot(new MeatState[2], new long[1], 0L, 0L, 0L, 0L, 0L));
            Assert.AreEqual(default(GrillSnapshot), new GrillSnapshot(null, null, 0L, 0L, 0L, 0L, 0L));
        }

        [Test]
        public void ShiftSnapshot_EqualityCoversGrillAndActions()
        {
            var grill = new GrillSnapshot(new[] { MeatState.Raw }, new[] { 5L }, 1L, 0L, 0L, 0L, 0L);
            var otherGrill = new GrillSnapshot(new[] { MeatState.Raw }, new[] { 6L }, 1L, 0L, 0L, 0L, 0L);
            var actions = new ActionQueueSnapshot(
                new[] { Action(ShiftActionType.TakeMeat, 0) }, 0L, 1L, 0L,
                Action(ShiftActionType.PlaceMeat, 0), ShiftActionResult.Done);
            var otherActions = new ActionQueueSnapshot(
                null, 0L, 1L, 0L, Action(ShiftActionType.PlaceMeat, 0), ShiftActionResult.Done);

            var snapshot = new ShiftSnapshot(ShiftState.Running, 10L, 20L, 30L, false, 40UL, grill, actions);
            var same = new ShiftSnapshot(ShiftState.Running, 10L, 20L, 30L, false, 40UL, grill, actions);

            Assert.IsTrue(snapshot.Equals(same));
            Assert.AreEqual(snapshot.GetHashCode(), same.GetHashCode());
            Assert.AreEqual(grill, snapshot.Grill);
            Assert.AreEqual(actions, snapshot.Actions);
            Assert.IsFalse(snapshot.Equals(
                new ShiftSnapshot(ShiftState.Running, 10L, 20L, 30L, false, 40UL, otherGrill, actions)));
            Assert.IsFalse(snapshot.Equals(
                new ShiftSnapshot(ShiftState.Running, 10L, 20L, 30L, false, 40UL, grill, otherActions)));
            Assert.IsFalse(snapshot.Equals(new ShiftSnapshot(ShiftState.Running, 10L, 20L, 30L, false, 40UL)));
        }

        [Test]
        public void ShiftSnapshot_ToString_ShowsGrillAndActions_AndIsCultureIndependent()
        {
            var grill = new GrillSnapshot(
                new[] { MeatState.Raw, MeatState.Empty, MeatState.Burnt }, new[] { 1234L, 0L, 72L }, 5L, 2L, 1L, 1L, 4L);
            var actions = new ActionQueueSnapshot(
                new[] { Action(ShiftActionType.TakeMeat, 0), Action(ShiftActionType.PlaceMeat, GrillSlot.Any) },
                2L, 6L, 3L, Action(ShiftActionType.DiscardBurntMeat, 2), ShiftActionResult.Done);
            var snapshot = new ShiftSnapshot(ShiftState.Paused, 1500L, 300L, 0L, false, 255UL, grill, actions);

            Assert.AreEqual(
                "Paused elapsed=1500 remaining=300 pending=0 continueUsed=False random=0x00000000000000FF"
                + " grill=[Raw:1234 Empty:0 Burnt:72] placed=5 taken=2 burnt=1 discarded=1 penalty=4"
                + " actions=[TakeMeat:0 PlaceMeat:-1] progress=2 executed=6 ignored=3 last=DiscardBurntMeat:2>Done",
                snapshot.ToString());
        }
    }
}
