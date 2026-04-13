namespace Vaultling.Models;

public record WeatherInfo(string City, string? Summary, string? Sunrise, string? Sunset);

public static class WmoWeatherCode
{
    private static readonly Dictionary<int, (string Emoji, string Description)> Codes = new()
    {
        { 0,  ("☀️", "Clear sky") },
        { 1,  ("🌤️", "Mainly clear") },
        { 2,  ("⛅", "Partly cloudy") },
        { 3,  ("☁️", "Overcast") },
        { 45, ("🌫️", "Fog") },
        { 48, ("🌫️", "Icy fog") },
        { 51, ("🌦️", "Light drizzle") },
        { 53, ("🌦️", "Drizzle") },
        { 55, ("🌦️", "Heavy drizzle") },
        { 61, ("🌧️", "Light rain") },
        { 63, ("🌧️", "Rain") },
        { 65, ("🌧️", "Heavy rain") },
        { 71, ("🌨️", "Light snow") },
        { 73, ("🌨️", "Snow") },
        { 75, ("🌨️", "Heavy snow") },
        { 77, ("🌨️", "Snow grains") },
        { 80, ("🌦️", "Light showers") },
        { 81, ("🌦️", "Showers") },
        { 82, ("⛈️", "Heavy showers") },
        { 85, ("🌨️", "Snow showers") },
        { 86, ("🌨️", "Heavy snow showers") },
        { 95, ("⛈️", "Thunderstorm") },
        { 96, ("⛈️", "Thunderstorm with hail") },
        { 99, ("⛈️", "Heavy thunderstorm with hail") },
    };

    public static (string Emoji, string Description) Resolve(int code)
    {
        if (Codes.TryGetValue(code, out var result))
            return result;
        // WMO codes are sometimes reported as the nearest even tens boundary
        var rounded = code / 10 * 10;
        return Codes.TryGetValue(rounded, out var fallback) ? fallback : ("🌡️", "Unknown");
    }
}
