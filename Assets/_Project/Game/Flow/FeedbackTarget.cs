namespace KebabStreet.Game
{
    /// <summary>Що саме на екрані зміни підсвічується або показує смужку дії (S1-21).</summary>
    public enum FeedbackTarget
    {
        None = 0,
        GrillSlot = 1,

        /// <summary>Кнопка станції; індекс — номер інгредієнта.</summary>
        Station = 2,

        /// <summary>Інгредієнт на дошці; індекс — місце на дошці.</summary>
        BoardItem = 3,

        /// <summary>Кнопка «Очистити дошку» (смітник); індекс завжди 0.</summary>
        Clear = 4,

        /// <summary>Картка клієнта (видача); індекс — слот.</summary>
        Customer = 5,
    }
}
