namespace KebabStreet.Core
{
    /// <summary>
    /// Станція кухні (GAME_DESIGN.md, розд. 3; stations.json → stations[id]).
    /// Числові значення входять у конфіги й повідомлення — не змінювати порядок.
    /// </summary>
    public enum StationKind
    {
        /// <summary>Станції немає (значення за замовчуванням).</summary>
        None = 0,

        /// <summary>Гриль: джерело готового м'яса. Параметри — окремо, у <see cref="GrillConfig"/>.</summary>
        Grill = 1,

        /// <summary>Хліб (піта).</summary>
        Bread = 2,

        /// <summary>Соуси.</summary>
        Sauce = 3,

        /// <summary>Салатний бар.</summary>
        Salad = 4,

        /// <summary>Робоча дошка: збирання і видача страви.</summary>
        Board = 5,

        /// <summary>Смітник: очистити дошку.</summary>
        Bin = 6,
    }

    /// <summary>
    /// Параметри станції: з якого рівня доступна (stations[id].unlockLevel) і скільки триває
    /// одна дія гравця на ній (stations[id].actionTimeSec, тут — цілі мілісекунди). Незмінний об'єкт.
    /// </summary>
    public sealed class StationConfig
    {
        public StationConfig(StationKind kind, int unlockLevel, int actionMs)
        {
            Kind = kind;
            UnlockLevel = unlockLevel;
            ActionMs = actionMs;
        }

        /// <summary>Яка це станція.</summary>
        public StationKind Kind { get; }

        /// <summary>Номер рівня, з якого станція доступна.</summary>
        public int UnlockLevel { get; }

        /// <summary>Тривалість однієї дії гравця на станції, мс; 0 — миттєво.</summary>
        public int ActionMs { get; }

        /// <summary>Тривалість дії в тактах (округлення до найближчого такту).</summary>
        public long ActionTicks(int ticksPerSecond)
        {
            return TickTime.FromMs(ActionMs, ticksPerSecond);
        }
    }

    /// <summary>Переведення мілісекунд у такти цілочисельною арифметикою.</summary>
    public static class TickTime
    {
        private const int MsPerSecond = 1000;

        /// <summary>Округлення до найближчого такту, половина — вгору (як у <see cref="GrillConfig"/>).</summary>
        public static long FromMs(int ms, int ticksPerSecond)
        {
            return ((long)ms * ticksPerSecond + MsPerSecond / 2) / MsPerSecond;
        }
    }
}
