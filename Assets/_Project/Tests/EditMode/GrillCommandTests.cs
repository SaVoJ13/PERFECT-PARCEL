using KebabStreet.Core;
using NUnit.Framework;

namespace KebabStreet.Tests.EditMode
{
    /// <summary>
    /// Команди гравця на грилі: покласти, зняти, викинути пригоріле (GAME_DESIGN.md, розд. 2 і 4).
    /// Правило: дія, яку не можна виконати, ігнорується — стан гриля не змінюється,
    /// а причина видна в LastActionResult. Дії в цих тестах миттєві (виконуються на наступному такті).
    /// </summary>
    public sealed class GrillCommandTests
    {
        private const long Cook = GrillTestKit.CookTicks;
        private const long BurntAt = GrillTestKit.BurntAtTicks;

        public enum Setup
        {
            Intro,
            Paused,
            Won,
            Lost,
        }

        public enum Command
        {
            Place,
            Take,
            Discard,
            Cancel,
        }

        /// <summary>Надсилає команду, виконує один такт і повертає результат дії.</summary>
        private static ShiftActionResult Run(ShiftSimulation shift, Command command, int slot)
        {
            long handledBefore = shift.ExecutedActionCount + shift.IgnoredActionCount;
            Assert.IsTrue(Send(shift, command, slot), "Команду не прийнято в чергу");
            shift.Tick();
            Assert.AreEqual(
                handledBefore + 1, shift.ExecutedActionCount + shift.IgnoredActionCount, "Дія мала виконатися за такт");
            return shift.LastActionResult;
        }

        private static bool Send(ShiftSimulation shift, Command command, int slot)
        {
            switch (command)
            {
                case Command.Place:
                    return shift.PlaceMeat(slot);
                case Command.Take:
                    return shift.TakeMeat(slot);
                case Command.Discard:
                    return shift.DiscardBurntMeat(slot);
                default:
                    return shift.CancelQueuedAction(slot);
            }
        }

        /// <summary>
        /// Виконує дію, яку гриль має проігнорувати, і перевіряє, що вона нічого не змінила:
        /// стан кожного слота і всі лічильники гриля ті самі (таймери за цей такт ідуть далі).
        /// </summary>
        private static void AssertIgnored(
            ShiftSimulation shift, Command command, int slot, ShiftActionResult expected)
        {
            GrillSnapshot before = shift.Grill.CaptureSnapshot();
            long ignoredBefore = shift.IgnoredActionCount;
            long executedBefore = shift.ExecutedActionCount;

            Assert.AreEqual(expected, Run(shift, command, slot));

            GrillSnapshot after = shift.Grill.CaptureSnapshot();
            Assert.AreEqual(ignoredBefore + 1, shift.IgnoredActionCount);
            Assert.AreEqual(executedBefore, shift.ExecutedActionCount);
            Assert.AreEqual(before.PlacedCount, after.PlacedCount);
            Assert.AreEqual(before.TakenCount, after.TakenCount);
            Assert.AreEqual(before.BurntCount, after.BurntCount);
            Assert.AreEqual(before.DiscardedCount, after.DiscardedCount);
            Assert.AreEqual(before.PenaltyCoins, after.PenaltyCoins);
            for (int i = 0; i < before.SlotCount; i++)
            {
                Assert.AreEqual(before.GetState(i), after.GetState(i), "Слот " + i);
            }
        }

        private static ShiftSimulation FullGrill(long ageTicks)
        {
            ShiftSimulation shift = GrillTestKit.CreateRunning();
            Assert.IsTrue(shift.PlaceMeat());
            Assert.IsTrue(shift.PlaceMeat());
            Assert.IsTrue(shift.PlaceMeat());
            GrillTestKit.Tick(shift, 1 + ageTicks);
            Assert.AreEqual(0, shift.Grill.FreeSlotCount, "Підготовка тесту: гриль заповнено");
            return shift;
        }

        // ---------- Покласти ----------

        [Test]
        public void Place_WithoutSlot_FillsFirstFreeSlot()
        {
            ShiftSimulation shift = GrillTestKit.CreateRunning();

            Assert.AreEqual(ShiftActionResult.Done, Run(shift, Command.Place, GrillSlot.Any));
            Assert.AreEqual(MeatState.Raw, GrillTestKit.StateOf(shift, 0));
            Assert.AreEqual(MeatState.Empty, GrillTestKit.StateOf(shift, 1));

            Assert.AreEqual(ShiftActionResult.Done, Run(shift, Command.Place, GrillSlot.Any));
            Assert.AreEqual(MeatState.Raw, GrillTestKit.StateOf(shift, 1));
            Assert.AreEqual(MeatState.Empty, GrillTestKit.StateOf(shift, 2));
            Assert.AreEqual(2L, shift.Grill.PlacedCount);
            Assert.AreEqual(1, shift.Grill.FreeSlotCount);
        }

        [Test]
        public void Place_WithoutSlot_SkipsOccupiedSlots()
        {
            ShiftSimulation shift = GrillTestKit.CreateRunning();
            GrillTestKit.Place(shift, 0);
            GrillTestKit.Place(shift, 2);

            Assert.AreEqual(ShiftActionResult.Done, Run(shift, Command.Place, GrillSlot.Any));

            Assert.AreEqual(MeatState.Raw, GrillTestKit.StateOf(shift, 1));
            Assert.AreEqual(0L, shift.Grill.GetSlot(1).AgeTicks);
            Assert.AreEqual(0, shift.Grill.FreeSlotCount);
        }

        [Test]
        public void Place_IntoNamedSlot_UsesThatSlot()
        {
            ShiftSimulation shift = GrillTestKit.CreateRunning();

            Assert.AreEqual(ShiftActionResult.Done, Run(shift, Command.Place, 2));

            Assert.AreEqual(MeatState.Empty, GrillTestKit.StateOf(shift, 0));
            Assert.AreEqual(MeatState.Empty, GrillTestKit.StateOf(shift, 1));
            Assert.AreEqual(MeatState.Raw, GrillTestKit.StateOf(shift, 2));
        }

        [TestCase(0L)]
        [TestCase(Cook)]
        [TestCase(BurntAt)]
        public void Place_IntoOccupiedSlot_IsIgnored_AndDoesNotRestartTimer(long ageTicks)
        {
            ShiftSimulation shift = GrillTestKit.CreateRunning();
            GrillTestKit.PlaceAndAge(shift, 1, ageTicks);
            MeatState state = GrillTestKit.StateOf(shift, 1);

            Assert.AreEqual(ShiftActionResult.SlotOccupied, Run(shift, Command.Place, 1));

            Assert.AreEqual(ageTicks < BurntAt ? ageTicks + 1 : BurntAt, shift.Grill.GetSlot(1).AgeTicks);
            Assert.AreEqual(state, GrillTestKit.StateOf(shift, 1));
            Assert.AreEqual(1L, shift.Grill.PlacedCount);
            Assert.AreEqual(1L, shift.IgnoredActionCount);
        }

        [TestCase(0L)]
        [TestCase(Cook)]
        [TestCase(BurntAt)]
        public void Place_OnFullGrill_IsIgnored_WhateverTheMeatState(long ageTicks)
        {
            ShiftSimulation shift = FullGrill(ageTicks);

            AssertIgnored(shift, Command.Place, GrillSlot.Any, ShiftActionResult.GrillFull);

            Assert.AreEqual(3L, shift.Grill.PlacedCount);
            for (int i = 0; i < GrillTestKit.Slots; i++)
            {
                Assert.AreEqual(ageTicks < BurntAt ? ageTicks + 1 : BurntAt, shift.Grill.GetSlot(i).AgeTicks);
            }
        }

        [Test]
        public void Place_BecomesPossibleAgain_AfterSlotIsFreed()
        {
            ShiftSimulation shift = FullGrill(Cook);

            Assert.AreEqual(ShiftActionResult.GrillFull, Run(shift, Command.Place, GrillSlot.Any));
            Assert.AreEqual(ShiftActionResult.Done, Run(shift, Command.Take, 1));
            Assert.AreEqual(ShiftActionResult.Done, Run(shift, Command.Place, GrillSlot.Any));

            Assert.AreEqual(MeatState.Raw, GrillTestKit.StateOf(shift, 1));
            Assert.AreEqual(0L, shift.Grill.GetSlot(1).AgeTicks, "Нове м'ясо починає з нуля");
            Assert.AreEqual(4L, shift.Grill.PlacedCount);
        }

        // ---------- Зняти ----------

        [Test]
        public void Take_DoneMeat_FreesSlot_AndCountsPiece()
        {
            ShiftSimulation shift = GrillTestKit.CreateRunning();
            GrillTestKit.PlaceAndAge(shift, 1, Cook);

            Assert.AreEqual(ShiftActionResult.Done, Run(shift, Command.Take, 1));

            GrillSlot slot = shift.Grill.GetSlot(1);
            Assert.AreEqual(MeatState.Empty, slot.State);
            Assert.AreEqual(0L, slot.AgeTicks);
            Assert.AreEqual(1L, shift.Grill.TakenCount);
            Assert.AreEqual(0L, shift.Grill.BurntCount);
            Assert.AreEqual(0L, shift.Grill.PenaltyCoins);
            Assert.AreEqual(GrillTestKit.Slots, shift.Grill.FreeSlotCount);
        }

        [Test]
        public void Take_Repeated_IsIgnoredSecondTime_AndPieceIsCountedOnce()
        {
            ShiftSimulation shift = GrillTestKit.CreateRunning();
            GrillTestKit.PlaceAndAge(shift, 0, Cook);

            Assert.AreEqual(ShiftActionResult.Done, Run(shift, Command.Take, 0));
            AssertIgnored(shift, Command.Take, 0, ShiftActionResult.SlotEmpty);
            AssertIgnored(shift, Command.Take, 0, ShiftActionResult.SlotEmpty);

            Assert.AreEqual(1L, shift.Grill.TakenCount);
        }

        [Test]
        public void Take_TwiceInSameTick_GivesOnePiece()
        {
            ShiftSimulation shift = GrillTestKit.CreateRunning();
            GrillTestKit.PlaceAndAge(shift, 0, Cook);

            Assert.IsTrue(shift.TakeMeat(0));
            Assert.IsTrue(shift.TakeMeat(0));
            shift.Tick();

            Assert.AreEqual(1L, shift.Grill.TakenCount);
            Assert.AreEqual(2L, shift.ExecutedActionCount, "Виконано: покласти і одне зняття");
            Assert.AreEqual(1L, shift.IgnoredActionCount);
            Assert.AreEqual(ShiftActionResult.SlotEmpty, shift.LastActionResult);
        }

        [Test]
        public void Take_FromEmptySlot_IsIgnored()
        {
            ShiftSimulation shift = GrillTestKit.CreateRunning();

            AssertIgnored(shift, Command.Take, 0, ShiftActionResult.SlotEmpty);
            AssertIgnored(shift, Command.Take, 2, ShiftActionResult.SlotEmpty);

            Assert.AreEqual(0L, shift.Grill.TakenCount);
        }

        [TestCase(0L)]
        [TestCase(Cook - 2)]
        public void Take_RawMeat_IsIgnored_AndMeatKeepsCooking(long ageTicks)
        {
            ShiftSimulation shift = GrillTestKit.CreateRunning();
            GrillTestKit.PlaceAndAge(shift, 0, ageTicks);

            AssertIgnored(shift, Command.Take, 0, ShiftActionResult.MeatRaw);

            Assert.AreEqual(MeatState.Raw, GrillTestKit.StateOf(shift, 0));
            Assert.AreEqual(ageTicks + 1, shift.Grill.GetSlot(0).AgeTicks);
        }

        [Test]
        public void Take_OnTickWhenMeatBecomesDone_Succeeds()
        {
            ShiftSimulation shift = GrillTestKit.CreateRunning();
            GrillTestKit.PlaceAndAge(shift, 0, Cook - 1);
            Assert.AreEqual(MeatState.Raw, GrillTestKit.StateOf(shift, 0));

            Assert.AreEqual(ShiftActionResult.Done, Run(shift, Command.Take, 0));

            Assert.AreEqual(1L, shift.Grill.TakenCount);
        }

        [Test]
        public void Take_OnLastDoneTick_Succeeds()
        {
            ShiftSimulation shift = GrillTestKit.CreateRunning();
            GrillTestKit.PlaceAndAge(shift, 0, BurntAt - 2);

            Assert.AreEqual(ShiftActionResult.Done, Run(shift, Command.Take, 0));

            Assert.AreEqual(1L, shift.Grill.TakenCount);
            Assert.AreEqual(0L, shift.Grill.BurntCount);
        }

        [Test]
        public void Take_OnTickWhenMeatBurns_IsIgnored()
        {
            ShiftSimulation shift = GrillTestKit.CreateRunning();
            GrillTestKit.PlaceAndAge(shift, 0, BurntAt - 1);
            Assert.AreEqual(MeatState.Done, GrillTestKit.StateOf(shift, 0));

            Assert.AreEqual(ShiftActionResult.MeatBurnt, Run(shift, Command.Take, 0));

            Assert.AreEqual(MeatState.Burnt, GrillTestKit.StateOf(shift, 0));
            Assert.AreEqual(0L, shift.Grill.TakenCount);
            Assert.AreEqual(1L, shift.Grill.BurntCount);
        }

        [Test]
        public void Take_BurntMeat_IsIgnored_AndSlotStaysOccupied()
        {
            ShiftSimulation shift = GrillTestKit.CreateRunning();
            GrillTestKit.PlaceAndAge(shift, 0, BurntAt);

            AssertIgnored(shift, Command.Take, 0, ShiftActionResult.MeatBurnt);

            Assert.AreEqual(MeatState.Burnt, GrillTestKit.StateOf(shift, 0));
            Assert.AreEqual(0L, shift.Grill.TakenCount);
        }

        [Test]
        public void Take_WithoutSlot_TakesMeatClosestToBurning()
        {
            ShiftSimulation shift = GrillTestKit.CreateRunning();
            GrillTestKit.Place(shift, 2);
            GrillTestKit.Tick(shift, 4);
            GrillTestKit.Place(shift, 0);
            GrillTestKit.Tick(shift, Cook);
            Assert.AreEqual(MeatState.Done, GrillTestKit.StateOf(shift, 0));
            Assert.AreEqual(MeatState.Done, GrillTestKit.StateOf(shift, 2));

            Assert.AreEqual(ShiftActionResult.Done, Run(shift, Command.Take, GrillSlot.Any));

            Assert.AreEqual(MeatState.Done, GrillTestKit.StateOf(shift, 0), "Молодший шматок лишився");
            Assert.AreEqual(MeatState.Empty, GrillTestKit.StateOf(shift, 2), "Старший шматок знято");
        }

        [Test]
        public void Take_WithoutSlot_OnEqualAge_TakesLowestSlot_AndSkipsRawAndBurnt()
        {
            ShiftSimulation shift = GrillTestKit.CreateRunning(GrillTestKit.GrillParams(slots: 4), actionQueueMax: 4);
            GrillTestKit.PlaceAndAge(shift, 0, BurntAt - Cook - 1);
            Assert.IsTrue(shift.PlaceMeat(2));
            Assert.IsTrue(shift.PlaceMeat(1));
            GrillTestKit.Tick(shift, 1 + Cook);
            GrillTestKit.Place(shift, 3);

            Assert.AreEqual(MeatState.Burnt, GrillTestKit.StateOf(shift, 0));
            Assert.AreEqual(MeatState.Done, GrillTestKit.StateOf(shift, 1));
            Assert.AreEqual(MeatState.Done, GrillTestKit.StateOf(shift, 2));
            Assert.AreEqual(MeatState.Raw, GrillTestKit.StateOf(shift, 3));
            Assert.AreEqual(shift.Grill.GetSlot(1).AgeTicks, shift.Grill.GetSlot(2).AgeTicks);

            Assert.AreEqual(ShiftActionResult.Done, Run(shift, Command.Take, GrillSlot.Any));
            Assert.AreEqual(MeatState.Empty, GrillTestKit.StateOf(shift, 1));
            Assert.AreEqual(MeatState.Done, GrillTestKit.StateOf(shift, 2));

            Assert.AreEqual(ShiftActionResult.Done, Run(shift, Command.Take, GrillSlot.Any));
            Assert.AreEqual(MeatState.Empty, GrillTestKit.StateOf(shift, 2));

            AssertIgnored(shift, Command.Take, GrillSlot.Any, ShiftActionResult.NoDoneMeat);
            Assert.AreEqual(MeatState.Burnt, GrillTestKit.StateOf(shift, 0));
            Assert.AreEqual(MeatState.Raw, GrillTestKit.StateOf(shift, 3));
            Assert.AreEqual(2L, shift.Grill.TakenCount);
        }

        [Test]
        public void Take_WithoutSlot_OnEmptyGrill_IsIgnored()
        {
            ShiftSimulation shift = GrillTestKit.CreateRunning();

            AssertIgnored(shift, Command.Take, GrillSlot.Any, ShiftActionResult.NoDoneMeat);
        }

        // ---------- Викинути пригоріле ----------

        [Test]
        public void Discard_BurntMeat_FreesSlot_AndChargesPenaltyFromConfig()
        {
            ShiftSimulation shift = GrillTestKit.CreateRunning();
            GrillTestKit.PlaceAndAge(shift, 1, BurntAt);

            Assert.AreEqual(ShiftActionResult.Done, Run(shift, Command.Discard, 1));

            Assert.AreEqual(MeatState.Empty, GrillTestKit.StateOf(shift, 1));
            Assert.AreEqual(0L, shift.Grill.GetSlot(1).AgeTicks);
            Assert.AreEqual(1L, shift.Grill.DiscardedCount);
            Assert.AreEqual(1L, shift.Grill.BurntCount);
            Assert.AreEqual(0L, shift.Grill.TakenCount);
            Assert.AreEqual((long)GrillTestKit.BurntPenaltyCoins, shift.Grill.PenaltyCoins);
        }

        [Test]
        public void Discard_OnTickWhenMeatBurns_Succeeds()
        {
            ShiftSimulation shift = GrillTestKit.CreateRunning();
            GrillTestKit.PlaceAndAge(shift, 0, BurntAt - 1);

            Assert.AreEqual(ShiftActionResult.Done, Run(shift, Command.Discard, 0));

            Assert.AreEqual(MeatState.Empty, GrillTestKit.StateOf(shift, 0));
            Assert.AreEqual(1L, shift.Grill.BurntCount);
        }

        [Test]
        public void Discard_Repeated_IsIgnoredSecondTime_AndPenaltyIsChargedOnce()
        {
            ShiftSimulation shift = GrillTestKit.CreateRunning();
            GrillTestKit.PlaceAndAge(shift, 0, BurntAt);

            Assert.AreEqual(ShiftActionResult.Done, Run(shift, Command.Discard, 0));
            AssertIgnored(shift, Command.Discard, 0, ShiftActionResult.SlotEmpty);

            Assert.AreEqual(1L, shift.Grill.DiscardedCount);
            Assert.AreEqual((long)GrillTestKit.BurntPenaltyCoins, shift.Grill.PenaltyCoins);
        }

        [TestCase(0L)]
        [TestCase(Cook)]
        [TestCase(BurntAt - 2)]
        public void Discard_RawOrDoneMeat_IsIgnored_WithoutPenalty(long ageTicks)
        {
            ShiftSimulation shift = GrillTestKit.CreateRunning();
            GrillTestKit.PlaceAndAge(shift, 0, ageTicks);

            AssertIgnored(shift, Command.Discard, 0, ShiftActionResult.MeatNotBurnt);

            Assert.AreEqual(ageTicks + 1, shift.Grill.GetSlot(0).AgeTicks, "М'ясо далі готується");
            Assert.AreEqual(0L, shift.Grill.PenaltyCoins);
        }

        [Test]
        public void Discard_FromEmptySlot_IsIgnored()
        {
            ShiftSimulation shift = GrillTestKit.CreateRunning();

            AssertIgnored(shift, Command.Discard, 1, ShiftActionResult.SlotEmpty);
            AssertIgnored(shift, Command.Discard, GrillSlot.Any, ShiftActionResult.NoBurntMeat);
        }

        [Test]
        public void Discard_WithoutSlot_RemovesFirstBurnt_AndLeavesDoneMeat()
        {
            ShiftSimulation shift = GrillTestKit.CreateRunning();
            Assert.IsTrue(shift.PlaceMeat(1));
            Assert.IsTrue(shift.PlaceMeat(2));
            GrillTestKit.Tick(shift, 1 + BurntAt - Cook);
            GrillTestKit.Place(shift, 0);
            GrillTestKit.Tick(shift, Cook);

            Assert.AreEqual(MeatState.Done, GrillTestKit.StateOf(shift, 0));
            Assert.AreEqual(MeatState.Burnt, GrillTestKit.StateOf(shift, 1));
            Assert.AreEqual(MeatState.Burnt, GrillTestKit.StateOf(shift, 2));

            Assert.AreEqual(ShiftActionResult.Done, Run(shift, Command.Discard, GrillSlot.Any));
            Assert.AreEqual(MeatState.Empty, GrillTestKit.StateOf(shift, 1));
            Assert.AreEqual(MeatState.Burnt, GrillTestKit.StateOf(shift, 2));

            Assert.AreEqual(ShiftActionResult.Done, Run(shift, Command.Discard, GrillSlot.Any));
            Assert.AreEqual(MeatState.Empty, GrillTestKit.StateOf(shift, 2));

            AssertIgnored(shift, Command.Discard, GrillSlot.Any, ShiftActionResult.NoBurntMeat);
            Assert.AreEqual(MeatState.Done, GrillTestKit.StateOf(shift, 0));
            Assert.AreEqual(2L, shift.Grill.DiscardedCount);
        }

        // ---------- Штраф за пригоріле (ADR-020) ----------

        [Test]
        public void BurntPenalty_IsChargedForEveryDiscardedPiece()
        {
            ShiftSimulation shift = FullGrill(BurntAt);
            Assert.AreEqual(0L, shift.Grill.PenaltyCoins, "Пригоріле на грилі ще не оштрафоване");

            for (int i = 0; i < GrillTestKit.Slots; i++)
            {
                Assert.AreEqual(ShiftActionResult.Done, Run(shift, Command.Discard, i));
                Assert.AreEqual((i + 1L) * GrillTestKit.BurntPenaltyCoins, shift.Grill.PenaltyCoins);
            }

            Assert.AreEqual(3L, shift.Grill.BurntCount);
            Assert.AreEqual(3L, shift.Grill.DiscardedCount);
        }

        [Test]
        public void BurntPenalty_ZeroInConfig_MeansNoPenalty_ButBurntMeatIsStillCounted()
        {
            // ADR-020: навчальні рівні 1–2 мають grill.burntPenaltyCoins = 0.
            ShiftSimulation shift = GrillTestKit.CreateRunning(GrillTestKit.GrillParams(burntPenaltyCoins: 0));
            Assert.IsTrue(shift.PlaceMeat());
            Assert.IsTrue(shift.PlaceMeat());
            GrillTestKit.Tick(shift, 1 + BurntAt);

            Assert.AreEqual(ShiftActionResult.Done, Run(shift, Command.Discard, 0));
            Assert.AreEqual(ShiftActionResult.Done, Run(shift, Command.Discard, 1));

            Assert.AreEqual(0L, shift.Grill.PenaltyCoins);
            Assert.AreEqual(2L, shift.Grill.BurntCount);
            Assert.AreEqual(2L, shift.Grill.DiscardedCount);
            Assert.AreEqual(GrillTestKit.Slots, shift.Grill.FreeSlotCount);
        }

        [TestCase(1)]
        [TestCase(7)]
        [TestCase(GrillConfig.MaxBurntPenaltyCoins)]
        public void BurntPenalty_IsOnlyAParameter_GrillBehavesTheSameWithAndWithoutIt(int penaltyCoins)
        {
            GrillSnapshot free = RunBurnScenario(0);
            GrillSnapshot charged = RunBurnScenario(penaltyCoins);

            Assert.AreEqual(0L, free.PenaltyCoins);
            Assert.AreEqual(2L * penaltyCoins, charged.PenaltyCoins);
            Assert.AreEqual(free.SlotCount, charged.SlotCount);
            for (int i = 0; i < free.SlotCount; i++)
            {
                Assert.AreEqual(free.GetState(i), charged.GetState(i));
                Assert.AreEqual(free.GetAgeTicks(i), charged.GetAgeTicks(i));
            }

            Assert.AreEqual(free.PlacedCount, charged.PlacedCount);
            Assert.AreEqual(free.TakenCount, charged.TakenCount);
            Assert.AreEqual(free.BurntCount, charged.BurntCount);
            Assert.AreEqual(free.DiscardedCount, charged.DiscardedCount);
        }

        /// <summary>Три шматки: один знято готовим, два пригоріли і викинуті, потім покладено новий.</summary>
        private static GrillSnapshot RunBurnScenario(int penaltyCoins)
        {
            ShiftSimulation shift = GrillTestKit.CreateRunning(GrillTestKit.GrillParams(burntPenaltyCoins: penaltyCoins));
            shift.PlaceMeat();
            shift.PlaceMeat();
            shift.PlaceMeat();
            GrillTestKit.Tick(shift, 1 + Cook);
            shift.TakeMeat(1);
            GrillTestKit.Tick(shift, BurntAt - Cook);
            shift.DiscardBurntMeat();
            shift.DiscardBurntMeat();
            shift.PlaceMeat();
            GrillTestKit.Tick(shift, 10);
            return shift.Grill.CaptureSnapshot();
        }

        // ---------- Команди, які не приймаються ----------

        [TestCase(Command.Place, -2)]
        [TestCase(Command.Place, GrillTestKit.Slots)]
        [TestCase(Command.Place, int.MaxValue)]
        [TestCase(Command.Place, int.MinValue)]
        [TestCase(Command.Take, -2)]
        [TestCase(Command.Take, GrillTestKit.Slots)]
        [TestCase(Command.Discard, -2)]
        [TestCase(Command.Discard, GrillTestKit.Slots)]
        public void CommandForMissingSlot_ReturnsFalse_AndChangesNothing(Command command, int slot)
        {
            ShiftSimulation shift = FullGrill(Cook);
            ShiftSnapshot before = shift.CaptureSnapshot();

            Assert.IsFalse(Send(shift, command, slot));

            Assert.AreEqual(before, shift.CaptureSnapshot());
            Assert.AreEqual(0, shift.QueuedActionCount);
        }

        private static ShiftSimulation Arrange(Setup setup)
        {
            ShiftSimulation shift = GrillTestKit.Create(goalAchieved: setup == Setup.Won, durationSec: 10);
            if (setup == Setup.Intro)
            {
                return shift;
            }

            shift.Start();
            GrillTestKit.PlaceAndAge(shift, 0, Cook);
            GrillTestKit.PlaceAndAge(shift, 1, BurntAt);
            Assert.IsTrue(shift.PlaceMeat(2), "У паузі ця дія лишається в черзі");
            if (setup == Setup.Paused)
            {
                shift.Pause();
            }
            else
            {
                ShiftTestKit.TickUntilNotRunning(shift);
            }

            return shift;
        }

        [TestCase(Setup.Intro, Command.Place)]
        [TestCase(Setup.Intro, Command.Take)]
        [TestCase(Setup.Intro, Command.Discard)]
        [TestCase(Setup.Intro, Command.Cancel)]
        [TestCase(Setup.Paused, Command.Place)]
        [TestCase(Setup.Paused, Command.Take)]
        [TestCase(Setup.Paused, Command.Discard)]
        [TestCase(Setup.Paused, Command.Cancel)]
        [TestCase(Setup.Won, Command.Place)]
        [TestCase(Setup.Won, Command.Take)]
        [TestCase(Setup.Won, Command.Discard)]
        [TestCase(Setup.Won, Command.Cancel)]
        [TestCase(Setup.Lost, Command.Place)]
        [TestCase(Setup.Lost, Command.Take)]
        [TestCase(Setup.Lost, Command.Discard)]
        [TestCase(Setup.Lost, Command.Cancel)]
        public void CommandOutsideRunning_ReturnsFalse_AndChangesNothing(Setup setup, Command command)
        {
            ShiftSimulation shift = Arrange(setup);
            Assert.AreNotEqual(ShiftState.Running, shift.State, "Підготовка тесту");
            ShiftSnapshot before = shift.CaptureSnapshot();

            Assert.IsFalse(Send(shift, command, 0));
            Assert.IsFalse(Send(shift, command, command == Command.Cancel ? 0 : GrillSlot.Any));

            Assert.AreEqual(before, shift.CaptureSnapshot());
        }

        [Test]
        public void CommandsDuringResolving_AreRejected()
        {
            var goal = new GrillMeddlingGoal();
            var shift = new ShiftSimulation(GrillTestKit.Config(durationSec: 1), ShiftTestKit.Seed, goal);
            shift.Start();

            ShiftTestKit.TickUntilNotRunning(shift);

            Assert.IsTrue(goal.WasCalled);
            Assert.IsFalse(goal.AnythingAccepted, "У Resolving жодна команда гриля не має спрацювати");
            Assert.AreEqual(0, shift.QueuedActionCount);
        }

        /// <summary>Ціль, яка під час перевірки намагається керувати грилем.</summary>
        private sealed class GrillMeddlingGoal : IShiftGoal
        {
            public bool WasCalled { get; private set; }

            public bool AnythingAccepted { get; private set; }

            public bool IsAchieved(ShiftSimulation shift)
            {
                WasCalled = true;
                AnythingAccepted = shift.PlaceMeat() | shift.TakeMeat() | shift.DiscardBurntMeat()
                    | shift.CancelQueuedAction(0);
                return false;
            }
        }
    }
}
