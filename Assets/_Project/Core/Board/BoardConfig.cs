using System;
using System.Collections.Generic;
using System.Globalization;

namespace KebabStreet.Core
{
    /// <summary>
    /// Параметри збирання страви в зміні: номер рівня, станції, інгредієнти, рецепти і штраф
    /// за очищення дошки (GAME_DESIGN.md, розд. 3 і 5). Незмінний об'єкт. Доступність станції
    /// чи рецепта — лише порівняння unlockLevel із номером рівня, без гілок коду під рівень.
    /// </summary>
    public sealed class BoardConfig
    {
        /// <summary>Найбільший номер рівня.</summary>
        public const int MaxLevel = 1000;

        /// <summary>Найбільша кількість інгредієнтів.</summary>
        public const int MaxIngredients = 32;

        /// <summary>Найбільша кількість рецептів.</summary>
        public const int MaxRecipes = 32;

        /// <summary>Найбільша кількість інгредієнтів в одному рецепті (кроки + необов'язкові).</summary>
        public const int MaxRecipeLength = 16;

        /// <summary>Найбільша ціна, надбавка чи штраф, монет.</summary>
        public const int MaxCoins = 1000000;

        /// <summary>Найбільша тривалість однієї дії гравця на станції, мс.</summary>
        public const int MaxActionMs = 60000;

        private readonly StationConfig[] _stations;
        private readonly Ingredient[] _ingredients;
        private readonly Recipe[] _recipes;

        /// <summary>Списки копіюються; null означає порожній список (це помилка конфіга).</summary>
        public BoardConfig(
            int level,
            int clearPenaltyCoins,
            IEnumerable<StationConfig> stations,
            IEnumerable<Ingredient> ingredients,
            IEnumerable<Recipe> recipes)
        {
            Level = level;
            ClearPenaltyCoins = clearPenaltyCoins;
            _stations = stations == null ? new StationConfig[0] : new List<StationConfig>(stations).ToArray();
            _ingredients = ingredients == null ? new Ingredient[0] : new List<Ingredient>(ingredients).ToArray();
            _recipes = recipes == null ? new Recipe[0] : new List<Recipe>(recipes).ToArray();
        }

        /// <summary>Номер рівня, для якого зібрано зміну: з ним порівнюються unlockLevel.</summary>
        public int Level { get; }

        /// <summary>Штраф за очищення дошки у смітник, монет (rules.boardClearPenaltyCoins); 0 — без штрафу.</summary>
        public int ClearPenaltyCoins { get; }

        /// <summary>Кількість станцій у конфігу.</summary>
        public int StationCount => _stations.Length;

        /// <summary>Кількість інгредієнтів; номер інгредієнта в командах — 0..IngredientCount-1.</summary>
        public int IngredientCount => _ingredients.Length;

        /// <summary>Кількість рецептів.</summary>
        public int RecipeCount => _recipes.Length;

        /// <summary>
        /// Місткість дошки: найбільша довжина рецепта (кроки + необов'язкові) серед усіх рецептів.
        /// </summary>
        public int Capacity
        {
            get
            {
                int capacity = 0;
                for (int i = 0; i < _recipes.Length; i++)
                {
                    if (_recipes[i] != null && _recipes[i].Length > capacity)
                    {
                        capacity = _recipes[i].Length;
                    }
                }

                return capacity;
            }
        }

        public StationConfig GetStation(int index)
        {
            return _stations[index];
        }

        public Ingredient GetIngredient(int index)
        {
            return _ingredients[index];
        }

        public Recipe GetRecipe(int index)
        {
            return _recipes[index];
        }

        /// <summary>Параметри станції або null, якщо її немає в конфігу.</summary>
        public StationConfig FindStation(StationKind kind)
        {
            for (int i = 0; i < _stations.Length; i++)
            {
                if (_stations[i] != null && _stations[i].Kind == kind)
                {
                    return _stations[i];
                }
            }

            return null;
        }

        /// <summary>Чи доступна станція на цьому рівні: вона є в конфігу і unlockLevel не більший за рівень.</summary>
        public bool IsStationAvailable(StationKind kind)
        {
            StationConfig station = FindStation(kind);
            return station != null && station.UnlockLevel <= Level;
        }

        /// <summary>Чи доступний рецепт на цьому рівні.</summary>
        public bool IsRecipeAvailable(int index)
        {
            return index >= 0
                && index < _recipes.Length
                && _recipes[index] != null
                && _recipes[index].UnlockLevel <= Level;
        }

        /// <summary>Номер інгредієнта за назвою з конфіга; -1 — такого немає.</summary>
        public int IndexOfIngredient(string id)
        {
            for (int i = 0; i < _ingredients.Length; i++)
            {
                if (_ingredients[i] != null && string.Equals(_ingredients[i].Id, id, StringComparison.Ordinal))
                {
                    return i;
                }
            }

            return -1;
        }

        /// <summary>Номер рецепта за назвою з конфіга; -1 — такого немає.</summary>
        public int IndexOfRecipe(string id)
        {
            for (int i = 0; i < _recipes.Length; i++)
            {
                if (_recipes[i] != null && string.Equals(_recipes[i].Id, id, StringComparison.Ordinal))
                {
                    return i;
                }
            }

            return -1;
        }

        /// <summary>
        /// Перевіряє конфіг. hasGrill — чи є в зміні гриль (джерело м'яса). Порожній список —
        /// конфіг придатний; інакше кожен рядок називає поле і причину.
        /// </summary>
        public IReadOnlyList<string> GetErrors(bool hasGrill)
        {
            var errors = new List<string>();
            CheckRange(errors, "board.level", Level, 1, MaxLevel);
            CheckRange(errors, "rules.boardClearPenaltyCoins", ClearPenaltyCoins, 0, MaxCoins);
            CheckStations(errors);
            CheckIngredients(errors, hasGrill);
            CheckRecipes(errors, hasGrill);
            return errors;
        }

        private void CheckStations(List<string> errors)
        {
            for (int i = 0; i < _stations.Length; i++)
            {
                StationConfig station = _stations[i];
                string name = Indexed("stations", i);
                if (station == null)
                {
                    errors.Add(name + ": порожній запис");
                    continue;
                }

                if (station.Kind != StationKind.Bread
                    && station.Kind != StationKind.Sauce
                    && station.Kind != StationKind.Salad
                    && station.Kind != StationKind.Board
                    && station.Kind != StationKind.Bin)
                {
                    errors.Add(name + ".id=" + station.Kind + ": дозволено Bread, Sauce, Salad, Board, Bin");
                }
                else if (FindStation(station.Kind) != station)
                {
                    errors.Add(name + ".id=" + station.Kind + ": станція повторюється");
                }

                CheckRange(errors, name + ".unlockLevel", station.UnlockLevel, 1, MaxLevel);
                CheckRange(errors, name + ".actionMs", station.ActionMs, 0, MaxActionMs);
            }

            StationConfig board = FindStation(StationKind.Board);
            if (board == null)
            {
                errors.Add("stations: немає станції Board");
            }
            else if (board.UnlockLevel > Level)
            {
                errors.Add(string.Format(
                    CultureInfo.InvariantCulture,
                    "stations.Board.unlockLevel={0}: дошка має бути доступна на рівні {1}",
                    board.UnlockLevel,
                    Level));
            }
        }

        private void CheckIngredients(List<string> errors, bool hasGrill)
        {
            if (_ingredients.Length < 1 || _ingredients.Length > MaxIngredients)
            {
                errors.Add(string.Format(
                    CultureInfo.InvariantCulture,
                    "ingredients.count={0}: дозволено 1..{1}",
                    _ingredients.Length,
                    MaxIngredients));
            }

            int grillIngredients = 0;
            for (int i = 0; i < _ingredients.Length; i++)
            {
                Ingredient ingredient = _ingredients[i];
                string name = Indexed("ingredients", i);
                if (ingredient == null)
                {
                    errors.Add(name + ": порожній запис");
                    continue;
                }

                if (string.IsNullOrEmpty(ingredient.Id))
                {
                    errors.Add(name + ".id: порожня назва");
                }
                else if (IndexOfIngredient(ingredient.Id) != i)
                {
                    errors.Add(name + ".id=" + ingredient.Id + ": назва повторюється");
                }

                if (ingredient.Station == StationKind.Grill)
                {
                    grillIngredients++;
                }
                else if (ingredient.Station != StationKind.Bread
                    && ingredient.Station != StationKind.Sauce
                    && ingredient.Station != StationKind.Salad)
                {
                    errors.Add(name + ".station=" + ingredient.Station + ": дозволено Grill, Bread, Sauce, Salad");
                }
                else if (FindStation(ingredient.Station) == null)
                {
                    errors.Add(name + ".station=" + ingredient.Station + ": такої станції немає в stations");
                }

                CheckRange(errors, name + ".extraPriceCoins", ingredient.ExtraPriceCoins, 0, MaxCoins);
            }

            if (hasGrill && grillIngredients != 1)
            {
                errors.Add(string.Format(
                    CultureInfo.InvariantCulture,
                    "ingredients: із грилем потрібен рівно один інгредієнт станції Grill, знайдено {0}",
                    grillIngredients));
            }
            else if (!hasGrill && grillIngredients > 0)
            {
                errors.Add("ingredients: інгредієнт станції Grill, але гриля в зміні немає");
            }
        }

        private void CheckRecipes(List<string> errors, bool hasGrill)
        {
            if (_recipes.Length < 1 || _recipes.Length > MaxRecipes)
            {
                errors.Add(string.Format(
                    CultureInfo.InvariantCulture, "recipes.count={0}: дозволено 1..{1}", _recipes.Length, MaxRecipes));
            }

            bool anyAvailable = false;
            for (int i = 0; i < _recipes.Length; i++)
            {
                Recipe recipe = _recipes[i];
                string name = Indexed("recipes", i);
                if (recipe == null)
                {
                    errors.Add(name + ": порожній запис");
                    continue;
                }

                if (string.IsNullOrEmpty(recipe.Id))
                {
                    errors.Add(name + ".id: порожня назва");
                }
                else if (IndexOfRecipe(recipe.Id) != i)
                {
                    errors.Add(name + ".id=" + recipe.Id + ": назва повторюється");
                }

                if (recipe.StepCount < 1)
                {
                    errors.Add(name + ".steps: потрібен хоча б один крок");
                }

                if (recipe.Length > MaxRecipeLength)
                {
                    errors.Add(string.Format(
                        CultureInfo.InvariantCulture,
                        "{0}.length={1}: кроків і необов'язкових разом не більше {2}",
                        name,
                        recipe.Length,
                        MaxRecipeLength));
                }

                CheckRange(errors, name + ".price", recipe.PriceCoins, 0, MaxCoins);
                CheckRange(errors, name + ".unlockLevel", recipe.UnlockLevel, 1, MaxLevel);

                bool available = recipe.UnlockLevel <= Level;
                anyAvailable |= available;
                for (int k = 0; k < recipe.Length; k++)
                {
                    string id = recipe.GetItem(k);
                    int index = IndexOfIngredient(id);
                    if (index < 0)
                    {
                        errors.Add(name + ": невідомий інгредієнт «" + id + "»");
                        continue;
                    }

                    if (IsRepeatedInRecipe(recipe, k))
                    {
                        errors.Add(name + ": інгредієнт «" + id + "» повторюється");
                    }

                    if (available && k < recipe.StepCount && !IsIngredientAvailable(index, hasGrill))
                    {
                        errors.Add(string.Format(
                            CultureInfo.InvariantCulture,
                            "{0}.steps: інгредієнт «{1}» недоступний на рівні {2}",
                            name,
                            id,
                            Level));
                    }
                }
            }

            if (_recipes.Length > 0 && !anyAvailable)
            {
                errors.Add(string.Format(
                    CultureInfo.InvariantCulture, "recipes: жоден рецепт не доступний на рівні {0}", Level));
            }
        }

        private bool IsIngredientAvailable(int index, bool hasGrill)
        {
            StationKind station = _ingredients[index].Station;
            return station == StationKind.Grill ? hasGrill : IsStationAvailable(station);
        }

        private static bool IsRepeatedInRecipe(Recipe recipe, int position)
        {
            string id = recipe.GetItem(position);
            for (int i = 0; i < position; i++)
            {
                if (string.Equals(recipe.GetItem(i), id, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static string Indexed(string list, int index)
        {
            return string.Format(CultureInfo.InvariantCulture, "{0}[{1}]", list, index);
        }

        private static void CheckRange(List<string> errors, string field, int value, int min, int max)
        {
            if (value < min || value > max)
            {
                errors.Add(string.Format(
                    CultureInfo.InvariantCulture, "{0}={1}: дозволено {2}..{3}", field, value, min, max));
            }
        }
    }
}
