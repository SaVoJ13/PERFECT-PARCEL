using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using KebabStreet.Core;
using NUnit.Framework;
using UnityEngine;

namespace KebabStreet.Tests.EditMode
{
    /// <summary>
    /// Правила збірки KebabStreet.Core, від яких залежить детермінізм (E0-06): перевіряється
    /// скомпільована збірка і її вихідний код. Доповнює ArchitectureSmokeTests (той читає asmdef).
    /// </summary>
    public sealed class CoreRulesTests
    {
        private static readonly string[] ForbiddenInSource =
        {
            "UnityEngine", "UnityEditor", "System.Random", "new Random(", "DateTime", "Stopwatch",
            "Environment.TickCount", "Guid.NewGuid", "System.Threading",
        };

        private static Assembly CoreAssembly
        {
            get { return typeof(ShiftSimulation).Assembly; }
        }

        [Test]
        public void CompiledCore_ReferencesNoUnityAssemblies()
        {
            Assert.AreEqual("KebabStreet.Core", CoreAssembly.GetName().Name);

            foreach (AssemblyName reference in CoreAssembly.GetReferencedAssemblies())
            {
                StringAssert.DoesNotStartWith("Unity", reference.Name);
            }
        }

        [Test]
        public void Core_HasNoMutableStaticState()
        {
            CollectionAssert.IsEmpty(
                FindMutableStaticFields(CoreAssembly.GetTypes()), "У Core знайдено змінний статичний стан");
        }

        /// <summary>
        /// BUG-016: перевірка має ловити і звичайні статичні поля, і статичні автовластивості
        /// (їхнє сховище компілятор називає &lt;Name&gt;k__BackingField), але не константи й readonly.
        /// </summary>
        [Test]
        public void MutableStaticStateCheck_CatchesFieldsAndAutoProperties()
        {
            List<string> found = FindMutableStaticFields(new[] { typeof(StaticStateSamples) });

            Assert.AreEqual(2, found.Count, string.Join(", ", found));
            Assert.IsTrue(found.Exists(name => name.EndsWith(".MutableField", StringComparison.Ordinal)));
            Assert.IsTrue(found.Exists(name => name.Contains("<MutableProperty>")));
        }

        [Test]
        public void MutableStaticStateCheck_AcceptsConstantsAndReadOnlyState()
        {
            CollectionAssert.IsEmpty(FindMutableStaticFields(new[] { typeof(ReadOnlyStateSamples) }));
        }

        private static List<string> FindMutableStaticFields(IEnumerable<Type> types)
        {
            const BindingFlags flags =
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            var found = new List<string>();

            foreach (Type type in types)
            {
                // Типи, які створює компілятор (кеш делегатів тощо), не є станом гри.
                if (type.Name.StartsWith("<", StringComparison.Ordinal))
                {
                    continue;
                }

                foreach (FieldInfo field in type.GetFields(flags))
                {
                    // Службові поля компілятора пропускаються, але сховища автовластивостей — ні (BUG-016).
                    bool autoPropertyStorage = field.Name.EndsWith(">k__BackingField", StringComparison.Ordinal);
                    if (field.Name.StartsWith("<", StringComparison.Ordinal) && !autoPropertyStorage)
                    {
                        continue;
                    }

                    if (!field.IsLiteral && !field.IsInitOnly)
                    {
                        found.Add(type.FullName + "." + field.Name);
                    }
                }
            }

            return found;
        }

        /// <summary>Зразок порушень для перевірки самої перевірки (живе в тестовій збірці, не в Core).</summary>
        private static class StaticStateSamples
        {
            public const int Constant = 1;
            public static readonly int ReadOnlyField = 2;
            public static int MutableField = 3;

            public static int MutableProperty { get; set; }
        }

        private static class ReadOnlyStateSamples
        {
            public const int Constant = 1;
            public static readonly int ReadOnlyField = 2;

            public static int ReadOnlyProperty { get; } = 3;
        }

        [Test]
        public void CoreSource_DoesNotUseEngineSystemClockOrRuntimeRandom()
        {
            string coreFolder = Path.Combine(Application.dataPath, "_Project", "Core");
            string[] files = Directory.GetFiles(coreFolder, "*.cs", SearchOption.AllDirectories);
            Assert.IsNotEmpty(files, "Не знайдено вихідного коду Core у " + coreFolder);

            foreach (string file in files)
            {
                string[] lines = File.ReadAllLines(file);
                for (int i = 0; i < lines.Length; i++)
                {
                    string line = lines[i].TrimStart();
                    if (line.StartsWith("//", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    foreach (string forbidden in ForbiddenInSource)
                    {
                        Assert.IsFalse(
                            line.Contains(forbidden),
                            Path.GetFileName(file) + ":" + (i + 1) + " містить заборонене «" + forbidden + "»");
                    }
                }
            }
        }
    }
}
