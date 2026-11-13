using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using KebabStreet.Game;
using NUnit.Framework;
using UnityEngine;

namespace KebabStreet.Tests.EditMode
{
    /// <summary>Тимчасові звуки (S1-13): файли, реєстр, програвач і підключення до подій без Unity-аудіо.</summary>
    public sealed class SfxTests
    {
        private const ulong Seed = 20261113UL;

        private static readonly string[] AudioExtensions = { ".wav", ".ogg", ".mp3", ".aif", ".aiff", ".flac" };

        private sealed class RecordingSink : ISfxSink
        {
            public readonly List<SfxCue> Cues = new List<SfxCue>();

            public void Play(SfxCue cue)
            {
                Cues.Add(cue);
            }
        }

        private static string AssetsDir => Path.Combine(Application.dataPath, "_Project");

        private static string RegistryPath => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "docs", "art", "ASSET_REGISTRY.md"));

        /// <summary>Усі аудіофайли проєкту: обидві теки аудіо, рекурсивно.</summary>
        private static string[] AllAudioFiles()
        {
            var files = new List<string>();
            foreach (string dir in new[] { Path.Combine(AssetsDir, "Resources", "Audio"), Path.Combine(AssetsDir, "Audio") })
            {
                if (Directory.Exists(dir))
                {
                    files.AddRange(Directory.GetFiles(dir, "*", SearchOption.AllDirectories)
                        .Where(f => AudioExtensions.Contains(Path.GetExtension(f).ToLowerInvariant())));
                }
            }

            return files.ToArray();
        }

        [TearDown]
        public void ResetHub()
        {
            SfxHub.Install(null);
        }

        // ---- файли ----

        [Test]
        public void AudioFilesMatchCatalog()
        {
            string[] names = AllAudioFiles().Select(Path.GetFileNameWithoutExtension).ToArray();
            CollectionAssert.AreEquivalent(SfxCatalog.AllNames, names);
            foreach (SfxCue cue in Enum.GetValues(typeof(SfxCue)))
            {
                Assert.Contains(SfxCatalog.NameOf(cue), SfxCatalog.AllNames, cue.ToString());
            }

            Assert.IsNull(SfxCatalog.NameOf((SfxCue)0));
        }

        [Test]
        public void WavFilesHaveValidHeaderAndAreShortAndQuiet()
        {
            string[] files = AllAudioFiles();
            Assert.AreEqual(SfxCatalog.AllNames.Length, files.Length);
            foreach (string file in files)
            {
                string label = Path.GetFileName(file);
                Assert.AreEqual(".wav", Path.GetExtension(file), label);
                byte[] b = File.ReadAllBytes(file);
                Assert.GreaterOrEqual(b.Length, 44, label);
                Assert.AreEqual("RIFF", Encoding.ASCII.GetString(b, 0, 4), label);
                Assert.AreEqual("WAVE", Encoding.ASCII.GetString(b, 8, 4), label);
                Assert.AreEqual("fmt ", Encoding.ASCII.GetString(b, 12, 4), label);
                Assert.AreEqual(16, BitConverter.ToInt32(b, 16), label);
                Assert.AreEqual(1, BitConverter.ToInt16(b, 20), label + ": PCM");
                Assert.AreEqual(1, BitConverter.ToInt16(b, 22), label + ": моно");
                int rate = BitConverter.ToInt32(b, 24);
                Assert.That(rate, Is.InRange(22050, 44100), label + ": частота");
                Assert.AreEqual(rate * 2, BitConverter.ToInt32(b, 28), label);
                Assert.AreEqual(2, BitConverter.ToInt16(b, 32), label);
                Assert.AreEqual(16, BitConverter.ToInt16(b, 34), label + ": біт");
                Assert.AreEqual("data", Encoding.ASCII.GetString(b, 36, 4), label);
                int dataBytes = BitConverter.ToInt32(b, 40);
                Assert.AreEqual(b.Length - 44, dataBytes, label + ": розмір data");
                Assert.AreEqual(b.Length - 8, BitConverter.ToInt32(b, 4), label + ": розмір RIFF");
                double seconds = dataBytes / 2.0 / rate;
                Assert.That(seconds, Is.GreaterThan(0.0).And.LessThanOrEqualTo(1.0), label + ": тривалість");

                int peak = 0;
                for (int i = 44; i + 1 < b.Length; i += 2)
                {
                    peak = Math.Max(peak, Math.Abs((int)BitConverter.ToInt16(b, i)));
                }

                Assert.Greater(peak, 1000, label + ": тиша");
                Assert.LessOrEqual(peak, (int)(0.5 * 32767), label + ": надто гучно (захист слуху)");
            }
        }

        [Test]
        public void EveryAudioFileHasRegistryRowWithLicense()
        {
            Assert.IsTrue(File.Exists(RegistryPath), "немає " + RegistryPath);
            string[] lines = File.ReadAllLines(RegistryPath);
            foreach (string file in AllAudioFiles())
            {
                string name = Path.GetFileName(file);
                string row = lines.FirstOrDefault(l => l.StartsWith("| `" + name + "`"));
                Assert.IsNotNull(row, "немає рядка в ASSET_REGISTRY.md для " + name);
                string[] cells = row.Split('|').Select(c => c.Trim()).ToArray();
                Assert.GreaterOrEqual(cells.Length, 8, name);
                Assert.AreEqual("Original", cells[4], "ліцензія " + name);
                Assert.IsTrue(Regex.IsMatch(cells[7], @"^\d{4}-\d{2}-\d{2}$"), "дата " + name);
            }
        }

        [Test]
        public void EveryAudioFileHasMetaWithUniqueGuid()
        {
            var guids = new HashSet<string>();
            foreach (string file in AllAudioFiles())
            {
                string meta = file + ".meta";
                Assert.IsTrue(File.Exists(meta), "немає .meta: " + file);
                string text = File.ReadAllText(meta);
                StringAssert.Contains("AudioImporter:", text, file);
                Match m = Regex.Match(text, @"^guid: ([0-9a-f]{32})\s*$", RegexOptions.Multiline);
                Assert.IsTrue(m.Success, "guid: " + file);
                Assert.IsTrue(guids.Add(m.Groups[1].Value), "повторний guid: " + file);
            }
        }

        // ---- програвач ----

        [Test]
        public void Router_IsOnByDefault_AndSilentWhenPausedDisabledOrWithoutSink()
        {
            var sink = new RecordingSink();
            var router = new SfxRouter(sink);
            Assert.IsTrue(router.Enabled);
            Assert.IsTrue(router.Play(SfxCue.Cash));
            router.Paused = true;
            Assert.IsFalse(router.Play(SfxCue.Cash));
            router.Paused = false;
            router.Enabled = false;
            Assert.IsFalse(router.Play(SfxCue.Cash));
            Assert.IsFalse(new SfxRouter(null).Play(SfxCue.Cash));
            CollectionAssert.AreEqual(new[] { SfxCue.Cash }, sink.Cues);
        }

        [Test]
        public void UnitySink_WithMissingFileOrSource_DoesNotThrow_AndLoadsOnce()
        {
            var go = new GameObject("sfx-test");
            try
            {
                AudioSource source = go.AddComponent<AudioSource>();
                int loads = 0;
                var sink = new UnitySfxSink(source, name =>
                {
                    loads++;
                    return null;
                });
                foreach (SfxCue cue in Enum.GetValues(typeof(SfxCue)))
                {
                    SfxCue captured = cue;
                    Assert.DoesNotThrow(() => sink.Play(captured));
                    Assert.DoesNotThrow(() => sink.Play(captured));
                }

                Assert.DoesNotThrow(() => sink.Play((SfxCue)0));
                Assert.AreEqual(SfxCatalog.AllNames.Length, loads, "відсутній файл кешується");
                Assert.DoesNotThrow(() => new UnitySfxSink(null).Play(SfxCue.Tap));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void DefaultLoad_ReturnsNullForUnknownName_WithoutException()
        {
            Assert.IsNull(UnitySfxSink.DefaultLoad("no_such_sound"));
            Assert.IsNull(UnitySfxSink.DefaultLoad(null));
        }

        // ---- події ----

        [Test]
        public void Director_RequestsMatchingCueForEachEvent()
        {
            var sink = new RecordingSink();
            var router = new SfxRouter(sink);
            var counters = new SfxCounters();
            var director = new SfxDirector(() => counters, router);

            director.Update(false);
            CollectionAssert.IsEmpty(sink.Cues);

            counters.Placed = 1;
            director.Update(false);
            counters.Served = 1;
            counters.Tips = 3;
            director.Update(false);
            counters.Left = 1;
            director.Update(false);
            counters.Burnt = 1;
            director.Update(false);
            director.Update(false);

            CollectionAssert.AreEqual(
                new[] { SfxCue.Sizzle, SfxCue.Cash, SfxCue.Happy, SfxCue.Sad, SfxCue.Burnt }, sink.Cues);
        }

        [Test]
        public void Director_OnPause_RequestsNothing_AndDoesNotReplayAfterResume()
        {
            var sink = new RecordingSink();
            var router = new SfxRouter(sink);
            var counters = new SfxCounters();
            var director = new SfxDirector(() => counters, router);

            counters.Burnt = 2;
            counters.Placed = 2;
            director.Update(true);
            Assert.IsTrue(router.Paused);
            Assert.IsFalse(router.Play(SfxCue.Tap));
            director.Update(false);
            Assert.IsFalse(router.Paused);
            CollectionAssert.IsEmpty(sink.Cues);
        }

        [Test]
        public void Director_ManyEventsInOneFrame_PlayEachCueOnce()
        {
            var sink = new RecordingSink();
            var counters = new SfxCounters();
            var director = new SfxDirector(() => counters, new SfxRouter(sink));
            counters.Burnt = 4;
            counters.Left = 3;
            director.Update(false);
            CollectionAssert.AreEquivalent(new[] { SfxCue.Sad, SfxCue.Burnt }, sink.Cues);
        }

        [Test]
        public void Director_WithRealShift_PlacingMeatSizzles_AndOtherCuesFollowCounters()
        {
            var sink = new RecordingSink();
            var driver = new ShiftDriver(ConfigTestKit.Level(1), Seed);
            SfxDirector director = SfxDirector.ForDriver(driver, new SfxRouter(sink));
            driver.Begin();
            Assert.IsTrue(driver.Tap(ShiftTap.PlaceMeat));

            for (int i = 0; i < 4000 && !driver.IsFinished; i++)
            {
                driver.Frame(0.25);
                director.Update(driver.IsPaused);
            }

            Assert.IsTrue(driver.IsFinished, "зміна має закінчитися");
            Assert.IsTrue(sink.Cues.Contains(SfxCue.Sizzle));
            Assert.AreEqual(driver.Simulation.BurntCount > 0, sink.Cues.Contains(SfxCue.Burnt));
            long left = driver.Simulation.Counter == null ? 0L : driver.Simulation.Counter.LeftCount;
            Assert.AreEqual(left > 0, sink.Cues.Contains(SfxCue.Sad));
            Assert.AreEqual(driver.Simulation.ServedCount > 0, sink.Cues.Contains(SfxCue.Cash));
        }

        [Test]
        public void ButtonTapWrapper_RequestsTapSound_AndRunsAction()
        {
            var sink = new RecordingSink();
            SfxRouter router = SfxHub.Install(sink);
            int runs = 0;
            Action wrapped = SfxHub.WithTapSound(() => runs++);
            wrapped();
            CollectionAssert.AreEqual(new[] { SfxCue.Tap }, sink.Cues);
            Assert.AreEqual(1, runs);

            router.Paused = true;
            wrapped();
            Assert.AreEqual(1, sink.Cues.Count, "на паузі звуку немає");
            Assert.AreEqual(2, runs, "дія кнопки виконується завжди");
            Assert.DoesNotThrow(() => SfxHub.WithTapSound(null)());
        }

        [Test]
        public void Hub_WithoutSink_IsSilentWithoutException()
        {
            SfxHub.Install(null);
            Assert.IsFalse(SfxHub.Play(SfxCue.Cash));
        }
    }
}
