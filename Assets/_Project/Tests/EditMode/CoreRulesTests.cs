using System;
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
            const BindingFlags flags =
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

            foreach (Type type in CoreAssembly.GetTypes())
            {
                foreach (FieldInfo field in type.GetFields(flags))
                {
                    // Поля, які створює компілятор (кеш делегатів тощо), не є станом гри.
                    if (field.Name.StartsWith("<", StringComparison.Ordinal)
                        || type.Name.StartsWith("<", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    Assert.IsTrue(
                        field.IsLiteral || field.IsInitOnly,
                        "Змінне статичне поле " + type.FullName + "." + field.Name);
                }
            }
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
