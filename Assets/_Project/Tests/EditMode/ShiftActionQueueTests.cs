using System;
using KebabStreet.Core;
using NUnit.Framework;

namespace KebabStreet.Tests.EditMode
{
    /// <summary>
    /// Черга дій гравця (GAME_DESIGN.md, розд. 2, п. 4): до actionQueueMax тапів запам'ятовуються
    /// і виконуються по черзі; дію в черзі можна скасувати. У тестах із тривалістю дія гриля
    /// триває 500 мс = 30 тактів.
    /// </summary>
    public sealed class ShiftActionQueueTests
    {
        private const long ActionTicks = 30L;
        private const long Cook = GrillTestKit.CookTicks;
        private const long BurntAt = GrillTestKit.BurntAtTicks;

        private static ShiftSimulation CreateTimed(int actionQueueMax = GrillTestKit.QueueMax)
        {
            ShiftSimulation shift = GrillTestKit.CreateRunning(GrillTestKit.GrillParams(actionMs: 500), actionQueueMax);
            Assert.AreEqual(ActionTicks, shift.Grill.ActionTicks, "Підготовка тесту");
            return shift;
        }

        private static ShiftAction Place(int slot)
        {
            return new ShiftAction(ShiftActionType.PlaceMeat, slot);
        }

        [Test]
        public void NewShift_HasEmptyQueue_AndNoExecutedActions()
        {
            ShiftSimulation shift = GrillTestKit.CreateRunning();

            Assert.AreEqual(0, shift.QueuedActionCount);
            Assert.AreEqual(0L, shift.HeadActionProgressTicks);
            Assert.AreEqual(0L, shift.ExecutedActionCount);
            Assert.AreEqual(0L, shift.IgnoredActionCount);
            Assert.AreEqual(ShiftActionResult.None, shift.LastActionResult);
            Assert.AreEqual(default(ShiftAction), shift.LastAction);
            Assert.AreEqual(default(ActionQueueSnapshot), shift.CaptureSnapshot().Actions);
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(4)]
        public void Queue_AcceptsActionsUpToMax_AndRejectsTheNextOne(int actionQueueMax)
        {
            ShiftSimulation shift = CreateTimed(actionQueueMax);

            for (int i = 0; i < actionQueueMax; i++)
            {
                Assert.IsTrue(shift.PlaceMeat(), "Дія " + i);
                Assert.AreEqual(i + 1, shift.QueuedActionCount);
            }

            ShiftSnapshot full = shift.CaptureSnapshot();

            Assert.IsFalse(shift.PlaceMeat());
            Assert.IsFalse(shift.TakeMeat(0));
            Assert.IsFalse(shift.DiscardBurntMeat());

            Assert.AreEqual(full, shift.CaptureSnapshot(), "Відхилена команда нічого не змінює");
            Assert.AreEqual(actionQueueMax, shift.QueuedActionCount);
        }

        [Test]
        public void Queue_KeepsActionsInOrderOfTaps()
        {
            ShiftSimulation shift = CreateTimed();

            shift.PlaceMeat(2);
            shift.TakeMeat(0);
            shift.DiscardBurntMeat();
            shift.PlaceMeat();

            Assert.AreEqual(4, shift.QueuedActionCount);
            Assert.AreEqual(Place(2), shift.GetQueuedAction(0));
            Assert.AreEqual(new ShiftAction(ShiftActionType.TakeMeat, 0), shift.GetQueuedAction(1));
            Assert.AreEqual(new ShiftAction(ShiftActionType.DiscardBurntMeat, GrillSlot.Any), shift.GetQueuedAction(2));
            Assert.AreEqual(Place(GrillSlot.Any), shift.GetQueuedAction(3));
        }

        [Test]
        public void GetQueuedAction_OutsideQueue_Throws()
        {
            ShiftSimulation shift = CreateTimed();
            shift.PlaceMeat();

            Assert.Throws<ArgumentOutOfRangeException>(() => shift.GetQueuedAction(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => shift.GetQueuedAction(1));
        }

        [Test]
        public void InstantActions_AllExecuteOnNextTick_InOrder()
        {
            ShiftSimulation shift = GrillTestKit.CreateRunning();
            Assert.AreEqual(0L, shift.Grill.ActionTicks, "Підготовка тесту");

            shift.PlaceMeat(1);
            shift.PlaceMeat(1);
            shift.PlaceMeat();
            shift.TakeMeat(1);
            Assert.AreEqual(0L, shift.Grill.PlacedCount, "До такту нічого не виконано");

            shift.Tick();

            Assert.AreEqual(0, shift.QueuedActionCount);
            Assert.AreEqual(2L, shift.ExecutedActionCount);
            Assert.AreEqual(2L, shift.IgnoredActionCount, "Повторне «покласти в слот 1» і «зняти сире»");
            Assert.AreEqual(MeatState.Raw, GrillTestKit.StateOf(shift, 0));
            Assert.AreEqual(MeatState.Raw, GrillTestKit.StateOf(shift, 1));
            Assert.AreEqual(new ShiftAction(ShiftActionType.TakeMeat, 1), shift.LastAction);
            Assert.AreEqual(ShiftActionResult.MeatRaw, shift.LastActionResult);
        }

        [Test]
        public void TimedAction_ExecutesExactlyWhenItsDurationElapses()
        {
            ShiftSimulation shift = CreateTimed();
            shift.PlaceMeat(0);

            GrillTestKit.Tick(shift, ActionTicks - 1);

            Assert.AreEqual(MeatState.Empty, GrillTestKit.StateOf(shift, 0), "За такт до кінця дії");
            Assert.AreEqual(1, shift.QueuedActionCount);
            Assert.AreEqual(ActionTicks - 1, shift.HeadActionProgressTicks);
            Assert.AreEqual(ShiftActionResult.None, shift.LastActionResult);

            shift.Tick();

            Assert.AreEqual(MeatState.Raw, GrillTestKit.StateOf(shift, 0), "На такті кінця дії");
            Assert.AreEqual(0L, shift.Grill.GetSlot(0).AgeTicks);
            Assert.AreEqual(0, shift.QueuedActionCount);
            Assert.AreEqual(0L, shift.HeadActionProgressTicks);
            Assert.AreEqual(Place(0), shift.LastAction);
            Assert.AreEqual(ShiftActionResult.Done, shift.LastActionResult);
        }

        [Test]
        public void TimedActions_ExecuteOneAfterAnother_EachTakingFullDuration()
        {
            ShiftSimulation shift = CreateTimed();
            shift.PlaceMeat(0);
            shift.PlaceMeat(1);
            shift.PlaceMeat(2);

            GrillTestKit.Tick(shift, ActionTicks);
            Assert.AreEqual(1L, shift.Grill.PlacedCount);
            Assert.AreEqual(2, shift.QueuedActionCount);
            Assert.AreEqual(0L, shift.HeadActionProgressTicks, "Наступна дія починається з наступного такту");

            GrillTestKit.Tick(shift, ActionTicks - 1);
            Assert.AreEqual(1L, shift.Grill.PlacedCount);

            shift.Tick();
            Assert.AreEqual(2L, shift.Grill.PlacedCount);

            GrillTestKit.Tick(shift, ActionTicks);
            Assert.AreEqual(3L, shift.Grill.PlacedCount);
            Assert.AreEqual(0, shift.QueuedActionCount);
            Assert.AreEqual(2 * ActionTicks, shift.Grill.GetSlot(0).AgeTicks);
            Assert.AreEqual(ActionTicks, shift.Grill.GetSlot(1).AgeTicks);
            Assert.AreEqual(0L, shift.Grill.GetSlot(2).AgeTicks);
        }

        [Test]
        public void IdleTime_IsNotBanked_ActionStartsWhenItIsTapped()
        {
            ShiftSimulation shift = CreateTimed();
            GrillTestKit.Tick(shift, 100);

            shift.PlaceMeat(0);
            GrillTestKit.Tick(shift, ActionTicks - 1);
            Assert.AreEqual(MeatState.Empty, GrillTestKit.StateOf(shift, 0));

            shift.Tick();
            Assert.AreEqual(MeatState.Raw, GrillTestKit.StateOf(shift, 0));
        }

        [Test]
        public void FullQueue_AcceptsNewAction_OnlyAfterOneIsExecuted()
        {
            ShiftSimulation shift = CreateTimed(actionQueueMax: 2);
            Assert.IsTrue(shift.PlaceMeat());
            Assert.IsTrue(shift.PlaceMeat());

            GrillTestKit.Tick(shift, ActionTicks - 1);
            Assert.IsFalse(shift.PlaceMeat(), "Перша дія ще виконується і займає місце в черзі");

            shift.Tick();
            Assert.IsTrue(shift.PlaceMeat());
            Assert.AreEqual(2, shift.QueuedActionCount);
        }

        [Test]
        public void Cancel_RemovesWaitingAction_AndKeepsOrderAndProgress()
        {
            ShiftSimulation shift = CreateTimed();
            shift.PlaceMeat(0);
            shift.PlaceMeat(1);
            shift.PlaceMeat(2);
            GrillTestKit.Tick(shift, 10);

            Assert.IsTrue(shift.CancelQueuedAction(1));

            Assert.AreEqual(2, shift.QueuedActionCount);
            Assert.AreEqual(Place(0), shift.GetQueuedAction(0));
            Assert.AreEqual(Place(2), shift.GetQueuedAction(1));
            Assert.AreEqual(10L, shift.HeadActionProgressTicks, "Скасування чужої дії не збиває поточну");

            GrillTestKit.Tick(shift, 2 * ActionTicks - 10);

            Assert.AreEqual(MeatState.Raw, GrillTestKit.StateOf(shift, 0));
            Assert.AreEqual(MeatState.Empty, GrillTestKit.StateOf(shift, 1), "Скасована дія не виконалась");
            Assert.AreEqual(MeatState.Raw, GrillTestKit.StateOf(shift, 2));
            Assert.AreEqual(2L, shift.ExecutedActionCount);
            Assert.AreEqual(0L, shift.IgnoredActionCount);
        }

        [Test]
        public void Cancel_ActionInProgress_ResetsProgress_AndNextActionTakesFullDuration()
        {
            ShiftSimulation shift = CreateTimed();
            shift.PlaceMeat(0);
            shift.PlaceMeat(1);
            GrillTestKit.Tick(shift, ActionTicks - 1);

            Assert.IsTrue(shift.CancelQueuedAction(0));

            Assert.AreEqual(1, shift.QueuedActionCount);
            Assert.AreEqual(Place(1), shift.GetQueuedAction(0));
            Assert.AreEqual(0L, shift.HeadActionProgressTicks);

            GrillTestKit.Tick(shift, ActionTicks - 1);
            Assert.AreEqual(0L, shift.Grill.PlacedCount);

            shift.Tick();
            Assert.AreEqual(MeatState.Empty, GrillTestKit.StateOf(shift, 0));
            Assert.AreEqual(MeatState.Raw, GrillTestKit.StateOf(shift, 1));
            Assert.AreEqual(1L, shift.Grill.PlacedCount);
        }

        [Test]
        public void Cancel_LastAction_FreesPlaceInQueue()
        {
            ShiftSimulation shift = CreateTimed(actionQueueMax: 2);
            shift.PlaceMeat(0);
            shift.PlaceMeat(1);
            Assert.IsFalse(shift.PlaceMeat(2));

            Assert.IsTrue(shift.CancelQueuedAction(1));
            Assert.IsTrue(shift.PlaceMeat(2));

            Assert.AreEqual(Place(0), shift.GetQueuedAction(0));
            Assert.AreEqual(Place(2), shift.GetQueuedAction(1));
        }

        [TestCase(-1)]
        [TestCase(2)]
        [TestCase(int.MaxValue)]
        [TestCase(int.MinValue)]
        public void Cancel_WithIndexOutsideQueue_ReturnsFalse_AndChangesNothing(int index)
        {
            ShiftSimulation shift = CreateTimed();
            shift.PlaceMeat(0);
            shift.PlaceMeat(1);
            GrillTestKit.Tick(shift, 5);
            ShiftSnapshot before = shift.CaptureSnapshot();

            Assert.IsFalse(shift.CancelQueuedAction(index));

            Assert.AreEqual(before, shift.CaptureSnapshot());
        }

        [Test]
        public void Cancel_OnEmptyQueue_ReturnsFalse()
        {
            ShiftSimulation shift = CreateTimed();
            ShiftSnapshot before = shift.CaptureSnapshot();

            Assert.IsFalse(shift.CancelQueuedAction(0));

            Assert.AreEqual(before, shift.CaptureSnapshot());
        }

        [Test]
        public void Pause_FreezesActionInProgress()
        {
            ShiftSimulation shift = CreateTimed();
            shift.PlaceMeat(0);
            GrillTestKit.Tick(shift, ActionTicks - 1);

            shift.Pause();
            ShiftSnapshot paused = shift.CaptureSnapshot();
            for (int i = 0; i < 100; i++)
            {
                shift.Tick();
                shift.Advance(1d);
            }

            Assert.AreEqual(paused, shift.CaptureSnapshot());
            Assert.AreEqual(MeatState.Empty, GrillTestKit.StateOf(shift, 0));

            shift.Resume();
            shift.Tick();

            Assert.AreEqual(MeatState.Raw, GrillTestKit.StateOf(shift, 0));
        }

        [Test]
        public void QueueSurvivesLostShift_AndContinuesAfterContinue()
        {
            ShiftSimulation shift = GrillTestKit.CreateRunning(
                GrillTestKit.GrillParams(actionMs: 2000), goalAchieved: false, durationSec: 1);
            shift.PlaceMeat(0);

            GrillTestKit.Tick(shift, 60);

            Assert.AreEqual(ShiftState.Lost, shift.State);
            Assert.AreEqual(1, shift.QueuedActionCount);
            Assert.AreEqual(60L, shift.HeadActionProgressTicks);
            Assert.IsFalse(shift.CancelQueuedAction(0), "Після поразки чергу не змінити");

            Assert.IsTrue(shift.Continue());
            GrillTestKit.Tick(shift, 59);
            Assert.AreEqual(MeatState.Empty, GrillTestKit.StateOf(shift, 0));

            shift.Tick();
            Assert.AreEqual(MeatState.Raw, GrillTestKit.StateOf(shift, 0));
        }

        [Test]
        public void ActionQueuedBeforeLastTick_IsExecutedOnLastTick()
        {
            ShiftSimulation shift = GrillTestKit.CreateRunning(durationSec: 1);
            GrillTestKit.Tick(shift, 59);
            shift.PlaceMeat(0);

            shift.Tick();

            Assert.AreEqual(ShiftState.Won, shift.State);
            Assert.AreEqual(MeatState.Raw, GrillTestKit.StateOf(shift, 0));
            Assert.AreEqual(0, shift.QueuedActionCount);
        }

        [Test]
        public void Take_StartedInTime_SucceedsIfMeatIsStillDoneWhenActionEnds()
        {
            ShiftSimulation shift = CreateTimed();
            shift.PlaceMeat(0);
            GrillTestKit.Tick(shift, ActionTicks + BurntAt - ActionTicks - 1);
            Assert.AreEqual(BurntAt - ActionTicks - 1, shift.Grill.GetSlot(0).AgeTicks, "Підготовка тесту");

            shift.TakeMeat(0);
            GrillTestKit.Tick(shift, ActionTicks);

            Assert.AreEqual(ShiftActionResult.Done, shift.LastActionResult);
            Assert.AreEqual(1L, shift.Grill.TakenCount);
            Assert.AreEqual(0L, shift.Grill.BurntCount);
        }

        [Test]
        public void Take_StartedTooLate_IsIgnoredBecauseMeatBurnsWhileActionRuns()
        {
            ShiftSimulation shift = CreateTimed();
            shift.PlaceMeat(0);
            GrillTestKit.Tick(shift, ActionTicks + BurntAt - ActionTicks);
            Assert.AreEqual(MeatState.Done, GrillTestKit.StateOf(shift, 0), "У момент тапу м'ясо ще готове");

            shift.TakeMeat(0);
            GrillTestKit.Tick(shift, ActionTicks);

            Assert.AreEqual(ShiftActionResult.MeatBurnt, shift.LastActionResult);
            Assert.AreEqual(MeatState.Burnt, GrillTestKit.StateOf(shift, 0));
            Assert.AreEqual(0L, shift.Grill.TakenCount);
            Assert.AreEqual(1L, shift.Grill.BurntCount);
            Assert.AreEqual(1L, shift.IgnoredActionCount);
        }

        [Test]
        public void Take_QueuedWhileMeatIsRaw_SucceedsIfMeatIsDoneWhenActionEnds()
        {
            ShiftSimulation shift = CreateTimed();
            shift.PlaceMeat(0);
            GrillTestKit.Tick(shift, ActionTicks + Cook - ActionTicks);
            Assert.AreEqual(MeatState.Raw, GrillTestKit.StateOf(shift, 0), "У момент тапу м'ясо ще сире");

            shift.TakeMeat(0);
            GrillTestKit.Tick(shift, ActionTicks);

            Assert.AreEqual(ShiftActionResult.Done, shift.LastActionResult);
            Assert.AreEqual(1L, shift.Grill.TakenCount);
        }

        [Test]
        public void Snapshot_ContainsQueueContents()
        {
            ShiftSimulation first = CreateTimed();
            ShiftSimulation second = CreateTimed();
            first.PlaceMeat(0);
            second.PlaceMeat(1);

            ActionQueueSnapshot actions = first.CaptureSnapshot().Actions;
            Assert.AreEqual(1, actions.Count);
            Assert.AreEqual(Place(0), actions.GetAction(0));
            Assert.AreNotEqual(first.CaptureSnapshot(), second.CaptureSnapshot());

            first.Tick();
            Assert.AreEqual(1L, first.CaptureSnapshot().Actions.HeadProgressTicks);
        }

        [Test]
        public void ActionQueueSnapshot_EqualityCoversEveryField()
        {
            ShiftAction[] queue = { Place(0), Place(1) };
            var snapshot = new ActionQueueSnapshot(queue, 1L, 2L, 3L, Place(2), ShiftActionResult.Done);
            var same = new ActionQueueSnapshot(
                new[] { Place(0), Place(1) }, 1L, 2L, 3L, Place(2), ShiftActionResult.Done);

            Assert.IsTrue(snapshot.Equals(same));
            Assert.IsTrue(snapshot.Equals((object)same));
            Assert.AreEqual(snapshot.GetHashCode(), same.GetHashCode());

            Assert.IsFalse(snapshot.Equals(new ActionQueueSnapshot(
                new[] { Place(0) }, 1L, 2L, 3L, Place(2), ShiftActionResult.Done)));
            Assert.IsFalse(snapshot.Equals(new ActionQueueSnapshot(
                new[] { Place(0), Place(2) }, 1L, 2L, 3L, Place(2), ShiftActionResult.Done)));
            Assert.IsFalse(snapshot.Equals(new ActionQueueSnapshot(
                new[] { Place(0), new ShiftAction(ShiftActionType.TakeMeat, 1) }, 1L, 2L, 3L, Place(2), ShiftActionResult.Done)));
            Assert.IsFalse(snapshot.Equals(new ActionQueueSnapshot(queue, 9L, 2L, 3L, Place(2), ShiftActionResult.Done)));
            Assert.IsFalse(snapshot.Equals(new ActionQueueSnapshot(queue, 1L, 9L, 3L, Place(2), ShiftActionResult.Done)));
            Assert.IsFalse(snapshot.Equals(new ActionQueueSnapshot(queue, 1L, 2L, 9L, Place(2), ShiftActionResult.Done)));
            Assert.IsFalse(snapshot.Equals(new ActionQueueSnapshot(queue, 1L, 2L, 3L, Place(1), ShiftActionResult.Done)));
            Assert.IsFalse(snapshot.Equals(new ActionQueueSnapshot(queue, 1L, 2L, 3L, Place(2), ShiftActionResult.GrillFull)));
            Assert.IsFalse(snapshot.Equals("не знімок"));
        }

        [Test]
        public void ActionQueueSnapshot_CopiesItsArray()
        {
            ShiftAction[] queue = { Place(0) };
            var snapshot = new ActionQueueSnapshot(queue, 0L, 0L, 0L, default(ShiftAction), ShiftActionResult.None);

            queue[0] = Place(2);

            Assert.AreEqual(Place(0), snapshot.GetAction(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => snapshot.GetAction(1));
        }
    }
}
