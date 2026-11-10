namespace KebabStreet.Core
{
    /// <summary>
    /// Стани зміни (GAME_DESIGN.md, розд. 1). Час зміни йде лише в <see cref="Running"/>.
    /// Числові значення входять у знімок стану — не змінювати порядок.
    /// </summary>
    public enum ShiftState
    {
        /// <summary>Показ цілі; чекає команди Start.</summary>
        Intro = 0,

        /// <summary>Таймер іде.</summary>
        Running = 1,

        /// <summary>Пауза гравця: усі таймери стоять.</summary>
        Paused = 2,

        /// <summary>Час вийшов, перевіряється ціль (у межах того самого такту).</summary>
        Resolving = 3,

        /// <summary>Ціль виконано.</summary>
        Won = 4,

        /// <summary>Ціль не виконано; можливе одне продовження.</summary>
        Lost = 5,
    }
}
