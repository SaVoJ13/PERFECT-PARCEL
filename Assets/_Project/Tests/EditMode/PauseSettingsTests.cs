using System.Collections.Generic;
using System.IO;
using KebabStreet.Core;
using KebabStreet.Game;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace KebabStreet.Tests.EditMode
{
    /// <summary>
    /// Q-05h (QB-PAUSE-02/04/06, QB-TUT-06, QB-ALL-17, QB-ALL-11): налаштування в PlayerPrefs-подібному сховищі (не в схемі
    /// збереження гри), підключення до SfxRouter і UiMotion.Reduced; панель паузи: затемнення, кнопки, підтвердження.
    /// </summary>
    public sealed class PauseSettingsTests
    {
        private const ulong Seed = 20284958UL;
        private static readonly ILocalizer Text = new PlaceholderLocalizer();

        private sealed class FakeSink : ISfxSink
        {
            public readonly List<SfxCue> Played = new List<SfxCue>();
            public float Effects = -1f;
            public float Music = -1f;
            public readonly bool[] Loops = new bool[3];

            public void Play(SfxCue cue, float pitch)
            {
                Played.Add(cue);
            }

            public void SetLoop(SfxLoop loop, bool on)
            {
                Loops[(int)loop] = on;
            }

            public void SetVolumes(float effects, float music)
            {
                Effects = effects;
                Music = music;
            }
        }

        private sealed class Calls
        {
            public int Resume;
            public int Restart;
            public int Menu;
            public int TutorialChanged;
        }

        [SetUp]
        public void SetUp()
        {
            UiMotion.Clock = null;
            UiMotion.Reduced = false;
            SfxHub.Install(null);
        }

        [TearDown]
        public void TearDown()
        {
            UiMotion.Clock = null;
            UiMotion.Reduced = false;
            SfxHub.Install(null);
            GameSettings.Install(null);
        }

        private static ShiftDriver Started(int level = 1)
        {
            var driver = new ShiftDriver(ConfigTestKit.Level(level), Seed);
            Assert.IsTrue(driver.Begin());
            return driver;
        }

        private static ShiftScreen Build(ShiftDriver driver, GameSettings settings, Calls calls, TutorialDirector tutorial = null, bool restart = true)
        {
            return new ShiftScreen(
                new VisualElement(),
                driver,
                Text,
                () => { },
                () => calls.Resume++,
                () => calls.Menu++,
                null,
                null,
                false,
                tutorial,
                () => calls.TutorialChanged++,
                restart ? (System.Action)(() => calls.Restart++) : null,
                settings);
        }

        private static void Pause(ShiftDriver driver, ShiftScreen screen)
        {
            driver.SetPaused(true);
            screen.Refresh();
        }

        [Test]
        public void Settings_DefaultToOn_ExceptReducedMotion()
        {
            var settings = new GameSettings(new MemorySettingsStore());
            Assert.IsTrue(settings.MusicOn);
            Assert.IsTrue(settings.SoundsOn);
            Assert.IsTrue(settings.HapticsEnabled);
            Assert.IsFalse(settings.ReducedMotion);
        }

        [Test]
        public void Settings_PersistInStore_AndSurviveReload_AndFlushEachChange()
        {
            var store = new MemorySettingsStore();
            var settings = new GameSettings(store);
            settings.SetMusic(false);
            settings.SetSounds(false);
            settings.SetHaptics(false);
            settings.SetReducedMotion(true);
            Assert.AreEqual(4, store.Flushes, "кожна зміна скидається на диск");

            var reloaded = new GameSettings(store);
            Assert.IsFalse(reloaded.MusicOn);
            Assert.IsFalse(reloaded.SoundsOn);
            Assert.IsFalse(reloaded.HapticsEnabled);
            Assert.IsTrue(reloaded.ReducedMotion);
        }

        [Test]
        public void Settings_KeysAreSeparateFromSaveSchema()
        {
            StringAssert.StartsWith("ks.settings.", GameSettings.MusicKey);
            StringAssert.StartsWith("ks.settings.", GameSettings.SoundsKey);
            StringAssert.StartsWith("ks.settings.", GameSettings.HapticsKey);
            StringAssert.StartsWith("ks.settings.", GameSettings.ReducedKey);
            Assert.IsNull(typeof(SaveData).GetProperty("MusicOn"), "налаштування не потрапляють у SaveData");
            Assert.IsNull(typeof(SaveData).GetProperty("HapticsEnabled"));
        }

        [Test]
        public void ApplyTo_MusicOff_SilencesMusicBusAndLoop_SoundsStillPlay()
        {
            var sink = new FakeSink();
            double clock = 0.0;
            var router = new SfxRouter(sink, () => clock);
            router.SetLoop(SfxLoop.Music, true);
            Assert.IsTrue(sink.Loops[(int)SfxLoop.Music]);

            var settings = new GameSettings(new MemorySettingsStore());
            settings.SetMusic(false);
            settings.ApplyTo(router);

            Assert.AreEqual(0f, router.MusicVolume);
            Assert.AreEqual(1f, router.EffectsVolume);
            Assert.IsFalse(sink.Loops[(int)SfxLoop.Music], "музика вимкнена");
            Assert.IsTrue(router.Play(SfxCue.Tap), "звуки лишились");
        }

        [Test]
        public void ApplyTo_SoundsOff_BlocksEffects_MusicStillOn()
        {
            var sink = new FakeSink();
            var router = new SfxRouter(sink, () => 0.0);
            var settings = new GameSettings(new MemorySettingsStore());
            settings.SetSounds(false);
            settings.ApplyTo(router);

            Assert.AreEqual(0f, router.EffectsVolume);
            Assert.AreEqual(1f, router.MusicVolume);
            Assert.IsFalse(router.Play(SfxCue.Tap));
            Assert.IsEmpty(sink.Played);
        }

        [Test]
        public void ApplyTo_ReducedMotion_SetsUiMotionFlag_AndClearsIt()
        {
            var settings = new GameSettings(new MemorySettingsStore());
            settings.SetReducedMotion(true);
            settings.ApplyTo(null);
            Assert.IsTrue(UiMotion.Reduced);
            Assert.AreEqual(1f, UiMotion.Breath(0.5f, 0f), "у «менше анімацій» дихання вимкнене");

            settings.SetReducedMotion(false);
            settings.ApplyTo(null);
            Assert.IsFalse(UiMotion.Reduced);
        }

        [Test]
        public void PausePanel_Hidden_UntilPause_AndShownImmediately()
        {
            ShiftDriver driver = Started();
            ShiftScreen screen = Build(driver, new GameSettings(new MemorySettingsStore()), new Calls());
            Assert.IsFalse(screen.Pause.Visible);
            Assert.AreEqual(DisplayStyle.None, screen.Pause.Root.style.display.value);

            Pause(driver, screen);
            Assert.IsTrue(screen.Pause.Visible, "стан паузи фіксується в тому ж кадрі (QB-PAUSE-01)");
            Assert.IsTrue(screen.PausePanelVisible);
            Assert.AreEqual(DisplayStyle.Flex, screen.Pause.Root.style.display.value);
        }

        [Test]
        public void PausePanel_DimsGameLayer_Between55And65Percent()
        {
            ShiftScreen screen = Build(Started(), new GameSettings(new MemorySettingsStore()), new Calls());
            float alpha = screen.Pause.Root.style.backgroundColor.value.a;
            Assert.GreaterOrEqual(alpha, 0.55f);
            Assert.LessOrEqual(alpha, 0.65f);
            Assert.AreEqual(OverlayMotion.PauseDimAlpha, alpha, 0.001f);
        }

        [Test]
        public void PausePanel_CardAppears_WithRealClock_NotGameClock()
        {
            float now = 100f;
            UiMotion.Clock = () => now;
            ShiftDriver driver = Started();
            ShiftScreen screen = Build(driver, new GameSettings(new MemorySettingsStore()), new Calls());
            Pause(driver, screen);
            Assert.AreEqual(0f, screen.Pause.CardMotion.Opacity, 0.01f, "у момент паузи картка ще прозора");

            now += OverlayMotion.PausePopSec * 0.5f;
            screen.Refresh();
            Assert.Greater(screen.Pause.CardMotion.Opacity, 0.2f, "хоч ігровий час стоїть, картка з'являється за справжнім часом");
            Assert.Less(screen.Pause.CardMotion.Opacity, 1f);

            now += 1f;
            screen.Refresh();
            Assert.AreEqual(1f, screen.Pause.CardMotion.Opacity, 0.001f);
            Assert.AreEqual(1f, screen.Pause.CardMotion.ScaleX, 0.001f);
        }

        [Test]
        public void PausePanel_ReducedMotion_ShowsCardWithoutAnimation()
        {
            UiMotion.Reduced = true;
            ShiftDriver driver = Started();
            ShiftScreen screen = Build(driver, new GameSettings(new MemorySettingsStore()), new Calls());
            Pause(driver, screen);
            Assert.AreEqual(1f, screen.Pause.CardMotion.Opacity, 0.001f);
            Assert.AreEqual(1f, screen.Pause.CardMotion.ScaleX, 0.001f);
        }

        [Test]
        public void PausePanel_ResumeIsLargestButton_AndIsLast()
        {
            ShiftScreen screen = Build(Started(), new GameSettings(new MemorySettingsStore()), new Calls());
            VisualElement card = screen.Pause.Card;
            Button resume = card.Q<Button>("pause-resume");
            Assert.IsNotNull(resume);
            float resumeArea = resume.style.height.value.value * resume.style.fontSize.value.value;
            foreach (string name in new[] { "pause-restart", "pause-menu", "pause-music", "pause-sounds", "pause-haptics", "pause-reduced" })
            {
                Button other = card.Q<Button>(name);
                Assert.IsNotNull(other, name);
                Assert.Less(other.style.height.value.value * Mathf.Max(1f, other.style.fontSize.value.value), resumeArea, name + " менша за «Продовжити»");
            }

            Assert.AreSame(resume, card[card.childCount - 1], "«Продовжити» - останній елемент картки (внизу)");
        }

        [Test]
        public void PausePanel_Resume_CallsCallback_WithoutConfirmation()
        {
            var calls = new Calls();
            ShiftScreen screen = Build(Started(), new GameSettings(new MemorySettingsStore()), calls);
            Assert.IsTrue(screen.Pause.Press(PauseControl.Resume));
            Assert.AreEqual(1, calls.Resume);
        }

        [TestCase(PauseControl.Restart, PauseConfirm.Restart)]
        [TestCase(PauseControl.Menu, PauseConfirm.Menu)]
        public void PausePanel_RestartAndMenu_NeedConfirmation_NoDoesNothing(PauseControl button, PauseConfirm expected)
        {
            var calls = new Calls();
            ShiftScreen screen = Build(Started(), new GameSettings(new MemorySettingsStore()), calls);
            Assert.IsTrue(screen.Pause.Press(button));
            Assert.AreEqual(expected, screen.Pause.Confirm);
            Assert.AreEqual(0, calls.Restart + calls.Menu, "до підтвердження нічого не відбулось");
            Assert.IsNotEmpty(screen.Pause.ConfirmText);

            Assert.IsTrue(screen.Pause.Press(PauseControl.ConfirmNo));
            Assert.AreEqual(PauseConfirm.None, screen.Pause.Confirm);
            Assert.AreEqual(0, calls.Restart + calls.Menu, "«Скасувати» нічого не робить");
        }

        [Test]
        public void PausePanel_ConfirmYes_RunsRestart_OrMenu_Once()
        {
            var calls = new Calls();
            ShiftScreen screen = Build(Started(), new GameSettings(new MemorySettingsStore()), calls);
            screen.Pause.Press(PauseControl.Restart);
            screen.Pause.Press(PauseControl.ConfirmYes);
            Assert.AreEqual(1, calls.Restart);
            Assert.AreEqual(0, calls.Menu);
            Assert.IsFalse(screen.Pause.Press(PauseControl.ConfirmYes), "підтвердження вже закрите");
            Assert.AreEqual(1, calls.Restart);

            screen.Pause.Press(PauseControl.Menu);
            screen.Pause.Press(PauseControl.ConfirmYes);
            Assert.AreEqual(1, calls.Menu);
        }

        [Test]
        public void PausePanel_RestartHidden_WhenNoRestartHandler()
        {
            ShiftScreen screen = Build(Started(), new GameSettings(new MemorySettingsStore()), new Calls(), null, false);
            Assert.IsFalse(screen.Pause.RestartVisible);
            Assert.IsFalse(screen.Pause.Press(PauseControl.Restart));
        }

        [Test]
        public void PausePanel_ReopeningPause_ClosesOldConfirmation()
        {
            ShiftDriver driver = Started();
            ShiftScreen screen = Build(driver, new GameSettings(new MemorySettingsStore()), new Calls());
            Pause(driver, screen);
            screen.Pause.Press(PauseControl.Menu);
            driver.SetPaused(false);
            screen.Refresh();
            Pause(driver, screen);
            Assert.AreEqual(PauseConfirm.None, screen.Pause.Confirm);
        }

        [Test]
        public void PausePanel_Toggles_SaveToStore_AndApplyToSoundAndMotion()
        {
            var sink = new FakeSink();
            SfxRouter router = SfxHub.Install(sink, () => 0.0);
            var store = new MemorySettingsStore();
            var settings = new GameSettings(store);
            ShiftScreen screen = Build(Started(), settings, new Calls());

            Assert.IsTrue(screen.Pause.IsOn(PauseControl.Music));
            screen.Pause.Press(PauseControl.Music);
            Assert.IsFalse(screen.Pause.IsOn(PauseControl.Music));
            Assert.AreEqual(0, store.GetInt(GameSettings.MusicKey, 1), "записано в сховище");
            Assert.AreEqual(0f, router.MusicVolume);

            screen.Pause.Press(PauseControl.Sounds);
            Assert.AreEqual(0, store.GetInt(GameSettings.SoundsKey, 1));
            Assert.AreEqual(0f, router.EffectsVolume);

            screen.Pause.Press(PauseControl.Haptics);
            Assert.IsFalse(settings.HapticsEnabled);
            Assert.AreEqual(0, store.GetInt(GameSettings.HapticsKey, 1));

            screen.Pause.Press(PauseControl.ReducedMotion);
            Assert.IsTrue(UiMotion.Reduced, "QB-ALL-17: перемикач вмикає «менше анімацій»");
            Assert.AreEqual(1, store.GetInt(GameSettings.ReducedKey, 0));

            screen.Pause.Press(PauseControl.Music);
            Assert.IsTrue(screen.Pause.IsOn(PauseControl.Music));
            Assert.AreEqual(1f, router.MusicVolume);
        }

        [Test]
        public void PausePanel_ToggleTrackColor_ShowsState()
        {
            ShiftScreen screen = Build(Started(), new GameSettings(new MemorySettingsStore()), new Calls());
            Color on = screen.Pause.TrackColorOf(PauseControl.Music);
            screen.Pause.Press(PauseControl.Music);
            Color off = screen.Pause.TrackColorOf(PauseControl.Music);
            Assert.AreNotEqual(on, off);
            Assert.IsNotEmpty(screen.Pause.ToggleLabel(PauseControl.Music), "є підпис для доступності");
        }

        [Test]
        public void PausePanel_StartsWithSavedSettings()
        {
            var store = new MemorySettingsStore();
            store.SetInt(GameSettings.SoundsKey, 0);
            ShiftScreen screen = Build(Started(), new GameSettings(store), new Calls());
            Assert.IsFalse(screen.Pause.IsOn(PauseControl.Sounds));
            Assert.IsTrue(screen.Pause.IsOn(PauseControl.Music));
        }

        [Test]
        public void PausePanel_SkipTutorial_VisibleOnlyWithTutorial_AndSkipsAll()
        {
            ShiftDriver plain = Started();
            ShiftScreen withoutTutorial = Build(plain, new GameSettings(new MemorySettingsStore()), new Calls());
            Pause(plain, withoutTutorial);
            Assert.IsFalse(withoutTutorial.Pause.SkipTutorialVisible, "без навчання кнопки немає");

            ShiftDriver driver = Started();
            TutorialDirector director = TutorialDirector.Create(driver, ResourceConfigSource.LoadTutorial().ValueOrThrow(), new SaveData());
            var calls = new Calls();
            ShiftScreen screen = Build(driver, new GameSettings(new MemorySettingsStore()), calls, director);
            Pause(driver, screen);
            Assert.IsTrue(screen.Pause.SkipTutorialVisible, "QB-TUT-06: пропуск навчання видно в паузі");
            Assert.IsTrue(screen.Tutorial.IsVisible);

            Assert.IsTrue(screen.Pause.Press(PauseControl.SkipTutorial));
            Assert.IsTrue(director.Model.IsSkippedAll);
            Assert.IsFalse(screen.Tutorial.IsVisible, "плашку знято");
            Assert.AreEqual(1, calls.TutorialChanged, "профіль можна зберегти");
            Assert.IsFalse(screen.Pause.SkipTutorialVisible);
        }

        [Test]
        public void Localization_PauseLabels_FitInAllLanguages()
        {
            string folder = Path.Combine(Application.dataPath, "_Project", "Resources", "Localization");
            foreach (string code in new[] { "en", "uk", "pl" })
            {
                LocalizationTable table = LocalizationTable.Parse(File.ReadAllText(Path.Combine(folder, code + ".json")));
                foreach (string key in new[] { TextKeys.PauseMusic, TextKeys.PauseSounds, TextKeys.PauseHaptics, TextKeys.PauseReducedMotion })
                {
                    string value;
                    Assert.IsTrue(table.TryGet(key, out value), code + " " + key);
                    Assert.LessOrEqual(value.Length, 16, code + ": підпис перемикача вміщається у дві рядки по 28 px, " + key);
                }

                foreach (string key in new[] { TextKeys.PauseYes, TextKeys.PauseNo, TextKeys.PauseRestart })
                {
                    string value;
                    Assert.IsTrue(table.TryGet(key, out value), code + " " + key);
                    Assert.LessOrEqual(value.Length, 12, code + " " + key);
                }
            }
        }
    }
}
