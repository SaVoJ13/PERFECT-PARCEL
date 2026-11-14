using System;

namespace KebabStreet.Game
{
    /// <summary>
    /// Події зміни, за якими працює навчання (tutorial.json: showWhen / completeWhen). Core не має потоку подій,
    /// тож їх виводить <see cref="TutorialEventTracker"/> зі стану симуляції.
    /// </summary>
    public enum TutorialEventKind
    {
        None = 0,
        ShiftStarted = 1,
        PlaceMeat = 2,
        MeatDone = 3,
        TakeMeat = 4,
        AddIngredient = 5,
        ServeDish = 6,
        MeatBurnt = 7,
        DiscardBurntMeat = 8,
        CustomerArrived = 9,
        OrderShown = 10,
    }

    /// <summary>Подія навчання: вид і необов'язкові уточнення (інгредієнт, тип клієнта) — рядки з конфігів, без створення нових.</summary>
    public readonly struct TutorialEvent : IEquatable<TutorialEvent>
    {
        public TutorialEvent(TutorialEventKind kind, string ingredient = null, string customer = null)
        {
            Kind = kind;
            Ingredient = ingredient;
            Customer = customer;
        }

        public TutorialEventKind Kind { get; }

        /// <summary>Інгредієнт (AddIngredient; для OrderShown — необов'язковий інгредієнт замовлення); null — не стосується.</summary>
        public string Ingredient { get; }

        /// <summary>Ідентифікатор типу клієнта (CustomerArrived, ServeDish, OrderShown); null — не стосується.</summary>
        public string Customer { get; }

        public bool Equals(TutorialEvent other)
        {
            return Kind == other.Kind
                && string.Equals(Ingredient, other.Ingredient, StringComparison.Ordinal)
                && string.Equals(Customer, other.Customer, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is TutorialEvent other && Equals(other);
        }

        public override int GetHashCode()
        {
            return ((int)Kind * 397) ^ (Ingredient == null ? 0 : Ingredient.GetHashCode());
        }

        public override string ToString()
        {
            return Kind + (Ingredient == null ? string.Empty : ":" + Ingredient) + (Customer == null ? string.Empty : "@" + Customer);
        }
    }
}
