using KebabStreet.Game;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace KebabStreet.Tests.EditMode
{
    /// <summary>S1-18 (BUG-045): спрайти, підключені до екранів, завантажуються за іменами з реєстру; без спрайта — резервний вигляд.</summary>
    public sealed class ThemeSpritesTests
    {
        private static readonly string[] Connected =
        {
            ArtLibrary.HeartFull, ArtLibrary.HeartEmpty, ArtLibrary.Coin, ArtLibrary.StarFull, ArtLibrary.StarEmpty,
            ArtLibrary.Lock, ArtLibrary.Pause, ArtLibrary.Menu,
            "ing_pita", "ing_meat", "ing_salad", "ing_onion", "ing_sauce_garlic", "ing_sauce_hot",
        };

        [Test]
        public void ConnectedSprites_AreInRegistry_AndLoad()
        {
            foreach (string name in Connected)
            {
                CollectionAssert.Contains(ArtLibrary.AllNames, name);
                Assert.IsNotNull(ArtLibrary.Get(name), "спрайт не завантажується: " + name);
            }
        }

        [Test]
        public void AddIcon_WithSprite_AddsIconElement_WithoutSprite_AddsNothing()
        {
            var parent = new VisualElement();
            VisualElement icon = ArtLibrary.AddIcon(parent, ArtLibrary.Coin, 64f);
            Assert.IsNotNull(icon);
            Assert.AreEqual(1, parent.childCount);
            Assert.IsTrue(icon.ClassListContains("ks-icon"));

            Assert.IsNull(ArtLibrary.AddIcon(parent, "no_such_sprite", 64f));
            Assert.AreEqual(1, parent.childCount, "немає спрайта - нічого не додано");
        }

        [Test]
        public void ApplyFixed_UnknownSprite_ReturnsFalse()
        {
            Assert.IsFalse(ArtLibrary.ApplyFixed(new VisualElement(), "no_such_sprite", 48f));
        }
    }
}
