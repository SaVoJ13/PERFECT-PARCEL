using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace KebabStreet.Tests.EditMode
{
    /// <summary>Q-06: шрифт Nunito (OFL) — файли, ліцензія в реєстрі, підключення до теми, покриття EN/UK/PL.</summary>
    public sealed class FontAssetTests
    {
        private const string FontsFolder = "Assets/_Project/Art/Fonts";
        private static readonly string[] FontFiles = { "Nunito-Regular.ttf", "Nunito-ExtraBold.ttf" };

        private static string ProjectRoot => Path.GetFullPath(Path.Combine(Application.dataPath, ".."));

        private static string Read(string relative)
        {
            return File.ReadAllText(Path.Combine(ProjectRoot, relative));
        }

        [Test]
        public void FontFilesHaveOflTextAndMetaWithEmbeddedData()
        {
            foreach (string file in FontFiles.Concat(new[] { "OFL.txt" }))
            {
                Assert.IsTrue(File.Exists(Path.Combine(ProjectRoot, FontsFolder, file)), file);
                Assert.IsTrue(File.Exists(Path.Combine(ProjectRoot, FontsFolder, file + ".meta")), "немає .meta: " + file);
            }

            StringAssert.Contains("SIL OPEN FONT LICENSE Version 1.1", Read(FontsFolder + "/OFL.txt"));
            foreach (string file in FontFiles)
            {
                StringAssert.Contains("includeFontData: 1", Read(FontsFolder + "/" + file + ".meta"), "шрифт має вбудовуватись у збірку: " + file);
            }
        }

        [Test]
        public void EveryFontFileIsInAssetRegisterWithOflAndDate()
        {
            string[] lines = Read("docs/design/ASSET_REGISTER.md").Split('\n');
            foreach (string file in Directory.GetFiles(Path.Combine(ProjectRoot, FontsFolder)).Where(f => !f.EndsWith(".meta")).Select(Path.GetFileName))
            {
                string row = lines.FirstOrDefault(l => l.StartsWith("| `Assets/_Project/Art/Fonts/" + file + "`"));
                Assert.IsNotNull(row, "немає рядка в ASSET_REGISTER.md: " + file);
                StringAssert.Contains("OFL", row, file);
                Assert.IsTrue(Regex.IsMatch(row, @"\d{4}-\d{2}-\d{2}"), "дата: " + file);
            }
        }

        [Test]
        public void ThemeUsesBothNunitoFilesThatExist()
        {
            string uss = Read("Assets/_Project/Resources/Theme/kebab_theme.uss");
            foreach (string file in FontFiles)
            {
                StringAssert.Contains("Art/Fonts/" + file, uss);
            }

            StringAssert.DoesNotContain("Fredoka", uss);
        }

        [Test]
        public void FontsCoverEnglishUkrainianAndPolishTexts()
        {
            const string extra = "ąćęłńóśźżĄĆĘŁŃÓŚŹŻґҐєЄіІїЇ'’₴€—«»…№";
            foreach (string file in FontFiles)
            {
                var font = AssetDatabase.LoadAssetAtPath<Font>(FontsFolder + "/" + file);
                Assert.IsNotNull(font, file);
                foreach (char c in extra)
                {
                    Assert.IsTrue(font.HasCharacter(c), file + ": немає символу " + c + " (U+" + ((int)c).ToString("X4") + ")");
                }

                foreach (string code in new[] { "en", "uk", "pl" })
                {
                    string text = Read("Assets/_Project/Resources/Localization/" + code + ".json");
                    foreach (char c in text.Where(ch => ch > 127 && ch != '﻿').Distinct())
                    {
                        Assert.IsTrue(font.HasCharacter(c), file + " / " + code + ".json: немає символу " + c + " (U+" + ((int)c).ToString("X4") + ")");
                    }
                }
            }
        }

        [Test]
        public void LocalizationDoesNotUseStarGlyphMissingInFont()
        {
            foreach (string code in new[] { "en", "uk", "pl" })
            {
                StringAssert.DoesNotContain("★", Read("Assets/_Project/Resources/Localization/" + code + ".json"), code);
            }
        }
    }
}
