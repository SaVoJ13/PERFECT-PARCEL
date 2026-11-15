using System.Collections.Generic;
using KebabStreet.Game;
using NUnit.Framework;

namespace KebabStreet.Tests.EditMode
{
    /// <summary>Q-05i (QB-ALL-14): вібрація - перемикач, ліміти частоти, пауза, відповідність подій силі, константи Android.</summary>
    public sealed class HapticsTests
    {
        private sealed class FakeHaptics : IHapticsService
        {
            public readonly List<HapticKind> Calls = new List<HapticKind>();

            public void Perform(HapticKind kind)
            {
                Calls.Add(kind);
            }
        }

        private sealed class FakeSink : ISfxSink
        {
            public int Played;

            public void Play(SfxCue cue, float pitch)
            {
                Played++;
            }

            public void SetLoop(SfxLoop loop, bool on)
            {
            }

            public void SetVolumes(float effects, float music)
            {
            }
        }

        private double _now;
        private bool _enabled;
        private FakeHaptics _service;
        private HapticsRouter _router;

        [SetUp]
        public void SetUp()
        {
            _now = 100.0;
            _enabled = true;
            _service = new FakeHaptics();
            _router = new HapticsRouter(_service, () => _enabled, () => _now);
            HapticsHub.Install(null);
        }

        [TearDown]
        public void TearDown()
        {
            HapticsHub.Install(null);
            SfxHub.Install(null);
        }

        [Test]
        public void KindOf_LightAndStrongEvents_NoneForNoise()
        {
            Assert.AreEqual(HapticKind.Light, HapticsCatalog.KindOf(SfxCue.Tap));
            Assert.AreEqual(HapticKind.Light, HapticsCatalog.KindOf(SfxCue.Cash));
            Assert.AreEqual(HapticKind.Strong, HapticsCatalog.KindOf(SfxCue.Burnt));
            Assert.AreEqual(HapticKind.Strong, HapticsCatalog.KindOf(SfxCue.Sad));
            Assert.AreEqual(HapticKind.Strong, HapticsCatalog.KindOf(SfxCue.StarChime));
            Assert.AreEqual(HapticKind.None, HapticsCatalog.KindOf(SfxCue.CoinTick));
            Assert.AreEqual(HapticKind.None, HapticsCatalog.KindOf(SfxCue.TimerTick));
            Assert.AreEqual(HapticKind.None, HapticsCatalog.KindOf(SfxCue.TimerTickUrgent));
            Assert.AreEqual(HapticKind.None, HapticsCatalog.KindOf(SfxCue.Bell));
        }

        [Test]
        public void Cue_Enabled_CallsServiceWithKind()
        {
            Assert.IsTrue(_router.Cue(SfxCue.Tap));
            _now += 1.0;
            Assert.IsTrue(_router.Cue(SfxCue.Burnt));
            CollectionAssert.AreEqual(new[] { HapticKind.Light, HapticKind.Strong }, _service.Calls);
        }

        [Test]
        public void Cue_Disabled_NoCalls()
        {
            _enabled = false;
            Assert.IsFalse(_router.Cue(SfxCue.Tap));
            Assert.IsFalse(_router.Cue(SfxCue.Burnt));
            Assert.IsFalse(_router.Confirm());
            Assert.AreEqual(0, _service.Calls.Count);
        }

        [Test]
        public void Cue_NoService_NoCalls()
        {
            var router = new HapticsRouter(null, () => true, () => _now);
            Assert.IsFalse(router.Cue(SfxCue.Tap));
            Assert.IsFalse(HapticsHub.Router.Cue(SfxCue.Tap));
        }

        [Test]
        public void Cue_NoneKind_NoCalls()
        {
            Assert.IsFalse(_router.Cue(SfxCue.CoinTick));
            Assert.IsFalse(_router.Cue(SfxCue.Bell));
            Assert.AreEqual(0, _service.Calls.Count);
        }

        [Test]
        public void Cue_RealtimeEvents_AtMostTwoPerSecond()
        {
            int fired = 0;
            for (int i = 0; i < 100; i++)
            {
                if (_router.Cue(SfxCue.Cash))
                {
                    fired++;
                }

                _now += 0.1; // 10 подій на секунду, 10 секунд
            }

            Assert.LessOrEqual(fired, 20, "не більше 2 на секунду");
            Assert.GreaterOrEqual(fired, 15);
        }

        [Test]
        public void Cue_UserTaps_NotThrottledAsRealtime()
        {
            Assert.IsTrue(_router.Cue(SfxCue.Tap));
            _now += 0.1;
            Assert.IsTrue(_router.Cue(SfxCue.Tap));
            _now += 0.02;
            Assert.IsFalse(_router.Cue(SfxCue.Tap), "захист від дробу");
        }

        [Test]
        public void Cue_SameInstant_StrongBeatsLightButNotViceVersa()
        {
            Assert.IsTrue(_router.Cue(SfxCue.Cash));
            Assert.IsTrue(_router.Cue(SfxCue.Burnt), "сильніший витісняє слабший");
            Assert.IsFalse(_router.Cue(SfxCue.Tap), "слабший після сильного відсікається");
            CollectionAssert.AreEqual(new[] { HapticKind.Light, HapticKind.Strong }, _service.Calls);
        }

        [Test]
        public void Confirm_Enabled_FiresLight()
        {
            Assert.IsTrue(_router.Confirm());
            CollectionAssert.AreEqual(new[] { HapticKind.Light }, _service.Calls);
        }

        [Test]
        public void SfxRouter_Paused_NoHaptics()
        {
            var sfx = new SfxRouter(new FakeSink(), () => _now) { Haptics = _router };
            sfx.Paused = true;
            sfx.Play(SfxCue.Tap);
            sfx.Play(SfxCue.Burnt);
            Assert.AreEqual(0, _service.Calls.Count);
            sfx.Paused = false;
            sfx.Play(SfxCue.Tap);
            Assert.AreEqual(1, _service.Calls.Count);
        }

        [Test]
        public void SfxRouter_SoundsOff_StillVibrates()
        {
            var sink = new FakeSink();
            var sfx = new SfxRouter(sink, () => _now) { Haptics = _router, EffectsVolume = 0f };
            sfx.Play(SfxCue.Tap);
            Assert.AreEqual(0, sink.Played, "звук вимкнено");
            Assert.AreEqual(1, _service.Calls.Count, "вібрація від звуку не залежить");
        }

        [Test]
        public void SfxHub_UsesInstalledHapticsHub()
        {
            var service = new FakeHaptics();
            HapticsHub.Install(service, () => true, () => _now);
            SfxHub.Install(new FakeSink(), () => _now);
            SfxHub.Play(SfxCue.Tap);
            Assert.AreEqual(1, service.Calls.Count);
        }

        [Test]
        public void HapticsHub_DefaultEnabledFollowsSettings()
        {
            var settings = new GameSettings(new MemorySettingsStore());
            GameSettings.Install(settings);
            try
            {
                var service = new FakeHaptics();
                HapticsHub.Install(service, null, () => _now);
                Assert.IsTrue(HapticsHub.Router.Cue(SfxCue.Tap));
                settings.SetHaptics(false);
                _now += 1.0;
                Assert.IsFalse(HapticsHub.Router.Cue(SfxCue.Tap));
                Assert.AreEqual(1, service.Calls.Count);
            }
            finally
            {
                GameSettings.Install(null);
            }
        }

        [Test]
        public void AndroidConstants_MatchSystemValues()
        {
            Assert.AreEqual(1, AndroidHapticsService.ConstantFor(HapticKind.Light, 25));
            Assert.AreEqual(0, AndroidHapticsService.ConstantFor(HapticKind.Strong, 29));
            Assert.AreEqual(16, AndroidHapticsService.ConstantFor(HapticKind.Strong, 30));
            Assert.AreEqual(16, AndroidHapticsService.ConstantFor(HapticKind.Strong, 36));
        }

        [Test]
        public void Create_InEditor_IsStub()
        {
            Assert.IsInstanceOf<NullHapticsService>(AndroidHapticsService.Create());
        }
    }
}
