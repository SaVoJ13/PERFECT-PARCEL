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
    /// <summary>Реєстр і ресурси плейсхолдер-графіки (S1-12): кожен файл має рядок з ліцензією, шляхи з коду існують.</summary>
    public sealed class ArtRegistryTests
    {
        private static string ArtDir => Path.Combine(Application.dataPath, "_Project", "Resources", "Art");

        private static string RegistryPath => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "docs", "art", "ASSET_REGISTRY.md"));

        private static string[] PngNames()
        {
            return Directory.GetFiles(ArtDir, "*.png").Select(Path.GetFileNameWithoutExtension).OrderBy(n => n).ToArray();
        }

        [Test]
        public void EveryArtFileHasRegistryRowWithLicense()
        {
            Assert.IsTrue(File.Exists(RegistryPath), "немає " + RegistryPath);
            string[] lines = File.ReadAllLines(RegistryPath);
            foreach (string name in PngNames())
            {
                string token = "`" + name + ".png`";
                string row = lines.FirstOrDefault(l => l.StartsWith("| " + token));
                Assert.IsNotNull(row, "немає рядка в ASSET_REGISTRY.md для " + name);
                string[] cells = row.Split('|').Select(c => c.Trim()).ToArray();
                // | файл | що | джерело | ліцензія | комерційне | атрибуція | дата |
                Assert.GreaterOrEqual(cells.Length, 8, name);
                Assert.IsTrue(cells[4].Length > 0 && cells[4] != "?", "ліцензія не вказана: " + name);
                Assert.AreEqual("Original", cells[4], "ліцензія " + name);
                Assert.IsTrue(Regex.IsMatch(cells[7], @"^\d{4}-\d{2}-\d{2}$"), "дата " + name);
            }
        }

        [Test]
        public void ArtFilesMatchArtLibraryNames()
        {
            CollectionAssert.AreEquivalent(ArtLibrary.AllNames, PngNames());
        }

        [Test]
        public void EverySpriteConstantInCodeIsListedAndExists()
        {
            string[] files = PngNames();
            var constants = typeof(ArtLibrary).GetFields(BindingFlags.Public | BindingFlags.Static)
                .Where(f => f.IsLiteral && f.FieldType == typeof(string) && f.Name != "ArtFolder" && f.Name != "ThemePath")
                .Select(f => (string)f.GetRawConstantValue()).ToArray();
            Assert.Greater(constants.Length, 10);
            foreach (string name in constants)
            {
                Assert.Contains(name, files, "спрайта немає у Resources/Art: " + name);
            }

            Assert.IsTrue(File.Exists(Path.Combine(Application.dataPath, "_Project", "Resources", ArtLibrary.ThemePath + ".uss")), "немає теми USS");
        }

        [Test]
        public void EveryPngImportsAsSpriteWithUniqueGuid()
        {
            var guids = new HashSet<string>();
            foreach (string png in Directory.GetFiles(ArtDir, "*.png"))
            {
                string metaPath = png + ".meta";
                Assert.IsTrue(File.Exists(metaPath), "немає .meta: " + png);
                string meta = File.ReadAllText(metaPath);
                StringAssert.Contains("textureType: 8", meta, png);
                StringAssert.Contains("spriteMode: 1", meta, png);
                Match m = Regex.Match(meta, @"^guid: ([0-9a-f]{32})\s*$", RegexOptions.Multiline);
                Assert.IsTrue(m.Success, "guid: " + png);
                Assert.IsTrue(guids.Add(m.Groups[1].Value), "повторний guid: " + png);
            }
        }

        [Test]
        public void MeatStatesHaveDistinctIconFiles()
        {
            string[] states = { ArtLibrary.MeatRaw, ArtLibrary.MeatCooking, ArtLibrary.MeatDone, ArtLibrary.MeatWarning, ArtLibrary.MeatBurnt };
            Assert.AreEqual(states.Length, states.Distinct().Count());
            string registry = File.ReadAllText(RegistryPath);
            foreach (string state in states)
            {
                StringAssert.Contains(state + ".png", registry);
            }

            // Файли справді різні за вмістом (форма, а не лише колір).
            var hashes = states.Select(s => Convert.ToBase64String(System.Security.Cryptography.SHA1.Create()
                .ComputeHash(File.ReadAllBytes(Path.Combine(ArtDir, s + ".png"))))).ToArray();
            Assert.AreEqual(states.Length, hashes.Distinct().Count());
        }

        [TestCase("customer.olya.name", "chr_olya")]
        [TestCase("customer.hanna.name", "chr_hanna")]
        [TestCase("customer.name", null)]
        [TestCase("", null)]
        [TestCase(null, null)]
        public void PortraitNameFollowsNameKey(string key, string expected)
        {
            Assert.AreEqual(expected, ArtLibrary.PortraitName(key));
        }

        [Test]
        public void MissingSpriteFallsBackWithoutException()
        {
            Assert.IsNull(ArtLibrary.Get("no_such_sprite"));
            Assert.IsNull(ArtLibrary.Get(null));
            Assert.IsFalse(ArtLibrary.Apply(new UnityEngine.UIElements.VisualElement(), "no_such_sprite"));
            Assert.IsFalse(ArtLibrary.Apply(null, ArtLibrary.Coin));
        }
    }
}
