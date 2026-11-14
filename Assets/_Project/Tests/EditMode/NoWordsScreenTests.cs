using System.Collections.Generic;
using KebabStreet.Core;
using KebabStreet.Game;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace KebabStreet.Tests.EditMode
{
    /// <summary>
    /// Q-05b: ігровий екран читається значками, а не словами (QB-SHIFT-05/12/23/24/28/29/33, G-02/G-03/G-21).
    /// Текст лишається доступним (getter-и, tooltip), але на екрані першим є значок.
    /// </summary>
    public sealed class NoWordsScreenTests
    {
        private const ulong Seed = 20284958UL;
        private static readonly ILocalizer Text = new PlaceholderLocalizer();

        private static ShiftScreen Build(int level, out ShiftDriver driver, out VisualElement root)
        {
            driver = new ShiftDriver(ConfigTestKit.Level(level), Seed);
            Assert.IsTrue(driver.Begin());
            root = new VisualElement();
            return new ShiftScreen(root, driver, Text, () => { }, () => { }, () => { });
        }

        private static void Run(ShiftDriver driver, ShiftScreen screen, int ticks)
        {
            double tick = 1.0 / driver.Level.Shift.TicksPerSecond;
            for (int i = 0; i < ticks && !driver.IsFinished; i++)
            {
                driver.Frame(tick);
                screen.Refresh();
            }
        }

        [Test]
        public void PauseButton_IsSquareIconWithoutWord_AtLeast48dp()
        {
            ShiftScreen screen;
            ShiftDriver driver;
            VisualElement root;
            screen = Build(1, out driver, out root);
            Button pause = root.Q<Button>("pause-button");
            Assert.IsNotNull(pause);
            Assert.AreEqual(string.Empty, pause.text, "QB-SHIFT-05: без слова");
            Assert.AreEqual(Text.Get(TextKeys.ShiftPause), pause.tooltip, "слово лишається для a11y");
            Assert.GreaterOrEqual(pause.style.width.value.value, 144f, "48 dp = 144 px референсу");
            Assert.AreEqual(pause.style.width.value.value, pause.style.height.value.value, "квадратна кнопка-значок");
            VisualElement icon = pause.Q("pause-icon");
            Assert.IsNotNull(icon);
            Assert.AreEqual(DisplayStyle.Flex, icon.style.display.value, "значок паузи показано");
            Assert.IsNotNull(screen);
        }

        [Test]
        public void Hud_ShowsOnlyNumbers_FullTextKeptForAccessibility()
        {
            ShiftDriver driver;
            VisualElement root;
            ShiftScreen screen = Build(1, out driver, out root);
            Run(driver, screen, 30);
            Assert.AreEqual(
                string.Format(ShiftScreen.GoalShownFormat, 0, driver.Level.Goal.Target), screen.GoalShownText);
            Assert.AreEqual("0", screen.CoinsShownText);
            StringAssert.IsMatch("^[0-9/]+$", screen.GoalShownText);
            StringAssert.IsMatch("^[0-9]+$", screen.CoinsShownText);
            Assert.AreEqual(string.Format(Text.Get(TextKeys.ShiftHudCoins), 0), screen.CoinsText, "повний текст лишився");
        }

        [Test]
        public void GrillSlots_ShowIconInsteadOfStateWord_InEveryState()
        {
            ShiftDriver driver;
            VisualElement root;
            ShiftScreen screen = Build(1, out driver, out root);
            GrillPanel grill = screen.Grill;
            Button slot = root.Q<Button>("grill-slot-0");
            Assert.AreEqual(string.Empty, slot.text, "порожній слот: значок-підказка, без слова");
            Assert.IsTrue(grill.IsSlotIconShown(0));
            Assert.IsNotEmpty(grill.GetSlotText(0), "слово доступне для a11y");

            Assert.IsTrue(grill.TapSlot(0));
            double tick = 1.0 / driver.Level.Shift.TicksPerSecond;
            var states = new HashSet<GrillSlotDisplay>();
            for (int guard = 0; guard < 100000 && grill.GetShownView(0).Display != GrillSlotDisplay.Burnt; guard++)
            {
                driver.Frame(tick);
                screen.Refresh();
                states.Add(grill.GetShownView(0).Display);
                Assert.IsTrue(grill.IsSlotIconShown(0), "стан " + grill.GetShownView(0).Display + " показано значком");
                Assert.AreEqual(string.Empty, slot.text, "слова на слоті немає");
            }

            Assert.IsTrue(states.Contains(GrillSlotDisplay.Raw) && states.Contains(GrillSlotDisplay.Cooking));
        }

        [Test]
        public void Stations_AreLargeIconButtons_WithoutNameText()
        {
            ShiftDriver driver;
            VisualElement root;
            ShiftScreen screen = Build(4, out driver, out root);
            AssemblyPanel assembly = screen.Assembly;
            Assert.Greater(assembly.StationCount, 0);
            for (int i = 0; i < assembly.StationCount; i++)
            {
                Assert.IsTrue(assembly.IsStationIconShown(i), "станція " + i + " має значок");
                Assert.AreEqual(string.Empty, assembly.GetVisibleStationText(i));
                Assert.IsNotEmpty(assembly.GetStationText(i));
                Button button = root.Q<Button>("assembly-station-" + i);
                Assert.GreaterOrEqual(button.style.height.value.value, 176f, "станція велика");
                Assert.IsNotNull(button.Q(UiDecor.ShadowName), "QB-SHIFT-24: глибина станції");
                Assert.IsNotNull(button.Q(UiDecor.GlossName));
            }
        }

        [Test]
        public void BoardItemsAndQueue_ShowIcons_AndQueueTitleIsHidden()
        {
            ShiftDriver driver;
            VisualElement root;
            ShiftScreen screen = Build(1, out driver, out root);
            AssemblyPanel assembly = screen.Assembly;
            Assert.AreEqual(DisplayStyle.None, root.Q<Label>("assembly-queue-title").style.display.value, "«Далі:» не показано");

            Assert.IsTrue(assembly.TapStation(0));
            screen.Refresh();
            Assert.IsTrue(assembly.IsQueuedShown(0));
            Assert.IsTrue(assembly.IsQueuedIconShown(0), "черга - значок");
            Assert.AreEqual(string.Empty, root.Q<Button>("assembly-queued-0").text);
            Assert.IsNotEmpty(assembly.GetQueuedText(0));

            Run(driver, screen, 400);
            Assert.IsTrue(assembly.IsItemShown(0), "інгредієнт на дошці");
            Assert.IsTrue(assembly.IsItemIconShown(0));
            Assert.AreEqual(string.Empty, assembly.GetVisibleItemText(0));
            Assert.IsNotEmpty(assembly.GetItemText(0));
        }

        [Test]
        public void QueueIconName_MapsActionsToExistingSprites()
        {
            string[] ingredients = { "ing_pita", "ing_meat" };
            Assert.AreEqual("ing_meat", AssemblyPanel.QueueIconName(new ShiftAction(ShiftActionType.AddIngredient, 1), ingredients));
            Assert.IsNull(AssemblyPanel.QueueIconName(new ShiftAction(ShiftActionType.AddIngredient, 9), ingredients));
            Assert.AreEqual("ing_meat", AssemblyPanel.QueueIconName(new ShiftAction(ShiftActionType.PlaceMeat, 0), ingredients));
            Assert.AreEqual(ArtLibrary.MeatDone, AssemblyPanel.QueueIconName(new ShiftAction(ShiftActionType.TakeMeat, 0), ingredients));
            Assert.AreEqual(ArtLibrary.MeatBurnt, AssemblyPanel.QueueIconName(new ShiftAction(ShiftActionType.DiscardBurntMeat, 0), ingredients));
            Assert.AreEqual(ArtLibrary.Coin, AssemblyPanel.QueueIconName(new ShiftAction(ShiftActionType.ServeDish, 0), ingredients));
            Assert.IsNull(AssemblyPanel.QueueIconName(new ShiftAction(ShiftActionType.None, 0), ingredients));
            foreach (string name in new[] { "ing_meat", ArtLibrary.MeatDone, ArtLibrary.MeatBurnt, ArtLibrary.Coin })
            {
                Assert.IsNotNull(ArtLibrary.Get(name), "спрайт існує: " + name);
            }
        }

        [Test]
        public void ClearButton_IsTrashShape_WithoutWord()
        {
            ShiftDriver driver;
            VisualElement root;
            ShiftScreen screen = Build(4, out driver, out root);
            Button clear = root.Q<Button>("assembly-clear");
            Assert.IsNotNull(clear);
            Assert.AreEqual(string.Empty, clear.text, "QB-SHIFT-28: без слова");
            Assert.IsNotNull(clear.Q(UiDecor.TrashName), "смітник - форма");
            Assert.AreEqual(clear.tooltip, screen.Assembly.ClearText);
            Assert.IsNotEmpty(screen.Assembly.ClearText);
        }

        [Test]
        public void TrashGlyph_IsAddedOnce_AndIgnoresTouches()
        {
            var button = new Button();
            VisualElement first = UiDecor.AddTrashGlyph(button, 64f, Color.white, Color.black);
            VisualElement second = UiDecor.AddTrashGlyph(button, 64f, Color.white, Color.black);
            Assert.AreSame(first, second);
            Assert.AreEqual(1, button.Query(UiDecor.TrashName).ToList().Count);
            Assert.AreEqual(PickingMode.Ignore, first.pickingMode);
            Assert.IsNull(UiDecor.AddTrashGlyph(null, 64f, Color.white, Color.black));
        }

        [Test]
        public void OrderChips_AreSprites_NoWordsInBubble()
        {
            ShiftDriver driver;
            VisualElement root;
            ShiftScreen screen = Build(4, out driver, out root);
            Run(driver, screen, 600);
            CounterPanel counter = screen.Counter;
            int seen = 0;
            for (int slot = 0; slot < counter.SlotCount; slot++)
            {
                int chips = counter.GetOrderChipCount(slot);
                for (int i = 0; i < chips; i++)
                {
                    seen++;
                    Assert.IsTrue(counter.IsOrderChipIcon(slot, i), "значок замовлення без слова (G-21: немає літери під маркером)");
                    Assert.IsNotEmpty(counter.GetOrderChipText(slot, i), "слово є для a11y");
                }
            }

            Assert.Greater(seen, 0, "хоч один клієнт із замовленням");
        }

        [Test]
        public void GameplayZones_ShowNoIngredientOrStateWords()
        {
            ShiftDriver driver;
            VisualElement root;
            ShiftScreen screen = Build(4, out driver, out root);
            var forbidden = new HashSet<string>();
            BoardConfig config = driver.Simulation.Board.Config;
            for (int i = 0; i < config.IngredientCount; i++)
            {
                forbidden.Add(Text.Get(config.GetIngredient(i).NameKey));
            }

            foreach (string key in new[]
            {
                TextKeys.GrillSlotEmpty, TextKeys.GrillSlotRaw, TextKeys.GrillSlotCooking, TextKeys.GrillSlotDone,
                TextKeys.GrillSlotBurnt, TextKeys.GrillSlotBurning, TextKeys.AssemblyClear, TextKeys.AssemblyQueue,
                TextKeys.ShiftPause, TextKeys.ActionPlaceMeat, TextKeys.ActionTakeMeat, TextKeys.ActionDiscard,
                TextKeys.ActionServe,
            })
            {
                forbidden.Add(Text.Get(key));
            }

            Assert.IsTrue(screen.Grill.TapSlot(0));
            for (int round = 0; round < 12; round++)
            {
                if (round == 1)
                {
                    screen.Assembly.TapStation(0);
                }

                Run(driver, screen, 120);
                var shown = new List<string>();
                root.Query<TextElement>().ForEach(e =>
                {
                    bool visible = e.resolvedStyle.display != DisplayStyle.None && e.style.display.value != DisplayStyle.None;
                    if (visible && !string.IsNullOrEmpty(e.text))
                    {
                        shown.Add(e.text);
                    }
                });

                foreach (string text in shown)
                {
                    CollectionAssert.DoesNotContain(forbidden, text, "слово замість значка на екрані: " + text);
                }
            }
        }
    }
}
