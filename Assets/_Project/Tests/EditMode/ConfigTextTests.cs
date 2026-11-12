using System.Collections.Generic;
using KebabStreet.Core;
using NUnit.Framework;
using static KebabStreet.Tests.EditMode.ConfigTestKit;

namespace KebabStreet.Tests.EditMode
{
    /// <summary>
    /// «Жодних видимих рядків у конфігах» (S1-04, AGENTS.md розд. 5): будь-який рядок у справжніх
    /// файлах — ідентифікатор або ключ локалізації; поля nameKey / tutorialKeys / storyKeys — саме ключі.
    /// </summary>
    public sealed class ConfigTextTests
    {
        private static readonly string[] Docs =
            { Stations, Recipes, Customers, Rules, "L01", "L02", "L03", "L04", "L05" };

        [Test]
        public void EveryStringInRealConfigs_IsIdentifierOrKey_NotVisibleText()
        {
            int checkedStrings = 0;
            foreach (string doc in Docs)
            {
                object tree = SaveJson.Parse(TextOf(Real(), doc));
                var strings = new List<string>();
                CollectStrings(tree, doc, strings);
                foreach (string entry in strings)
                {
                    string value = entry.Substring(entry.IndexOf('=') + 1);
                    Assert.IsTrue(
                        ConfigText.IsIdentifierOrKey(value),
                        doc + ": рядок схожий на видимий текст: " + entry);
                    checkedStrings++;
                }
            }

            Assert.Greater(checkedStrings, 100, "перевірка справді пройшла по рядках");
        }

        [Test]
        public void EveryKeyField_InRealConfigs_IsLocalizationKey()
        {
            int keys = 0;
            foreach (string doc in Docs)
            {
                object tree = SaveJson.Parse(TextOf(Real(), doc));
                var found = new List<string>();
                CollectKeyFields(tree, found);
                foreach (string key in found)
                {
                    Assert.IsTrue(ConfigText.IsLocalizationKey(key), doc + ": не ключ локалізації: " + key);
                    keys++;
                }
            }

            Assert.Greater(keys, 30);
        }

        [Test]
        public void LevelKeys_UseExpectedPrefixes()
        {
            for (int n = 1; n <= 5; n++)
            {
                LevelDefinition level = Level(n);
                StringAssert.StartsWith("level.", level.NameKey);
                StringAssert.StartsWith("story.", level.StoryIntroKey);
                StringAssert.StartsWith("story.", level.StoryOutroKey);
                for (int i = 0; i < level.TutorialKeyCount; i++)
                {
                    StringAssert.StartsWith("tutorial.", level.GetTutorialKey(i));
                }
            }
        }

        [TestCase("ui.level.goal.serve", true)]
        [TestCase("tutorial.l01.step1", true)]
        [TestCase("recipe.kebab_pita.name", true)]
        [TestCase("Press the button", false)]
        [TestCase("Привіт", false)]
        [TestCase("ui..goal", false)]
        [TestCase("ui.goal.", false)]
        [TestCase(".ui.goal", false)]
        [TestCase("ui", false)]
        [TestCase("1ui.goal", false)]
        [TestCase("", false)]
        public void IsLocalizationKey_AcceptsKeysAndRejectsText(string text, bool expected)
        {
            Assert.AreEqual(expected, ConfigText.IsLocalizationKey(text));
        }

        [TestCase("olya", true)]
        [TestCase("sauce_hot", true)]
        [TestCase("ui", true)]
        [TestCase("tutorial.l01.step1", true)]
        [TestCase("Студентка Оля", false)]
        [TestCase("Hello world", false)]
        [TestCase("a-b", false)]
        [TestCase("", false)]
        public void IsIdentifierOrKey_RejectsVisibleText(string text, bool expected)
        {
            Assert.AreEqual(expected, ConfigText.IsIdentifierOrKey(text));
        }

        private static void CollectStrings(object node, string path, List<string> result)
        {
            var map = node as Dictionary<string, object>;
            if (map != null)
            {
                foreach (KeyValuePair<string, object> pair in map)
                {
                    CollectStrings(pair.Value, path + "." + pair.Key, result);
                }

                return;
            }

            var list = node as List<object>;
            if (list != null)
            {
                for (int i = 0; i < list.Count; i++)
                {
                    CollectStrings(list[i], path + "[" + i + "]", result);
                }

                return;
            }

            if (node is string)
            {
                result.Add(path + "=" + (string)node);
            }
        }

        private static void CollectKeyFields(object node, List<string> result)
        {
            var map = node as Dictionary<string, object>;
            if (map != null)
            {
                foreach (KeyValuePair<string, object> pair in map)
                {
                    bool keyField = pair.Key == "nameKey" || pair.Key == "intro" || pair.Key == "outro";
                    if (keyField && pair.Value is string)
                    {
                        result.Add((string)pair.Value);
                    }
                    else if (pair.Key == "tutorialKeys")
                    {
                        foreach (object item in (List<object>)pair.Value)
                        {
                            result.Add((string)item);
                        }
                    }
                    else
                    {
                        CollectKeyFields(pair.Value, result);
                    }
                }

                return;
            }

            var list = node as List<object>;
            if (list != null)
            {
                foreach (object item in list)
                {
                    CollectKeyFields(item, result);
                }
            }
        }
    }
}
