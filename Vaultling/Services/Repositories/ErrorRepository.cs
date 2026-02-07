namespace Vaultling.Services.Repositories;

using Microsoft.Extensions.Options;
using Vaultling.Configuration;
using Vaultling.Models;

public class ErrorRepository(IOptions<ErrorOptions> options, TimeProvider timeProvider)
{
    private readonly ErrorOptions _options = options.Value;

    public void WriteErrorLog(Exception exception)
    {
        var errorFileName = $"{timeProvider.GetUtcNow().ToIsoDateString()}-error.md";
        var errorFilePath = Path.Combine(_options.LogDirectory, errorFileName);

        var errorContent = $"""
            # Error Log - {timeProvider.GetUtcNow():yyyy-MM-dd HH:mm:ss} UTC

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
