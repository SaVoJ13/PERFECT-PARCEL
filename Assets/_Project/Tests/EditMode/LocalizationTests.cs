using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using KebabStreet.Game;
using NUnit.Framework;
using UnityEngine;

namespace KebabStreet.Tests.EditMode
{
    /// <summary>Локалізація (E0-08): повнота ключів у en/uk/pl, плейсхолдери, розбір JSON, запасна мова.</summary>
    public sealed class LocalizationTests
    {
        private static readonly string[] Languages = { "en", "uk", "pl" };

        private static string FolderPath()
        {
            return Path.Combine(Application.dataPath, "_Project", "Resources", "Localization");
        }

        private static LocalizationTable LoadFromDisk(string code)
        {
            string path = Path.Combine(FolderPath(), code + ".json");
            Assert.IsTrue(File.Exists(path), "немає файлу " + path);
            return LocalizationTable.Parse(File.ReadAllText(path));
        }

        private static List<string> AllTextKeys()
        {
            return typeof(TextKeys)
                .GetFields(BindingFlags.Public | BindingFlags.Static)
                .Select(f => (string)f.GetRawConstantValue())
                .ToList();
        }

        private static string Placeholders(string text)
        {
            return string.Join(",", Regex.Matches(text, @"\{\d+\}").Cast<Match>().Select(m => m.Value).OrderBy(v => v, StringComparer.Ordinal));
        }

        [TestCase("en")]
        [TestCase("uk")]
        [TestCase("pl")]
        public void LanguageFile_ContainsEveryTextKey_NonEmpty(string code)
        {
            LocalizationTable table = LoadFromDisk(code);
            List<string> keys = AllTextKeys();
            Assert.Greater(keys.Count, 0);
            foreach (string key in keys)
            {
                string value;
                Assert.IsTrue(table.TryGet(key, out value), code + ": немає ключа " + key);
                Assert.IsFalse(string.IsNullOrWhiteSpace(value), code + ": порожній рядок " + key);
            }
        }

        [TestCase("en")]
        [TestCase("uk")]
        [TestCase("pl")]
        public void LanguageFile_HasNoExtraKeys(string code)
        {
            HashSet<string> known = new HashSet<string>(AllTextKeys());
            string[] extra = LoadFromDisk(code).Keys.Where(k => !known.Contains(k)).ToArray();
            Assert.IsEmpty(extra, code + ": зайві ключі " + string.Join(", ", extra));
        }

        [Test]
        public void Placeholders_AreIdenticalInAllLanguages()
        {
            LocalizationTable en = LoadFromDisk("en");
            foreach (string code in Languages.Where(c => c != "en"))
            {
                LocalizationTable other = LoadFromDisk(code);
                foreach (string key in AllTextKeys())
                {
                    string enText, otherText;
                    Assert.IsTrue(en.TryGet(key, out enText), key);
                    Assert.IsTrue(other.TryGet(key, out otherText), code + " " + key);
                    Assert.AreEqual(Placeholders(enText), Placeholders(otherText), code + ": плейсхолдери " + key);
                }
            }
        }

        [Test]
        public void TimeTemplate_HasExactlyOnePlaceholder_InEveryLanguage()
        {
            foreach (string code in Languages)
            {
                string text;
                Assert.IsTrue(LoadFromDisk(code).TryGet(TextKeys.ShiftTimeLeft, out text));
                Assert.AreEqual("{0}", Placeholders(text), code);
                StringAssert.Contains("1:30", string.Format(text, "1:30"));
            }
        }

        [Test]
        public void Parse_HandlesEscapesAndCyrillic()
        {
            LocalizationTable table = LocalizationTable.Parse(
                "﻿{ \"a\": \"Привіт, світе\", \"b\": \"line1\\nline2\\t\\\"q\\\" \\\\ \\/\", \"c\": \"\\u0416\\u00f3\\u0142w\", \"d\": \"Żółć\" }");
            string value;
            Assert.AreEqual(4, table.Count);
            Assert.IsTrue(table.TryGet("a", out value));
            Assert.AreEqual("Привіт, світе", value);
            Assert.IsTrue(table.TryGet("b", out value));
            Assert.AreEqual("line1\nline2\t\"q\" \\ /", value);
            Assert.IsTrue(table.TryGet("c", out value));
            Assert.AreEqual("Жółw", value);
            Assert.IsTrue(table.TryGet("d", out value));
            Assert.AreEqual("Żółć", value);
        }

        [Test]
        public void Parse_EmptyObject_IsEmptyTable()
        {
            Assert.AreEqual(0, LocalizationTable.Parse(" { } ").Count);
        }

        [TestCase("")]
        [TestCase("[]")]
        [TestCase("{")]
        [TestCase("{\"a\"}")]
        [TestCase("{\"a\":\"x\",}")]
        [TestCase("{\"a\":\"x\"} tail")]
        [TestCase("{\"a\":\"x")]
        [TestCase("{\"a\":\"bad\\q\"}")]
        [TestCase("{\"a\":\"bad\\u12\"}")]
        [TestCase("{\"a\":1}")]
        [TestCase("{\"a\":\"x\",\"a\":\"y\"}")]
        public void Parse_RejectsMalformedJson(string json)
        {
            Assert.Throws<FormatException>(() => LocalizationTable.Parse(json));
        }

        [Test]
        public void Parse_Null_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => LocalizationTable.Parse(null));
        }

        [Test]
        public void Localizer_UsesPrimary_ThenFallback_ThenMarksUnknown()
        {
            LocalizationTable primary = LocalizationTable.Parse("{\"x\":\"primary\"}");
            LocalizationTable fallback = LocalizationTable.Parse("{\"x\":\"fallback\",\"y\":\"only fallback\"}");
            var localizer = new JsonLocalizer(primary, fallback);
            Assert.AreEqual("primary", localizer.Get("x"));
            Assert.AreEqual("only fallback", localizer.Get("y"));
            Assert.AreEqual("[nope]", localizer.Get("nope"));
            Assert.AreEqual("[]", localizer.Get(null));
        }

        [Test]
        public void Localizer_WithMissingTables_DoesNotThrow()
        {
            var localizer = new JsonLocalizer(null, null);
            Assert.AreEqual("[k]", localizer.Get("k"));
            Assert.AreEqual("fb", new JsonLocalizer(null, LocalizationTable.Parse("{\"k\":\"fb\"}")).Get("k"));
        }

        [Test]
        public void Localizer_WithRealFiles_ResolvesEveryKeyInEveryLanguage()
        {
            LocalizationTable en = LoadFromDisk("en");
            foreach (string code in Languages)
            {
                var localizer = new JsonLocalizer(LoadFromDisk(code), en);
                foreach (string key in AllTextKeys())
                {
                    Assert.AreNotEqual("[" + key + "]", localizer.Get(key), code + " " + key);
                }
            }
        }

        [TestCase(SystemLanguage.Ukrainian, "uk")]
        [TestCase(SystemLanguage.Polish, "pl")]
        [TestCase(SystemLanguage.English, "en")]
        [TestCase(SystemLanguage.German, "en")]
        [TestCase(SystemLanguage.Russian, "en")]
        [TestCase(SystemLanguage.Unknown, "en")]
        public void LanguageCode_FollowsSystemLanguage_WithEnglishFallback(SystemLanguage language, string expected)
        {
            Assert.AreEqual(expected, JsonLocalizer.LanguageCodeOf(language));
        }
    }
}
