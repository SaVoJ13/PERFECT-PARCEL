using KebabStreet.Game;
using NUnit.Framework;
using UnityEngine;

namespace KebabStreet.Tests.EditMode
{
    /// <summary>S1-17: контраст статичних пар «текст/фон» (WCAG) і розрахунок безпечної зони.</summary>
    public sealed class ReadabilityTests
    {
        private const float NormalText = 4.5f;
        private const float LargeText = 3f;

        [Test]
        public void EveryStaticTextColorPairMeetsWcagContrast()
        {
            Assert.GreaterOrEqual(UiColors.Pairs.Length, 25, "пар замало: хтось викинув перевірку");
            foreach (ContrastPair pair in UiColors.Pairs)
            {
                float ratio = UiColors.Contrast(pair.Text, pair.Background);
                float need = pair.Large ? LargeText : NormalText;
                Assert.GreaterOrEqual(ratio, need, pair.Name + ": контраст " + ratio.ToString("0.00") + " < " + need);
            }
        }

        [Test]
        public void ContrastMathMatchesKnownValues()
        {
            Assert.AreEqual(21f, UiColors.Contrast(Color.white, Color.black), 0.01f);
            Assert.AreEqual(1f, UiColors.Contrast(Color.white, Color.white), 0.001f);
        }

        [Test]
        public void WhiteTextOnWhiteAwningStripeWithoutBackdropIsUnreadable()
        {
            // Відтворення скриншота S1-16: білий напис на білій смузі навісу.
            Assert.Less(UiColors.Contrast(Color.white, new Color(1f, 0.95f, 0.84f)), LargeText);
            Assert.GreaterOrEqual(UiColors.Contrast(Color.white, UiColors.HudBackdrop), NormalText);
        }

        [Test]
        public void GrillTextIsDarkOnLightStatesAndWhiteOnDarkStates()
        {
            Assert.AreEqual(UiColors.Ink, UiColors.GrillText(UiColors.GrillRaw));
            Assert.AreEqual(UiColors.Ink, UiColors.GrillText(UiColors.GrillCooking));
            Assert.AreEqual(UiColors.Ink, UiColors.GrillText(UiColors.GrillWarning));
            Assert.AreEqual(UiColors.White, UiColors.GrillText(UiColors.GrillDone));
            Assert.AreEqual(UiColors.White, UiColors.GrillText(UiColors.GrillBurnt));
        }

        [Test]
        public void SafeAreaInsetsAreScaledToLogicalPixels()
        {
            // 1440x3120, виріз 100 px зверху, жестова панель 60 px знизу, логічна висота 1920/… = 3120 / 1.625.
            float scale = SafeAreaMath.ScaleOf(3120f, 1920f);
            Assert.AreEqual(1.625f, scale, 0.001f);
            Assert.AreEqual(100f / 1.625f, SafeAreaMath.Top(3120f, 60f, 3120f - 60f - 100f, scale), 0.01f);
            Assert.AreEqual(60f / 1.625f, SafeAreaMath.Bottom(60f, scale), 0.01f);
        }

        [Test]
        public void SafeAreaIsZeroWithoutCutoutAndSurvivesUnknownLayout()
        {
            Assert.AreEqual(0f, SafeAreaMath.Top(2000f, 0f, 2000f, 1f), 0.001f);
            Assert.AreEqual(0f, SafeAreaMath.Bottom(0f, 1f), 0.001f);
            Assert.AreEqual(1f, SafeAreaMath.ScaleOf(2000f, 0f), 0.001f);
            Assert.AreEqual(50f, SafeAreaMath.Bottom(50f, 0f), 0.001f);
        }
    }
}
