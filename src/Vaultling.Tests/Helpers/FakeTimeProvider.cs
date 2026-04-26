namespace Vaultling.Tests.Helpers;

internal sealed class FakeTimeProvider(DateTime localNow) : TimeProvider
{
    private readonly DateTimeOffset _utcNow =
        new(DateTime.SpecifyKind(localNow, DateTimeKind.Utc));

    public override DateTimeOffset GetUtcNow() => _utcNow;

    public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
}
