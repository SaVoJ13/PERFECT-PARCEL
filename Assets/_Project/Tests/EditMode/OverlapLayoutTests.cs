using KebabStreet.Game;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace KebabStreet.Tests.EditMode
{
    /// <summary>S1-23 (BUG-051, BUG-053, BUG-054): іконки не лежать на тексті кнопок, замовлення — стовпчик, слухач звуку один.</summary>
    public sealed class OverlapLayoutTests
    {
        private static readonly ILocalizer Text = new PlaceholderLocalizer();

        [Test]
        public void AddLeadingIcons_PutsIconsAtEdge_AndReservesSymmetricPadding()
        {
            var button = new Button { text = "Рівень 1" };
            string[] names = { ArtLibrary.StarFull, ArtLibrary.StarFull, ArtLibrary.StarEmpty };
            VisualElement strip = ArtLibrary.AddLeadingIcons(button, names, 44f, 20f);

            Assert.IsNotNull(strip);
            Assert.AreEqual(3, strip.childCount);
            Assert.AreEqual(Position.Absolute, strip.style.position.value);
            Assert.AreEqual(132f + 40f + ArtLibrary.IconTextGap, button.style.paddingLeft.value.value, 0.01f);
            Assert.AreEqual(20f, button.style.paddingRight.value.value, 0.01f, "праворуч лише поле: напис не стискується");
            Assert.AreEqual("Рівень 1", button.text, "текст кнопки не змінено");
        }

        [Test]
        public void AddLeadingIcons_NoSprites_ChangesNothing()
        {
            var button = new Button();
            Assert.IsNull(ArtLibrary.AddLeadingIcons(button, new[] { "no_such_sprite" }, 44f, 20f));
            Assert.AreEqual(0, button.childCount);
        }

        [Test]
        public void AddTopIcon_ReservesTopSpace_NoSideSqueeze()
        {
            var button = new Button { text = "Часник" };
            Assert.IsNotNull(ArtLibrary.AddTopIcon(button, ArtLibrary.Coin, 48f, 8f));
            Assert.AreEqual(64f, button.style.paddingTop.value.value, 0.01f);
            Assert.AreEqual(StyleKeyword.Null, button.style.paddingLeft.keyword);
        }

        [Test]
        public void CounterOrder_IsColumn_WithoutWrap()
        {
            var driver = new ShiftDriver(ConfigTestKit.Level(3), 4410337UL);
            Assert.IsTrue(driver.Begin());
            var screen = new ShiftScreen(new VisualElement(), driver, Text, () => { }, () => { }, () => { });
            VisualElement order = screen.Counter.Root.Q("counter-order-0");

            Assert.IsNotNull(order);
            Assert.AreEqual(FlexDirection.Column, order.style.flexDirection.value);
            Assert.AreEqual(Wrap.NoWrap, order.style.flexWrap.value);
            for (int i = 0; i < screen.Counter.GetOrderChipCount(0); i++)
            {
                Assert.Greater(order[i].style.height.value.value, 0f, "значок має власну висоту, а не нульову");
            }
        }

        [Test]
        public void AttachListener_AddsExactlyOne_RepeatedCallDoesNotDuplicate()
        {
            var holder = new GameObject("test-sfx");
            try
            {
                if (Object.FindFirstObjectByType<AudioListener>() != null)
                {
                    Assert.Inconclusive("у відкритій сцені вже є слухач");
                }

                Assert.AreEqual(1, UnitySfxSink.AttachListener(holder));
                Assert.AreEqual(1, UnitySfxSink.AttachListener(holder));
                Assert.AreEqual(1, holder.GetComponents<AudioListener>().Length);
                Assert.AreEqual(0, UnitySfxSink.AttachListener(null));
            }
            finally
            {
                Object.DestroyImmediate(holder);
            }
        }
    }
}
