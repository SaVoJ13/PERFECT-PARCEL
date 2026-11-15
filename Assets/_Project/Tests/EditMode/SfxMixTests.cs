using System;
using System.Collections.Generic;
using KebabStreet.Game;
using NUnit.Framework;
using UnityEngine;

namespace KebabStreet.Tests.EditMode
{
    /// <summary>Q-05g: синтезовані звуки, мікс (шини, ducking), ліміти частоти/копій, варіація висоти, події.</summary>
    public sealed class SfxMixTests
    {
        private sealed class Rec : ISfxSink
        {
            public readonly List<SfxCue> Cues = new List<SfxCue>();
            public readonly List<float> Pitches = new List<float>();
            public readonly List<string> Loops = new List<string>();
            public float Effects = -1f;
            public float Music = -1f;

            public void Play(SfxCue cue, float pitch)
            {
                Cues.Add(cue);
                Pitches.Add(pitch);
            }

            public void SetLoop(SfxLoop loop, bool on)
            {
                Loops.Add(loop + (on ? ":on" : ":off"));
            }

            public void SetVolumes(float effects, float music)
            {
                Effects = effects;
                Music = music;
            }
        }

        private sealed class Clock
        {
            public double Now;

            public double Read()
            {
                return Now;
            }
        }

        private static SfxCue[] SynthCues()
        {
            var list = new List<SfxCue>();
            foreach (SfxCue cue in Enum.GetValues(typeof(SfxCue)))
            {
                if (SfxCatalog.IsSynth(cue))
                {
                    list.Add(cue);
                }
            }

            return list.ToArray();
        }

        private static float Peak(float[] data)
        {
            float peak = 0f;
            foreach (float v in data)
            {
                peak = Math.Max(peak, Math.Abs(v));
            }

            return peak;
        }

        [TearDown]
        public void ResetHub()
        {
            SfxHub.Install(null);
        }

        // ---- синтез ----

        [Test]
        public void SynthCues_AreTheSevenNewOnes_AndHaveNoFiles()
        {
            Assert.AreEqual(7, SynthCues().Length);
            foreach (SfxCue cue in SynthCues())
            {
                Assert.IsNull(SfxCatalog.NameOf(cue), cue.ToString());
                Assert.IsTrue(SfxCatalog.IsKnown(cue), cue.ToString());
            }
        }

        [Test]
        public void SynthClips_HaveExpectedLength_AndAreNotSilent_AndAreQuiet()
        {
            foreach (SfxCue cue in SynthCues())
            {
                float[] data = SfxSynth.Generate(cue);
                int expected = (int)Math.Round(SfxSynth.DurationSec(cue) * SfxSynth.SampleRate);
                Assert.AreEqual(expected, data.Length, cue.ToString());
                Assert.LessOrEqual(data.Length / (double)SfxSynth.SampleRate, 1.0, cue + ": ≤ 1 с");
                float peak = Peak(data);
                Assert.Greater(peak, 0.05f, cue + ": тиша");
                Assert.LessOrEqual(peak, SfxSynth.PeakCeiling + 0.0001f, cue + ": захист слуху");
                Assert.AreEqual(SfxSynth.PeakOf(cue), peak, 0.001f, cue.ToString());
                foreach (float v in data)
                {
                    Assert.IsFalse(float.IsNaN(v) || float.IsInfinity(v), cue.ToString());
                }
            }
        }

        [Test]
        public void SynthClips_AreDeterministic()
        {
            foreach (SfxCue cue in SynthCues())
            {
                CollectionAssert.AreEqual(SfxSynth.Generate(cue), SfxSynth.Generate(cue), cue.ToString());
            }

            foreach (SfxLoop loop in new[] { SfxLoop.Grill, SfxLoop.Music })
            {
                CollectionAssert.AreEqual(SfxSynth.GenerateLoop(loop), SfxSynth.GenerateLoop(loop), loop.ToString());
            }
        }

        [Test]
        public void Generate_ForFileCueOrUnknown_IsEmpty()
        {
            Assert.AreEqual(0, SfxSynth.Generate(SfxCue.Tap).Length);
            Assert.AreEqual(0, SfxSynth.Generate((SfxCue)0).Length);
        }

        [Test]
        public void Loops_HaveExpectedLength_AreQuiet_AndHaveNoSeamJump()
        {
            foreach (SfxLoop loop in new[] { SfxLoop.Grill, SfxLoop.Music })
            {
                float[] data = SfxSynth.GenerateLoop(loop);
                double sec = data.Length / (double)SfxSynth.SampleRate;
                Assert.AreEqual(loop == SfxLoop.Grill ? SfxSynth.GrillLoopSec : SfxSynth.MusicSec, sec, 0.01, loop.ToString());
                float peak = Peak(data);
                Assert.AreEqual(SfxSynth.PeakOf(loop), peak, 0.001f, loop.ToString());
                Assert.LessOrEqual(peak, 0.25f, loop + ": петля тиха");
                float seam = Math.Abs(data[0] - data[data.Length - 1]);
                float maxStep = 0f;
                for (int i = 1; i < data.Length; i++)
                {
                    maxStep = Math.Max(maxStep, Math.Abs(data[i] - data[i - 1]));
                }

                Assert.LessOrEqual(seam, maxStep + 0.0001f, loop + ": шов не більший за звичайний крок між відліками");
            }
        }

        [Test]
        public void MusicLoop_EndsNearSilence_SoLoopIsSeamless()
        {
            float[] data = SfxSynth.GenerateLoop(SfxLoop.Music);
            Assert.Less(Math.Abs(data[data.Length - 1]), 0.002f);
            Assert.Less(Math.Abs(data[0]), 0.002f);
        }

        [Test]
        public void TimerUrgentTick_IsLouderThanTimerTick()
        {
            Assert.Greater(SfxSynth.PeakOf(SfxCue.TimerTickUrgent), SfxSynth.PeakOf(SfxCue.TimerTick));
        }

        [Test]
        public void TimerTickWindow_MatchesUiTimerWarning()
        {
            Assert.AreEqual(UiMotion.TimerWarningSec, SfxCatalog.TimerTickFromSec);
        }

        [Test]
        public void UnitySink_SynthClips_AreCreatedOnceAndCached()
        {
            var go = new GameObject("sfx-synth-test");
            try
            {
                var sink = new UnitySfxSink(go.AddComponent<AudioSource>(), name => null);
                AudioClip first = sink.SynthClip(SfxCue.Bell);
                Assert.IsNotNull(first);
                Assert.AreSame(first, sink.SynthClip(SfxCue.Bell));
                Assert.AreEqual(SfxSynth.SampleRate, first.frequency);
                Assert.AreEqual(1, first.channels);
                Assert.AreEqual((int)Math.Round(SfxSynth.DurationSec(SfxCue.Bell) * SfxSynth.SampleRate), first.samples);
                Assert.IsNull(sink.SynthClip(SfxCue.Tap));
                int count = sink.SynthClipCount;
                sink.SynthClip(SfxCue.Bell);
                Assert.AreEqual(count, sink.SynthClipCount);
                Assert.DoesNotThrow(() => sink.Play(SfxCue.Bell, 1.05f));
                Assert.DoesNotThrow(() => sink.SetLoop(SfxLoop.Music, true));
                Assert.DoesNotThrow(() => sink.SetLoop(SfxLoop.Music, false));
                Assert.DoesNotThrow(() => sink.SetVolumes(0.5f, 0.2f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        // ---- ліміти ----

        [Test]
        public void Limiter_SameCueWithin50ms_IsRejected_AfterThat_Allowed()
        {
            var limiter = new SfxLimiter();
            Assert.IsTrue(limiter.TryAcquire(SfxCue.Tap, 1.00, 0.05, 0.1, 3));
            Assert.IsFalse(limiter.TryAcquire(SfxCue.Tap, 1.03, 0.05, 0.1, 3));
            Assert.IsFalse(limiter.TryAcquire(SfxCue.Tap, 1.0499, 0.05, 0.1, 3));
            Assert.IsTrue(limiter.TryAcquire(SfxCue.Tap, 1.05, 0.05, 0.1, 3));
        }

        [Test]
        public void Limiter_DifferentCues_DoNotBlockEachOther()
        {
            var limiter = new SfxLimiter();
            Assert.IsTrue(limiter.TryAcquire(SfxCue.Tap, 1.0, 0.05, 0.1, 3));
            Assert.IsTrue(limiter.TryAcquire(SfxCue.Cash, 1.0, 0.05, 0.1, 3));
        }

        [Test]
        public void Limiter_AtMostThreeCopiesAtOnce()
        {
            var limiter = new SfxLimiter();
            Assert.IsTrue(limiter.TryAcquire(SfxCue.Cash, 0.00, 0.05, 1.0, 3));
            Assert.IsTrue(limiter.TryAcquire(SfxCue.Cash, 0.10, 0.05, 1.0, 3));
            Assert.IsTrue(limiter.TryAcquire(SfxCue.Cash, 0.20, 0.05, 1.0, 3));
            Assert.IsFalse(limiter.TryAcquire(SfxCue.Cash, 0.30, 0.05, 1.0, 3), "четверта копія");
            Assert.IsTrue(limiter.TryAcquire(SfxCue.Cash, 1.01, 0.05, 1.0, 3), "перша скінчилась");
        }

        [Test]
        public void Limiter_UnknownCueId_IsRejected()
        {
            Assert.IsFalse(new SfxLimiter().TryAcquire((SfxCue)99, 0.0, 0.05, 0.1, 3));
        }

        [Test]
        public void CoinTick_IntervalIsAtMost15PerSecond()
        {
            Assert.GreaterOrEqual(SfxCatalog.IntervalOf(SfxCue.CoinTick), 1.0 / 15.0 - 1e-9);
            Assert.AreEqual(0.05, SfxCatalog.IntervalOf(SfxCue.Tap), 1e-9);
        }

        [Test]
        public void Router_RateLimit_DropsRapidDuplicates()
        {
            var sink = new Rec();
            var clock = new Clock();
            var router = new SfxRouter(sink, clock.Read);
            Assert.IsTrue(router.Play(SfxCue.Tap));
            clock.Now = 0.02;
            Assert.IsFalse(router.Play(SfxCue.Tap));
            clock.Now = 0.06;
            Assert.IsTrue(router.Play(SfxCue.Tap));
            Assert.AreEqual(2, sink.Cues.Count);
        }

        [Test]
        public void Router_NoDuplicateInSameInstant_ForEveryCue()
        {
            foreach (SfxCue cue in Enum.GetValues(typeof(SfxCue)))
            {
                var sink = new Rec();
                var router = new SfxRouter(sink, () => 5.0);
                Assert.IsTrue(router.Play(cue), cue.ToString());
                Assert.IsFalse(router.Play(cue), cue + ": дубль у ту саму мить");
                Assert.AreEqual(1, sink.Cues.Count, cue.ToString());
            }
        }

        [Test]
        public void Router_UnknownCue_IsNotPlayed()
        {
            var sink = new Rec();
            Assert.IsFalse(new SfxRouter(sink, () => 0.0).Play((SfxCue)0));
            Assert.IsFalse(new SfxRouter(sink, () => 0.0).Play((SfxCue)77));
            Assert.AreEqual(0, sink.Cues.Count);
        }

        // ---- висота ----

        [Test]
        public void Jitter_IsWithinThreeToSixPercent_AndAlternates()
        {
            float previous = 1f;
            for (int i = 0; i < 200; i++)
            {
                float j = SfxPitch.Jitter(i);
                float d = Math.Abs(j - 1f);
                Assert.That(d, Is.InRange(SfxPitch.JitterMin - 0.0001f, SfxPitch.JitterMax + 0.0001f), "i=" + i);
                Assert.AreNotEqual(previous > 1f, j > 1f, "знак чергується, i=" + i);
                previous = j;
            }

            Assert.AreEqual(SfxPitch.Jitter(7), SfxPitch.Jitter(7), "детерміновано");
        }

        [Test]
        public void Steps_GrowByWholeToneEach_UpToFive()
        {
            Assert.AreEqual(1f, SfxPitch.Steps(0), 1e-6f);
            Assert.AreEqual(1f, SfxPitch.Steps(-3), 1e-6f);
            Assert.AreEqual(Math.Pow(2.0, 2.0 / 12.0), SfxPitch.Steps(1), 1e-5);
            for (int i = 1; i <= 8; i++)
            {
                Assert.GreaterOrEqual(SfxPitch.Steps(i), SfxPitch.Steps(i - 1));
            }

            Assert.AreEqual(SfxPitch.Steps(SfxPitch.MaxSteps), SfxPitch.Steps(99), 1e-6f);
        }

        [Test]
        public void Router_StarChime_GetsRisingPitch_WithoutJitter()
        {
            var sink = new Rec();
            var clock = new Clock();
            var router = new SfxRouter(sink, clock.Read);
            for (int i = 0; i < 3; i++)
            {
                clock.Now = i * 0.4;
                Assert.IsTrue(router.Play(SfxCue.StarChime, i));
            }

            Assert.AreEqual(1f, sink.Pitches[0], 1e-6f);
            Assert.Greater(sink.Pitches[1], sink.Pitches[0]);
            Assert.Greater(sink.Pitches[2], sink.Pitches[1]);
        }

        [Test]
        public void Router_RepeatedTap_PitchVariesWithinRange()
        {
            var sink = new Rec();
            var clock = new Clock();
            var router = new SfxRouter(sink, clock.Read);
            for (int i = 0; i < 10; i++)
            {
                clock.Now = i * 0.2;
                router.Play(SfxCue.Tap);
            }

            var distinct = new HashSet<float>(sink.Pitches);
            Assert.Greater(distinct.Count, 3);
            foreach (float p in sink.Pitches)
            {
                Assert.That(Math.Abs(p - 1f), Is.InRange(0.0299f, 0.0601f));
            }
        }

        // ---- мікс ----

        [Test]
        public void Router_Volumes_AreClampedAndSentToSink()
        {
            var sink = new Rec();
            var router = new SfxRouter(sink, () => 0.0);
            router.EffectsVolume = 2f;
            router.MusicVolume = -1f;
            Assert.AreEqual(1f, router.EffectsVolume);
            Assert.AreEqual(0f, router.MusicVolume);
            router.MusicVolume = 0.5f;
            Assert.AreEqual(1f, sink.Effects, 1e-6f);
            Assert.AreEqual(0.5f, sink.Music, 1e-6f);
            router.Enabled = false;
            Assert.AreEqual(0f, sink.Effects);
            Assert.AreEqual(0f, sink.Music);
        }

        [Test]
        public void Router_EffectsVolumeZero_SilencesEffects()
        {
            var sink = new Rec();
            var router = new SfxRouter(sink, () => 0.0) { EffectsVolume = 0f };
            Assert.IsFalse(router.Play(SfxCue.Cash));
            Assert.AreEqual(0, sink.Cues.Count);
        }

        [Test]
        public void Ducking_ImportantCue_LowersMusicAbout8dB_ThenRecovers()
        {
            var sink = new Rec();
            var clock = new Clock();
            var router = new SfxRouter(sink, clock.Read);
            router.SetLoop(SfxLoop.Music, true);
            router.Tick(0.016f);
            Assert.AreEqual(1f, sink.Music, 1e-6f);

            Assert.IsTrue(router.Play(SfxCue.StarChime));
            Assert.IsTrue(router.IsDucking);
            for (int i = 0; i < 60; i++)
            {
                router.Tick(0.016f);
            }

            Assert.AreEqual(SfxRouter.DuckGain, sink.Music, 1e-4f);
            double db = 20.0 * Math.Log10(SfxRouter.DuckGain);
            Assert.That(db, Is.InRange(-10.0, -6.0));

            clock.Now = SfxRouter.DuckHoldSec + 0.1;
            Assert.IsFalse(router.IsDucking);
            for (int i = 0; i < 120; i++)
            {
                router.Tick(0.016f);
            }

            Assert.AreEqual(1f, sink.Music, 1e-4f);
        }

        [Test]
        public void Ducking_OrdinaryCue_DoesNotDuck()
        {
            var sink = new Rec();
            var router = new SfxRouter(sink, () => 0.0);
            router.Play(SfxCue.Tap);
            router.Play(SfxCue.Cash);
            Assert.IsFalse(router.IsDucking);
        }

        [Test]
        public void Ducking_Explicit_ForPopupsAndTutorial()
        {
            var sink = new Rec();
            var clock = new Clock();
            var router = new SfxRouter(sink, clock.Read);
            router.Duck(2f);
            Assert.IsTrue(router.IsDucking);
            clock.Now = 2.5;
            Assert.IsFalse(router.IsDucking);
        }

        [Test]
        public void Ducking_SmoothNotInstant()
        {
            var sink = new Rec();
            var router = new SfxRouter(sink, () => 0.0);
            router.Duck(1f);
            router.Tick(0.016f);
            Assert.Less(sink.Music, 1f);
            Assert.Greater(sink.Music, SfxRouter.DuckGain);
        }

        [Test]
        public void Loops_OnlyWhenWantedAndNotPausedOrDisabled()
        {
            var sink = new Rec();
            var router = new SfxRouter(sink, () => 0.0);
            router.SetLoop(SfxLoop.Grill, true);
            router.SetLoop(SfxLoop.Grill, true);
            CollectionAssert.AreEqual(new[] { "Grill:on" }, sink.Loops, "без повторів");
            router.Paused = true;
            Assert.IsFalse(router.IsLoopPlaying(SfxLoop.Grill));
            Assert.IsTrue(router.IsLoopWanted(SfxLoop.Grill));
            router.Paused = false;
            Assert.IsTrue(router.IsLoopPlaying(SfxLoop.Grill));
            router.Enabled = false;
            Assert.IsFalse(router.IsLoopPlaying(SfxLoop.Grill));
            router.Enabled = true;
            router.MusicVolume = 0f;
            router.SetLoop(SfxLoop.Music, true);
            Assert.IsFalse(router.IsLoopPlaying(SfxLoop.Music), "музика при нульовій гучності");
            router.MusicVolume = 0.6f;
            Assert.IsTrue(router.IsLoopPlaying(SfxLoop.Music));
            router.StopLoops();
            Assert.IsFalse(router.IsLoopPlaying(SfxLoop.Grill));
            Assert.IsFalse(router.IsLoopPlaying(SfxLoop.Music));
        }

        // ---- події ----

        private static SfxDirector NewDirector(Rec sink, Func<SfxCounters> read, out SfxRouter router, Clock clock)
        {
            router = new SfxRouter(sink, clock.Read);
            return new SfxDirector(read, router);
        }

        [Test]
        public void Director_NewCustomer_RingsBell()
        {
            var sink = new Rec();
            var c = new SfxCounters();
            SfxRouter router;
            SfxDirector d = NewDirector(sink, () => c, out router, new Clock());
            c.Arrived = 1;
            d.Update(false);
            CollectionAssert.AreEqual(new[] { SfxCue.Bell }, sink.Cues);
            d.Update(false);
            Assert.AreEqual(1, sink.Cues.Count, "без повтору");
        }

        [Test]
        public void Director_BurnWarning_PlaysAlarmOncePerNewSlot()
        {
            var sink = new Rec();
            var clock = new Clock();
            var c = new SfxCounters();
            SfxRouter router;
            SfxDirector d = NewDirector(sink, () => c, out router, clock);
            c.WarnMask = 1;
            d.Update(false);
            d.Update(false);
            clock.Now = 1.0;
            c.WarnMask = 1;
            d.Update(false);
            CollectionAssert.AreEqual(new[] { SfxCue.BurntAlarm }, sink.Cues);
            clock.Now = 2.0;
            c.WarnMask = 3;
            d.Update(false);
            Assert.AreEqual(2, sink.Cues.Count);
            clock.Now = 3.0;
            c.WarnMask = 0;
            d.Update(false);
            Assert.AreEqual(2, sink.Cues.Count, "зняття попередження звуку не дає");
        }

        [Test]
        public void Director_TimerTicks_OnlyInLastTenSeconds_LouderInLastThree()
        {
            var sink = new Rec();
            var clock = new Clock();
            var c = new SfxCounters { RemainingSec = 20 };
            SfxRouter router;
            SfxDirector d = NewDirector(sink, () => c, out router, clock);
            for (long s = 19; s >= 0; s--)
            {
                clock.Now += 1.0;
                c.RemainingSec = s;
                d.Update(false);
            }

            var expected = new List<SfxCue>();
            for (long s = 10; s >= 1; s--)
            {
                expected.Add(s <= 3 ? SfxCue.TimerTickUrgent : SfxCue.TimerTick);
            }

            CollectionAssert.AreEqual(expected, sink.Cues);
        }

        [Test]
        public void Director_TimerTicks_SilentOnPause()
        {
            var sink = new Rec();
            var clock = new Clock();
            var c = new SfxCounters { RemainingSec = 9 };
            SfxRouter router;
            SfxDirector d = NewDirector(sink, () => c, out router, clock);
            clock.Now = 1.0;
            c.RemainingSec = 8;
            d.Update(true);
            Assert.AreEqual(0, sink.Cues.Count);
        }

        [Test]
        public void Director_GrillLoop_OnlyWhileMeatIsCooking()
        {
            var sink = new Rec();
            var c = new SfxCounters();
            SfxRouter router;
            SfxDirector d = NewDirector(sink, () => c, out router, new Clock());
            d.Update(false);
            Assert.IsFalse(router.IsLoopPlaying(SfxLoop.Grill));
            c.Cooking = 2;
            d.Update(false);
            Assert.IsTrue(router.IsLoopPlaying(SfxLoop.Grill));
            d.Update(true);
            Assert.IsFalse(router.IsLoopPlaying(SfxLoop.Grill), "пауза");
            d.Update(false);
            Assert.IsTrue(router.IsLoopPlaying(SfxLoop.Grill));
            c.Cooking = 0;
            d.Update(false);
            Assert.IsFalse(router.IsLoopPlaying(SfxLoop.Grill));
        }

        [Test]
        public void Director_Music_PlaysDuringShift_StopsOnPauseAndStop()
        {
            var sink = new Rec();
            var c = new SfxCounters();
            SfxRouter router;
            SfxDirector d = NewDirector(sink, () => c, out router, new Clock());
            d.Update(false);
            Assert.IsTrue(router.IsLoopPlaying(SfxLoop.Music));
            d.Update(true);
            Assert.IsFalse(router.IsLoopPlaying(SfxLoop.Music));
            d.Update(false);
            Assert.IsTrue(router.IsLoopPlaying(SfxLoop.Music));
            c.Cooking = 1;
            d.Update(false);
            d.Stop();
            d.Update(false);
            Assert.IsFalse(router.IsLoopPlaying(SfxLoop.Music));
            Assert.IsFalse(router.IsLoopPlaying(SfxLoop.Grill));
        }

        [Test]
        public void Counters_Equality_IncludesNewFields()
        {
            var a = new SfxCounters { Arrived = 1, RemainingSec = 5, Cooking = 2, WarnMask = 4 };
            var b = a;
            Assert.IsTrue(a.Equals(b));
            Assert.AreEqual(a.GetHashCode(), b.GetHashCode());
            b.WarnMask = 5;
            Assert.IsFalse(a.Equals(b));
            b = a;
            b.Cooking = 3;
            Assert.IsFalse(a.Equals(b));
        }

        [Test]
        public void Director_WithRealShift_ReadsRemainingAndArrivals()
        {
            var driver = new ShiftDriver(ConfigTestKit.Level(1), 20261115UL);
            driver.Begin();
            SfxCounters start = SfxDirector.Read(driver);
            Assert.Greater(start.RemainingSec, 0);
            for (int i = 0; i < 4000 && !driver.IsFinished; i++)
            {
                driver.Frame(0.25);
            }

            SfxCounters end = SfxDirector.Read(driver);
            Assert.Greater(end.Arrived, 0, "клієнти приходили");
            Assert.LessOrEqual(end.RemainingSec, start.RemainingSec);
        }

        [Test]
        public void Hub_PlayWithStep_ReachesSink()
        {
            var sink = new Rec();
            SfxHub.Install(sink, () => 0.0);
            Assert.IsTrue(SfxHub.Play(SfxCue.StarChime, 2));
            Assert.AreEqual(SfxPitch.Steps(2), sink.Pitches[0], 1e-6f);
        }
    }
}
