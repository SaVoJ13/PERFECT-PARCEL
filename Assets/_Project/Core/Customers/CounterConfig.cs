using System;
using System.Collections.Generic;
using System.Globalization;

namespace KebabStreet.Core
{
    /// <summary>
    /// Параметри прилавка і клієнтів на зміну (GAME_DESIGN.md, розд. 6): слоти, комбо, штраф
    /// терпіння за хибну страву, типи клієнтів і розклад приходу. Незмінний об'єкт; у логіці
    /// немає власних чисел. Навчальні рівні (ADR-020) відрізняються лише значеннями параметрів.
    /// </summary>
    public sealed class CounterConfig
    {
        /// <summary>Найбільша кількість слотів прилавка.</summary>
        public const int MaxSlots = 12;

        /// <summary>Найбільша кількість типів клієнтів.</summary>
        public const int MaxTypes = 32;

        /// <summary>Найбільше терпіння, пільговий час чи штраф терпіння, мс.</summary>
        public const int MaxPatienceMs = 600000;

        /// <summary>Найбільше вікно комбо, мс.</summary>
        public const int MaxComboWindowMs = 600000;

        /// <summary>Найбільший множник плати, чайові чи крок комбо, відсотків.</summary>
        public const int MaxPercent = 1000;

        /// <summary>Найбільша кількість кроків комбо, за які росте надбавка.</summary>
        public const int MaxComboSteps = 100;

        private readonly CustomerType[] _types;

        /// <summary>Список типів копіюється; null означає порожній список (це помилка конфіга).</summary>
        public CounterConfig(
            int slots,
            int comboWindowMs,
            int comboStepPercent,
            int comboMaxSteps,
            int wrongDishPatiencePenaltyMs,
            bool countLostCustomers,
            bool extraOptionalIsWrong,
            IEnumerable<CustomerType> types,
            ArrivalSchedule schedule)
        {
            Slots = slots;
            ComboWindowMs = comboWindowMs;
            ComboStepPercent = comboStepPercent;
            ComboMaxSteps = comboMaxSteps;
            WrongDishPatiencePenaltyMs = wrongDishPatiencePenaltyMs;
            CountLostCustomers = countLostCustomers;
            ExtraOptionalIsWrong = extraOptionalIsWrong;
            _types = types == null ? new CustomerType[0] : new List<CustomerType>(types).ToArray();
            Schedule = schedule;
        }

        /// <summary>Кількість слотів прилавка (level.counterSlots).</summary>
        public int Slots { get; }

        /// <summary>Вікно комбо, мс (rules.comboWindowSec): наступна вдала видача не пізніше — комбо росте.</summary>
        public int ComboWindowMs { get; }

        /// <summary>Надбавка до плати за страву за кожен крок комбо після першої видачі, відсотків.</summary>
        public int ComboStepPercent { get; }

        /// <summary>За скільки кроків комбо надбавка ще росте (далі комбо рахується, але надбавка та сама).</summary>
        public int ComboMaxSteps { get; }

        /// <summary>Скільки терпіння втрачає клієнт за хибну страву, мс (rules.wrongDishPatiencePenalty).</summary>
        public int WrongDishPatiencePenaltyMs { get; }

        /// <summary>
        /// Чи рахувати клієнтів, що пішли, у lostCustomers. false — навчальні рівні 1–2 (ADR-020):
        /// клієнт іде, але втрата не рахується. Це значення конфіга, а не гілка коду під рівень.
        /// </summary>
        public bool CountLostCustomers { get; }

        /// <summary>
        /// true — необов'язковий інгредієнт, якого клієнт не просив, робить страву хибною
        /// (GAME_DESIGN.md, розд. 5). false — хибною страву роблять лише інгредієнти, які клієнт
        /// просив не класти; решта необов'язкових лише додають ціну.
        /// </summary>
        public bool ExtraOptionalIsWrong { get; }

        /// <summary>Розклад приходу клієнтів.</summary>
        public ArrivalSchedule Schedule { get; }

        /// <summary>Кількість типів клієнтів.</summary>
        public int TypeCount => _types.Length;

        public CustomerType GetCustomerType(int index)
        {
            return _types[index];
        }

        /// <summary>Номер типу клієнта за назвою з конфіга; -1 — такого немає.</summary>
        public int IndexOfType(string id)
        {
            for (int i = 0; i < _types.Length; i++)
            {
                if (_types[i] != null && string.Equals(_types[i].Id, id, StringComparison.Ordinal))
                {
                    return i;
                }
            }

            return -1;
        }

        /// <summary>
        /// Перевіряє конфіг. board — дошка зміни (рецепти й інгредієнти замовлень); boardIsValid —
        /// чи сама дошка без помилок (інакше замовлення не звіряються: помилки дошки вже названі).
        /// Порожній список — конфіг придатний; інакше кожен рядок називає поле і причину.
        /// </summary>
        public IReadOnlyList<string> GetErrors(BoardConfig board, bool boardIsValid, int ticksPerSecond)
        {
            var errors = new List<string>();
            CheckRange(errors, "counter.slots", Slots, 1, MaxSlots);
            CheckRange(errors, "rules.comboWindowMs", ComboWindowMs, 0, MaxComboWindowMs);
            CheckRange(errors, "rules.comboStepPercent", ComboStepPercent, 0, MaxPercent);
            CheckRange(errors, "rules.comboMaxSteps", ComboMaxSteps, 0, MaxComboSteps);
            CheckRange(errors, "rules.wrongDishPatiencePenaltyMs", WrongDishPatiencePenaltyMs, 0, MaxPatienceMs);
            if (board == null)
            {
                errors.Add("counter: клієнтам потрібна дошка (board)");
            }

            bool rateOk = ticksPerSecond >= ShiftConfig.MinTicksPerSecond
                && ticksPerSecond <= ShiftConfig.MaxTicksPerSecond;
            CheckTypes(errors, rateOk ? ticksPerSecond : 0);
            CheckSchedule(errors, board != null && boardIsValid ? board : null);
            return errors;
        }

        /// <summary>
        /// Переводить замовлення в номери й маски рецепта: recipeIndex — номер рецепта в дошці,
        /// requestedMask — необов'язкові, які мають бути у страві (замовлення + «завжди» типу),
        /// excludeMask — які клієнт просив не класти (замовлення + тип). Біт i — i-й необов'язковий
        /// рецепта, як у <see cref="Dish.OptionalMask"/>. errors = null — помилки не збираються.
        /// </summary>
        internal static bool TryResolveOrder(
            BoardConfig board,
            CustomerType type,
            Order order,
            string name,
            List<string> errors,
            out int recipeIndex,
            out int requestedMask,
            out int excludeMask)
        {
            requestedMask = 0;
            excludeMask = 0;
            recipeIndex = board.IndexOfRecipe(order.Recipe);
            if (recipeIndex < 0)
            {
                Report(errors, name + ".order.recipe=" + order.Recipe + ": такого рецепта немає");
                return false;
            }

            bool ok = true;
            if (!board.IsRecipeAvailable(recipeIndex))
            {
                Report(errors, string.Format(
                    CultureInfo.InvariantCulture,
                    "{0}.order.recipe={1}: рецепт недоступний на рівні {2}",
                    name,
                    order.Recipe,
                    board.Level));
                ok = false;
            }

            Recipe recipe = board.GetRecipe(recipeIndex);
            for (int i = 0; i < order.OptionalCount; i++)
            {
                ok &= AddToMask(board, recipe, order.GetOptional(i), true, name + ".order.optional", errors, ref requestedMask);
            }

            for (int i = 0; i < type.AlwaysCount; i++)
            {
                // Те, що тип просить завжди, може вже бути названо в самому замовленні.
                string id = type.GetAlways(i);
                int bit = OptionalBit(recipe, id);
                if (bit == 0 || (requestedMask & bit) == 0)
                {
                    ok &= AddToMask(board, recipe, id, true, name + ".customer.always", errors, ref requestedMask);
                }
            }

            for (int i = 0; i < order.ExcludeCount; i++)
            {
                ok &= AddToMask(board, recipe, order.GetExclude(i), false, name + ".order.exclude", errors, ref excludeMask);
            }

            for (int i = 0; i < type.ExcludeCount; i++)
            {
                string id = type.GetExclude(i);
                int bit = OptionalBit(recipe, id);
                if (bit == 0 || (excludeMask & bit) == 0)
                {
                    ok &= AddToMask(board, recipe, id, false, name + ".customer.exclude", errors, ref excludeMask);
                }
            }

            if ((requestedMask & excludeMask) != 0)
            {
                Report(errors, name + ".order: той самий інгредієнт і просять, і просять не класти");
                ok = false;
            }

            return ok;
        }

        private static bool AddToMask(
            BoardConfig board,
            Recipe recipe,
            string id,
            bool mustBeAvailable,
            string name,
            List<string> errors,
            ref int mask)
        {
            int bit = OptionalBit(recipe, id);
            if (bit == 0)
            {
                Report(errors, name + ": «" + id + "» немає серед необов'язкових рецепта " + recipe.Id);
                return false;
            }

            if ((mask & bit) != 0)
            {
                Report(errors, name + ": «" + id + "» повторюється");
                return false;
            }

            if (mustBeAvailable)
            {
                StationKind station = board.GetIngredient(board.IndexOfIngredient(id)).Station;
                if (station != StationKind.Grill && !board.IsStationAvailable(station))
                {
                    Report(errors, string.Format(
                        CultureInfo.InvariantCulture,
                        "{0}: інгредієнт «{1}» недоступний на рівні {2}",
                        name,
                        id,
                        board.Level));
                    return false;
                }
            }

            mask |= bit;
            return true;
        }

        /// <summary>Біт необов'язкового інгредієнта в масці рецепта; 0 — такого серед необов'язкових немає.</summary>
        private static int OptionalBit(Recipe recipe, string id)
        {
            for (int i = 0; i < recipe.OptionalCount; i++)
            {
                if (string.Equals(recipe.GetOptional(i), id, StringComparison.Ordinal))
                {
                    return 1 << i;
                }
            }

            return 0;
        }

        private void CheckTypes(List<string> errors, int ticksPerSecond)
        {
            if (_types.Length < 1 || _types.Length > MaxTypes)
            {
                errors.Add(string.Format(
                    CultureInfo.InvariantCulture, "customers.count={0}: дозволено 1..{1}", _types.Length, MaxTypes));
            }

            for (int i = 0; i < _types.Length; i++)
            {
                CustomerType type = _types[i];
                string name = Indexed("customers", i);
                if (type == null)
                {
                    errors.Add(name + ": порожній запис");
                    continue;
                }

                if (string.IsNullOrEmpty(type.Id))
                {
                    errors.Add(name + ".id: порожня назва");
                }
                else if (IndexOfType(type.Id) != i)
                {
                    errors.Add(name + ".id=" + type.Id + ": назва повторюється");
                }

                bool patienceOk = CheckRange(errors, name + ".patienceMs", type.PatienceMs, 1, MaxPatienceMs);
                if (patienceOk && ticksPerSecond > 0 && type.PatienceTicks(ticksPerSecond) < 1L)
                {
                    errors.Add(string.Format(
                        CultureInfo.InvariantCulture,
                        "{0}.patienceMs={1}: менше одного такту при ticksPerSecond={2}",
                        name,
                        type.PatienceMs,
                        ticksPerSecond));
                }

                CheckRange(errors, name + ".payPercent", type.PayPercent, 0, MaxPercent);
                if (type.Tip == null)
                {
                    errors.Add(name + ".tipRule: немає правила чайових");
                }
                else
                {
                    CheckRange(errors, name + ".tipRule.maxPercent", type.Tip.MaxPercent, 0, MaxPercent);
                    CheckRange(
                        errors,
                        name + ".tipRule.minPatiencePercent",
                        type.Tip.MinPatiencePercent,
                        0,
                        CoinMath.PercentBase);
                }

                for (int k = 0; k < type.AlwaysCount; k++)
                {
                    if (string.IsNullOrEmpty(type.GetAlways(k)))
                    {
                        errors.Add(name + ".always: порожня назва інгредієнта");
                    }
                }

                for (int k = 0; k < type.ExcludeCount; k++)
                {
                    if (string.IsNullOrEmpty(type.GetExclude(k)))
                    {
                        errors.Add(name + ".exclude: порожня назва інгредієнта");
                    }
                }
            }
        }

        private void CheckSchedule(List<string> errors, BoardConfig board)
        {
            if (Schedule == null)
            {
                errors.Add("waves: немає розкладу приходу");
                return;
            }

            if (Schedule.Count < 1 || Schedule.Count > ArrivalSchedule.MaxArrivals)
            {
                errors.Add(string.Format(
                    CultureInfo.InvariantCulture,
                    "waves.count={0}: дозволено 1..{1}",
                    Schedule.Count,
                    ArrivalSchedule.MaxArrivals));
            }

            int previousAtMs = 0;
            for (int i = 0; i < Schedule.Count; i++)
            {
                Arrival arrival = Schedule.Get(i);
                string name = Indexed("waves", i);
                if (arrival == null)
                {
                    errors.Add(name + ": порожній запис");
                    continue;
                }

                if (CheckRange(errors, name + ".atMs", arrival.AtMs, 0, ArrivalSchedule.MaxAtMs))
                {
                    if (arrival.AtMs < previousAtMs)
                    {
                        errors.Add(string.Format(
                            CultureInfo.InvariantCulture,
                            "{0}.atMs={1}: раніше за попередній прихід ({2})",
                            name,
                            arrival.AtMs,
                            previousAtMs));
                    }

                    previousAtMs = arrival.AtMs;
                }

                CheckRange(errors, name + ".jitterMs", arrival.JitterMs, 0, ArrivalSchedule.MaxJitterMs);
                CheckRange(errors, name + ".graceMs", arrival.GraceMs, 0, MaxPatienceMs);

                int typeIndex = IndexOfType(arrival.Customer);
                if (typeIndex < 0)
                {
                    errors.Add(name + ".customer=" + arrival.Customer + ": такого типу клієнта немає");
                }

                if (arrival.Order == null)
                {
                    errors.Add(name + ".order: немає замовлення");
                }
                else if (string.IsNullOrEmpty(arrival.Order.Recipe))
                {
                    errors.Add(name + ".order.recipe: порожня назва");
                }
                else if (typeIndex >= 0 && board != null)
                {
                    int recipeIndex;
                    int requestedMask;
                    int excludeMask;
                    TryResolveOrder(
                        board,
                        _types[typeIndex],
                        arrival.Order,
                        name,
                        errors,
                        out recipeIndex,
                        out requestedMask,
                        out excludeMask);
                }
            }
        }

        private static void Report(List<string> errors, string error)
        {
            if (errors != null)
            {
                errors.Add(error);
            }
        }

        private static string Indexed(string list, int index)
        {
            return string.Format(CultureInfo.InvariantCulture, "{0}[{1}]", list, index);
        }

        private static bool CheckRange(List<string> errors, string field, int value, int min, int max)
        {
            if (value >= min && value <= max)
            {
                return true;
            }

            errors.Add(string.Format(
                CultureInfo.InvariantCulture, "{0}={1}: дозволено {2}..{3}", field, value, min, max));
            return false;
        }
    }
}
