using System.Collections.Generic;
using KebabStreet.Services;

namespace KebabStreet.Tests.EditMode
{
    /// <summary>Аналітика-журнал для тестів. Живе лише в тестовій збірці (Editor), у плеєр не потрапляє.</summary>
    public sealed class FakeAnalyticsService : IAnalyticsService
    {
        public bool IsAvailable { get; set; } = true;

        public sealed class LoggedEvent
        {
            public LoggedEvent(string name, IReadOnlyDictionary<string, string> parameters)
            {
                Name = name;
                Parameters = parameters;
            }

            public string Name { get; }

            public IReadOnlyDictionary<string, string> Parameters { get; }
        }

        public List<LoggedEvent> Events { get; } = new List<LoggedEvent>();

        public Dictionary<string, string> UserProperties { get; } = new Dictionary<string, string>();

        public void LogEvent(string name, IReadOnlyDictionary<string, string> parameters)
        {
            Events.Add(new LoggedEvent(name, parameters));
        }

        public void SetUserProperty(string name, string value)
        {
            UserProperties[name] = value;
        }
    }
}
