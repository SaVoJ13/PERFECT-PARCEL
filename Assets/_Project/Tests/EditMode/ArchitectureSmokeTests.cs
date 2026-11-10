using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace KebabStreet.Tests.EditMode
{
    /// <summary>
    /// Димові тести архітектури (E0-04): перевіряють межі збірок за їхніми asmdef-файлами.
    /// Справжні тести логіки з'являться в E0-06.
    /// </summary>
    public sealed class ArchitectureSmokeTests
    {
        [System.Serializable]
        private sealed class AsmdefData
        {
            public string name;
            public string[] references;
            public bool noEngineReferences;
        }

        private static AsmdefData LoadAsmdef(string folder, string fileName)
        {
            string path = Path.Combine(Application.dataPath, "_Project", folder, fileName);
            Assert.IsTrue(File.Exists(path), "Немає файлу " + path);
            return JsonUtility.FromJson<AsmdefData>(File.ReadAllText(path));
        }

        [Test]
        public void Core_DoesNotReferenceUnityEngine()
        {
            AsmdefData core = LoadAsmdef("Core", "KebabStreet.Core.asmdef");

            Assert.AreEqual("KebabStreet.Core", core.name);
            Assert.IsTrue(core.noEngineReferences, "Core має бути збіркою без посилань на UnityEngine");
            Assert.IsEmpty(core.references, "Core не повинен посилатися на інші збірки");
        }

        [Test]
        public void Services_ReferenceCore()
        {
            AsmdefData services = LoadAsmdef("Services", "KebabStreet.Services.asmdef");

            Assert.AreEqual("KebabStreet.Services", services.name);
            CollectionAssert.Contains(services.references, "KebabStreet.НавмисноЗламано");
        }

        [Test]
        public void TestAssembly_SeesCoreAndServices()
        {
            AsmdefData tests = LoadAsmdef(Path.Combine("Tests", "EditMode"), "KebabStreet.Tests.EditMode.asmdef");

            Assert.AreEqual("KebabStreet.Tests.EditMode", tests.name);
            CollectionAssert.Contains(tests.references, "KebabStreet.Core");
            CollectionAssert.Contains(tests.references, "KebabStreet.Services");
        }
    }
}
