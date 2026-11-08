using System;
using System.Collections.Generic;

namespace PerfectParcel.Core
{
    /// <summary>
    /// Легковаговий слабкозв'язаний Event Bus для обміну подіями між системами
    /// без жорстких взаємних посилань (Decoupled Architecture).
    /// </summary>
    public static class EventBus
    {
        private static readonly Dictionary<Type, List<Delegate>> Subscribers = new Dictionary<Type, List<Delegate>>();

        public static void Subscribe<T>(Action<T> handler)
        {
            var type = typeof(T);
            if (!Subscribers.TryGetValue(type, out var list))
            {
                list = new List<Delegate>();
                Subscribers[type] = list;
            }
            if (!list.Contains(handler))
            {
                list.Add(handler);
            }
        }

        public static void Unsubscribe<T>(Action<T> handler)
        {
            var type = typeof(T);
            if (Subscribers.TryGetValue(type, out var list))
            {
                list.Remove(handler);
                if (list.Count == 0)
                {
                    Subscribers.Remove(type);
                }
            }
        }

        public static void Publish<T>(T eventMessage)
        {
            var type = typeof(T);
            if (Subscribers.TryGetValue(type, out var list))
            {
                // Створюємо копію для безпечної ітерації при відписці під час виклику
                var handlers = list.ToArray();
                for (int i = 0; i < handlers.Length; i++)
                {
                    ((Action<T>)handlers[i])?.Invoke(eventMessage);
                }
            }
        }

        public static void Clear()
        {
            Subscribers.Clear();
        }
    }

    #region Event Definitions
    public struct GameStateChangedEvent
    {
        public GameState NewState;
        public GameState PreviousState;
    }

    public struct ParcelSortedEvent
    {
        public int ParcelId;
        public bool IsCorrect;
        public int PointsEarned;
        public int CoinsEarned;
        public int CurrentCombo;
        public Parcels.ParcelType ParcelType;
    }

    public struct ParcelMissedEvent
    {
        public int ParcelId;
        public int RemainingLives;
    }

    public struct LivesChangedEvent
    {
        public int CurrentLives;
        public int MaxLives;
        /// <summary>true — життя втрачено (shake/вібрація/звук помилки); false — лише синхронізація HUD (старт рівня).</summary>
        public bool Decreased;
    }

    public struct ScoreChangedEvent
    {
        public int CurrentScore;
        public int CurrentCoins;
        public int SortedCount;
        public int TargetCount;
    }
    #endregion
}
