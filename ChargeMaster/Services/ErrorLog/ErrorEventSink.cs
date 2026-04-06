using Serilog.Core;
using Serilog.Events;

namespace ChargeMaster.Services.ErrorLog;

/// <summary>
/// Custom Serilog sink som fångar LogError-meddelanden och publicerar dem som events.
/// </summary>
public class ErrorEventSink(Action<ErrorLogEventArgs> errorHandler) : ILogEventSink
{
    /// <summary>
    /// Tar emot ett logg-event och publicerar det vidare om nivån är Error eller Fatal.
    /// </summary>
    /// <param name="logEvent">Logg-eventet från Serilog.</param>
    public void Emit(LogEvent logEvent)
    {
        // Fånga endast Error och Fatal
        if (logEvent.Level != LogEventLevel.Error && logEvent.Level != LogEventLevel.Fatal)
            return;

        var message = logEvent.MessageTemplate.Render(logEvent.Properties);
        var source = logEvent.Properties.TryGetValue("SourceContext", out var sourceContext)
            ? sourceContext.ToString().Trim('"')
            : "Unknown";

        var errorArgs = new ErrorLogEventArgs
        {
            Timestamp = logEvent.Timestamp.UtcDateTime,
            Message = message,
            Exception = logEvent.Exception,
            Source = source
        };

        errorHandler(errorArgs);
    }
}
