namespace PerfectParcel.Core
{
    /// <summary>
    /// Глобальні константи ігрового процесу та налаштування розмірів.
    /// Затверджено в межах Phase 0 / Phase 1 (Roadmap 1.4).
    /// </summary>
    public static class GameConstants
    {
        // Базові розміри віртуального екрана (Portrait 9:16)
        public const float ReferenceScreenWidth = 1080f;
        public const float ReferenceScreenHeight = 1920f;

        // Початкові ігрові параметри прототипу
        public const int DefaultStartingLives = 3;
        public const int TargetParcelsLevel1 = 15;
        public const float DefaultConveyorSpeed = 260f; // пікселів за секунду
        public const float DefaultSpawnInterval = 1.6f;  // секунд між появою

        // Зони конвеєра по осі Y
        public const float ConveyorSpawnY = 750f;
        public const float ConveyorDecisionZoneY = -150f;
        public const float ConveyorDropLineY = -550f;

        // Налаштування свайпу
        public const float MinSwipeDistance = 50f;
        public const float SwipeThresholdDegrees = 45f;

        // Базові нагороди
        public const int BaseStandardPoints = 100;
        public const int BaseStandardCoins = 5;
    }
}
