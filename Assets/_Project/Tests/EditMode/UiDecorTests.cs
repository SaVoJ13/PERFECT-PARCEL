using System.Collections.Generic;
using KebabStreet.Core;
using KebabStreet.Game;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace KebabStreet.Tests.EditMode
{
    /// <summary>Q-05a: відгук натискання (QB-ALL-02), глибина кнопок/панелей (QB-ALL-06/07), фон (QB-ALL-08).</summary>
    public sealed class UiDecorTests
    {
        private const ulong Seed = 5051UL;

        [Test]
        public void ThemeDefinesSpringPressWithoutLayoutProperties()
        {
            string uss = System.IO.File.ReadAllText(
                System.IO.Path.Combine(Application.dataPath, "_Project/Resources/Theme/kebab_theme.uss"));
            Assert.IsNotNull(ArtLibraryThemeSheet(), "тема має завантажуватися");
            StringAssert.Contains(".ks-press", uss);
            StringAssert.Contains("transition-property: scale, translate;", uss);
            StringAssert.Contains("ease-out-back", uss);
            StringAssert.Contains("scale: 0.94 0.94;", uss);
            StringAssert.Contains("translate: 0 4px;", uss);
            int active = uss.IndexOf(".ks-press:active", System.StringComparison.Ordinal);
            Assert.GreaterOrEqual(active, 0);
            string block = uss.Substring(active, uss.IndexOf('}', active) - active);
            StringAssert.DoesNotContain("margin", block, "натискання не змінює розмітку (QB-ALL-16)");
            StringAssert.DoesNotContain("border", block, "натискання не змінює розмітку (QB-ALL-16)");
        }

        private static StyleSheet ArtLibraryThemeSheet()
        {
            return Resources.Load<StyleSheet>(ArtLibrary.ThemePath);
        }

        [Test]
        public void VignetteIsClearInCenterAndWithinQualityBarAtCorners()
        {
            Assert.AreEqual(0f, UiDecor.VignetteAlpha(0.5f, 0.5f), 0.0001f);
            float corner = UiDecor.VignetteAlpha(0f, 0f);
            Assert.AreEqual(UiDecor.VignetteMaxAlpha, corner, 0.0001f);
            Assert.GreaterOrEqual(corner, 0.15f, "QB-ALL-08: краї темніші на 15-25%");
            Assert.LessOrEqual(corner, 0.25f);
            Assert.Greater(UiDecor.VignetteAlpha(0f, 0.5f), UiDecor.VignetteAlpha(0.25f, 0.5f));
        }

        [Test]
        public void GradientIsLighterOnTopAndDarkerAtBottom()
        {
            Color top = UiDecor.GradientAt(0f);
            Color bottom = UiDecor.GradientAt(1f);
            Assert.Greater(top.r, bottom.r, "зверху світла плівка, знизу темна");
            Assert.AreNotEqual(top, bottom);
            Assert.AreEqual(UiDecor.GradientAt(0f), UiDecor.GradientAt(-3f));
        }

        [Test]
        public void ShadowIsDarkerThanFaceByFifteenPercent()
        {
            Color face = UiColors.Saffron;
            Color dark = UiDecor.Darken(face);
            Assert.AreEqual(face.r * 0.85f, dark.r, 0.0001f);
            Assert.AreEqual(face.a, dark.a, 0.0001f);
        }

        [Test]
        public void FactoryButtonHasPressClassShadowAndGloss_AddedOnce()
        {
            var parent = new VisualElement();
            Button button = UiFactory.AddButton(parent, "x", () => { });
            Assert.IsTrue(button.ClassListContains(UiFactory.PressClass));
            Assert.IsTrue(button.ClassListContains("ks-button"));
            VisualElement shadow = button.Q(UiDecor.ShadowName);
            VisualElement gloss = button.Q(UiDecor.GlossName);
            Assert.IsNotNull(shadow);
            Assert.IsNotNull(gloss);
            Assert.AreEqual(PickingMode.Ignore, shadow.pickingMode);
            Assert.AreEqual(PickingMode.Ignore, gloss.pickingMode);
            Assert.AreEqual(UiDecor.ButtonShadowOffset, -shadow.style.bottom.value.value, 0.001f);

            int children = button.childCount;
            UiDecor.AddButtonDepth(button, UiColors.Saffron);
            Assert.AreEqual(children, button.childCount, "повторний виклик нічого не додає");
        }

        [Test]
        public void PanelShadowOpacityAndOffsetMatchQualityBar()
        {
            var panel = new VisualElement();
            UiDecor.AddPanelShadow(panel);
            VisualElement shadow = panel.Q(UiDecor.ShadowName);
            Assert.IsNotNull(shadow);
            Assert.That(shadow.style.backgroundColor.value.a, Is.InRange(0.25f, 0.35f));
            Assert.That(UiDecor.PanelShadowOffset, Is.InRange(6f, 10f));
        }

        [Test]
        public void BackdropAddsGradientAndVignetteBehindContent()
        {
            var container = new VisualElement();
            container.Add(new Label("content"));
            UiDecor.AddBackdrop(container);
            Assert.AreEqual(UiDecor.GradientName, container[0].name);
            Assert.AreEqual(UiDecor.VignetteName, container[1].name);
            Assert.AreEqual(PickingMode.Ignore, container[0].pickingMode);
            Assert.AreEqual(PickingMode.Ignore, container[1].pickingMode);
            Assert.IsNotNull(container[0].style.backgroundImage.value.texture);
            Assert.IsNotNull(container[1].style.backgroundImage.value.texture);
        }

        [Test]
        public void ScreenGetsBackdrop()
        {
            var root = new VisualElement();
            VisualElement screen = UiFactory.Screen(root);
            Assert.IsNotNull(screen.Q(UiDecor.GradientName));
            Assert.IsNotNull(screen.Q(UiDecor.VignetteName));
        }

        [Test]
        public void EveryButtonOnShiftScreenHasSpringPress()
        {
            var driver = new ShiftDriver(ConfigTestKit.Level(2), Seed);
            Assert.IsTrue(driver.Begin());
            var container = new VisualElement();
            new ShiftScreen(container, driver, new PlaceholderLocalizer(), () => { }, () => { }, () => { });
            var buttons = new List<Button>();
            container.Query<Button>().ForEach(buttons.Add);
            Assert.Greater(buttons.Count, 5);
            foreach (Button b in buttons)
            {
                Assert.IsTrue(b.ClassListContains(UiFactory.PressClass), "кнопка без «пружинки»: " + b.name);
            }

            Assert.IsNotNull(container.Q("shift-screen").Q(UiDecor.VignetteName), "віньєтка на екрані зміни");
            Assert.IsNotNull(container.Q("shift-hud").Q(UiDecor.ShadowName), "тінь HUD-панелі");
        }
    }
}
