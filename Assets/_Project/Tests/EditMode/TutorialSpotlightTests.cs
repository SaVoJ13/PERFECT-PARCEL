using KebabStreet.Core;
using KebabStreet.Game;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace KebabStreet.Tests.EditMode
{
    /// <summary>
    /// Q-05h (QB-TUT-01...06, QB-ALL-17): математика прожектора, руки й друку; плашка Алі друкує репліку, тап показує її
    /// повністю, прожектор і рука з'являються лише для кроків із дією, тап поза вирізом пульсує рамкою.
    /// </summary>
    public sealed class TutorialSpotlightTests
    {
        private const ulong Seed = 20284958UL;
        private static readonly ILocalizer Text = new PlaceholderLocalizer();
        private static readonly Rect Bounds = new Rect(0f, 150f, 1080f, 1700f);
        private static readonly Rect Target = new Rect(100f, 700f, 500f, 300f);

        [SetUp]
        public void SetUp()
        {
            UiMotion.Clock = null;
            UiMotion.Reduced = false;
        }

        [TearDown]
        public void TearDown()
        {
            UiMotion.Clock = null;
            UiMotion.Reduced = false;
        }

        private static ShiftScreen BuildWithTutorial(out TutorialDirector director)
        {
            var driver = new ShiftDriver(ConfigTestKit.Level(1), Seed);
            Assert.IsTrue(driver.Begin());
            director = TutorialDirector.Create(driver, ResourceConfigSource.LoadTutorial().ValueOrThrow(), new SaveData());
            return new ShiftScreen(new VisualElement(), driver, Text, () => { }, () => { }, () => { }, null, null, false, director);
        }

        [Test]
        public void Cutout_HasMargin_AndStaysInsideBounds()
        {
            Rect cut = OverlayMotion.Cutout(Target, Bounds, 1f);
            Assert.AreEqual(Target.xMin - OverlayMotion.CutoutMarginPx, cut.xMin, 0.01f);
            Assert.AreEqual(Target.yMax + OverlayMotion.CutoutMarginPx, cut.yMax, 0.01f);
            Assert.GreaterOrEqual(OverlayMotion.CutoutMarginPx, 12f, "запас 12-16 px");
            Assert.LessOrEqual(OverlayMotion.CutoutMarginPx, 16f);

            Rect wide = OverlayMotion.Cutout(new Rect(0f, 160f, 1080f, 400f), Bounds, 0f);
            Assert.GreaterOrEqual(wide.xMin, Bounds.xMin);
            Assert.GreaterOrEqual(wide.yMin, Bounds.yMin);
            Assert.LessOrEqual(wide.xMax, Bounds.xMax);
            Assert.LessOrEqual(wide.yMax, Bounds.yMax);
        }

        [Test]
        public void Cutout_OpensFromWideToTight_During200To300ms()
        {
            Assert.GreaterOrEqual(OverlayMotion.SpotlightSec, 0.2f);
            Assert.LessOrEqual(OverlayMotion.SpotlightSec, 0.3f);
            float wide = OverlayMotion.Cutout(Target, Bounds, OverlayMotion.SpotlightProgress(0f)).width;
            float mid = OverlayMotion.Cutout(Target, Bounds, OverlayMotion.SpotlightProgress(0.1f)).width;
            float tight = OverlayMotion.Cutout(Target, Bounds, OverlayMotion.SpotlightProgress(OverlayMotion.SpotlightSec)).width;
            Assert.Greater(wide, mid);
            Assert.Greater(mid, tight);
            Assert.AreEqual(Target.width + (2f * OverlayMotion.CutoutMarginPx), tight, 0.01f);
        }

        [Test]
        public void DimRects_CoverBoundsExceptCutout_WithoutOverlap()
        {
            Rect cut = OverlayMotion.Cutout(Target, Bounds, 1f);
            var rects = new Rect[4];
            OverlayMotion.DimRects(Bounds, cut, rects);

            float area = 0f;
            for (int i = 0; i < rects.Length; i++)
            {
                Assert.GreaterOrEqual(rects[i].width, 0f);
                Assert.GreaterOrEqual(rects[i].height, 0f);
                area += rects[i].width * rects[i].height;
                Assert.IsFalse(rects[i].Overlaps(cut), "затемнення не заходить у виріз " + i);
                for (int j = i + 1; j < rects.Length; j++)
                {
                    Assert.IsFalse(rects[i].Overlaps(rects[j]), "прямокутники " + i + " і " + j + " не перекриваються");
                }
            }

            Assert.AreEqual((Bounds.width * Bounds.height) - (cut.width * cut.height), area, 1f);
        }

        [Test]
        public void DimAlpha_IsBetween55And70Percent()
        {
            Assert.GreaterOrEqual(OverlayMotion.DimAlpha, 0.55f);
            Assert.LessOrEqual(OverlayMotion.DimAlpha, 0.70f);
        }

        [Test]
        public void Hand_TapsDownAndUp_WithinSpec_AndStandsStillWhenReduced()
        {
            Assert.GreaterOrEqual(OverlayMotion.HandCycleSec, 0.8f);
            Assert.LessOrEqual(OverlayMotion.HandCycleSec, 1.2f);
            float half = OverlayMotion.HandCycleSec / 2f;
            Assert.AreEqual(0f, OverlayMotion.HandOffset(0f), 0.01f);
            Assert.AreEqual(OverlayMotion.HandTravelPx, OverlayMotion.HandOffset(half), 0.01f);
            Assert.GreaterOrEqual(OverlayMotion.HandTravelPx, 10f);
            Assert.LessOrEqual(OverlayMotion.HandTravelPx, 16f);
            Assert.AreEqual(1f, OverlayMotion.HandScale(0f), 0.001f);
            Assert.AreEqual(0.9f, OverlayMotion.HandScale(half), 0.001f);
            Assert.AreEqual(OverlayMotion.HandOffset(0.2f), OverlayMotion.HandOffset(0.2f + OverlayMotion.HandCycleSec), 0.01f, "цикл повторюється");

            UiMotion.Reduced = true;
            Assert.AreEqual(0f, OverlayMotion.HandOffset(half));
            Assert.AreEqual(1f, OverlayMotion.HandScale(half));
            Assert.AreEqual(1f, OverlayMotion.FramePulse(half));
            Assert.AreEqual(1f, OverlayMotion.BlockedPulse(0.05f));
        }

        [Test]
        public void Typing_Is30To45CharsPerSecond_AndReducedShowsAll()
        {
            Assert.GreaterOrEqual(OverlayMotion.CharsPerSec, 30f);
            Assert.LessOrEqual(OverlayMotion.CharsPerSec, 45f);
            Assert.AreEqual(0, OverlayMotion.TypedChars(0f, 50));
            Assert.AreEqual((int)OverlayMotion.CharsPerSec, OverlayMotion.TypedChars(1f, 500));
            Assert.AreEqual(10, OverlayMotion.TypedChars(100f, 10), "не більше довжини тексту");
            Assert.AreEqual(0, OverlayMotion.TypedChars(1f, 0));

            UiMotion.Reduced = true;
            Assert.AreEqual(50, OverlayMotion.TypedChars(0f, 50));
        }

        [Test]
        public void Overlay_TypesLine_TapShowsItWhole()
        {
            float now = 10f;
            UiMotion.Clock = () => now;
            TutorialDirector director;
            ShiftScreen screen = BuildWithTutorial(out director);
            TutorialOverlay overlay = screen.Tutorial;
            screen.Refresh();

            string full = Text.Get(TextKeys.TutorialL01Step1);
            Assert.AreEqual(full, overlay.Text);
            Assert.AreEqual(string.Empty, overlay.ShownText, "у першому кадрі нічого не надруковано");

            now += 0.5f;
            screen.Refresh();
            Assert.Greater(overlay.ShownText.Length, 0);
            Assert.Less(overlay.ShownText.Length, full.Length);
            StringAssert.StartsWith(overlay.ShownText, full);

            overlay.CompleteTyping();
            Assert.AreEqual(full, overlay.ShownText, "тап - показати повністю");
            now += 5f;
            screen.Refresh();
            Assert.AreEqual(full, overlay.ShownText);
        }

        [Test]
        public void Overlay_ReducedMotion_ShowsTextAtOnce()
        {
            UiMotion.Reduced = true;
            TutorialDirector director;
            ShiftScreen screen = BuildWithTutorial(out director);
            Assert.AreEqual(Text.Get(TextKeys.TutorialL01Step1), screen.Tutorial.ShownText);
            Assert.AreEqual(1f, screen.Tutorial.PlaqueMotion.Opacity, 0.001f);
        }

        [Test]
        public void Overlay_PlaqueFadesAndRisesIn()
        {
            float now = 10f;
            UiMotion.Clock = () => now;
            TutorialDirector director;
            ShiftScreen screen = BuildWithTutorial(out director);
            screen.Refresh();
            Assert.AreEqual(0f, screen.Tutorial.PlaqueMotion.Opacity, 0.01f);
            Assert.AreEqual(OverlayMotion.PlaqueRisePx, screen.Tutorial.PlaqueMotion.TranslateY, 0.5f);

            now += 1f;
            screen.Refresh();
            Assert.AreEqual(1f, screen.Tutorial.PlaqueMotion.Opacity, 0.001f);
            Assert.AreEqual(0f, screen.Tutorial.PlaqueMotion.TranslateY, 0.001f);
        }

        [Test]
        public void Overlay_ActionStep_HasSpotlightAndHand_AndDimBlocksTapsOutsideCutout()
        {
            TutorialDirector director;
            ShiftScreen screen = BuildWithTutorial(out director);
            TutorialOverlay overlay = screen.Tutorial;
            Assert.IsTrue(overlay.SpotlightActive, "крок із дією (покласти м'ясо) має прожектор");
            Assert.AreEqual(TutorialZone.Grill, overlay.HighlightedZone);

            VisualElement hand = screen.Tutorial.HandMotion == null ? null : FindByName(screen, TutorialOverlay.HandName);
            Assert.IsNotNull(hand);
            Assert.AreEqual(DisplayStyle.Flex, hand.style.display.value);

            overlay.ApplySpotlight(Target, Bounds, 1f);
            Assert.IsTrue(overlay.DimVisible);
            Rect cut = overlay.Cutout;
            Assert.AreEqual(Target.width + (2f * OverlayMotion.CutoutMarginPx), cut.width, 0.01f);

            for (int i = 0; i < 4; i++)
            {
                VisualElement dimmer = FindByName(screen, TutorialOverlay.DimName + "-" + i);
                Assert.IsNotNull(dimmer, "затемнення " + i);
                Assert.AreEqual(PickingMode.Position, dimmer.pickingMode, "затемнення ловить дотики (QB-TUT-05)");
            }

            VisualElement dim = FindByName(screen, TutorialOverlay.DimName);
            Assert.AreEqual(PickingMode.Ignore, dim.pickingMode, "виріз не перекритий: дотик у ньому йде до гри");
        }

        [Test]
        public void Overlay_BlockedTap_PulsesFrameThenSettles()
        {
            float now = 10f;
            UiMotion.Clock = () => now;
            TutorialDirector director;
            ShiftScreen screen = BuildWithTutorial(out director);
            TutorialOverlay overlay = screen.Tutorial;

            overlay.ApplySpotlight(Target, Bounds, 5f);
            float calm = overlay.FrameMotion.ScaleX;
            Assert.Less(calm, 1f + OverlayMotion.FramePulseAmplitude + 0.001f);

            overlay.OnBlockedTap();
            now += OverlayMotion.BlockedPulseSec * 0.5f;
            overlay.ApplySpotlight(Target, Bounds, 5f);
            Assert.Greater(overlay.FrameMotion.ScaleX, 1.05f, "відгук на тап поза вирізом: рамка пульсує");

            now += 1f;
            overlay.ApplySpotlight(Target, Bounds, 5f);
            Assert.Less(overlay.FrameMotion.ScaleX, 1.05f);
        }

        [Test]
        public void Overlay_ReducedMotion_FramePulseAndHandStay()
        {
            UiMotion.Reduced = true;
            TutorialDirector director;
            ShiftScreen screen = BuildWithTutorial(out director);
            screen.Tutorial.OnBlockedTap();
            screen.Tutorial.ApplySpotlight(Target, Bounds, 0.3f);
            Assert.AreEqual(1f, screen.Tutorial.FrameMotion.ScaleX, 0.001f);
            Assert.AreEqual(1f, screen.Tutorial.HandMotion.ScaleX, 0.001f);
            Assert.AreEqual(0f, screen.Tutorial.HandMotion.TranslateY, 0.001f);
        }

        [Test]
        public void Overlay_StepEnds_DimAndHandDisappear()
        {
            TutorialDirector director;
            ShiftScreen screen = BuildWithTutorial(out director);
            screen.Tutorial.ApplySpotlight(Target, Bounds, 1f);
            Assert.IsTrue(screen.Tutorial.DimVisible);

            Assert.IsTrue(director.SkipAll());
            screen.Refresh();
            Assert.IsFalse(screen.Tutorial.DimVisible, "навчання завершено - затемнення знято");
            Assert.IsFalse(screen.Tutorial.SpotlightActive);
        }

        private static VisualElement FindByName(ShiftScreen screen, string name)
        {
            return screen.Pause.Root.parent.Q<VisualElement>(name);
        }
    }
}
