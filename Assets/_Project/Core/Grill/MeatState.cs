namespace KebabStreet.Core
{
    /// <summary>
    /// Стан слота гриля (GAME_DESIGN.md, розд. 4). Числові значення входять у знімок
    /// стану — не змінювати порядок.
    /// </summary>
    public enum MeatState
    {
        /// <summary>Слот порожній.</summary>
        Empty = 0,

        /// <summary>Сире: готується, брати не можна.</summary>
        Raw = 1,

        /// <summary>Готове: можна зняти на дошку; з часом пригорить.</summary>
        Done = 2,

        /// <summary>Пригоріле: займає слот, доки не викинуть.</summary>
        Burnt = 3,
    }
}
