using System;
using System.Collections.Generic;

namespace KebabStreet.Core
{
    /// <summary>Тип цілі рівня (levels/Lxx.json → goal.type).</summary>
    public enum GoalType
    {
        /// <summary>Обслужити N клієнтів (goal.count).</summary>
        Serve = 1,

        /// <summary>Заробити N монет (goal.coins).</summary>
        Coins = 2,
    }

    /// <summary>Мета рівня як дані. Підрахунок виконання — окреме завдання (S1-06), тут лише зберігається.</summary>
    public sealed class LevelGoal
    {
        public LevelGoal(GoalType type, int target)
        {
            Type = type;
            Target = target;
        }

        public GoalType Type { get; }

        /// <summary>Скільки клієнтів обслужити (Serve) або монет заробити (Coins).</summary>
        public int Target { get; }
    }

    /// <summary>Додаткова умова третьої зірки (levels/Lxx.json → stars.three.condition).</summary>
    public enum StarCondition
    {
        None = 0,

        /// <summary>Жодного втраченого клієнта.</summary>
        NoLostCustomers = 1,

        /// <summary>Жодного пригорілого шматка.</summary>
        NoBurnt = 2,

        /// <summary>Жодної хибної страви.</summary>
        NoWrongDishes = 3,

        /// <summary>Комбо не нижче stars.three.value.</summary>
        Combo = 4,
    }

    /// <summary>Пороги зірок як дані (S1-06 рахує; тут лише зчитано).</summary>
    public sealed class LevelStars
    {
        public LevelStars(int twoCoins, StarCondition threeCondition, int threeComboValue)
        {
            TwoCoins = twoCoins;
            ThreeCondition = threeCondition;
            ThreeComboValue = threeComboValue;
        }

        /// <summary>Скільки монет за зміну дає другу зірку (stars.two.value).</summary>
        public int TwoCoins { get; }

        /// <summary>Умова третьої зірки (stars.three.condition).</summary>
        public StarCondition ThreeCondition { get; }

        /// <summary>Поріг комбо для третьої зірки; 0, якщо умова не Combo (stars.three.value).</summary>
        public int ThreeComboValue { get; }
    }

    /// <summary>Умови поразки рівня як дані (levels/Lxx.json → lose).</summary>
    public sealed class LevelLose
    {
        public LevelLose(int? maxLostCustomers)
        {
            MaxLostCustomers = maxLostCustomers;
        }

        /// <summary>Скільки клієнтів можна втратити; null — втрата клієнтів не веде до поразки (рівні 1–2, ADR-020).</summary>
        public int? MaxLostCustomers { get; }
    }

    /// <summary>
    /// Один рівень, зібраний із JSON: параметри зміни для симуляції (<see cref="Shift"/>) і дані для
    /// екранів та підрахунку (ціль, зірки, поразка, ключі підказок і сюжету). Незмінний об'єкт.
    /// </summary>
    public sealed class LevelDefinition
    {
        private readonly string[] _tutorialKeys;

        public LevelDefinition(
            string id,
            int number,
            int location,
            string nameKey,
            bool draft,
            ShiftConfig shift,
            LevelGoal goal,
            LevelStars stars,
            LevelLose lose,
            IEnumerable<string> tutorialKeys,
            string storyIntroKey,
            string storyOutroKey)
        {
            Id = id;
            Number = number;
            Location = location;
            NameKey = nameKey;
            Draft = draft;
            Shift = shift;
            Goal = goal;
            Stars = stars;
            Lose = lose;
            _tutorialKeys = tutorialKeys == null ? new string[0] : new List<string>(tutorialKeys).ToArray();
            StoryIntroKey = storyIntroKey;
            StoryOutroKey = storyOutroKey;
        }

        /// <summary>Назва рівня в конфігах («L01»).</summary>
        public string Id { get; }

        /// <summary>Номер рівня (1 для «L01»); з ним порівнюються unlockLevel.</summary>
        public int Number { get; }

        public int Location { get; }

        /// <summary>Ключ локалізації назви рівня.</summary>
        public string NameKey { get; }

        /// <summary>true — числа чернеткові, їх уточнює бот-тест S1-05.</summary>
        public bool Draft { get; }

        /// <summary>Параметри симуляції зміни (гриль, дошка, клієнти, розклад).</summary>
        public ShiftConfig Shift { get; }

        public LevelGoal Goal { get; }

        public LevelStars Stars { get; }

        public LevelLose Lose { get; }

        /// <summary>Ключі підказок навчання в порядку показу.</summary>
        public int TutorialKeyCount => _tutorialKeys.Length;

        public string StoryIntroKey { get; }

        public string StoryOutroKey { get; }

        public string GetTutorialKey(int index)
        {
            if (index < 0 || index >= _tutorialKeys.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            return _tutorialKeys[index];
        }
    }

    /// <summary>Усі рівні, завантажені з конфігів, за зростанням номера.</summary>
    public sealed class LevelCatalog
    {
        private readonly LevelDefinition[] _levels;

        public LevelCatalog(IEnumerable<LevelDefinition> levels)
        {
            _levels = levels == null ? new LevelDefinition[0] : new List<LevelDefinition>(levels).ToArray();
        }

        public int Count => _levels.Length;

        public LevelDefinition Get(int index)
        {
            if (index < 0 || index >= _levels.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            return _levels[index];
        }

        /// <summary>Рівень за номером або null.</summary>
        public LevelDefinition FindByNumber(int number)
        {
            for (int i = 0; i < _levels.Length; i++)
            {
                if (_levels[i].Number == number)
                {
                    return _levels[i];
                }
            }

            return null;
        }
    }

    /// <summary>Результат завантаження: значення або список помилок із шляхами до ключів.</summary>
    public sealed class ConfigResult<T> where T : class
    {
        public ConfigResult(T value, IReadOnlyList<string> errors)
        {
            Errors = errors ?? new string[0];
            Value = Errors.Count == 0 ? value : null;
        }

        /// <summary>Зібраний об'єкт; null, якщо є хоч одна помилка.</summary>
        public T Value { get; }

        public IReadOnlyList<string> Errors { get; }

        public bool IsValid => Value != null && Errors.Count == 0;

        /// <summary>Значення або виняток зі списком помилок (для завантаження в грі, де конфіг уже перевірено тестами).</summary>
        public T ValueOrThrow()
        {
            if (!IsValid)
            {
                throw new InvalidOperationException("Помилки конфігів: " + string.Join("; ", new List<string>(Errors)));
            }

            return Value;
        }
    }

    /// <summary>Тексти файлів конфігів (читає їх обгортка поза Core). null — файл не знайдено.</summary>
    public sealed class ConfigSources
    {
        private readonly KeyValuePair<string, string>[] _levels;

        /// <param name="levels">Пари «назва файла без розширення (L01)» → текст JSON.</param>
        public ConfigSources(
            string stations,
            string recipes,
            string customers,
            string rules,
            IEnumerable<KeyValuePair<string, string>> levels)
        {
            Stations = stations;
            Recipes = recipes;
            Customers = customers;
            Rules = rules;
            _levels = levels == null
                ? new KeyValuePair<string, string>[0]
                : new List<KeyValuePair<string, string>>(levels).ToArray();
        }

        public string Stations { get; }

        public string Recipes { get; }

        public string Customers { get; }

        public string Rules { get; }

        public int LevelCount => _levels.Length;

        public string GetLevelName(int index)
        {
            return _levels[index].Key;
        }

        public string GetLevelText(int index)
        {
            return _levels[index].Value;
        }

        /// <summary>Текст рівня за назвою; null — такого файла немає.</summary>
        public string FindLevelText(string name)
        {
            for (int i = 0; i < _levels.Length; i++)
            {
                if (string.Equals(_levels[i].Key, name, StringComparison.Ordinal))
                {
                    return _levels[i].Value;
                }
            }

            return null;
        }
    }
}
