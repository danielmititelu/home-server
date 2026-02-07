namespace Vaultling.Services.Repositories;

public class ErrorRepository(IOptions<ErrorOptions> options, TimeProvider timeProvider)
{
    private readonly ErrorOptions _options = options.Value;

    public void WriteErrorLog(Exception exception)
    {
        var errorFileName = $"{timeProvider.GetLocalNow().ToIsoDateString()}-error.md";
        var errorFilePath = Path.Combine(_options.LogDirectory, errorFileName);

        var errorContent = $"""
            # Error Log - {timeProvider.GetLocalNow():yyyy-MM-dd HH:mm:ss}

            ## Exception Type
            {exception.GetType().FullName}

            ## Message
            {exception.Message}

            ## Stack Trace
            ```
            {exception.StackTrace}
            ```
            """;

        File.WriteAllText(errorFilePath, errorContent);
    }
}
