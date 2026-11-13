using System.Collections.Generic;

namespace KebabStreet.Game
{
    /// <summary>Англійські рядки для тестів каркаса (у грі не використовується; у грі діє JsonLocalizer).</summary>
    public sealed class PlaceholderLocalizer : ILocalizer
    {
        private static readonly Dictionary<string, string> Strings = new Dictionary<string, string>
        {
            { TextKeys.GameTitle, "Kebab Street" },
            { TextKeys.BootLoading, "Loading..." },
            { TextKeys.MenuPlay, "Play" },
            { TextKeys.MenuSettings, "Settings" },
            { TextKeys.MenuSettingsSoon, "Settings are coming soon" },
            { TextKeys.ShiftTimeLeft, "Time left: {0}" },
            { TextKeys.ShiftFinished, "Shift finished" },
            { TextKeys.ShiftExit, "Exit" },
            { TextKeys.MenuLevel, "Level {0}" },
            { TextKeys.ShiftLevelTitle, "Level {0}" },
            { TextKeys.ShiftPaused, "Paused" },
            { TextKeys.ShiftGoalDoneStar2, "Goal reached! You still have time for ★★" },
            { TextKeys.ShiftResultWon, "Level complete!" },
            { TextKeys.ShiftResultLost, "Shift failed" },
            { TextKeys.ShiftResultStars, "Stars: {0}" },
            { TextKeys.ShiftResultCoins, "Coins: {0}" },
            { TextKeys.ShiftHudGoalServe, "Served: {0}/{1}" },
            { TextKeys.ShiftHudGoalCoins, "Goal: {0}/{1} coins" },
            { TextKeys.ShiftHudCoins, "Coins: {0}" },
            { TextKeys.ShiftPause, "Pause" },
            { TextKeys.ShiftResume, "Resume" },
            { TextKeys.GrillSlotEmpty, "Add meat" },
            { TextKeys.GrillSlotRaw, "Raw" },
            { TextKeys.GrillSlotCooking, "Cooking" },
            { TextKeys.GrillSlotDone, "Ready! Take it" },
            { TextKeys.GrillSlotBurning, "Burning soon!" },
            { TextKeys.GrillSlotBurnt, "Burnt! Throw away" },
            { TextKeys.CounterSlotFree, "Free" },
            { TextKeys.CounterQueue, "In line: {0}" },
            { TextKeys.CounterLeft, "Customer left" },
            { TextKeys.CounterWrongDish, "Wrong order!" },
            { TextKeys.CounterPaid, "+{0}" },
            { TextKeys.CounterPaidTip, "+{0}, tip {1}" },
            { TextKeys.AssemblyBoardEmpty, "Board is empty" },
            { TextKeys.AssemblyDishReady, "Ready! Tap a customer" },
            { TextKeys.AssemblyDishWrong, "Not the recipe: tap an item to remove it" },
            { TextKeys.AssemblyClear, "Clear board" },
            { TextKeys.AssemblyQueue, "Next:" },
            { TextKeys.AssemblyCombo, "Combo x{0}" },
            { TextKeys.HintMeatNotReady, "Meat is not ready yet" },
            { TextKeys.HintGrillFull, "No free spot on the grill" },
            { TextKeys.HintBoardFull, "The board is full" },
            { TextKeys.HintDishIncomplete, "The dish is not finished" },
            { TextKeys.HintDishWrong, "The dish does not match the recipe" },
            { TextKeys.ActionPlaceMeat, "Grill meat" },
            { TextKeys.ActionTakeMeat, "Take meat" },
            { TextKeys.ActionDiscard, "Throw away" },
            { TextKeys.ActionRemove, "Remove" },
            { TextKeys.ActionClear, "Clear" },
            { TextKeys.ActionServe, "Serve" },
            { TextKeys.CustomerOlyaName, "Olya" },
            { TextKeys.CustomerMarkoName, "Marko" },
            { TextKeys.CustomerHannaName, "Hanna" },
            { TextKeys.IngredientPita, "Pita" },
            { TextKeys.IngredientMeat, "Meat" },
            { TextKeys.IngredientSalad, "Salad" },
            { TextKeys.IngredientOnion, "Onion" },
            { TextKeys.IngredientSauceGarlic, "Garlic" },
            { TextKeys.IngredientSauceHot, "Hot" },
            { TextKeys.NoticeProgressReset, "Your saved progress could not be restored and was reset." },
            { TextKeys.NoticeUpdateGame, "Your save was made by a newer version. Please update the game. Progress will not be saved." },
            { TextKeys.NoticeStorageUnavailable, "Saved data cannot be read right now. Progress will not be saved. Restart the game." },
            { TextKeys.NoticeSaveDisabled, "Progress saving is turned off because of a problem with the saved data." },
            { TextKeys.NoticeSaveFailed, "Progress could not be saved. Please try again later." },
        };

        public string Get(string key)
        {
            string value;
            return key != null && Strings.TryGetValue(key, out value) ? value : "[" + key + "]";
        }
    }
}
