namespace KebabStreet.Game
{
    /// <summary>Ключі локалізації екранів каркаса. Видимі тексти в коді не пишуться, лише ці ключі (E0-08 додасть справжні мови).</summary>
    public static class TextKeys
    {
        public const string GameTitle = "game.title";
        public const string BootLoading = "boot.loading";
        public const string MenuPlay = "menu.play";
        public const string MenuSettings = "menu.settings";
        public const string MenuSettingsSoon = "menu.settings_soon";
        public const string ShiftTimeLeft = "shift.time_left";
        public const string ShiftFinished = "shift.finished";
        public const string ShiftExit = "shift.exit";
        public const string MenuLevel = "menu.level";
        public const string ShiftLevelTitle = "shift.level";
        public const string ShiftPaused = "shift.paused";
        public const string ShiftGoalDoneStar2 = "shift.goal_done_star2";
        public const string ShiftResultWon = "shift.result_won";
        public const string ShiftResultLost = "shift.result_lost";
        public const string ShiftResultStars = "shift.result_stars";
        public const string ShiftResultCoins = "shift.result_coins";
        public const string ShiftHudGoalServe = "shift.hud_goal_serve";
        public const string ShiftHudGoalCoins = "shift.hud_goal_coins";
        public const string ShiftHudCoins = "shift.hud_coins";
        public const string ShiftPause = "shift.pause";
        public const string ShiftResume = "shift.resume";
        public const string GrillSlotEmpty = "grill.slot_empty";
        public const string GrillSlotRaw = "grill.slot_raw";
        public const string GrillSlotCooking = "grill.slot_cooking";
        public const string GrillSlotDone = "grill.slot_done";
        public const string GrillSlotBurning = "grill.slot_burning";
        public const string GrillSlotBurnt = "grill.slot_burnt";
        public const string CounterSlotFree = "counter.slot_free";
        public const string CounterQueue = "counter.queue";
        public const string CounterLeft = "counter.left";
        public const string CounterWrongDish = "counter.wrong_dish";
        public const string CounterPaid = "counter.paid";
        public const string CounterPaidTip = "counter.paid_tip";

        // Імена клієнтів і назви інгредієнтів: ці ключі вказані в customers.json / recipes.json (nameKey).
        public const string CustomerOlyaName = "customer.olya.name";
        public const string CustomerMarkoName = "customer.marko.name";
        public const string CustomerHannaName = "customer.hanna.name";
        public const string IngredientPita = "ingredient.pita";
        public const string IngredientMeat = "ingredient.meat";
        public const string IngredientSalad = "ingredient.salad";
        public const string IngredientOnion = "ingredient.onion";
        public const string IngredientSauceGarlic = "ingredient.sauce_garlic";
        public const string IngredientSauceHot = "ingredient.sauce_hot";
        public const string NoticeProgressReset = "notice.progress_reset";
        public const string NoticeUpdateGame = "notice.update_game";
        public const string NoticeStorageUnavailable = "notice.storage_unavailable";
        public const string NoticeSaveDisabled = "notice.save_disabled";
        public const string NoticeSaveFailed = "notice.save_failed";
    }
}
