using System.Collections.Generic;

namespace KebabStreet.Services
{
    /// <summary>Реліз до Етапу 3: нічого не записує й не надсилає.</summary>
    public sealed class NullAnalyticsService : IAnalyticsService
    {
        public bool IsAvailable => false;

        public void LogEvent(string name, IReadOnlyDictionary<string, string> parameters)
        {
        }

        public void SetUserProperty(string name, string value)
        {
        }
    }
}
