using System;
using System.Collections.Generic;
using KebabStreet.Core;

namespace KebabStreet.Game
{
    /// <summary>
    /// Модель вигляду станції збирання (S1-09c): які кнопки станцій показати, що написати про
    /// страву на дошці, яку підказку дати на проігноровану дію, як назвати дію в черзі і чи
    /// показувати комбо. Лише читання стану Core; правил гри тут немає.
    /// </summary>
    public static class AssemblyView
    {
        /// <summary>Комбо показується з другої вдалої видачі поспіль («×1» нічого не каже гравцю). Вигляд, не баланс.</summary>
        public const long ComboShownFrom = 2L;

        /// <summary>Інгредієнти, для яких на цьому рівні є кнопка станції (у порядку конфіга; без м'яса — воно з гриля).</summary>
        public static int[] Stations(Board board)
        {
            if (board == null)
            {
                throw new ArgumentNullException(nameof(board));
            }

            var stations = new List<int>(board.Config.IngredientCount);
            for (int i = 0; i < board.Config.IngredientCount; i++)
            {
                if (board.CanAdd(i))
                {
                    stations.Add(i);
                }
            }

            return stations.ToArray();
        }

        /// <summary>Напис про зібране на дошці; null — нічого не писати (страва ще збирається).</summary>
        public static string DishTextKey(DishCheck check)
        {
            switch (check)
            {
                case DishCheck.Empty:
                    return TextKeys.AssemblyBoardEmpty;
                case DishCheck.Correct:
                    return TextKeys.AssemblyDishReady;
                case DishCheck.WrongOrder:
                case DishCheck.ExtraIngredient:
                    return TextKeys.AssemblyDishWrong;
                default:
                    return null;
            }
        }

        /// <summary>Підказка на дію, яку Core проігнорував; null — без підказки (про скаргу клієнта каже прилавок).</summary>
        public static string HintKeyOf(ShiftActionResult result)
        {
            switch (result)
            {
                case ShiftActionResult.MeatRaw:
                case ShiftActionResult.NoDoneMeat:
                    return TextKeys.HintMeatNotReady;
                case ShiftActionResult.GrillFull:
                case ShiftActionResult.SlotOccupied:
                    return TextKeys.HintGrillFull;
                case ShiftActionResult.BoardFull:
                    return TextKeys.HintBoardFull;
                case ShiftActionResult.BoardEmpty:
                case ShiftActionResult.DishIncomplete:
                    return TextKeys.HintDishIncomplete;
                case ShiftActionResult.DishWrongOrder:
                case ShiftActionResult.DishExtraIngredient:
                    return TextKeys.HintDishWrong;
                default:
                    return null;
            }
        }

        /// <summary>Назва дії в черзі; для «покласти інгредієнт» — null (показується назва інгредієнта).</summary>
        public static string ActionTextKey(ShiftActionType type)
        {
            switch (type)
            {
                case ShiftActionType.PlaceMeat:
                    return TextKeys.ActionPlaceMeat;
                case ShiftActionType.TakeMeat:
                    return TextKeys.ActionTakeMeat;
                case ShiftActionType.DiscardBurntMeat:
                    return TextKeys.ActionDiscard;
                case ShiftActionType.RemoveIngredient:
                    return TextKeys.ActionRemove;
                case ShiftActionType.ClearBoard:
                    return TextKeys.ActionClear;
                case ShiftActionType.ServeDish:
                    return TextKeys.ActionServe;
                default:
                    return null;
            }
        }

        /// <summary>Яке комбо показати; 0 — не показувати.</summary>
        public static long ComboShown(long combo)
        {
            return combo >= ComboShownFrom ? combo : 0L;
        }
    }
}
