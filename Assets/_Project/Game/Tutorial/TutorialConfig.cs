using System;
using System.Collections.Generic;

namespace KebabStreet.Game
{
    /// <summary>Умова з tutorial.json (showWhen / completeWhen): подія й уточнення. Лише дані й порівняння.</summary>
    public sealed class TutorialTrigger
    {
        public TutorialTrigger(
            TutorialEventKind kind,
            string ingredient,
            string orderOptional,
            string customer,
            string afterStep,
            bool firstTimeInProfile)
        {
            Kind = kind;
            Ingredient = ingredient;
            OrderOptional = orderOptional;
            Customer = customer;
            AfterStep = afterStep;
            FirstTimeInProfile = firstTimeInProfile;
        }

        public TutorialEventKind Kind { get; }

        /// <summary>Потрібний інгредієнт події AddIngredient; null — будь-який.</summary>
        public string Ingredient { get; }

        /// <summary>Потрібний необов'язковий інгредієнт замовлення для OrderShown; null — будь-який.</summary>
        public string OrderOptional { get; }

        /// <summary>Потрібний тип клієнта; null — будь-який.</summary>
        public string Customer { get; }

        /// <summary>Крок, який має бути завершений (виконаний або пропущений) раніше; null — без умови. Лише для showWhen.</summary>
        public string AfterStep { get; }

        /// <summary>Показувати лише якщо крок ще ні разу не завершувався в профілі (навіть коли oncePerProfile вимкнено).</summary>
        public bool FirstTimeInProfile { get; }

        public bool Matches(TutorialEvent e)
        {
            if (e.Kind != Kind)
            {
                return false;
            }

            if (Ingredient != null && !string.Equals(Ingredient, e.Ingredient, StringComparison.Ordinal))
            {
                return false;
            }

            if (OrderOptional != null && !string.Equals(OrderOptional, e.Ingredient, StringComparison.Ordinal))
            {
                return false;
            }

            return Customer == null || string.Equals(Customer, e.Customer, StringComparison.Ordinal);
        }
    }

    /// <summary>Крок навчання з tutorial.json.</summary>
    public sealed class TutorialStep
    {
        private readonly string[] _alsoOnLevels;

        public TutorialStep(
            string id,
            string textKey,
            string anchor,
            TutorialTrigger showWhen,
            TutorialTrigger completeWhen,
            int autoHideMs,
            bool skippable,
            bool penaltyFree,
            IEnumerable<string> alsoOnLevels)
        {
            Id = id;
            TextKey = textKey;
            Anchor = anchor;
            ShowWhen = showWhen;
            CompleteWhen = completeWhen;
            AutoHideMs = autoHideMs;
            Skippable = skippable;
            PenaltyFree = penaltyFree;
            _alsoOnLevels = alsoOnLevels == null ? new string[0] : new List<string>(alsoOnLevels).ToArray();
        }

        public string Id { get; }

        public string TextKey { get; }

        /// <summary>Місце на екрані, на яке вказує підказка (див. <see cref="TutorialAnchors"/>).</summary>
        public string Anchor { get; }

        public TutorialTrigger ShowWhen { get; }

        /// <summary>Подія, що завершує крок; null — крок лише зникає за часом або після пропуску.</summary>
        public TutorialTrigger CompleteWhen { get; }

        /// <summary>Через скільки мілісекунд крок зникає сам; 0 — не зникає.</summary>
        public int AutoHideMs { get; }

        public bool Skippable { get; }

        /// <summary>Помилка гравця на цьому кроці без штрафу (довідково, для дизайну).</summary>
        public bool PenaltyFree { get; }

        public int AlsoOnLevelCount => _alsoOnLevels.Length;

        public string GetAlsoOnLevel(int index)
        {
            return _alsoOnLevels[index];
        }

        public bool IsAlsoOn(string levelId)
        {
            for (int i = 0; i < _alsoOnLevels.Length; i++)
            {
                if (string.Equals(_alsoOnLevels[i], levelId, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }
    }

    /// <summary>Кроки одного рівня з tutorial.json.</summary>
    public sealed class TutorialLevel
    {
        private readonly TutorialStep[] _steps;

        public TutorialLevel(string levelId, IEnumerable<TutorialStep> steps)
        {
            LevelId = levelId;
            _steps = new List<TutorialStep>(steps).ToArray();
        }

        public string LevelId { get; }

        public int StepCount => _steps.Length;

        public TutorialStep GetStep(int index)
        {
            return _steps[index];
        }
    }

    /// <summary>Усі кроки навчання, правила пропуску й показу (tutorial.json).</summary>
    public sealed class TutorialConfig
    {
        private readonly TutorialLevel[] _levels;

        public TutorialConfig(
            string speaker,
            bool allowSkipStep,
            bool allowSkipAll,
            string skipStepButtonKey,
            string skipAllButtonKey,
            bool rememberSkipAll,
            int minGapMs,
            bool oncePerProfile,
            IEnumerable<TutorialLevel> levels)
        {
            Speaker = speaker;
            AllowSkipStep = allowSkipStep;
            AllowSkipAll = allowSkipAll;
            SkipStepButtonKey = skipStepButtonKey;
            SkipAllButtonKey = skipAllButtonKey;
            RememberSkipAll = rememberSkipAll;
            MinGapMs = minGapMs;
            OncePerProfile = oncePerProfile;
            _levels = new List<TutorialLevel>(levels).ToArray();
        }

        public string Speaker { get; }

        public bool AllowSkipStep { get; }

        public bool AllowSkipAll { get; }

        public string SkipStepButtonKey { get; }

        public string SkipAllButtonKey { get; }

        /// <summary>«Пропустити все» запам'ятовується в профілі.</summary>
        public bool RememberSkipAll { get; }

        /// <summary>Мінімальна пауза між кроками, мс часу зміни.</summary>
        public int MinGapMs { get; }

        /// <summary>Крок, що вже завершувався, у цьому профілі більше не показується.</summary>
        public bool OncePerProfile { get; }

        public int LevelCount => _levels.Length;

        public TutorialLevel GetLevel(int index)
        {
            return _levels[index];
        }

        /// <summary>
        /// Кроки, що діють на рівні: власні кроки рівня й кроки інших рівнів із <c>alsoOnLevels</c>, що містить цей рівень.
        /// Порядок: власні, далі додаткові.
        /// </summary>
        public List<TutorialStep> StepsFor(string levelId)
        {
            var result = new List<TutorialStep>();
            for (int i = 0; i < _levels.Length; i++)
            {
                if (string.Equals(_levels[i].LevelId, levelId, StringComparison.Ordinal))
                {
                    for (int s = 0; s < _levels[i].StepCount; s++)
                    {
                        result.Add(_levels[i].GetStep(s));
                    }
                }
            }

            for (int i = 0; i < _levels.Length; i++)
            {
                if (string.Equals(_levels[i].LevelId, levelId, StringComparison.Ordinal))
                {
                    continue;
                }

                for (int s = 0; s < _levels[i].StepCount; s++)
                {
                    if (_levels[i].GetStep(s).IsAlsoOn(levelId))
                    {
                        result.Add(_levels[i].GetStep(s));
                    }
                }
            }

            return result;
        }

        /// <summary>Ключі підказок, що діють на рівні (для звірки з <c>tutorialKeys</c> у конфігу рівня), у порядку <see cref="StepsFor"/>.</summary>
        public List<string> TextKeysFor(string levelId)
        {
            var keys = new List<string>();
            foreach (TutorialStep step in StepsFor(levelId))
            {
                keys.Add(step.TextKey);
            }

            return keys;
        }

        public TutorialStep FindStep(string id)
        {
            for (int i = 0; i < _levels.Length; i++)
            {
                for (int s = 0; s < _levels[i].StepCount; s++)
                {
                    if (string.Equals(_levels[i].GetStep(s).Id, id, StringComparison.Ordinal))
                    {
                        return _levels[i].GetStep(s);
                    }
                }
            }

            return null;
        }
    }

    /// <summary>Куди на екрані вказує підказка. Підсвічується зона, у якій лежить місце-якір.</summary>
    public enum TutorialZone
    {
        None = 0,
        Grill = 1,
        Assembly = 2,
        Counter = 3,
    }

    /// <summary>Допустимі якорі кроків (tutorial.json: anchor) і зона екрана кожного з них. Без UnityEngine.</summary>
    public static class TutorialAnchors
    {
        public static TutorialZone ZoneOf(string anchor)
        {
            switch (anchor)
            {
                case "grill.slot1":
                case "grill.burntSlot":
                    return TutorialZone.Grill;
                case "board.pita":
                case "sauce.garlic":
                case "saladbar":
                    return TutorialZone.Assembly;
                case "counter.customer":
                case "customer.patience":
                case "customer.request":
                    return TutorialZone.Counter;
                default:
                    return TutorialZone.None;
            }
        }
    }

    /// <summary>Результат розбору tutorial.json: конфіг або список зрозумілих помилок із шляхом до ключа.</summary>
    public sealed class TutorialLoadResult
    {
        public TutorialLoadResult(TutorialConfig config, IReadOnlyList<string> errors)
        {
            Errors = errors ?? new string[0];
            Config = Errors.Count == 0 ? config : null;
        }

        public TutorialConfig Config { get; }

        public IReadOnlyList<string> Errors { get; }

        public bool IsValid => Config != null && Errors.Count == 0;

        public TutorialConfig ValueOrThrow()
        {
            if (!IsValid)
            {
                throw new InvalidOperationException("tutorial.json хибний: " + string.Join("; ", new List<string>(Errors)));
            }

            return Config;
        }
    }
}
