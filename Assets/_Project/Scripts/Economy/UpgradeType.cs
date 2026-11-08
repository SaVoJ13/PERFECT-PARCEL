namespace PerfectParcel.Economy
{
    /// <summary>
    /// 7 типів складських покращень відповідно до ECONOMY_DESIGN.md.
    /// </summary>
    public enum UpgradeType
    {
        SmoothBelt = 0,         // Плавний конвеєр (-3% швидкості)
        SafetyBuffer = 1,       // Буфер безпеки (+серця)
        MagneticGuides = 2,     // Магнітний сортувальник (+зона детекції)
        AutoScanner = 3,        // Швидкий авто-сканер (швидше розблокування)
        CoinMultiplier = 4,     // Золотий бонус (+5% монет за рівень)
        CushionedBin = 5,       // М'яка подушка (шанс врятувати крихку коробку)
        ExpressDeliveryFee = 6  // Преміальний контракт (+монети за експрес)
    }
}
