namespace KebabStreet.Tests.EditMode
{
    /// <summary>
    /// Профіль бота-гравця (S1-05). «Досвідчений» не робить пауз між діями і не помиляється;
    /// «новачок» після кожної дії зависає на час реакції, іноді відволікається і помиляється
    /// (випадковість — лише з генератора бота, заданого seed).
    /// </summary>
    internal sealed class BotProfile
    {
        public BotProfile(string name, int reactionTicks, int distractionTicks, int mistakePermille)
        {
            Name = name;
            ReactionTicks = reactionTicks;
            DistractionTicks = distractionTicks;
            MistakePermille = mistakePermille;
        }

        /// <summary>Коротка назва для звітів.</summary>
        public string Name { get; }

        /// <summary>Скільки тактів бот чекає після кожної дії, перш ніж вирішити наступну.</summary>
        public int ReactionTicks { get; }

        /// <summary>Скільки тактів триває «відволікання» (помилка-пауза).</summary>
        public int DistractionTicks { get; }

        /// <summary>Імовірність помилки на кожне рішення, у тисячних частках (0 — не помиляється).</summary>
        public int MistakePermille { get; }

        /// <summary>Мінімальні паузи: рішення без затримки, без помилок.</summary>
        public static BotProfile Expert
        {
            get { return new BotProfile("expert", 0, 0, 0); }
        }

        /// <summary>Затримка реакції 0,75 с (при 60 тактах/с), відволікання 2 с, помилка в 8% рішень.</summary>
        public static BotProfile Novice
        {
            get { return new BotProfile("novice", 45, 120, 80); }
        }
    }
}
