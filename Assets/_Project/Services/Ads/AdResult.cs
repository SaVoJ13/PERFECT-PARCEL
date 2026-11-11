namespace KebabStreet.Services
{
    /// <summary>Остаточний результат показу реклами. Колбек отримує рівно один такий результат.</summary>
    public enum AdResult
    {
        /// <summary>Реклама не готова або недоступна (немає сервісу, мережі, згоди). Нагороди немає.</summary>
        NotReady = 0,

        /// <summary>Помилка під час показу. Нагороди немає.</summary>
        Failed = 1,

        /// <summary>Рекламу показано й закрито без нагороди (interstitial, або rewarded закрито достроково).</summary>
        Closed = 2,

        /// <summary>Лише rewarded: гравець дивився до кінця — нагороду можна нараховувати (один раз).</summary>
        Rewarded = 3,
    }
}
