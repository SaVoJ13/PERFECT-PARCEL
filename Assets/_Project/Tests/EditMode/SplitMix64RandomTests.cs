using System;
using KebabStreet.Core;
using NUnit.Framework;

namespace KebabStreet.Tests.EditMode
{
    /// <summary>
    /// Генератор випадковості: «золоті» значення обчислено незалежною реалізацією SplitMix64
    /// (цілі довільної точності) і звірено з еталонними для seed 0 та 1234567.
    /// Якщо ці тести впали — змінився алгоритм, і всі рівні/бот-тести перестали відтворюватись.
    /// </summary>
    public sealed class SplitMix64RandomTests
    {
        private static ulong[] Take(ulong seed, int count)
        {
            var random = new SplitMix64Random(seed);
            var values = new ulong[count];
            for (int i = 0; i < count; i++)
            {
                values[i] = random.NextUInt64();
            }

            return values;
        }

        [Test]
        public void NextUInt64_SeedZero_MatchesGoldenValues()
        {
            var expected = new[]
            {
                0xE220A8397B1DCDAFUL, 0x6E789E6AA1B965F4UL, 0x06C45D188009454FUL,
                0xF88BB8A8724C81ECUL, 0x1B39896A51A8749BUL,
            };

            Assert.AreEqual(expected, Take(0UL, 5));
        }

        [Test]
        public void NextUInt64_Seed1234567_MatchesGoldenValues()
        {
            var expected = new[]
            {
                0x599ED017FB08FC85UL, 0x2C73F08458540FA5UL, 0x883EBCE5A3F27C77UL,
                0x3FBEF740E9177B3FUL, 0xE3B8346708CB5ECDUL,
            };

            Assert.AreEqual(expected, Take(1234567UL, 5));
        }

        [Test]
        public void NextUInt64_MaxSeed_WrapsAroundAndMatchesGoldenValues()
        {
            var expected = new[]
            {
                0xE4D971771B652C20UL, 0xE99FF867DBF682C9UL, 0x382FF84CB27281E9UL,
                0x6D1DB36CCBA982D2UL, 0xB4A0472E578069AEUL,
            };

            Assert.AreEqual(expected, Take(ulong.MaxValue, 5));
        }

        [Test]
        public void State_StartsAtSeed_AndAdvancesByFixedStep()
        {
            var random = new SplitMix64Random(0UL);
            Assert.AreEqual(0UL, random.State);

            for (int i = 0; i < 5; i++)
            {
                random.NextUInt64();
            }

            Assert.AreEqual(0x1715609F7C746C69UL, random.State);
        }

        [Test]
        public void SameSeed_GivesSameSequence()
        {
            Assert.AreEqual(Take(987654321UL, 1000), Take(987654321UL, 1000));
        }

        [Test]
        public void DifferentSeeds_GiveDifferentSequences()
        {
            ulong[] first = Take(1UL, 16);
            ulong[] second = Take(2UL, 16);

            for (int i = 0; i < first.Length; i++)
            {
                Assert.AreNotEqual(first[i], second[i], "Збіг на позиції " + i);
            }
        }

        [Test]
        public void NextInt_SmallRange_MatchesGoldenValues()
        {
            var random = new SplitMix64Random(42UL);
            var expected = new[] { 3, 1, 8, 4, 0, 2, 5, 8, 5, 4, 7, 6 };
            var actual = new int[expected.Length];
            for (int i = 0; i < actual.Length; i++)
            {
                actual[i] = random.NextInt(0, 10);
            }

            Assert.AreEqual(expected, actual);
        }

        [Test]
        public void NextInt_NegativeRange_MatchesGoldenValues()
        {
            var random = new SplitMix64Random(42UL);
            var expected = new[] { -2, -4, 3, -1, -5, -3, 0, 3 };
            var actual = new int[expected.Length];
            for (int i = 0; i < actual.Length; i++)
            {
                actual[i] = random.NextInt(-5, 5);
            }

            Assert.AreEqual(expected, actual);
        }

        [Test]
        public void NextInt_LargeRange_MatchesGoldenValues()
        {
            var random = new SplitMix64Random(7UL);
            var expected = new[]
            {
                554747637, 427130214, 502897936, 29311232, 78165165, 257152344, 997407076, 319755975,
            };
            var actual = new int[expected.Length];
            for (int i = 0; i < actual.Length; i++)
            {
                actual[i] = random.NextInt(0, 1000000007);
            }

            Assert.AreEqual(expected, actual);
        }

        [Test]
        public void NextInt_WidestRange_MatchesGoldenValues()
        {
            var random = new SplitMix64Random(7UL);
            var expected = new[] { 1023274939, 2022220531, 558459523, 2088310075, 1576099136, -1952880488 };
            var actual = new int[expected.Length];
            for (int i = 0; i < actual.Length; i++)
            {
                actual[i] = random.NextInt(int.MinValue, int.MaxValue);
            }

            Assert.AreEqual(expected, actual);
        }

        [Test]
        public void NextInt_StaysInsideRange_AndReachesBothEnds()
        {
            var random = new SplitMix64Random(2026UL);
            bool sawMin = false;
            bool sawMax = false;

            for (int i = 0; i < 2000; i++)
            {
                int value = random.NextInt(-3, 4);
                Assert.That(value, Is.InRange(-3, 3));
                sawMin |= value == -3;
                sawMax |= value == 3;
            }

            Assert.IsTrue(sawMin, "Нижня межа жодного разу не випала");
            Assert.IsTrue(sawMax, "Верхня межа (max - 1) жодного разу не випала");
        }

        [Test]
        public void NextInt_SingleValueRange_ReturnsThatValue()
        {
            var random = new SplitMix64Random(5UL);

            Assert.AreEqual(9, random.NextInt(9, 10));
        }

        [TestCase(5, 5)]
        [TestCase(6, 5)]
        [TestCase(int.MaxValue, int.MinValue)]
        public void NextInt_EmptyRange_ThrowsAndKeepsState(int min, int max)
        {
            var random = new SplitMix64Random(5UL);

            Assert.Throws<ArgumentOutOfRangeException>(() => random.NextInt(min, max));
            Assert.AreEqual(5UL, random.State);
        }
    }
}
