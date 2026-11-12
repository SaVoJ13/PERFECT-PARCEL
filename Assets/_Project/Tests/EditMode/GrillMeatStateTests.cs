using KebabStreet.Core;
using NUnit.Framework;

namespace KebabStreet.Tests.EditMode
{
    /// <summary>
    /// Стани м'яса на грилі (GAME_DESIGN.md, розд. 4): Empty → Raw → Done → Burnt.
    /// Кожен перехід перевіряється на межі таймера: такт до, такт переходу, такт після.
    /// </summary>
    public sealed class GrillMeatStateTests
    {
        private const long Cook = GrillTestKit.CookTicks;
        private const long BurntAt = GrillTestKit.BurntAtTicks;
        private const long Warn = GrillTestKit.WarnTicks;

        [Test]
        public void NewGrill_HasAllSlotsEmpty_AndZeroCounters()
        {
            ShiftSimulation shift = GrillTestKit.Create();

            Assert.AreEqual(GrillTestKit.Slots, shift.Grill.SlotCount);
            Assert.AreEqual(GrillTestKit.Slots, shift.Grill.FreeSlotCount);
            for (int i = 0; i < shift.Grill.SlotCount; i++)
            {
                GrillSlot slot = shift.Grill.GetSlot(i);
                Assert.AreEqual(i, slot.Index);
                Assert.AreEqual(MeatState.Empty, slot.State);
                Assert.AreEqual(0L, slot.AgeTicks);
                Assert.AreEqual(0L, slot.TicksUntilDone);
                Assert.AreEqual(0L, slot.TicksUntilBurnt);
                Assert.IsFalse(slot.IsBurnWarning);
            }

            Assert.AreEqual(
                new GrillSnapshot(new MeatState[GrillTestKit.Slots], new long[GrillTestKit.Slots], 0L, 0L, 0L, 0L, 0L),
                shift.Grill.CaptureSnapshot());
        }

        [Test]
        public void EmptySlots_StayEmpty_WhileTimeRuns()
        {
            ShiftSimulation shift = GrillTestKit.CreateRunning();
            GrillSnapshot before = shift.Grill.CaptureSnapshot();

            GrillTestKit.Tick(shift, BurntAt * 2);

            Assert.AreEqual(before, shift.Grill.CaptureSnapshot());
        }

        [Test]
        public void PlacedMeat_IsRaw_WithZeroAge_OnTickOfPlacement()
        {
            ShiftSimulation shift = GrillTestKit.CreateRunning();

            Assert.IsTrue(shift.PlaceMeat(1));
            Assert.AreEqual(MeatState.Empty, GrillTestKit.StateOf(shift, 1), "До такту команда лише в черзі");

            shift.Tick();

            GrillSlot slot = shift.Grill.GetSlot(1);
            Assert.AreEqual(MeatState.Raw, slot.State);
            Assert.AreEqual(0L, slot.AgeTicks);
            Assert.AreEqual(Cook, slot.TicksUntilDone);
            Assert.AreEqual(BurntAt, slot.TicksUntilBurnt);
            Assert.AreEqual(1L, shift.Grill.PlacedCount);
        }

        [Test]
        public void Raw_BecomesDone_ExactlyWhenCookTimeElapses()
        {
            ShiftSimulation shift = GrillTestKit.CreateRunning();
            GrillTestKit.PlaceAndAge(shift, 0, Cook - 1);
            GrillSlot slot = shift.Grill.GetSlot(0);

            Assert.AreEqual(MeatState.Raw, slot.State, "За такт до готовності");
            Assert.AreEqual(1L, slot.TicksUntilDone);

            shift.Tick();

            Assert.AreEqual(MeatState.Done, slot.State, "На такті готовності");
            Assert.AreEqual(Cook, slot.AgeTicks);
            Assert.AreEqual(0L, slot.TicksUntilDone);
            Assert.AreEqual(GrillTestKit.BurnTicks, slot.TicksUntilBurnt);

            shift.Tick();

            Assert.AreEqual(MeatState.Done, slot.State, "Такт після готовності");
            Assert.AreEqual(0L, shift.Grill.BurntCount);
        }

        [Test]
        public void Done_BecomesBurnt_ExactlyWhenBurnWindowElapses()
        {
            ShiftSimulation shift = GrillTestKit.CreateRunning();
            GrillTestKit.PlaceAndAge(shift, 0, BurntAt - 1);
            GrillSlot slot = shift.Grill.GetSlot(0);

            Assert.AreEqual(MeatState.Done, slot.State, "За такт до пригорання");
            Assert.AreEqual(1L, slot.TicksUntilBurnt);
            Assert.AreEqual(0L, shift.Grill.BurntCount);

            shift.Tick();

            Assert.AreEqual(MeatState.Burnt, slot.State, "На такті пригорання");
            Assert.AreEqual(BurntAt, slot.AgeTicks);
            Assert.AreEqual(0L, slot.TicksUntilBurnt);
            Assert.AreEqual(1L, shift.Grill.BurntCount);
        }

        [Test]
        public void Burnt_StaysBurnt_OccupiesSlot_AndIsCountedOnce()
        {
            ShiftSimulation shift = GrillTestKit.CreateRunning();
            GrillTestKit.PlaceAndAge(shift, 2, BurntAt);
            GrillSnapshot burnt = shift.Grill.CaptureSnapshot();

            GrillTestKit.Tick(shift, BurntAt * 3);

            Assert.AreEqual(burnt, shift.Grill.CaptureSnapshot(), "Пригоріле не змінюється само");
            Assert.AreEqual(MeatState.Burnt, GrillTestKit.StateOf(shift, 2));
            Assert.AreEqual(BurntAt, shift.Grill.GetSlot(2).AgeTicks);
            Assert.AreEqual(1L, shift.Grill.BurntCount);
            Assert.AreEqual(GrillTestKit.Slots - 1, shift.Grill.FreeSlotCount);
            Assert.AreEqual(0L, shift.Grill.PenaltyCoins, "Штраф лише за викинуте");
        }

        [Test]
        public void WholeLifeOfMeat_HasExactStateOnEveryTick()
        {
            ShiftSimulation shift = GrillTestKit.CreateRunning();
            GrillTestKit.Place(shift, 0);
            GrillSlot slot = shift.Grill.GetSlot(0);

            for (long age = 0; age <= BurntAt + 10; age++)
            {
                MeatState expected = age < Cook ? MeatState.Raw : age < BurntAt ? MeatState.Done : MeatState.Burnt;
                Assert.AreEqual(expected, slot.State, "Вік " + age);
                Assert.AreEqual(age < BurntAt ? age : BurntAt, slot.AgeTicks, "Вік " + age);
                Assert.AreEqual(age < Cook ? Cook - age : 0L, slot.TicksUntilDone, "Вік " + age);
                Assert.AreEqual(age < BurntAt ? BurntAt - age : 0L, slot.TicksUntilBurnt, "Вік " + age);
                shift.Tick();
            }
        }

        [Test]
        public void BurnWarning_StartsExactlyWarnTicksBeforeBurning()
        {
            ShiftSimulation shift = GrillTestKit.CreateRunning();
            GrillTestKit.PlaceAndAge(shift, 0, BurntAt - Warn - 1);
            GrillSlot slot = shift.Grill.GetSlot(0);

            Assert.AreEqual(MeatState.Done, slot.State);
            Assert.IsFalse(slot.IsBurnWarning, "За такт до попередження");

            shift.Tick();
            Assert.IsTrue(slot.IsBurnWarning, "Перший такт попередження");

            GrillTestKit.Tick(shift, Warn - 1);
            Assert.AreEqual(MeatState.Done, slot.State);
            Assert.IsTrue(slot.IsBurnWarning, "Останній такт перед пригоранням");

            shift.Tick();
            Assert.AreEqual(MeatState.Burnt, slot.State);
            Assert.IsFalse(slot.IsBurnWarning, "Пригоріле вже не попереджає");
        }

        [Test]
        public void BurnWarning_NeverShowsForRawMeat_AndIsOffWhenWarnTimeIsZero()
        {
            // Попередження довше за все вікно готовності не має вмикатися на сирому м'ясі.
            ShiftSimulation wide = GrillTestKit.CreateRunning(
                GrillTestKit.GrillParams(burnAfterDoneMs: 3000, warnBeforeBurnMs: 3000));
            GrillTestKit.PlaceAndAge(wide, 0, GrillTestKit.CookTicks - 1);
            Assert.IsFalse(wide.Grill.GetSlot(0).IsBurnWarning);
            wide.Tick();
            Assert.IsTrue(wide.Grill.GetSlot(0).IsBurnWarning, "Попередження на все вікно готовності");

            ShiftSimulation off = GrillTestKit.CreateRunning(GrillTestKit.GrillParams(warnBeforeBurnMs: 0));
            GrillTestKit.Place(off, 0);
            for (long age = 0; age <= BurntAt; age++)
            {
                Assert.IsFalse(off.Grill.GetSlot(0).IsBurnWarning, "Вік " + age);
                off.Tick();
            }
        }

        [Test]
        public void ShortestTimers_OneTickEach_StillPassThroughEveryState()
        {
            ShiftSimulation shift = GrillTestKit.CreateRunning(
                GrillTestKit.GrillParams(cookMs: 17, burnAfterDoneMs: 17, warnBeforeBurnMs: 0));
            Assert.AreEqual(1L, shift.Grill.CookTicks);
            Assert.AreEqual(1L, shift.Grill.BurnAfterDoneTicks);

            GrillTestKit.Place(shift, 0);
            Assert.AreEqual(MeatState.Raw, GrillTestKit.StateOf(shift, 0));
            shift.Tick();
            Assert.AreEqual(MeatState.Done, GrillTestKit.StateOf(shift, 0));
            shift.Tick();
            Assert.AreEqual(MeatState.Burnt, GrillTestKit.StateOf(shift, 0));
        }

        [Test]
        public void Slots_HaveIndependentTimers()
        {
            ShiftSimulation shift = GrillTestKit.CreateRunning();
            GrillTestKit.Place(shift, 0);
            GrillTestKit.Tick(shift, 99);
            GrillTestKit.Place(shift, 2);

            Assert.AreEqual(100L, shift.Grill.GetSlot(0).AgeTicks);
            Assert.AreEqual(0L, shift.Grill.GetSlot(2).AgeTicks);

            GrillTestKit.Tick(shift, Cook - 100);
            Assert.AreEqual(MeatState.Done, GrillTestKit.StateOf(shift, 0));
            Assert.AreEqual(MeatState.Empty, GrillTestKit.StateOf(shift, 1));
            Assert.AreEqual(MeatState.Raw, GrillTestKit.StateOf(shift, 2));

            GrillTestKit.Tick(shift, 100);
            Assert.AreEqual(MeatState.Done, GrillTestKit.StateOf(shift, 2));

            GrillTestKit.Tick(shift, BurntAt - Cook - 100);
            Assert.AreEqual(MeatState.Burnt, GrillTestKit.StateOf(shift, 0));
            Assert.AreEqual(MeatState.Done, GrillTestKit.StateOf(shift, 2));
            Assert.AreEqual(1L, shift.Grill.BurntCount);

            GrillTestKit.Tick(shift, 100);
            Assert.AreEqual(MeatState.Burnt, GrillTestKit.StateOf(shift, 2));
            Assert.AreEqual(2L, shift.Grill.BurntCount);
        }

        [Test]
        public void Pause_FreezesGrillTimers_AndResumeContinuesFromSameAge()
        {
            ShiftSimulation shift = GrillTestKit.CreateRunning();
            GrillTestKit.PlaceAndAge(shift, 0, Cook - 1);

            Assert.IsTrue(shift.Pause());
            ShiftSnapshot paused = shift.CaptureSnapshot();
            for (int i = 0; i < 500; i++)
            {
                Assert.IsFalse(shift.Tick());
                Assert.AreEqual(0, shift.Advance(1d));
            }

            Assert.AreEqual(paused, shift.CaptureSnapshot(), "У паузі гриль стоїть");
            Assert.AreEqual(MeatState.Raw, GrillTestKit.StateOf(shift, 0));

            Assert.IsTrue(shift.Resume());
            shift.Tick();

            Assert.AreEqual(MeatState.Done, GrillTestKit.StateOf(shift, 0));
            Assert.AreEqual(Cook, shift.Grill.GetSlot(0).AgeTicks);
        }

        [Test]
        public void GrillTimers_DoNotRunBeforeStart()
        {
            ShiftSimulation shift = GrillTestKit.Create();
            ShiftSnapshot before = shift.CaptureSnapshot();

            Assert.IsFalse(shift.Tick());
            Assert.AreEqual(0, shift.Advance(5d));

            Assert.AreEqual(before, shift.CaptureSnapshot());
        }

        [Test]
        public void GrillStopsWhenShiftIsLost_AndContinueResumesItFromSameAge()
        {
            ShiftSimulation shift = GrillTestKit.CreateRunning(goalAchieved: false, durationSec: 1);
            GrillTestKit.Place(shift, 0);
            GrillTestKit.Tick(shift, 59);

            Assert.AreEqual(ShiftState.Lost, shift.State);
            Assert.AreEqual(59L, shift.Grill.GetSlot(0).AgeTicks, "Гриль працює і на останньому такті зміни");
            ShiftSnapshot lost = shift.CaptureSnapshot();

            Assert.IsFalse(shift.Tick());
            Assert.AreEqual(0, shift.Advance(3d));
            Assert.AreEqual(lost, shift.CaptureSnapshot(), "Після поразки гриль стоїть");

            Assert.IsTrue(shift.Continue());
            GrillTestKit.Tick(shift, Cook - 59);

            Assert.AreEqual(MeatState.Done, GrillTestKit.StateOf(shift, 0));
            Assert.AreEqual(Cook, shift.Grill.GetSlot(0).AgeTicks);
        }

        [Test]
        public void Advance_MovesGrillByExactlyTheExecutedTicks()
        {
            ShiftSimulation shift = GrillTestKit.CreateRunning();
            GrillTestKit.Place(shift, 0);

            Assert.AreEqual(119, shift.Advance(119d / ShiftTestKit.TicksPerSecond));
            Assert.AreEqual(MeatState.Raw, GrillTestKit.StateOf(shift, 0));

            Assert.AreEqual(1, shift.Advance(1d / ShiftTestKit.TicksPerSecond));
            Assert.AreEqual(MeatState.Done, GrillTestKit.StateOf(shift, 0));
        }

        [Test]
        public void GrillDoesNotUseShiftRandom()
        {
            ShiftSimulation shift = GrillTestKit.CreateRunning();
            GrillTestKit.PlaceAndAge(shift, 0, BurntAt);
            shift.DiscardBurntMeat(0);
            shift.Tick();

            Assert.AreEqual(1L, shift.Grill.DiscardedCount);
            Assert.AreEqual(ShiftTestKit.Seed, shift.CaptureSnapshot().RandomState, "Гриль не тягне випадковість");
        }
    }
}
