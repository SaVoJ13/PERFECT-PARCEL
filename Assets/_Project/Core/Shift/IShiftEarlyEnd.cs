namespace KebabStreet.Core
{
    /// <summary>
    /// Необов'язкова можливість умови цілі: завершити зміну до кінця таймера. Симуляція питає
    /// після кожного такту; true — зміна переходить у Resolving і далі Won або Lost за
    /// <see cref="IShiftGoal.IsAchieved"/>. Має залежати лише від стану зміни.
    /// </summary>
    public interface IShiftEarlyEnd
    {
        bool ShouldEndEarly(ShiftSimulation shift);
    }
}
