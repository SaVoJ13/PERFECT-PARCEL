using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading;
using KebabStreet.Core;
using NUnit.Framework;

namespace KebabStreet.Tests.EditMode
{
    /// <summary>Формат збереження (E0-07): детермінізм, незалежність від культури, пошкоджений текст, версії й міграції.</summary>
    public sealed class SaveSerializerTests
    {
        private const string GoldenText =
            "{\"schemaVersion\":3,\"coins\":120,\"gems\":5,\"levelStars\":{\"loc1_01\":3,\"loc1_02\":1},"
            + "\"lastSavedUnixSec\":1700000000,\"lastKnownUnixSec\":1700000500,"
            + "\"tutorialSkipAll\":false,\"tutorialSeen\":[]}";

        private const string MinimalV2 =
            "{\"schemaVersion\":2,\"coins\":1,\"gems\":1,\"levelStars\":{\"a\":1},"
            + "\"lastSavedUnixSec\":1,\"lastKnownUnixSec\":1}";

        /// <summary>Зафіксовані тексти старих версій: індекс 0 — схема 1, індекс 1 — схема 2 (коли з'явиться схема 3) і так далі.</summary>
        private static readonly string[] OldVersionSamples = { SaveTestKit.V1Text, SaveTestKit.V2Text };

        /// <summary>Пошкоджені тексти: назва випадку і текст. У назвах тестів — лише назва (без керівних символів).</summary>
        private static readonly string[][] MalformedCases =
        {
            new[] { "Empty", "" },
            new[] { "Whitespace", "   " },
            new[] { "NullLiteral", "null" },
            new[] { "TrueLiteral", "true" },
            new[] { "ArrayRoot", "[]" },
            new[] { "EmptyObject", "{}" },
            new[] { "NumberRoot", "42" },
            new[] { "StringRoot", "\"text\"" },
            new[] { "Garbage", "\u0000\u0001 not json %$#" },
            new[] { "OnlyVersion", "{\"schemaVersion\":2}" },
            new[] { "TrailingCommaAfterVersion", "{\"schemaVersion\":2,}" },
            new[] { "VersionZero", "{\"schemaVersion\":0,\"coins\":1,\"gems\":1,\"levelStars\":{},\"lastSavedUnixSec\":1,\"lastKnownUnixSec\":1}" },
            new[] { "VersionNegative", "{\"schemaVersion\":-1,\"coins\":1,\"gems\":1,\"levelStars\":{},\"lastSavedUnixSec\":1,\"lastKnownUnixSec\":1}" },
            new[] { "VersionAsString", "{\"schemaVersion\":\"2\",\"coins\":1,\"gems\":1,\"levelStars\":{},\"lastSavedUnixSec\":1,\"lastKnownUnixSec\":1}" },
            new[] { "VersionFractional", "{\"schemaVersion\":2.0,\"coins\":1,\"gems\":1,\"levelStars\":{},\"lastSavedUnixSec\":1,\"lastKnownUnixSec\":1}" },
            new[] { "CoinsNegative", "{\"schemaVersion\":2,\"coins\":-1,\"gems\":1,\"levelStars\":{},\"lastSavedUnixSec\":1,\"lastKnownUnixSec\":1}" },
            new[] { "CoinsAsString", "{\"schemaVersion\":2,\"coins\":\"many\",\"gems\":1,\"levelStars\":{},\"lastSavedUnixSec\":1,\"lastKnownUnixSec\":1}" },
            new[] { "CoinsFractional", "{\"schemaVersion\":2,\"coins\":1.5,\"gems\":1,\"levelStars\":{},\"lastSavedUnixSec\":1,\"lastKnownUnixSec\":1}" },
            new[] { "CoinsTooLarge", "{\"schemaVersion\":2,\"coins\":99999999999999999999,\"gems\":1,\"levelStars\":{},\"lastSavedUnixSec\":1,\"lastKnownUnixSec\":1}" },
            new[] { "CoinsNull", "{\"schemaVersion\":2,\"coins\":null,\"gems\":1,\"levelStars\":{},\"lastSavedUnixSec\":1,\"lastKnownUnixSec\":1}" },
            new[] { "GemsMissing", "{\"schemaVersion\":2,\"coins\":1,\"levelStars\":{},\"lastSavedUnixSec\":1,\"lastKnownUnixSec\":1}" },
            new[] { "LevelStarsMissing", "{\"schemaVersion\":2,\"coins\":1,\"gems\":1,\"lastSavedUnixSec\":1,\"lastKnownUnixSec\":1}" },
            new[] { "LevelStarsAsArray", "{\"schemaVersion\":2,\"coins\":1,\"gems\":1,\"levelStars\":[],\"lastSavedUnixSec\":1,\"lastKnownUnixSec\":1}" },
            new[] { "StarsNegative", "{\"schemaVersion\":2,\"coins\":1,\"gems\":1,\"levelStars\":{\"a\":-1},\"lastSavedUnixSec\":1,\"lastKnownUnixSec\":1}" },
            new[] { "StarsAsString", "{\"schemaVersion\":2,\"coins\":1,\"gems\":1,\"levelStars\":{\"a\":\"3\"},\"lastSavedUnixSec\":1,\"lastKnownUnixSec\":1}" },
            new[] { "StarsTooLarge", "{\"schemaVersion\":2,\"coins\":1,\"gems\":1,\"levelStars\":{\"a\":4294967296},\"lastSavedUnixSec\":1,\"lastKnownUnixSec\":1}" },
            new[] { "LastKnownMissing", "{\"schemaVersion\":2,\"coins\":1,\"gems\":1,\"levelStars\":{},\"lastSavedUnixSec\":1}" },
            new[] { "LastSavedNegative", "{\"schemaVersion\":2,\"coins\":1,\"gems\":1,\"levelStars\":{},\"lastSavedUnixSec\":-5,\"lastKnownUnixSec\":1}" },
            new[] { "V1WithoutLastSaved", "{\"schemaVersion\":1,\"coins\":1,\"gems\":1,\"levelStars\":{}}" },
            new[] { "ExtraClosingBrace", MinimalV2 + "}" },
            new[] { "TwoObjects", MinimalV2 + MinimalV2 },
            new[] { "TrailingCommaInStars", "{\"schemaVersion\":2,\"coins\":1,\"gems\":1,\"levelStars\":{\"a\":1,},\"lastSavedUnixSec\":1,\"lastKnownUnixSec\":1}" },
            new[] { "BrokenLiteral", "{\"schemaVersion\":2,\"coins\":tru,\"gems\":1,\"levelStars\":{},\"lastSavedUnixSec\":1,\"lastKnownUnixSec\":1}" },
            new[] { "LoneMinus", "{\"schemaVersion\":2,\"coins\":-,\"gems\":1,\"levelStars\":{},\"lastSavedUnixSec\":1,\"lastKnownUnixSec\":1}" },
            new[] { "BadEscape", "{\"schemaVersion\":2,\"note\":\"bad \\q escape\",\"coins\":1,\"gems\":1,\"levelStars\":{},\"lastSavedUnixSec\":1,\"lastKnownUnixSec\":1}" },
            new[] { "BadUnicodeEscape", "{\"schemaVersion\":2,\"note\":\"\\u12G4\",\"coins\":1,\"gems\":1,\"levelStars\":{},\"lastSavedUnixSec\":1,\"lastKnownUnixSec\":1}" },
            new[] { "RawLineBreakInString", "{\"schemaVersion\":2,\"note\":\"line\nbreak\",\"coins\":1,\"gems\":1,\"levelStars\":{},\"lastSavedUnixSec\":1,\"lastKnownUnixSec\":1}" },
            new[] { "UnquotedKeys", "{schemaVersion:2,coins:1,gems:1,levelStars:{},lastSavedUnixSec:1,lastKnownUnixSec:1}" },
        };

        private static IEnumerable<string> MalformedCaseNames()
        {
            foreach (string[] pair in MalformedCases)
            {
                yield return pair[0];
            }
        }

        private static SaveData Golden()
        {
            var data = new SaveData
            {
                Coins = 120,
                Gems = 5,
                LastSavedUnixSec = 1700000000,
                LastKnownUnixSec = 1700000500,
            };
            data.LevelStars["loc1_01"] = 3;
            data.LevelStars["loc1_02"] = 1;
            return data;
        }

        private static SaveData Read(string text)
        {
            bool migrated;
            return SaveSerializer.Deserialize(text, out migrated);
        }

        [Test]
        public void Serialize_MatchesFixedText()
        {
            Assert.AreEqual(GoldenText, SaveSerializer.Serialize(Golden()));
        }

        [Test]
        public void Serialize_NewProfile_MatchesFixedText()
        {
            Assert.AreEqual(
                "{\"schemaVersion\":3,\"coins\":0,\"gems\":0,\"levelStars\":{},\"lastSavedUnixSec\":0,\"lastKnownUnixSec\":0,"
                + "\"tutorialSkipAll\":false,\"tutorialSeen\":[]}",
                SaveSerializer.Serialize(new SaveData()));
        }

        [Test]
        public void Serialize_IsDeterministic_SameDataGivesSameText()
        {
            Assert.AreEqual(SaveSerializer.Serialize(Golden()), SaveSerializer.Serialize(Golden()));

            SaveData once = Golden();
            Assert.AreEqual(SaveSerializer.Serialize(once), SaveSerializer.Serialize(once));
        }

        [Test]
        public void Serialize_IsDeterministic_RegardlessOfInsertionOrder()
        {
            var forward = new SaveData();
            var backward = new SaveData();
            string[] levels = { "loc1_01", "loc1_02", "loc1_10", "loc2_01", "bonus_1" };
            for (int i = 0; i < levels.Length; i++)
            {
                forward.LevelStars[levels[i]] = i % 4;
                backward.LevelStars[levels[levels.Length - 1 - i]] = (levels.Length - 1 - i) % 4;
            }

            Assert.AreEqual(SaveSerializer.Serialize(forward), SaveSerializer.Serialize(backward));
        }

        [Test]
        public void Serialize_OrdersLevelKeysOrdinally()
        {
            var data = new SaveData();
            data.LevelStars["b"] = 1;
            data.LevelStars["B"] = 2;
            data.LevelStars["a"] = 3;
            data.LevelStars["10"] = 0;
            data.LevelStars["9"] = 0;

            StringAssert.Contains(
                "\"levelStars\":{\"10\":0,\"9\":0,\"B\":2,\"a\":3,\"b\":1}", SaveSerializer.Serialize(data));
        }

        [Test]
        public void Serialize_Null_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => SaveSerializer.Serialize(null));
        }

        [TestCase("de-DE")]
        [TestCase("tr-TR")]
        [TestCase("ar-SA")]
        [TestCase("uk-UA")]
        public void SerializeAndDeserialize_DoNotDependOnCulture(string cultureName)
        {
            CultureInfo previous = Thread.CurrentThread.CurrentCulture;
            try
            {
                Thread.CurrentThread.CurrentCulture = new CultureInfo(cultureName);

                Assert.AreEqual(GoldenText, SaveSerializer.Serialize(Golden()));
                SaveTestKit.AssertSame(Golden(), Read(GoldenText));
            }
            finally
            {
                Thread.CurrentThread.CurrentCulture = previous;
            }
        }

        [Test]
        public void RoundTrip_PreservesAllFields()
        {
            SaveData restored = Read(SaveSerializer.Serialize(Golden()));

            SaveTestKit.AssertSame(Golden(), restored);
            Assert.AreEqual(SaveData.CurrentSchemaVersion, restored.SchemaVersion);
        }

        [Test]
        public void RoundTrip_PreservesExtremeValuesAndUnusualLevelIds()
        {
            var data = new SaveData
            {
                Coins = long.MaxValue,
                Gems = 0,
                LastSavedUnixSec = 253402300799,
                LastKnownUnixSec = long.MaxValue,
            };
            data.LevelStars["quote\"back\\slash/"] = int.MaxValue;
            data.LevelStars["рівень їжак"] = 2;
            data.LevelStars["tab\tnewline\ncontrol\u0001"] = 1;
            data.LevelStars["\uD83C\uDF2F"] = 3;
            data.LevelStars[string.Empty] = 0;

            string text = SaveSerializer.Serialize(data);
            SaveData restored = Read(text);

            SaveTestKit.AssertSame(data, restored);
            Assert.AreEqual(text, SaveSerializer.Serialize(restored));
            StringAssert.Contains("\\u0009", text);
            StringAssert.Contains("\\u0001", text);
        }

        [Test]
        public void Deserialize_CurrentVersion_IsNotMigrated()
        {
            bool migrated;
            SaveSerializer.Deserialize(GoldenText, out migrated);

            Assert.IsFalse(migrated);
        }

        [Test]
        public void Deserialize_UnknownFields_AreIgnored()
        {
            const string text =
                "{\"schemaVersion\":2,\"coins\":7,\"extra\":{\"nested\":[true,false,null,1.5e3,-2,\"s\",[],{}]},"
                + "\"gems\":1,\"levelStars\":{\"loc1_01\":2},\"lastSavedUnixSec\":10,\"lastKnownUnixSec\":20,"
                + "\"zzz\":\"\\u0041\\n\\t\\/\"}";

            SaveData data = Read(text);

            Assert.AreEqual(7, data.Coins);
            Assert.AreEqual(1, data.Gems);
            Assert.AreEqual(2, data.LevelStars["loc1_01"]);
            Assert.AreEqual(10, data.LastSavedUnixSec);
            Assert.AreEqual(20, data.LastKnownUnixSec);
            // Визначена поведінка: невідомі поля не зберігаються при наступному записі.
            StringAssert.DoesNotContain("extra", SaveSerializer.Serialize(data));
        }

        [Test]
        public void Deserialize_AcceptsWhitespaceAndAnyFieldOrder()
        {
            const string text =
                " {\r\n\t\"lastKnownUnixSec\" : 1700000500 ,\n \"levelStars\" : { \"loc1_02\" : 1 , \"loc1_01\" : 3 } ,\n"
                + " \"gems\" : 5 , \"coins\" : 120 , \"lastSavedUnixSec\" : 1700000000 , \"schemaVersion\" : 2 }\n ";

            Assert.AreEqual(GoldenText, SaveSerializer.Serialize(Read(text)));
        }

        [TestCaseSource(nameof(MalformedCaseNames))]
        public void Deserialize_MalformedText_ThrowsSaveFormatException(string caseName)
        {
            string text = null;
            foreach (string[] pair in MalformedCases)
            {
                if (pair[0] == caseName)
                {
                    text = pair[1];
                }
            }

            Assert.IsNotNull(text, "Невідомий випадок " + caseName);
            SaveFormatException error = Assert.Catch<SaveFormatException>(() => Read(text));

            Assert.IsNotInstanceOf<SaveVersionTooNewException>(error);
        }

        [Test]
        public void Deserialize_Null_ThrowsSaveFormatException()
        {
            Assert.Throws<SaveFormatException>(() => Read(null));
        }

        [Test]
        public void Deserialize_EveryTruncationOfValidText_ThrowsSaveFormatException()
        {
            for (int length = 0; length < GoldenText.Length; length++)
            {
                string truncated = GoldenText.Substring(0, length);
                Assert.Throws<SaveFormatException>(() => Read(truncated), "Довжина " + length);
            }
        }

        [Test]
        public void Deserialize_DeeplyNestedText_ThrowsInsteadOfOverflowingStack()
        {
            var builder = new StringBuilder("{\"schemaVersion\":2,\"deep\":");
            builder.Append('[', 100000);

            Assert.Throws<SaveFormatException>(() => Read(builder.ToString()));
        }

        [Test]
        public void Migration_V1ToV2_FixedText_GivesExpectedV2()
        {
            bool migrated;
            SaveData data = SaveSerializer.Deserialize(SaveTestKit.V1Text, out migrated);

            Assert.IsTrue(migrated);
            Assert.AreEqual(120, data.Coins);
            Assert.AreEqual(5, data.Gems);
            Assert.AreEqual(2, data.LevelStars.Count);
            Assert.AreEqual(3, data.LevelStars["loc1_01"]);
            Assert.AreEqual(1, data.LevelStars["loc1_02"]);
            Assert.AreEqual(1700000000, data.LastSavedUnixSec);
            Assert.AreEqual(1700000000, data.LastKnownUnixSec, "Нове поле схеми 2 береться з часу останнього збереження");
            Assert.IsFalse(data.TutorialSkipAll);
            Assert.AreEqual(0, data.TutorialSeen.Count, "Нові поля схеми 3 для старого профілю порожні");
            Assert.AreEqual(SaveTestKit.V1MigratedToCurrentText, SaveSerializer.Serialize(data));
        }

        [Test]
        public void Migration_V2ToV3_FixedText_AddsEmptyTutorialState()
        {
            bool migrated;
            SaveData data = SaveSerializer.Deserialize(SaveTestKit.V2Text, out migrated);

            Assert.IsTrue(migrated);
            Assert.AreEqual(120, data.Coins);
            Assert.AreEqual(1700000500, data.LastKnownUnixSec);
            Assert.IsFalse(data.TutorialSkipAll);
            Assert.AreEqual(0, data.TutorialSeen.Count);
            Assert.AreEqual(GoldenText, SaveSerializer.Serialize(data));
        }

        [Test]
        public void TutorialState_RoundTrips_SortedAndDeterministic()
        {
            SaveData data = Golden();
            data.TutorialSkipAll = true;
            data.TutorialSeen.Add("l01.step2");
            data.TutorialSeen.Add("l01.step1");

            string text = SaveSerializer.Serialize(data);

            StringAssert.EndsWith("\"tutorialSkipAll\":true,\"tutorialSeen\":[\"l01.step1\",\"l01.step2\"]}", text);
            SaveTestKit.AssertSame(data, Read(text));
        }

        [Test]
        public void Deserialize_V3_TutorialFieldsMissingOrWrong_Throws()
        {
            string noFlag = "{\"schemaVersion\":3,\"coins\":1,\"gems\":1,\"levelStars\":{},\"lastSavedUnixSec\":1,"
                + "\"lastKnownUnixSec\":1,\"tutorialSeen\":[]}";
            string flagAsNumber = "{\"schemaVersion\":3,\"coins\":1,\"gems\":1,\"levelStars\":{},\"lastSavedUnixSec\":1,"
                + "\"lastKnownUnixSec\":1,\"tutorialSkipAll\":1,\"tutorialSeen\":[]}";
            string seenNotArray = "{\"schemaVersion\":3,\"coins\":1,\"gems\":1,\"levelStars\":{},\"lastSavedUnixSec\":1,"
                + "\"lastKnownUnixSec\":1,\"tutorialSkipAll\":false,\"tutorialSeen\":{}}";
            string seenNotString = "{\"schemaVersion\":3,\"coins\":1,\"gems\":1,\"levelStars\":{},\"lastSavedUnixSec\":1,"
                + "\"lastKnownUnixSec\":1,\"tutorialSkipAll\":false,\"tutorialSeen\":[5]}";

            Assert.Throws<SaveFormatException>(() => Read(noFlag));
            Assert.Throws<SaveFormatException>(() => Read(flagAsNumber));
            Assert.Throws<SaveFormatException>(() => Read(seenNotArray));
            Assert.Throws<SaveFormatException>(() => Read(seenNotString));
        }

        [Test]
        public void Migration_EveryOlderVersion_HasFixedSampleThatLoads()
        {
            Assert.AreEqual(
                SaveData.CurrentSchemaVersion - 1,
                OldVersionSamples.Length,
                "Для кожної старої версії схеми потрібен зафіксований зразок і тест міграції");

            for (int i = 0; i < OldVersionSamples.Length; i++)
            {
                StringAssert.StartsWith(
                    "{\"schemaVersion\":" + (i + 1).ToString(CultureInfo.InvariantCulture) + ",", OldVersionSamples[i]);

                bool migrated;
                SaveData data = SaveSerializer.Deserialize(OldVersionSamples[i], out migrated);

                Assert.IsTrue(migrated, "Версія " + (i + 1));
                Assert.AreEqual(SaveData.CurrentSchemaVersion, data.SchemaVersion);
            }
        }

        [TestCase(4L)]
        [TestCase(100L)]
        [TestCase(long.MaxValue)]
        public void Deserialize_NewerVersion_ThrowsTooNewWithFoundVersion(long version)
        {
            string text = "{\"schemaVersion\":" + version.ToString(CultureInfo.InvariantCulture) + ",\"coins\":777}";

            SaveVersionTooNewException error = Assert.Throws<SaveVersionTooNewException>(() => Read(text));

            Assert.AreEqual(version, error.FoundVersion);
        }
    }
}
