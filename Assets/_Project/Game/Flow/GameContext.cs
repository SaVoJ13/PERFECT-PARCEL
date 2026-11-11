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
        public GameContext(ISaveStorage storage, IClock clock, ILocalizer text)
        {
            Clock = clock ?? throw new ArgumentNullException(nameof(clock));
            Text = text ?? throw new ArgumentNullException(nameof(text));
            Saves = new SaveService(storage, clock);
            Profile = new ProfileSession(Saves);
            Flow = new GameFlow();
        }

        /// <summary>Поточний контекст; null, доки сцена Boot його не встановила.</summary>
        public static GameContext Current { get; private set; }

        public IClock Clock { get; }

        public ILocalizer Text { get; }

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

        /// <summary>Нова порожня зміна; seed береться з годинника.</summary>
        public ShiftSimulation NewShift()
        {
            return EmptyShift.Create((ulong)Math.Max(0L, Clock.UtcNowUnixSec));
        }
    }
}
