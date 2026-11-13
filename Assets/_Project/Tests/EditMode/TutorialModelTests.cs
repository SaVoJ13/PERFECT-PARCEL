using System.Collections.Generic;
using KebabStreet.Core;
using KebabStreet.Game;
using NUnit.Framework;

namespace KebabStreet.Tests.EditMode
{
    /// <summary>
    /// Модель навчання (S1-11): L01 за подіями зміни, по одному кроку, пауза між кроками, тихе завершення кроку,
    /// «пропустити крок» і «пропустити все», пам'ять профілю (oncePerProfile, firstTimeInProfile).
    /// </summary>
    public sealed class TutorialModelTests
    {
        private const int GapMs = 2000;

        private static TutorialConfig Real()
        {
            return ResourceConfigSource.LoadTutorial().ValueOrThrow();
        }

        private static TutorialEvent Ev(TutorialEventKind kind, string ingredient = null, string customer = null)
        {
            return new TutorialEvent(kind, ingredient, customer);
        }

        private static void AssertAtMostOneActive(TutorialModel model)
        {
            int active = 0;
            for (int i = 0; i < model.StepCount; i++)
            {
                if (model.GetState(model.GetStep(i).Id) == TutorialStepState.Active)
                {
                    active++;
                }
            }

            Assert.LessOrEqual(active, 1, "одночасно показано не більше одного кроку");
            Assert.AreEqual(active == 1, model.Current != null);
        }

        private static string Json(string stepsJson, bool oncePerProfile = true)
        {
            return "{ \"version\": 1, \"speaker\": \"uncle_ali\","
                + " \"skip\": { \"allowSkipStep\": true, \"allowSkipAll\": true, \"skipStepButtonKey\": \"ui.tutorial.next\","
                + " \"skipAllButtonKey\": \"ui.tutorial.skip\", \"rememberSkipAll\": true },"
                + " \"display\": { \"maxConcurrent\": 1, \"minGapSec\": 2, \"pauseSimulation\": false, \"oncePerProfile\": "
                + (oncePerProfile ? "true" : "false") + " },"
                + " \"levels\": [ { \"level\": \"L01\", \"steps\": [ " + stepsJson + " ] } ] }";
        }

        private static TutorialConfig Custom(string stepsJson, bool oncePerProfile = true)
        {
            return TutorialConfigLoader.Parse(Json(stepsJson, oncePerProfile)).ValueOrThrow();
        }

        [Test]
        public void L01_WalksThroughSteps_OnBotEvents_OneAtATime()
        {
            var profile = new SaveData();
            var model = new TutorialModel(Real(), "L01", profile);
            Assert.AreEqual(5, model.StepCount);
            Assert.IsNull(model.Current, "до початку зміни підказки немає");

            model.Handle(Ev(TutorialEventKind.ShiftStarted), 0);
            Assert.AreEqual("l01.step1", model.Current.Id, "перший крок з'являється одразу: попереднього кроку ще не було");
            AssertAtMostOneActive(model);

            model.Handle(Ev(TutorialEventKind.PlaceMeat), 500);
            Assert.IsNull(model.Current, "між кроками пауза minGapSec");
            Assert.AreEqual(TutorialStepState.Done, model.GetState("l01.step1"));
            Assert.AreEqual(TutorialStepState.Armed, model.GetState("l01.step2"));
            model.Update(500 + GapMs - 1);
            Assert.IsNull(model.Current);
            model.Update(500 + GapMs);
            Assert.AreEqual("l01.step2", model.Current.Id);
            Assert.AreEqual("board.pita", model.Current.Anchor);
            AssertAtMostOneActive(model);

            model.Handle(Ev(TutorialEventKind.AddIngredient, "pita"), 3000);
            Assert.AreEqual(TutorialStepState.Done, model.GetState("l01.step2"));
            Assert.IsNull(model.Current);

            model.Handle(Ev(TutorialEventKind.MeatDone), 6000);
            Assert.AreEqual("l01.step3", model.Current.Id, "пауза від кінця попереднього кроку вже минула");
            AssertAtMostOneActive(model);

            model.Handle(Ev(TutorialEventKind.TakeMeat), 6500);
            Assert.IsNull(model.Current);
            Assert.AreEqual(TutorialStepState.Armed, model.GetState("l01.step4"));
            model.Update(8500);
            Assert.AreEqual("l01.step4", model.Current.Id);
            Assert.AreEqual("counter.customer", model.Current.Anchor);

            model.Handle(Ev(TutorialEventKind.ServeDish, null, "olya"), 9000);
            Assert.IsNull(model.Current);
            Assert.AreEqual(TutorialStepState.Done, model.GetState("l01.step4"));
            Assert.AreEqual(TutorialStepState.Pending, model.GetState("l02.burnt"), "крок про пригоріле чекає своєї події");

            Assert.IsTrue(model.Dirty);
            CollectionAssert.AreEquivalent(
                new[] { "l01.step1", "l01.step2", "l01.step3", "l01.step4" }, profile.TutorialSeen);
        }

        [Test]
        public void ActionDoneBeforeStepAppears_FinishesStepSilently()
        {
            var model = new TutorialModel(Real(), "L01", new SaveData());
            model.Handle(Ev(TutorialEventKind.ShiftStarted), 0);
            model.Handle(Ev(TutorialEventKind.PlaceMeat), 400);
            Assert.AreEqual(TutorialStepState.Armed, model.GetState("l01.step2"));

            model.Handle(Ev(TutorialEventKind.AddIngredient, "pita"), 900);

            Assert.AreEqual(TutorialStepState.Done, model.GetState("l01.step2"), "гравець устиг раніше підказки");
            Assert.IsNull(model.Current);
            model.Update(60000);
            Assert.IsNull(model.Current, "тихо завершений крок не з'являється пізніше");
        }

        [Test]
        public void WrongIngredient_DoesNotCompleteStep()
        {
            var model = new TutorialModel(Real(), "L01", new SaveData());
            model.Handle(Ev(TutorialEventKind.ShiftStarted), 0);
            model.Handle(Ev(TutorialEventKind.PlaceMeat), 400);
            model.Update(5000);
            Assert.AreEqual("l01.step2", model.Current.Id);

            model.Handle(Ev(TutorialEventKind.AddIngredient, "salad"), 5100);

            Assert.AreEqual("l01.step2", model.Current.Id, "потрібна піта, а не салат");
        }

        [Test]
        public void MeatDoneWhileStillOnPitaStep_WaitsForItsTurn()
        {
            var model = new TutorialModel(Real(), "L01", new SaveData());
            model.Handle(Ev(TutorialEventKind.ShiftStarted), 0);
            model.Handle(Ev(TutorialEventKind.PlaceMeat), 400);
            model.Update(3000);
            Assert.AreEqual("l01.step2", model.Current.Id);

            model.Handle(Ev(TutorialEventKind.MeatDone), 6000);
            Assert.AreEqual("l01.step2", model.Current.Id, "один крок одночасно");
            Assert.AreEqual(TutorialStepState.Armed, model.GetState("l01.step3"));

            model.Handle(Ev(TutorialEventKind.AddIngredient, "pita"), 7000);
            Assert.IsNull(model.Current, "після пропуску паузи 2 с");
            model.Update(9000);
            Assert.AreEqual("l01.step3", model.Current.Id);
        }

        [Test]
        public void SkipStep_FinishesCurrentStep_AndNextCanFollow()
        {
            var profile = new SaveData();
            var model = new TutorialModel(Real(), "L01", profile);
            Assert.IsFalse(model.SkipStep(0), "пропускати нічого");
            model.Handle(Ev(TutorialEventKind.ShiftStarted), 0);
            Assert.IsTrue(model.CanSkipStep);

            Assert.IsTrue(model.SkipStep(1000));

            Assert.IsNull(model.Current);
            Assert.AreEqual(TutorialStepState.Done, model.GetState("l01.step1"));
            Assert.IsTrue(profile.TutorialSeen.Contains("l01.step1"));
            Assert.IsFalse(profile.TutorialSkipAll, "пропущено лише крок");
            model.Handle(Ev(TutorialEventKind.PlaceMeat), 1500);
            model.Update(3000);
            Assert.AreEqual("l01.step2", model.Current.Id, "пропущений крок не блокує наступні");
        }

        [Test]
        public void SkipAll_EndsTutorial_RemembersInProfile_AndNothingComesBack()
        {
            var profile = new SaveData();
            var model = new TutorialModel(Real(), "L01", profile);
            model.Handle(Ev(TutorialEventKind.ShiftStarted), 0);
            Assert.IsTrue(model.CanSkipAll);

            Assert.IsTrue(model.SkipAll(100));

            Assert.IsNull(model.Current);
            Assert.IsTrue(model.IsSkippedAll);
            Assert.IsTrue(profile.TutorialSkipAll);
            Assert.IsTrue(model.Dirty);
            foreach (string id in new[] { "l01.step1", "l01.step2", "l01.step3", "l01.step4", "l02.burnt" })
            {
                Assert.AreEqual(TutorialStepState.Done, model.GetState(id));
                Assert.IsTrue(profile.TutorialSeen.Contains(id), id);
            }

            model.Handle(Ev(TutorialEventKind.PlaceMeat), 500);
            model.Handle(Ev(TutorialEventKind.MeatBurnt), 600);
            model.Update(60000);
            Assert.IsNull(model.Current, "після «пропустити все» підказок немає");
            Assert.IsFalse(model.SkipAll(700), "повторний виклик нічого не робить");

            // Новий рівень (інша модель) з тим самим профілем теж без підказок.
            var next = new TutorialModel(Real(), "L02", profile);
            next.Handle(Ev(TutorialEventKind.MeatBurnt), 0);
            Assert.IsNull(next.Current);
            Assert.IsTrue(next.IsSkippedAll);
        }

        [Test]
        public void SkipAll_WhenNotRemembered_OnlyMarksSeenSteps()
        {
            string json = Json("{ \"id\": \"a\", \"textKey\": \"tutorial.l01.step1\", \"anchor\": \"grill.slot1\","
                + " \"showWhen\": { \"event\": \"ShiftStarted\" }, \"completeWhen\": { \"event\": \"PlaceMeat\" },"
                + " \"autoHideSec\": null, \"skippable\": true }")
                .Replace("\"rememberSkipAll\": true", "\"rememberSkipAll\": false");
            TutorialConfig config = TutorialConfigLoader.Parse(json).ValueOrThrow();
            var profile = new SaveData();
            var model = new TutorialModel(config, "L01", profile);
            model.Handle(Ev(TutorialEventKind.ShiftStarted), 0);

            Assert.IsTrue(model.SkipAll(10));

            Assert.IsFalse(profile.TutorialSkipAll, "пам'ять про «пропустити все» вимкнена конфігом");
            Assert.IsTrue(profile.TutorialSeen.Contains("a"));
        }

        [Test]
        public void OncePerProfile_SeenStepsDoNotReturnOnNextShift_ButTheRestFollow()
        {
            var profile = new SaveData();
            var first = new TutorialModel(Real(), "L01", profile);
            first.Handle(Ev(TutorialEventKind.ShiftStarted), 0);
            first.Handle(Ev(TutorialEventKind.PlaceMeat), 500);
            Assert.IsTrue(profile.TutorialSeen.Contains("l01.step1"));

            // Перезапуск рівня: крок 1 вже пройдено і не повертається; крок 2 ще не пройдено.
            var second = new TutorialModel(Real(), "L01", profile);
            Assert.AreEqual(TutorialStepState.Done, second.GetState("l01.step1"));
            second.Handle(Ev(TutorialEventKind.ShiftStarted), 0);
            Assert.IsNull(second.Current, "перший крок не повертається");
            second.Handle(Ev(TutorialEventKind.PlaceMeat), 500);
            second.Update(500 + GapMs);
            Assert.AreEqual("l01.step2", second.Current.Id);
        }

        [Test]
        public void ProfileThatSawEverything_GetsNoHintsOnL01()
        {
            var profile = new SaveData();
            foreach (string id in new[] { "l01.step1", "l01.step2", "l01.step3", "l01.step4", "l02.burnt" })
            {
                profile.TutorialSeen.Add(id);
            }

            var model = new TutorialModel(Real(), "L01", profile);
            model.Handle(Ev(TutorialEventKind.ShiftStarted), 0);
            model.Handle(Ev(TutorialEventKind.PlaceMeat), 1);
            model.Handle(Ev(TutorialEventKind.MeatBurnt), 2);
            model.Update(100000);

            Assert.IsNull(model.Current);
            Assert.IsFalse(model.Dirty, "нічого нового профіль не отримав");
        }

        [Test]
        public void OncePerProfileOff_StepsReturnEveryShift_ExceptFirstTimeOnes()
        {
            const string repeat = "{ \"id\": \"a\", \"textKey\": \"tutorial.l01.step1\", \"anchor\": \"grill.slot1\","
                + " \"showWhen\": { \"event\": \"ShiftStarted\" }, \"completeWhen\": { \"event\": \"PlaceMeat\" },"
                + " \"autoHideSec\": null, \"skippable\": true },";
            const string once = "{ \"id\": \"b\", \"textKey\": \"tutorial.l02.burnt\", \"anchor\": \"grill.burntSlot\","
                + " \"showWhen\": { \"event\": \"MeatBurnt\", \"firstTimeInProfile\": true },"
                + " \"completeWhen\": { \"event\": \"DiscardBurntMeat\" }, \"autoHideSec\": 8, \"skippable\": true }";
            TutorialConfig config = Custom(repeat + once, false);
            var profile = new SaveData();

            var first = new TutorialModel(config, "L01", profile);
            first.Handle(Ev(TutorialEventKind.ShiftStarted), 0);
            Assert.AreEqual("a", first.Current.Id);
            first.Handle(Ev(TutorialEventKind.PlaceMeat), 100);
            first.Handle(Ev(TutorialEventKind.MeatBurnt), 5000);
            Assert.AreEqual("b", first.Current.Id);
            first.Handle(Ev(TutorialEventKind.DiscardBurntMeat), 5200);

            var second = new TutorialModel(config, "L01", profile);
            second.Handle(Ev(TutorialEventKind.ShiftStarted), 0);
            Assert.AreEqual("a", second.Current.Id, "без oncePerProfile крок повертається");
            second.Handle(Ev(TutorialEventKind.PlaceMeat), 100);
            second.Handle(Ev(TutorialEventKind.MeatBurnt), 5000);
            second.Update(10000);
            Assert.IsNull(second.Current, "firstTimeInProfile показується лише раз");
        }

        [Test]
        public void AutoHide_HidesStepAfterConfiguredTime_AndRemembersIt()
        {
            var profile = new SaveData();
            var model = new TutorialModel(Real(), "L01", profile);

            model.Handle(Ev(TutorialEventKind.MeatBurnt), 1000);
            Assert.AreEqual("l02.burnt", model.Current.Id);
            Assert.AreEqual(8000, model.Current.AutoHideMs);
            model.Update(8999);
            Assert.AreEqual("l02.burnt", model.Current.Id);
            model.Update(9000);

            Assert.IsNull(model.Current);
            Assert.AreEqual(TutorialStepState.Done, model.GetState("l02.burnt"));
            Assert.IsTrue(profile.TutorialSeen.Contains("l02.burnt"));

            var again = new TutorialModel(Real(), "L01", profile);
            again.Handle(Ev(TutorialEventKind.MeatBurnt), 0);
            Assert.IsNull(again.Current, "firstTimeInProfile: вдруге не показується");
        }

        [Test]
        public void BurntStep_BelongsToL02_AndIsCompletedByDiscard()
        {
            var model = new TutorialModel(Real(), "L02", new SaveData());
            Assert.AreEqual(1, model.StepCount);

            model.Handle(Ev(TutorialEventKind.MeatBurnt), 0);
            Assert.AreEqual("l02.burnt", model.Current.Id);
            Assert.AreEqual("grill.burntSlot", model.Current.Anchor);
            model.Handle(Ev(TutorialEventKind.DiscardBurntMeat), 1500);

            Assert.IsNull(model.Current);
        }

        [Test]
        public void Dirty_IsSetByProgress_AndClearedOnRequest()
        {
            var model = new TutorialModel(Real(), "L01", new SaveData());
            model.Handle(Ev(TutorialEventKind.ShiftStarted), 0);
            Assert.IsFalse(model.Dirty, "показ кроку профіль не змінює: крок ще не завершено");

            model.Handle(Ev(TutorialEventKind.PlaceMeat), 100);
            Assert.IsTrue(model.Dirty);
            model.ClearDirty();
            Assert.IsFalse(model.Dirty);
        }

        [Test]
        public void QuittingMidStep_DoesNotMarkItSeen_SoItReturnsOnRetry()
        {
            var profile = new SaveData();
            var model = new TutorialModel(Real(), "L01", profile);
            model.Handle(Ev(TutorialEventKind.ShiftStarted), 0);
            Assert.AreEqual("l01.step1", model.Current.Id);

            Assert.AreEqual(0, profile.TutorialSeen.Count, "крок лише показано, не пройдено");
            var retry = new TutorialModel(Real(), "L01", profile);
            retry.Handle(Ev(TutorialEventKind.ShiftStarted), 0);
            Assert.AreEqual("l01.step1", retry.Current.Id);
        }

        [Test]
        public void EventsOfUnknownKind_AreIgnored_AndTimeOnlyMovesForward()
        {
            var model = new TutorialModel(Real(), "L01", new SaveData());
            model.Handle(default(TutorialEvent), 0);
            model.Update(0);
            Assert.IsNull(model.Current);
        }

        [Test]
        public void UnknownLevel_HasNoSteps_AndNeverShowsAnything()
        {
            var model = new TutorialModel(Real(), "L99", new SaveData());
            Assert.AreEqual(0, model.StepCount);
            model.Handle(Ev(TutorialEventKind.ShiftStarted), 0);
            Assert.IsNull(model.Current);
            Assert.IsFalse(model.CanSkipAll);
        }

        [Test]
        public void GetState_UnknownStep_Throws()
        {
            var model = new TutorialModel(Real(), "L01", new SaveData());
            Assert.Throws<System.ArgumentException>(() => model.GetState("nope"));
        }

        [Test]
        public void Constructor_RejectsNulls()
        {
            Assert.Throws<System.ArgumentNullException>(() => new TutorialModel(null, "L01", new SaveData()));
            Assert.Throws<System.ArgumentNullException>(() => new TutorialModel(Real(), null, new SaveData()));
            Assert.Throws<System.ArgumentNullException>(() => new TutorialModel(Real(), "L01", null));
        }

        [Test]
        public void OrderShownStep_MatchesOnlyTheRequestedOptionalIngredient()
        {
            TutorialConfig config = Real();
            var model = new TutorialModel(config, "L03", new SaveData());
            model.Handle(Ev(TutorialEventKind.OrderShown, "salad", "olya"), 0);
            Assert.IsNull(model.Current, "салат не запускає підказку про часниковий соус");

            model.Handle(Ev(TutorialEventKind.OrderShown, "sauce_garlic", "olya"), 100);
            Assert.AreEqual("l03.sauce", model.Current.Id);
        }

        [Test]
        public void CustomerFilter_ServeDishByOtherCustomer_DoesNotComplete()
        {
            TutorialConfig config = Real();
            var model = new TutorialModel(config, "L04", new SaveData());
            model.Handle(Ev(TutorialEventKind.CustomerArrived, null, "hanna"), 0);
            Assert.AreEqual("l04.request", model.Current.Id);

            model.Handle(Ev(TutorialEventKind.ServeDish, null, "olya"), 100);
            Assert.AreEqual("l04.request", model.Current.Id, "видано не Ганні");
            model.Handle(Ev(TutorialEventKind.ServeDish, null, "hanna"), 200);
            Assert.IsNull(model.Current);
        }

        [Test]
        public void TextKeysOfAllShownSteps_AreLocalizationKeys()
        {
            var seen = new List<string>();
            TutorialConfig config = Real();
            foreach (TutorialStep step in config.StepsFor("L01"))
            {
                seen.Add(step.TextKey);
                Assert.IsTrue(ConfigText.IsLocalizationKey(step.TextKey), step.Id);
            }

            CollectionAssert.AllItemsAreUnique(seen);
        }
    }
}
