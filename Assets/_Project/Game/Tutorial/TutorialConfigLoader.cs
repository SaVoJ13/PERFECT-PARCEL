using System;
using System.Collections.Generic;
using System.Globalization;
using KebabStreet.Core;

namespace KebabStreet.Game
{
    /// <summary>
    /// Завантажувач tutorial.json (S1-11): розбирає JSON, суворо перевіряє схему (невідомі ключі, типи,
    /// події, якорі, посилання afterStep / alsoOnLevels, унікальність id) і збирає <see cref="TutorialConfig"/>.
    /// Помилки не кидаються, а збираються у список із шляхом до ключа: «tutorial.json: levels[0].steps[2].anchor=…».
    /// Без UnityEngine і без читання файлів: текст приходить ззовні.
    /// </summary>
    public static class TutorialConfigLoader
    {
        public const string Doc = "tutorial.json";

        private const int MaxLevels = 64;
        private const int MaxSteps = 32;
        private const double MaxSeconds = 600d;
        private const int MillisPerSecond = 1000;

        public static TutorialLoadResult Parse(string text)
        {
            var errors = new List<string>();
            if (text == null)
            {
                errors.Add(Doc + ": файл не знайдено");
                return new TutorialLoadResult(null, errors);
            }

            object parsed;
            try
            {
                parsed = SaveJson.Parse(text.TrimStart('﻿'));
            }
            catch (SaveFormatException e)
            {
                errors.Add(Doc + ": некоректний JSON: " + e.Message);
                return new TutorialLoadResult(null, errors);
            }

            Node root = Node.Open(parsed, string.Empty, errors);
            root.Number("version", 1, 1000);
            root.OptBool("draft", false);
            string speaker = root.Ident("speaker");

            Node skip = root.Obj("skip");
            bool allowSkipStep = skip.Bool("allowSkipStep");
            bool allowSkipAll = skip.Bool("allowSkipAll");
            string skipStepKey = skip.Key("skipStepButtonKey");
            string skipAllKey = skip.Key("skipAllButtonKey");
            bool rememberSkipAll = skip.Bool("rememberSkipAll");
            skip.Done();

            Node display = root.Obj("display");
            double maxConcurrent = display.Number("maxConcurrent", 1, 1000);
            if (maxConcurrent != 1d)
            {
                display.Report("maxConcurrent", "=" + maxConcurrent.ToString(CultureInfo.InvariantCulture)
                    + ": підтримується лише 1 крок одночасно");
            }

            int minGapMs = ToMillis(display, "minGapSec", display.Number("minGapSec", 0d, MaxSeconds));
            bool pause = display.Bool("pauseSimulation");
            if (pause)
            {
                display.Report("pauseSimulation", "=true: навчання не може зупиняти симуляцію (потрібно false)");
            }

            bool oncePerProfile = display.Bool("oncePerProfile");
            display.Done();

            var levels = new List<TutorialLevel>();
            var levelIds = new HashSet<string>(StringComparer.Ordinal);
            var stepIds = new HashSet<string>(StringComparer.Ordinal);
            var pendingAlso = new List<KeyValuePair<string, string>>();
            List<Node> levelNodes = root.ObjList("levels", 1, MaxLevels);
            for (int i = 0; i < levelNodes.Count; i++)
            {
                levels.Add(ParseLevel(levelNodes[i], levelIds, stepIds, pendingAlso));
            }

            foreach (KeyValuePair<string, string> pair in pendingAlso)
            {
                if (!levelIds.Contains(pair.Value))
                {
                    errors.Add(Doc + ": " + pair.Key + "=" + pair.Value + ": такого рівня немає в tutorial.json");
                }
            }

            root.Done();
            var config = new TutorialConfig(
                speaker, allowSkipStep, allowSkipAll, skipStepKey, skipAllKey, rememberSkipAll, minGapMs, oncePerProfile, levels);
            return new TutorialLoadResult(config, errors);
        }

        private static TutorialLevel ParseLevel(
            Node node, HashSet<string> levelIds, HashSet<string> stepIds, List<KeyValuePair<string, string>> pendingAlso)
        {
            string levelId = node.Str("level");
            if (levelId.Length > 0)
            {
                if (!IsLevelId(levelId))
                {
                    node.Report("level", "=" + levelId + ": очікувався ідентифікатор рівня у форматі L01");
                }
                else if (!levelIds.Add(levelId))
                {
                    node.Report("level", "=" + levelId + ": рівень повторюється");
                }
            }

            var steps = new List<TutorialStep>();
            var earlierInLevel = new HashSet<string>(StringComparer.Ordinal);
            List<Node> stepNodes = node.ObjList("steps", 0, MaxSteps);
            for (int i = 0; i < stepNodes.Count; i++)
            {
                TutorialStep step = ParseStep(stepNodes[i], levelId, stepIds, earlierInLevel, pendingAlso);
                steps.Add(step);
                earlierInLevel.Add(step.Id);
            }

            node.Done();
            return new TutorialLevel(levelId, steps);
        }

        private static TutorialStep ParseStep(
            Node node,
            string levelId,
            HashSet<string> stepIds,
            HashSet<string> earlierInLevel,
            List<KeyValuePair<string, string>> pendingAlso)
        {
            string id = node.Ident("id");
            if (id.Length > 0 && !stepIds.Add(id))
            {
                node.Report("id", "=" + id + ": крок з таким id уже є");
            }

            string textKey = node.Key("textKey");
            string anchor = node.Str("anchor");
            if (anchor.Length > 0 && TutorialAnchors.ZoneOf(anchor) == TutorialZone.None)
            {
                node.Report("anchor", "=" + anchor + ": невідомий якір (допустимі: grill.slot1, grill.burntSlot, board.pita, "
                    + "sauce.garlic, saladbar, counter.customer, customer.patience, customer.request)");
            }

            TutorialTrigger showWhen = ParseTrigger(node.Obj("showWhen"), true, earlierInLevel);

            TutorialTrigger completeWhen = null;
            if (node.IsNull("completeWhen"))
            {
                node.MarkUsed("completeWhen");
            }
            else
            {
                completeWhen = ParseTrigger(node.Obj("completeWhen"), false, earlierInLevel);
            }

            int autoHideMs = 0;
            if (node.IsNull("autoHideSec"))
            {
                node.MarkUsed("autoHideSec");
            }
            else
            {
                autoHideMs = ToMillis(node, "autoHideSec", node.Number("autoHideSec", 0.001d, MaxSeconds));
            }

            bool skippable = node.Bool("skippable");
            bool penaltyFree = node.OptBool("penaltyFree", false);
            List<string> also = node.StrList("alsoOnLevels");
            for (int i = 0; i < also.Count; i++)
            {
                if (!IsLevelId(also[i]))
                {
                    node.Report("alsoOnLevels[" + i.ToString(CultureInfo.InvariantCulture) + "]",
                        "=" + also[i] + ": очікувався ідентифікатор рівня у форматі L01");
                }
                else if (string.Equals(also[i], levelId, StringComparison.Ordinal))
                {
                    node.Report("alsoOnLevels[" + i.ToString(CultureInfo.InvariantCulture) + "]",
                        "=" + also[i] + ": власний рівень кроку вказувати не треба");
                }
                else
                {
                    pendingAlso.Add(new KeyValuePair<string, string>(node.PathOf("alsoOnLevels[" + i.ToString(CultureInfo.InvariantCulture) + "]"), also[i]));
                }
            }

            if (completeWhen == null && autoHideMs == 0)
            {
                node.Report("completeWhen", ": крок без completeWhen має мати autoHideSec, інакше він ніколи не зникне");
            }

            node.Done();
            return new TutorialStep(id, textKey, anchor, showWhen, completeWhen, autoHideMs, skippable, penaltyFree, also);
        }

        private static TutorialTrigger ParseTrigger(Node node, bool isShow, HashSet<string> earlierInLevel)
        {
            TutorialEventKind kind = ParseKind(node, "event");
            string ingredient = node.OptIdent("ingredient");
            string customer = node.OptIdent("customer");
            string orderOptional = null;
            string afterStep = null;
            bool firstTime = false;
            if (isShow)
            {
                orderOptional = node.OptIdent("orderOptional");
                afterStep = node.OptIdent("afterStep");
                firstTime = node.OptBool("firstTimeInProfile", false);
            }

            if (ingredient != null && kind != TutorialEventKind.AddIngredient)
            {
                node.Report("ingredient", "=" + ingredient + ": уточнення ingredient є лише для події AddIngredient");
            }

            if (orderOptional != null && kind != TutorialEventKind.OrderShown)
            {
                node.Report("orderOptional", "=" + orderOptional + ": уточнення orderOptional є лише для події OrderShown");
            }

            if (customer != null && kind != TutorialEventKind.CustomerArrived
                && kind != TutorialEventKind.ServeDish && kind != TutorialEventKind.OrderShown)
            {
                node.Report("customer", "=" + customer + ": уточнення customer є лише для CustomerArrived, OrderShown, ServeDish");
            }

            if (afterStep != null && !earlierInLevel.Contains(afterStep))
            {
                node.Report("afterStep", "=" + afterStep + ": має бути id попереднього кроку цього ж рівня");
            }

            node.Done();
            return new TutorialTrigger(kind, ingredient, orderOptional, customer, afterStep, firstTime);
        }

        private static TutorialEventKind ParseKind(Node node, string key)
        {
            string name = node.Str(key);
            if (name.Length == 0)
            {
                return TutorialEventKind.None;
            }

            foreach (TutorialEventKind kind in Enum.GetValues(typeof(TutorialEventKind)))
            {
                if (kind != TutorialEventKind.None && string.Equals(kind.ToString(), name, StringComparison.Ordinal))
                {
                    return kind;
                }
            }

            node.Report(key, "=" + name + ": невідома подія (допустимі: ShiftStarted, PlaceMeat, MeatDone, TakeMeat, "
                + "AddIngredient, ServeDish, MeatBurnt, DiscardBurntMeat, CustomerArrived, OrderShown)");
            return TutorialEventKind.None;
        }

        private static bool IsLevelId(string text)
        {
            if (text == null || text.Length < 2 || text[0] != 'L')
            {
                return false;
            }

            for (int i = 1; i < text.Length; i++)
            {
                if (text[i] < '0' || text[i] > '9')
                {
                    return false;
                }
            }

            return true;
        }

        private static int ToMillis(Node node, string key, double seconds)
        {
            double ms = Math.Round(seconds * MillisPerSecond, MidpointRounding.AwayFromZero);
            if (Math.Abs(seconds * MillisPerSecond - ms) > 1e-6)
            {
                node.Report(key, "=" + seconds.ToString(CultureInfo.InvariantCulture) + ": занадто точне значення (потрібно до 1 мс)");
            }

            return (int)ms;
        }

        /// <summary>Обгортка над розібраним JSON-об'єктом: читання з перевірками, помилки — у спільний список.</summary>
        private sealed class Node
        {
            private readonly Dictionary<string, object> _map;
            private readonly string _path;
            private readonly List<string> _errors;
            private readonly bool _broken;
            private readonly HashSet<string> _used = new HashSet<string>(StringComparer.Ordinal);

            private Node(Dictionary<string, object> map, string path, List<string> errors, bool broken)
            {
                _map = map;
                _path = path;
                _errors = errors;
                _broken = broken;
            }

            public static Node Open(object value, string path, List<string> errors)
            {
                var map = value as Dictionary<string, object>;
                if (map == null)
                {
                    errors.Add(Doc + ": " + (path.Length == 0 ? string.Empty : path + ": ") + "очікувався об'єкт");
                    return new Node(new Dictionary<string, object>(StringComparer.Ordinal), path, errors, true);
                }

                return new Node(map, path, errors, false);
            }

            public string PathOf(string key)
            {
                return _path.Length == 0 ? key : _path + "." + key;
            }

            public void Report(string key, string detail)
            {
                _errors.Add(Doc + ": " + PathOf(key) + detail);
            }

            public bool IsNull(string key)
            {
                object raw;
                return _map.TryGetValue(key, out raw) && raw == null;
            }

            public void MarkUsed(string key)
            {
                _used.Add(key);
            }

            public string Str(string key)
            {
                object raw;
                if (!_map.TryGetValue(key, out raw))
                {
                    Missing(key);
                    return string.Empty;
                }

                _used.Add(key);
                string text = raw as string;
                if (string.IsNullOrEmpty(text))
                {
                    Report(key, ": очікувався непорожній рядок");
                    return string.Empty;
                }

                return text;
            }

            /// <summary>Ідентифікатор (латиниця, цифри, «_», «.»), не видимий текст.</summary>
            public string Ident(string key)
            {
                string text = Str(key);
                if (text.Length > 0 && !ConfigText.IsIdentifierOrKey(text))
                {
                    Report(key, "=" + text + ": очікувався ідентифікатор (латиниця, цифри, «_», «.»)");
                }

                return text;
            }

            public string OptIdent(string key)
            {
                if (!_map.ContainsKey(key))
                {
                    return null;
                }

                return Ident(key);
            }

            public string Key(string key)
            {
                string text = Str(key);
                if (text.Length > 0 && !ConfigText.IsLocalizationKey(text))
                {
                    Report(key, "=" + text + ": очікувався ключ локалізації, а не текст");
                }

                return text;
            }

            public bool Bool(string key)
            {
                if (!_map.ContainsKey(key))
                {
                    Missing(key);
                    return false;
                }

                return OptBool(key, false);
            }

            public bool OptBool(string key, bool fallback)
            {
                object raw;
                if (!_map.TryGetValue(key, out raw))
                {
                    return fallback;
                }

                _used.Add(key);
                if (!(raw is bool))
                {
                    Report(key, ": очікувалось true або false");
                    return fallback;
                }

                return (bool)raw;
            }

            public double Number(string key, double min, double max)
            {
                object raw;
                if (!_map.TryGetValue(key, out raw))
                {
                    Missing(key);
                    return 0d;
                }

                _used.Add(key);
                double value;
                if (raw is long)
                {
                    value = (long)raw;
                }
                else if (raw is double)
                {
                    value = (double)raw;
                }
                else
                {
                    Report(key, ": очікувалось число");
                    return 0d;
                }

                if (double.IsNaN(value) || value < min || value > max)
                {
                    Report(key, "=" + value.ToString(CultureInfo.InvariantCulture) + ": дозволено "
                        + min.ToString(CultureInfo.InvariantCulture) + ".." + max.ToString(CultureInfo.InvariantCulture));
                    return 0d;
                }

                return value;
            }

            public Node Obj(string key)
            {
                object raw;
                if (!_map.TryGetValue(key, out raw))
                {
                    Missing(key);
                    return new Node(new Dictionary<string, object>(StringComparer.Ordinal), PathOf(key), _errors, true);
                }

                _used.Add(key);
                return Open(raw, PathOf(key), _errors);
            }

            public List<Node> ObjList(string key, int minCount, int maxCount)
            {
                var result = new List<Node>();
                object raw;
                if (!_map.TryGetValue(key, out raw))
                {
                    Missing(key);
                    return result;
                }

                _used.Add(key);
                var items = raw as List<object>;
                if (items == null)
                {
                    Report(key, ": очікувався масив");
                    return result;
                }

                if (items.Count < minCount || items.Count > maxCount)
                {
                    Report(key, ".count=" + items.Count.ToString(CultureInfo.InvariantCulture) + ": дозволено "
                        + minCount.ToString(CultureInfo.InvariantCulture) + ".." + maxCount.ToString(CultureInfo.InvariantCulture));
                }

                for (int i = 0; i < items.Count; i++)
                {
                    result.Add(Open(items[i], PathOf(key) + "[" + i.ToString(CultureInfo.InvariantCulture) + "]", _errors));
                }

                return result;
            }

            /// <summary>Необов'язковий масив рядків; немає поля — порожній список.</summary>
            public List<string> StrList(string key)
            {
                var result = new List<string>();
                object raw;
                if (!_map.TryGetValue(key, out raw))
                {
                    return result;
                }

                _used.Add(key);
                var items = raw as List<object>;
                if (items == null)
                {
                    Report(key, ": очікувався масив");
                    return result;
                }

                for (int i = 0; i < items.Count; i++)
                {
                    string item = items[i] as string;
                    if (string.IsNullOrEmpty(item))
                    {
                        Report(key + "[" + i.ToString(CultureInfo.InvariantCulture) + "]", ": очікувався непорожній рядок");
                        continue;
                    }

                    result.Add(item);
                }

                return result;
            }

            /// <summary>Повідомляє про ключі, яких ніхто не прочитав (помилка в назві чи зайве поле).</summary>
            public void Done()
            {
                if (_broken)
                {
                    return;
                }

                var unknown = new List<string>();
                foreach (KeyValuePair<string, object> pair in _map)
                {
                    if (!_used.Contains(pair.Key))
                    {
                        unknown.Add(pair.Key);
                    }
                }

                unknown.Sort(StringComparer.Ordinal);
                for (int i = 0; i < unknown.Count; i++)
                {
                    Report(unknown[i], ": невідомий ключ");
                }
            }

            private void Missing(string key)
            {
                if (!_broken)
                {
                    Report(key, ": обов'язкове поле відсутнє");
                }
            }
        }
    }
}
