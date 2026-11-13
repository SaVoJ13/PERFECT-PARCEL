using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using KebabStreet.Core;
using KebabStreet.Game;
using NUnit.Framework;

namespace KebabStreet.Tests.EditMode
{
    /// <summary>
    /// Заготовки для тестів конфігів рівнів (S1-04): справжні файли з Resources, редагування
    /// розібраного дерева JSON (вадний JSON для кожної категорії помилок) і зворотний запис у текст.
    /// </summary>
    internal static class ConfigTestKit
    {
        public const string Stations = "stations";
        public const string Recipes = "recipes";
        public const string Customers = "customers";
        public const string Rules = "rules";

        private static ConfigSources _real;

        /// <summary>Справжні конфіги з Assets/_Project/Resources/Config (читаються один раз).</summary>
        public static ConfigSources Real()
        {
            if (_real == null)
            {
                _real = ResourceConfigSource.Load();
            }

            return _real;
        }

        public static LevelCatalog LoadCatalog()
        {
            ConfigResult<LevelCatalog> result = ConfigLoader.LoadAll(Real());
            Assert.IsTrue(result.IsValid, "Справжні конфіги мають бути валідні:\n" + string.Join("\n", new List<string>(result.Errors)));
            return result.Value;
        }

        public static LevelDefinition Level(int number)
        {
            LevelDefinition level = LoadCatalog().FindByNumber(number);
            Assert.IsNotNull(level, "Немає рівня " + number);
            return level;
        }

        /// <summary>Текст документа: stations / recipes / customers / rules / L01…</summary>
        public static string TextOf(ConfigSources sources, string doc)
        {
            switch (doc)
            {
                case Stations: return sources.Stations;
                case Recipes: return sources.Recipes;
                case Customers: return sources.Customers;
                case Rules: return sources.Rules;
                default: return sources.FindLevelText(doc);
            }
        }

        /// <summary>Копія джерел, де документ замінено текстом (null — файл відсутній).</summary>
        public static ConfigSources Replace(ConfigSources sources, string doc, string text)
        {
            var levels = new List<KeyValuePair<string, string>>();
            for (int i = 0; i < sources.LevelCount; i++)
            {
                string name = sources.GetLevelName(i);
                levels.Add(new KeyValuePair<string, string>(name, name == doc ? text : sources.GetLevelText(i)));
            }

            return new ConfigSources(
                doc == Stations ? text : sources.Stations,
                doc == Recipes ? text : sources.Recipes,
                doc == Customers ? text : sources.Customers,
                doc == Rules ? text : sources.Rules,
                levels);
        }

        /// <summary>Справжні конфіги, де документ змінено правкою розібраного дерева JSON.</summary>
        public static ConfigSources Mutated(string doc, Action<Dictionary<string, object>> mutate)
        {
            ConfigSources real = Real();
            var tree = (Dictionary<string, object>)SaveJson.Parse(TextOf(real, doc));
            mutate(tree);
            return Replace(real, doc, Write(tree));
        }

        public static Dictionary<string, object> Map(object node, string key)
        {
            return (Dictionary<string, object>)((Dictionary<string, object>)node)[key];
        }

        public static List<object> Arr(object node, string key)
        {
            return (List<object>)((Dictionary<string, object>)node)[key];
        }

        public static Dictionary<string, object> Item(List<object> list, int index)
        {
            return (Dictionary<string, object>)list[index];
        }

        /// <summary>Елемент списку з полем id, що дорівнює значенню.</summary>
        public static Dictionary<string, object> ById(List<object> list, string id)
        {
            foreach (object item in list)
            {
                var map = (Dictionary<string, object>)item;
                if ((string)map["id"] == id)
                {
                    return map;
                }
            }

            throw new InvalidOperationException("Немає елемента id=" + id);
        }

        /// <summary>Усі помилки одним рядком (для повідомлень тестів).</summary>
        public static string Join(IReadOnlyList<string> errors)
        {
            return string.Join("\n", new List<string>(errors));
        }

        /// <summary>Найменші назви клієнтів, що приходять на рівні (за алфавітом, без повторів).</summary>
        public static List<string> UsedCustomers(LevelDefinition level)
        {
            var set = new List<string>();
            ArrivalSchedule schedule = level.Shift.Counter.Schedule;
            for (int i = 0; i < schedule.Count; i++)
            {
                string id = schedule.Get(i).Customer;
                if (!set.Contains(id))
                {
                    set.Add(id);
                }
            }

            set.Sort(StringComparer.Ordinal);
            return set;
        }

        /// <summary>Запис дерева JSON назад у текст (лише типи, які дає SaveJson).</summary>
        public static string Write(object tree)
        {
            var builder = new StringBuilder();
            WriteValue(builder, tree);
            return builder.ToString();
        }

        private static void WriteValue(StringBuilder builder, object value)
        {
            if (value == null)
            {
                builder.Append("null");
            }
            else if (value is bool)
            {
                builder.Append((bool)value ? "true" : "false");
            }
            else if (value is long)
            {
                builder.Append(((long)value).ToString(CultureInfo.InvariantCulture));
            }
            else if (value is int)
            {
                builder.Append(((int)value).ToString(CultureInfo.InvariantCulture));
            }
            else if (value is double)
            {
                string text = ((double)value).ToString("R", CultureInfo.InvariantCulture);
                builder.Append(text);
                if (text.IndexOfAny(new[] { '.', 'E', 'e' }) < 0)
                {
                    builder.Append(".0");
                }
            }
            else if (value is string)
            {
                SaveJson.AppendString(builder, (string)value);
            }
            else if (value is List<object>)
            {
                builder.Append('[');
                bool first = true;
                foreach (object item in (List<object>)value)
                {
                    if (!first)
                    {
                        builder.Append(',');
                    }

                    first = false;
                    WriteValue(builder, item);
                }

                builder.Append(']');
            }
            else if (value is Dictionary<string, object>)
            {
                builder.Append('{');
                bool first = true;
                foreach (KeyValuePair<string, object> pair in (Dictionary<string, object>)value)
                {
                    if (!first)
                    {
                        builder.Append(',');
                    }

                    first = false;
                    SaveJson.AppendString(builder, pair.Key);
                    builder.Append(':');
                    WriteValue(builder, pair.Value);
                }

                builder.Append('}');
            }
            else
            {
                throw new InvalidOperationException("Невідомий тип у дереві JSON: " + value.GetType());
            }
        }
    }
}
