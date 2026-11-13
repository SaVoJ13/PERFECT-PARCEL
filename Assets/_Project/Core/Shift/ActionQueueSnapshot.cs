using System;
using System.Globalization;
using System.Text;

namespace KebabStreet.Core
{
    /// <summary>
    /// Знімок черги дій гравця: дії, що чекають (перша — та, що виконується), поступ першої
    /// дії, лічильники і результат останньої виконаної дії. Значення за замовчуванням —
    /// порожня черга без жодної виконаної дії.
    /// </summary>
    public readonly struct ActionQueueSnapshot : IEquatable<ActionQueueSnapshot>
    {
        private readonly ShiftAction[] _actions;

        /// <summary>Масив копіюється; null означає порожню чергу.</summary>
        public ActionQueueSnapshot(
            ShiftAction[] actions,
            long headProgressTicks,
            long executedCount,
            long ignoredCount,
            ShiftAction lastAction,
            ShiftActionResult lastResult)
        {
            _actions = actions == null || actions.Length == 0 ? null : (ShiftAction[])actions.Clone();
            HeadProgressTicks = headProgressTicks;
            ExecutedCount = executedCount;
            IgnoredCount = ignoredCount;
            LastAction = lastAction;
            LastResult = lastResult;
        }

        /// <summary>Скільки дій у черзі.</summary>
        public int Count => _actions == null ? 0 : _actions.Length;

        /// <summary>Скільки тактів уже відпрацьовано над першою дією черги.</summary>
        public long HeadProgressTicks { get; }

        /// <summary>Скільки дій виконано з результатом Done.</summary>
        public long ExecutedCount { get; }

        /// <summary>Скільки дій проігноровано під час виконання (результат не Done).</summary>
        public long IgnoredCount { get; }

        /// <summary>Остання дія, що дійшла до виконання.</summary>
        public ShiftAction LastAction { get; }

        /// <summary>Її результат.</summary>
        public ShiftActionResult LastResult { get; }

        /// <summary>Дія в черзі за номером (0 — перша).</summary>
        public ShiftAction GetAction(int index)
        {
            if (index < 0 || index >= Count)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            return _actions[index];
        }

        public bool Equals(ActionQueueSnapshot other)
        {
            if (Count != other.Count
                || HeadProgressTicks != other.HeadProgressTicks
                || ExecutedCount != other.ExecutedCount
                || IgnoredCount != other.IgnoredCount
                || !LastAction.Equals(other.LastAction)
                || LastResult != other.LastResult)
            {
                return false;
            }

            for (int i = 0; i < Count; i++)
            {
                if (!_actions[i].Equals(other._actions[i]))
                {
                    return false;
                }
            }

            return true;
        }

        public override bool Equals(object obj)
        {
            return obj is ActionQueueSnapshot other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Count;
                for (int i = 0; i < Count; i++)
                {
                    hash = (hash * 397) ^ _actions[i].GetHashCode();
                }

                hash = (hash * 397) ^ HeadProgressTicks.GetHashCode();
                hash = (hash * 397) ^ ExecutedCount.GetHashCode();
                hash = (hash * 397) ^ IgnoredCount.GetHashCode();
                hash = (hash * 397) ^ LastAction.GetHashCode();
                hash = (hash * 397) ^ (int)LastResult;
                return hash;
            }
        }

        public override string ToString()
        {
            var text = new StringBuilder("[");
            for (int i = 0; i < Count; i++)
            {
                if (i > 0)
                {
                    text.Append(' ');
                }

                text.Append(_actions[i].ToString());
            }

            text.Append(string.Format(
                CultureInfo.InvariantCulture,
                "] progress={0} executed={1} ignored={2} last={3}>{4}",
                HeadProgressTicks,
                ExecutedCount,
                IgnoredCount,
                LastAction,
                LastResult));
            return text.ToString();
        }
    }
}
