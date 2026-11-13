namespace KebabStreet.Core
{
    /// <summary>Підсумок зміни: ціль виконано чи ні.</summary>
    public enum ShiftOutcome
    {
        Won = 1,
        Lost = 2,
    }

    /// <summary>
    /// Ключі локалізації головної причини поразки (GAME_DESIGN.md, розд. 9). Показується одна
    /// причина; пріоритет — у <see cref="ShiftEvaluator"/>.
    /// </summary>
    public static class LoseReasons
    {
        /// <summary>Мало видано до кінця часу (запасна причина).</summary>
        public const string TooSlow = "lost.reason.tooSlow";

        /// <summary>Пішло забагато клієнтів.</summary>
        public const string CustomersLeft = "lost.reason.customersLeft";

        /// <summary>Пригоріло забагато м'яса.</summary>
        public const string Burnt = "lost.reason.burnt";

        /// <summary>Видано забагато хибних страв.</summary>
        public const string WrongDishes = "lost.reason.wrongDishes";
    }

    /// <summary>
    /// Результат зміни, обчислений за лічильниками симуляції. Чиста незмінна структура:
    /// лише цілі числа й ключ причини, тож порівняння точне.
    /// </summary>
    public readonly struct ShiftResult
    {
        public ShiftResult(
            ShiftOutcome outcome,
            int stars,
            string loseReason,
            long shortfall,
            long rewardCoins,
            long coinsEarned,
            long servedCount,
            long lostCustomers,
            long wrongDishes,
            long burntCount,
            long maxCombo)
        {
            Outcome = outcome;
            Stars = stars;
            LoseReason = loseReason;
            Shortfall = shortfall;
            RewardCoins = rewardCoins;
            CoinsEarned = coinsEarned;
            ServedCount = servedCount;
            LostCustomers = lostCustomers;
            WrongDishes = wrongDishes;
            BurntCount = burntCount;
            MaxCombo = maxCombo;
        }

        public ShiftOutcome Outcome { get; }

        public bool Won
        {
            get { return Outcome == ShiftOutcome.Won; }
        }

        /// <summary>Зірки: 0 при поразці, 1–3 при перемозі.</summary>
        public int Stars { get; }

        /// <summary>Ключ lost.reason.* при поразці; null при перемозі.</summary>
        public string LoseReason { get; }

        /// <summary>Скільки не вистачило до цілі (клієнтів або монет); 0, якщо ціль виконано.</summary>
        public long Shortfall { get; }

        /// <summary>Нагорода монет у гаманець: coinsEarned при перемозі, 0 при поразці.</summary>
        public long RewardCoins { get; }

        public long CoinsEarned { get; }

        public long ServedCount { get; }

        public long LostCustomers { get; }

        public long WrongDishes { get; }

        public long BurntCount { get; }

        public long MaxCombo { get; }
    }

    /// <summary>
    /// Лічильники зміни, за якими рахується результат (GAME_DESIGN.md, розд. 7 і 9). Окрема структура,
    /// щоб правила оцінювання перевірялися без запуску симуляції.
    /// </summary>
    public readonly struct ShiftCounters
    {
        public ShiftCounters(
            long coinsEarned, long servedCount, long lostCustomers, long wrongDishes, long burntCount, long maxCombo)
        {
            CoinsEarned = coinsEarned;
            ServedCount = servedCount;
            LostCustomers = lostCustomers;
            WrongDishes = wrongDishes;
            BurntCount = burntCount;
            MaxCombo = maxCombo;
        }

        public long CoinsEarned { get; }

        public long ServedCount { get; }

        public long LostCustomers { get; }

        public long WrongDishes { get; }

        public long BurntCount { get; }

        public long MaxCombo { get; }
    }
}
