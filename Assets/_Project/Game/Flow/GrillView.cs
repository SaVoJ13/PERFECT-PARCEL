using System;
using KebabStreet.Core;

namespace KebabStreet.Game
{
    /// <summary>
    /// Що гравець бачить у слоті гриля (S1-09a). Core має чотири стани м'яса; п'ятий видимий стан
    /// «сире» — це порожній слот, для якого в черзі дій уже стоїть «покласти м'ясо» (шматок кладуть).
    /// Числові значення використовуються як номери в таблицях показу — не змінювати порядок.
    /// </summary>
    public enum GrillSlotDisplay
    {
        /// <summary>Порожній слот: тап кладе м'ясо.</summary>
        Empty = 0,

        /// <summary>Сире: м'ясо кладуть (дія в черзі), на грилі його ще немає.</summary>
        Raw = 1,

        /// <summary>Готується: поступ до готовності.</summary>
        Cooking = 2,

        /// <summary>Готове: тап знімає на дошку; поступ показує, скільки вікна до пригорання минуло.</summary>
        Done = 3,

        /// <summary>Пригоріле: тап викидає.</summary>
        Burnt = 4,
    }

    /// <summary>Дані для показу одного слота гриля. Лише значення, без об'єктів Unity.</summary>
    public readonly struct GrillSlotView : IEquatable<GrillSlotView>
    {
        public GrillSlotView(GrillSlotDisplay display, int progressPercent, bool burnWarning, bool actionQueued)
        {
            Display = display;
            ProgressPercent = progressPercent;
            BurnWarning = burnWarning;
            ActionQueued = actionQueued;
        }

        /// <summary>Видимий стан слота.</summary>
        public GrillSlotDisplay Display { get; }

        /// <summary>Індикатор поступу, 0..100 (зміст залежить від стану, див. <see cref="GrillSlotDisplay"/>).</summary>
        public int ProgressPercent { get; }

        /// <summary>Готове м'ясо скоро пригорить (grill.warnBeforeBurnSec).</summary>
        public bool BurnWarning { get; }

        /// <summary>Для цього слота в черзі вже стоїть дія гриля: новий тап нічого не додає.</summary>
        public bool ActionQueued { get; }

        public bool Equals(GrillSlotView other)
        {
            return Display == other.Display
                && ProgressPercent == other.ProgressPercent
                && BurnWarning == other.BurnWarning
                && ActionQueued == other.ActionQueued;
        }

        public override bool Equals(object obj)
        {
            return obj is GrillSlotView other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = (int)Display;
                hash = (hash * 397) ^ ProgressPercent;
                hash = (hash * 397) ^ (BurnWarning ? 1 : 0);
                hash = (hash * 397) ^ (ActionQueued ? 1 : 0);
                return hash;
            }
        }

        public override string ToString()
        {
            return Display + " " + ProgressPercent.ToString(System.Globalization.CultureInfo.InvariantCulture) + "%"
                + (BurnWarning ? " warn" : string.Empty)
                + (ActionQueued ? " queued" : string.Empty);
        }
    }

    /// <summary>
    /// Модель вигляду гриля (S1-09a): стан Core → дані для показу і тап по слоту → команда.
    /// Правил гри тут немає: чи вдасться команда, вирішує Core на такті. Модель лише не шле
    /// команду, якої гравець у цьому стані не має (сире м'ясо зняти не можна — GAME_DESIGN розд. 4),
    /// і не дублює дію, що вже стоїть у черзі для цього слота.
    /// </summary>
    public static class GrillView
    {
        private const int FullPercent = 100;

        /// <summary>Вигляд слота за «сирими» числами стану. Основа для обох джерел (симуляція, знімок).</summary>
        /// <param name="queuedAction">Перша дія гриля в черзі, адресована цьому слоту, або None.</param>
        /// <param name="queuedProgressTicks">Поступ цієї дії (0, якщо вона не перша в черзі).</param>
        public static GrillSlotView Compute(
            MeatState state,
            long ageTicks,
            long cookTicks,
            long burnAfterDoneTicks,
            long warnBeforeBurnTicks,
            ShiftActionType queuedAction,
            long queuedProgressTicks,
            long actionTicks)
        {
            bool queued = queuedAction != ShiftActionType.None;
            switch (state)
            {
                case MeatState.Raw:
                    return new GrillSlotView(GrillSlotDisplay.Cooking, Percent(ageTicks, cookTicks), false, queued);
                case MeatState.Done:
                    long sinceDone = ageTicks - cookTicks;
                    bool warning = burnAfterDoneTicks - sinceDone <= warnBeforeBurnTicks;
                    return new GrillSlotView(
                        GrillSlotDisplay.Done, Percent(sinceDone, burnAfterDoneTicks), warning, queued);
                case MeatState.Burnt:
                    return new GrillSlotView(GrillSlotDisplay.Burnt, FullPercent, false, queued);
                default:
                    if (queuedAction == ShiftActionType.PlaceMeat)
                    {
                        return new GrillSlotView(
                            GrillSlotDisplay.Raw, Percent(queuedProgressTicks, actionTicks), false, true);
                    }

                    return new GrillSlotView(GrillSlotDisplay.Empty, 0, false, queued);
            }
        }

        /// <summary>Вигляд слота з живої симуляції (без виділення пам'яті: для щокадрового показу).</summary>
        public static GrillSlotView FromSimulation(ShiftSimulation simulation, int slot)
        {
            if (simulation == null)
            {
                throw new ArgumentNullException(nameof(simulation));
            }

            Grill grill = simulation.Grill;
            if (grill == null || slot < 0 || slot >= grill.SlotCount)
            {
                throw new ArgumentOutOfRangeException(nameof(slot));
            }

            ShiftActionType queuedAction = ShiftActionType.None;
            long queuedProgress = 0L;
            int count = simulation.QueuedActionCount;
            for (int i = 0; i < count; i++)
            {
                ShiftAction action = simulation.GetQueuedAction(i);
                if (IsGrillAction(action.Type) && action.Slot == slot)
                {
                    queuedAction = action.Type;
                    queuedProgress = i == 0 ? simulation.HeadActionProgressTicks : 0L;
                    break;
                }
            }

            GrillSlot state = grill.GetSlot(slot);
            return Compute(
                state.State,
                state.AgeTicks,
                grill.CookTicks,
                grill.BurnAfterDoneTicks,
                grill.WarnBeforeBurnTicks,
                queuedAction,
                queuedProgress,
                grill.ActionTicks);
        }

        /// <summary>Вигляд слота зі знімка зміни і параметрів гриля рівня.</summary>
        public static GrillSlotView FromSnapshot(ShiftSnapshot snapshot, GrillConfig config, int ticksPerSecond, int slot)
        {
            if (config == null)
            {
                throw new ArgumentNullException(nameof(config));
            }

            if (slot < 0 || slot >= snapshot.Grill.SlotCount)
            {
                throw new ArgumentOutOfRangeException(nameof(slot));
            }

            ShiftActionType queuedAction = ShiftActionType.None;
            long queuedProgress = 0L;
            ActionQueueSnapshot actions = snapshot.Actions;
            for (int i = 0; i < actions.Count; i++)
            {
                ShiftAction action = actions.GetAction(i);
                if (IsGrillAction(action.Type) && action.Slot == slot)
                {
                    queuedAction = action.Type;
                    queuedProgress = i == 0 ? actions.HeadProgressTicks : 0L;
                    break;
                }
            }

            return Compute(
                snapshot.Grill.GetState(slot),
                snapshot.Grill.GetAgeTicks(slot),
                config.CookTicks(ticksPerSecond),
                config.BurnAfterDoneTicks(ticksPerSecond),
                config.WarnBeforeBurnTicks(ticksPerSecond),
                queuedAction,
                queuedProgress,
                config.ActionTicks(ticksPerSecond));
        }

        /// <summary>
        /// Яку команду шле тап по слоту в цьому вигляді. false — тап нічого не шле: м'ясо ще
        /// кладуть або воно готується, або дія для слота вже в черзі.
        /// </summary>
        public static bool TryGetTap(GrillSlotView view, out ShiftTap tap)
        {
            tap = default(ShiftTap);
            if (view.ActionQueued)
            {
                return false;
            }

            switch (view.Display)
            {
                case GrillSlotDisplay.Empty:
                    tap = ShiftTap.PlaceMeat;
                    return true;
                case GrillSlotDisplay.Done:
                    tap = ShiftTap.TakeMeat;
                    return true;
                case GrillSlotDisplay.Burnt:
                    tap = ShiftTap.DiscardBurntMeat;
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>Ключ локалізації напису в слоті.</summary>
        public static string TextKeyOf(GrillSlotView view)
        {
            switch (view.Display)
            {
                case GrillSlotDisplay.Raw:
                    return TextKeys.GrillSlotRaw;
                case GrillSlotDisplay.Cooking:
                    return TextKeys.GrillSlotCooking;
                case GrillSlotDisplay.Done:
                    return view.BurnWarning ? TextKeys.GrillSlotBurning : TextKeys.GrillSlotDone;
                case GrillSlotDisplay.Burnt:
                    return TextKeys.GrillSlotBurnt;
                default:
                    return TextKeys.GrillSlotEmpty;
            }
        }

        private static bool IsGrillAction(ShiftActionType type)
        {
            return type == ShiftActionType.PlaceMeat
                || type == ShiftActionType.TakeMeat
                || type == ShiftActionType.DiscardBurntMeat;
        }

        private static int Percent(long part, long whole)
        {
            if (whole <= 0L || part <= 0L)
            {
                return 0;
            }

            return part >= whole ? FullPercent : (int)(part * FullPercent / whole);
        }
    }
}
