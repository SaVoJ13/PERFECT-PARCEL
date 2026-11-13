using System;
using KebabStreet.Core;
using KebabStreet.Services;

namespace KebabStreet.Game
{
    /// <summary>
    /// Композиція гри (bootstrap): один SaveService, годинник, профіль, потік екранів, тексти.
    /// Створюється в сцені Boot і живе до кінця процесу.
    /// </summary>
    public sealed class GameContext
    {
        private LevelCatalog _levels;

        public GameContext(
            ISaveStorage storage,
            IClock clock,
            ILocalizer text,
            IAdsService ads = null,
            IPurchaseService purchases = null,
            IAnalyticsService analytics = null)
        {
            Clock = clock ?? throw new ArgumentNullException(nameof(clock));
            Text = text ?? throw new ArgumentNullException(nameof(text));
            Ads = ads ?? new NullAdsService();
            Purchases = purchases ?? new NullPurchaseService();
            Analytics = analytics ?? new NullAnalyticsService();
            Saves = new SaveService(storage, clock);
            Profile = new ProfileSession(Saves);
            Flow = new GameFlow();
        }

        /// <summary>Поточний контекст; null, доки сцена Boot його не встановила.</summary>
        public static GameContext Current { get; private set; }

        public IClock Clock { get; }

        public ILocalizer Text { get; }

        /// <summary>Реклама; до Етапу 3 — заглушка Null. UI показує кнопку лише коли <c>Ads.IsAvailable</c>.</summary>
        public IAdsService Ads { get; }

        /// <summary>Покупки; до Етапу 3 — заглушка Null. UI показує кнопку лише коли <c>Purchases.IsAvailable</c>.</summary>
        public IPurchaseService Purchases { get; }

        public IAnalyticsService Analytics { get; }

        public SaveService Saves { get; }

        public ProfileSession Profile { get; }

        public GameFlow Flow { get; }

        /// <summary>Встановлює єдиний контекст. Повторне встановлення відхиляється: SaveService має бути один.</summary>
        public static void Install(GameContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            if (Current != null)
            {
                throw new InvalidOperationException("Контекст гри вже встановлено.");
            }

            Current = context;
        }

        /// <summary>Лише для тестів.</summary>
        public static void ResetForTests()
        {
            Current = null;
        }

        /// <summary>Каталог рівнів із Resources/Config; завантажується один раз. Помилка конфігів — виняток (її ловить тест).</summary>
        public LevelCatalog Levels
        {
            get
            {
                if (_levels == null)
                {
                    ConfigResult<LevelCatalog> loaded = ResourceConfigSource.LoadCatalog();
                    if (!loaded.IsValid)
                    {
                        throw new InvalidOperationException("Конфіги рівнів хибні: " + string.Join("; ", loaded.Errors));
                    }

                    _levels = loaded.Value;
                }

                return _levels;
            }
        }

        /// <summary>Рівень, обраний у меню (номер 1…); 0 — не обрано.</summary>
        public int SelectedLevelNumber { get; private set; }

        /// <summary>Скільки змін запущено за цей процес (входить у seed).</summary>
        public int AttemptCount { get; private set; }

        /// <summary>Обирає рівень для наступної зміни; false, якщо такого рівня немає в каталозі.</summary>
        public bool SelectLevel(int number)
        {
            if (Levels.FindByNumber(number) == null)
            {
                return false;
            }

            SelectedLevelNumber = number;
            return true;
        }

        /// <summary>Новий seed для спроби: годинник і лічильник спроб (кожна спроба — інший seed).</summary>
        public ulong NextShiftSeed()
        {
            AttemptCount++;
            return ShiftSeeds.Next(Clock.UtcNowUnixSec, AttemptCount);
        }

        /// <summary>Нова порожня зміна; seed береться з годинника.</summary>
        public ShiftSimulation NewShift()
        {
            return EmptyShift.Create((ulong)Math.Max(0L, Clock.UtcNowUnixSec));
        }
    }
}
