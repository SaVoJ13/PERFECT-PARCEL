using System;

namespace KebabStreet.Core
{
    /// <summary>
    /// Гриль зміни (GAME_DESIGN.md, розд. 4): слоти м'яса з таймерами в цілих тактах.
    /// Випадковості не використовує. Зовні доступний лише для читання: стан змінюють
    /// тільки такт <see cref="ShiftSimulation"/> і виконані дії з її черги.
    /// </summary>
    public sealed class Grill
    {
        private readonly GrillSlot[] _slots;

        internal Grill(GrillConfig config, int ticksPerSecond)
        {
            Config = config ?? throw new ArgumentNullException(nameof(config));
            CookTicks = config.CookTicks(ticksPerSecond);
            BurnAfterDoneTicks = config.BurnAfterDoneTicks(ticksPerSecond);
            WarnBeforeBurnTicks = config.WarnBeforeBurnTicks(ticksPerSecond);
            ActionTicks = config.ActionTicks(ticksPerSecond);

            _slots = new GrillSlot[config.Slots];
            for (int i = 0; i < _slots.Length; i++)
            {
                _slots[i] = new GrillSlot(i, CookTicks, BurnAfterDoneTicks, WarnBeforeBurnTicks);
            }
        }

        /// <summary>Параметри гриля.</summary>
        public GrillConfig Config { get; }

        /// <summary>Час приготування Raw → Done, тактів.</summary>
        public long CookTicks { get; }

        /// <summary>Вікно Done → Burnt, тактів.</summary>
        public long BurnAfterDoneTicks { get; }

        /// <summary>Час попередження перед пригоранням, тактів.</summary>
        public long WarnBeforeBurnTicks { get; }

        /// <summary>Тривалість однієї дії гравця на грилі, тактів.</summary>
        public long ActionTicks { get; }

        /// <summary>Кількість слотів.</summary>
        public int SlotCount => _slots.Length;

        /// <summary>Скільки шматків покладено на гриль за зміну.</summary>
        public long PlacedCount { get; private set; }

        /// <summary>Скільки готових шматків знято з гриля за зміну.</summary>
        public long TakenCount { get; private set; }

        /// <summary>Скільки шматків пригоріло за зміну (рахується в момент пригорання).</summary>
        public long BurntCount { get; private set; }

        /// <summary>Скільки пригорілих шматків викинуто за зміну.</summary>
        public long DiscardedCount { get; private set; }

        /// <summary>
        /// Сума штрафів за викинуте пригоріле (grill.burntPenaltyCoins за кожен шматок), монет.
        /// Гриль лише рахує суму; списання з рахунку зміни — справа економіки.
        /// </summary>
        public long PenaltyCoins { get; private set; }

        /// <summary>Кількість порожніх слотів.</summary>
        public int FreeSlotCount
        {
            get
            {
                int free = 0;
                for (int i = 0; i < _slots.Length; i++)
                {
                    if (_slots[i].State == MeatState.Empty)
                    {
                        free++;
                    }
                }

                return free;
            }
        }

        /// <summary>Слот за номером (0..SlotCount-1).</summary>
        public GrillSlot GetSlot(int index)
        {
            return _slots[index];
        }

        /// <summary>Чи може команда назвати такий слот: номер наявного слота або <see cref="GrillSlot.Any"/>.</summary>
        public bool IsValidTarget(int slot)
        {
            return slot == GrillSlot.Any || (slot >= 0 && slot < _slots.Length);
        }

        /// <summary>Знімок повного стану гриля.</summary>
        public GrillSnapshot CaptureSnapshot()
        {
            var states = new MeatState[_slots.Length];
            var ages = new long[_slots.Length];
            for (int i = 0; i < _slots.Length; i++)
            {
                states[i] = _slots[i].State;
                ages[i] = _slots[i].AgeTicks;
            }

            return new GrillSnapshot(
                states, ages, PlacedCount, TakenCount, BurntCount, DiscardedCount, PenaltyCoins);
        }

        /// <summary>Один такт: просуває таймери м'яса в усіх слотах.</summary>
        internal void Advance()
        {
            for (int i = 0; i < _slots.Length; i++)
            {
                if (_slots[i].Advance())
                {
                    BurntCount++;
                }
            }
        }

        /// <summary>Покласти сире м'ясо: у названий слот або в перший вільний.</summary>
        internal ShiftActionResult Place(int slot)
        {
            if (slot == GrillSlot.Any)
            {
                slot = FindFirst(MeatState.Empty);
                if (slot < 0)
                {
                    return ShiftActionResult.GrillFull;
                }
            }
            else if (_slots[slot].State != MeatState.Empty)
            {
                return ShiftActionResult.SlotOccupied;
            }

            _slots[slot].PutRaw();
            PlacedCount++;
            return ShiftActionResult.Done;
        }

        /// <summary>
        /// Зняти готове м'ясо: з названого слота або те, що найближче до пригорання
        /// (за рівного часу — з меншим номером слота).
        /// </summary>
        internal ShiftActionResult Take(int slot)
        {
            if (slot == GrillSlot.Any)
            {
                slot = FindOldestDone();
                if (slot < 0)
                {
                    return ShiftActionResult.NoDoneMeat;
                }
            }

            switch (_slots[slot].State)
            {
                case MeatState.Empty:
                    return ShiftActionResult.SlotEmpty;
                case MeatState.Raw:
                    return ShiftActionResult.MeatRaw;
                case MeatState.Burnt:
                    return ShiftActionResult.MeatBurnt;
            }

            _slots[slot].Clear();
            TakenCount++;
            return ShiftActionResult.Done;
        }

        /// <summary>Викинути пригоріле: з названого слота або з першого пригорілого.</summary>
        internal ShiftActionResult Discard(int slot)
        {
            if (slot == GrillSlot.Any)
            {
                slot = FindFirst(MeatState.Burnt);
                if (slot < 0)
                {
                    return ShiftActionResult.NoBurntMeat;
                }
            }

            switch (_slots[slot].State)
            {
                case MeatState.Empty:
                    return ShiftActionResult.SlotEmpty;
                case MeatState.Raw:
                case MeatState.Done:
                    return ShiftActionResult.MeatNotBurnt;
            }

            _slots[slot].Clear();
            DiscardedCount++;
            PenaltyCoins += Config.BurntPenaltyCoins;
            return ShiftActionResult.Done;
        }

        private int FindFirst(MeatState state)
        {
            for (int i = 0; i < _slots.Length; i++)
            {
                if (_slots[i].State == state)
                {
                    return i;
                }
            }

            return -1;
        }

        private int FindOldestDone()
        {
            int found = -1;
            for (int i = 0; i < _slots.Length; i++)
            {
                if (_slots[i].State == MeatState.Done
                    && (found < 0 || _slots[i].AgeTicks > _slots[found].AgeTicks))
                {
                    found = i;
                }
            }

            return found;
        }
    }
}
