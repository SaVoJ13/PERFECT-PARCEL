namespace KebabStreet.Core
{
    /// <summary>
    /// Один слот гриля. Зовні доступний лише для читання: змінюють його тільки
    /// такт симуляції та виконані команди з черги дій.
    /// </summary>
    public sealed class GrillSlot
    {
        /// <summary>Замість номера слота в команді: «обери слот сам» (тап по станції).</summary>
        public const int Any = -1;

        private readonly long _cookTicks;
        private readonly long _burntAtTicks;
        private readonly long _warnTicks;

        internal GrillSlot(int index, long cookTicks, long burnAfterDoneTicks, long warnBeforeBurnTicks)
        {
            Index = index;
            _cookTicks = cookTicks;
            _burntAtTicks = cookTicks + burnAfterDoneTicks;
            _warnTicks = warnBeforeBurnTicks;
            State = MeatState.Empty;
        }

        /// <summary>Номер слота, від 0.</summary>
        public int Index { get; }

        /// <summary>Стан м'яса в слоті.</summary>
        public MeatState State { get; private set; }

        /// <summary>
        /// Скільки тактів м'ясо на грилі. Росте лише в Raw і Done; у Burnt зупиняється
        /// на такті пригорання; у порожньому слоті 0.
        /// </summary>
        public long AgeTicks { get; private set; }

        /// <summary>Скільки тактів лишилось до готовності (0, якщо м'ясо не сире).</summary>
        public long TicksUntilDone => State == MeatState.Raw ? _cookTicks - AgeTicks : 0L;

        /// <summary>Скільки тактів лишилось до пригорання (0 для порожнього і пригорілого).</summary>
        public long TicksUntilBurnt =>
            State == MeatState.Raw || State == MeatState.Done ? _burntAtTicks - AgeTicks : 0L;

        /// <summary>Готове м'ясо скоро пригорить (grill.warnBeforeBurnSec): час попередження.</summary>
        public bool IsBurnWarning => State == MeatState.Done && _burntAtTicks - AgeTicks <= _warnTicks;

        /// <summary>Один такт гриля; true — саме на цьому такті м'ясо пригоріло.</summary>
        internal bool Advance()
        {
            if (State == MeatState.Raw)
            {
                AgeTicks++;
                if (AgeTicks >= _cookTicks)
                {
                    State = MeatState.Done;
                }

                return false;
            }

            if (State == MeatState.Done)
            {
                AgeTicks++;
                if (AgeTicks >= _burntAtTicks)
                {
                    State = MeatState.Burnt;
                    return true;
                }
            }

            return false;
        }

        internal void PutRaw()
        {
            State = MeatState.Raw;
            AgeTicks = 0L;
        }

        internal void Clear()
        {
            State = MeatState.Empty;
            AgeTicks = 0L;
        }
    }
}
