using KebabStreet.Game;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace KebabStreet.Tests.EditMode
{
    /// <summary>Переходи між екранами (Q-05e, QB-ALL-04): тривалість 200-300 мс, лише opacity/translate, ввід під час переходу ігнорується.</summary>
    public sealed class ScreenTransitionTests
    {
        private const float Eps = 0.0001f;
        private float _clock;

        [SetUp]
        public void SetUp()
        {
            _clock = 50f;
            UiMotion.Reduced = false;
            UiMotion.Clock = () => _clock;
        }

        [TearDown]
        public void TearDown()
        {
            UiMotion.Reduced = false;
            UiMotion.Clock = () => Time.realtimeSinceStartup;
        }

        private static ScreenFader NewFader(out VisualElement root, out VisualElement content)
        {
            root = new VisualElement();
            content = new VisualElement();
            root.Add(content);
            return new ScreenFader(root, content);
        }

        [Test]
        public void Durations_AreBetween200And300ms_AndSlideAtMost40px()
        {
            Assert.GreaterOrEqual(ScreenTransition.InSec, 0.2f);
            Assert.LessOrEqual(ScreenTransition.InSec, 0.3f);
            Assert.GreaterOrEqual(ScreenTransition.OutSec, 0.2f);
            Assert.LessOrEqual(ScreenTransition.OutSec, 0.3f);
            Assert.LessOrEqual(ScreenTransition.SlidePx, 40f);
        }

        [Test]
        public void In_OverlayFadesFromOneToZero_ContentSlidesToPlace_Monotonic()
        {
            Assert.AreEqual(1f, ScreenTransition.InOverlay(0f), Eps);
            Assert.AreEqual(0f, ScreenTransition.InOverlay(ScreenTransition.InSec), Eps);
            Assert.AreEqual(ScreenTransition.SlidePx, ScreenTransition.InOffset(0f), Eps);
            Assert.AreEqual(0f, ScreenTransition.InOffset(ScreenTransition.InSec), Eps);
            float last = 1f;
            for (float t = 0f; t <= ScreenTransition.InSec; t += 0.01f)
            {
                float o = ScreenTransition.InOverlay(t);
                Assert.LessOrEqual(o, last + Eps);
                last = o;
            }
        }

        [Test]
        public void Out_OverlayDarkensToOne_ContentSlidesAway()
        {
            Assert.AreEqual(0f, ScreenTransition.OutOverlay(0f), Eps);
            Assert.AreEqual(1f, ScreenTransition.OutOverlay(ScreenTransition.OutSec), Eps);
            Assert.AreEqual(0f, ScreenTransition.OutOffset(0f), Eps);
            Assert.AreEqual(-ScreenTransition.SlidePx, ScreenTransition.OutOffset(ScreenTransition.OutSec), Eps);
        }

        [Test]
        public void Reduced_TransitionsAreInstant()
        {
            UiMotion.Reduced = true;
            Assert.AreEqual(0f, ScreenTransition.InOverlay(0f), Eps);
            Assert.AreEqual(0f, ScreenTransition.InOffset(0f), Eps);
            Assert.AreEqual(1f, ScreenTransition.OutOverlay(0f), Eps);
            Assert.AreEqual(0f, ScreenTransition.OutDuration, Eps);
        }

        [Test]
        public void Fader_PlayIn_BlocksInputUntilDone_ThenReleases()
        {
            VisualElement root;
            VisualElement content;
            ScreenFader fader = NewFader(out root, out content);
            VisualElement overlay = root.Q(ScreenFader.OverlayName);
            Assert.IsNotNull(overlay);

            fader.PlayIn();
            Assert.IsTrue(fader.Busy);
            Assert.AreEqual(1f, fader.OverlayOpacity, Eps);
            Assert.AreEqual(PickingMode.Position, overlay.pickingMode, "під час появи тапи ігноруються");

            _clock += ScreenTransition.InSec / 2f;
            fader.Tick();
            Assert.Greater(fader.OverlayOpacity, 0f);
            Assert.Less(fader.OverlayOpacity, 1f);
            Assert.Greater(fader.ContentOffset, 0f);

            _clock += ScreenTransition.InSec;
            fader.Tick();
            Assert.IsFalse(fader.Busy);
            Assert.AreEqual(0f, fader.OverlayOpacity, Eps);
            Assert.AreEqual(0f, fader.ContentOffset, Eps);
            Assert.AreEqual(PickingMode.Ignore, overlay.pickingMode, "накладку знято: ввід проходить");
        }

        [Test]
        public void Fader_Leave_CallsLoadOnceAfterFade_AndIgnoresSecondTap()
        {
            VisualElement root;
            VisualElement content;
            ScreenFader fader = NewFader(out root, out content);
            fader.PlayIn();
            _clock += ScreenTransition.InSec + 0.01f;
            fader.Tick();

            int loads = 0;
            Assert.IsTrue(fader.Leave(() => loads++));
            Assert.IsFalse(fader.Leave(() => loads += 10), "повторний тап під час переходу ігнорується");
            Assert.AreEqual(0, loads, "сцена ще не завантажується");
            Assert.AreEqual(PickingMode.Position, root.Q(ScreenFader.OverlayName).pickingMode);

            _clock += ScreenTransition.OutSec / 2f;
            fader.Tick();
            Assert.AreEqual(0, loads);
            Assert.Greater(fader.OverlayOpacity, 0f);

            _clock += ScreenTransition.OutSec;
            fader.Tick();
            fader.Tick();
            Assert.AreEqual(1, loads, "завантаження рівно один раз, після затемнення");
            Assert.AreEqual(1f, fader.OverlayOpacity, Eps);
        }

        [Test]
        public void Fader_Leave_WhenReduced_LoadsImmediately()
        {
            UiMotion.Reduced = true;
            VisualElement root;
            VisualElement content;
            ScreenFader fader = NewFader(out root, out content);
            int loads = 0;
            fader.Leave(() => loads++);
            Assert.AreEqual(1, loads);
        }

        [Test]
        public void Fader_UsesRawClock_NotPausedGameTime()
        {
            VisualElement root;
            VisualElement content;
            ScreenFader fader = NewFader(out root, out content);
            UiMotion.SetPaused(true);
            fader.PlayIn();
            _clock += ScreenTransition.InSec + 0.01f;
            fader.Tick();
            Assert.IsFalse(fader.Busy, "пауза гри не зупиняє перехід");
            UiMotion.ResetPause();
        }

        [Test]
        public void Fader_OnlyTransformAndOpacityChange_NoRedundantWrites()
        {
            VisualElement root;
            VisualElement content;
            ScreenFader fader = NewFader(out root, out content);
            fader.PlayIn();
            _clock += ScreenTransition.InSec + 0.01f;
            fader.Tick();
            Assert.IsFalse(fader.Busy);
            float offset = fader.ContentOffset;
            _clock += 1f;
            fader.Tick();
            Assert.AreEqual(offset, fader.ContentOffset, Eps);
            Assert.AreEqual(Position.Absolute, root.Q(ScreenFader.OverlayName).style.position.value);
        }
    }
}
