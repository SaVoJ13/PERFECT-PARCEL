namespace KebabStreet.Core
{
    /// <summary>
    /// Умова цілі зміни. Викликається один раз на кожне завершення таймера, коли зміна
    /// у стані <see cref="ShiftState.Resolving"/>. Має залежати лише від стану зміни
    /// та її генератора — інакше детермінізм втрачається.
    /// </summary>
    public interface IShiftGoal
    {
        /// <summary>true — зміна переходить у Won, false — у Lost.</summary>
        bool IsAchieved(ShiftSimulation shift);
    }
}
