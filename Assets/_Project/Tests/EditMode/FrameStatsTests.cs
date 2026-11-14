using System;
using KebabStreet.Game;
using NUnit.Framework;

namespace KebabStreet.Tests.EditMode
{
    /// <summary>S1-22: логіка вимірювання кадрів (середній FPS, найдовший, довгі кадри, інтервал, пауза).</summary>
    public sealed class FrameStatsTests
    {
        [Test]
        public void AverageFpsAndMax()
        {
            var stats = new FrameStats(5f, 50f);
            stats.Frame(0.010f, false);
            stats.Frame(0.030f, false);
            Assert.AreEqual(2, stats.Frames);
            Assert.AreEqual(50.0, stats.AverageFps, 0.01);
            Assert.AreEqual(30.0, stats.MaxFrameMs, 0.01);
        }

        [Test]
        public void CountsOnlyFramesOverThreshold()
        {
            var stats = new FrameStats(5f, 50f);
            stats.Frame(0.040f, false);
            stats.Frame(0.051f, false);
            stats.Frame(0.200f, false);
            Assert.AreEqual(2, stats.LongFrames);
        }

        [Test]
        public void ReportsOnlyAfterInterval()
        {
            var stats = new FrameStats(1f, 50f);
            for (int i = 0; i < 59; i++)
            {
                Assert.IsFalse(stats.Frame(1f / 60f, false), "кадр " + i);
            }

            Assert.IsTrue(stats.Frame(1f / 60f + 0.001f, false));
        }

        [Test]
        public void ReportHasFieldsAndResets()
        {
            var stats = new FrameStats(0.1f, 50f);
            stats.Frame(0.020f, false);
            stats.Frame(0.080f, false);
            string report = stats.TakeReport();
            Assert.AreEqual("frames=2 avgFps=20.0 maxMs=80.0 over50ms=1", report);
            Assert.AreEqual(0, stats.Frames);
            Assert.AreEqual(0, stats.LongFrames);
            Assert.AreEqual(0.0, stats.MaxFrameMs);
            Assert.AreEqual(string.Empty, stats.TakeReport());
        }

        [Test]
        public void PausedFramesAreIgnored()
        {
            var stats = new FrameStats(1f, 50f);
            for (int i = 0; i < 1000; i++)
            {
                Assert.IsFalse(stats.Frame(0.5f, true));
            }

            Assert.AreEqual(0, stats.Frames);
            Assert.AreEqual(string.Empty, stats.TakeReport());
        }

        [Test]
        public void NonPositiveDeltaIsIgnored()
        {
            var stats = new FrameStats(1f, 50f);
            stats.Frame(0f, false);
            stats.Frame(-1f, false);
            Assert.AreEqual(0, stats.Frames);
        }

        [Test]
        public void InvalidSettingsThrow()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new FrameStats(0f, 50f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new FrameStats(5f, 0f));
        }
    }
}
