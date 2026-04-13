namespace Vaultling.Services.Repositories;

using System.Net.Http.Json;
using System.Text.Json;
using Vaultling.Models;

public class WeatherRepository(HttpClient httpClient)
{
    public async Task<WeatherInfo?> FetchWeatherAsync(string city)
    {
        if (string.IsNullOrWhiteSpace(city))
            return null;

        try
        {
            var lat = 0.0;
            var lon = 0.0;

            var geoUrl = $"https://geocoding-api.open-meteo.com/v1/search?name={Uri.EscapeDataString(city)}&count=1&language=en&format=json";
            var geoResponse = await httpClient.GetFromJsonAsync<JsonElement>(geoUrl);

            if (!geoResponse.TryGetProperty("results", out var results) || results.GetArrayLength() == 0)
                return new WeatherInfo(City: city, Summary: null, Sunrise: null, Sunset: null);

            var location = results[0];
            lat = location.GetProperty("latitude").GetDouble();
            lon = location.GetProperty("longitude").GetDouble();

            var forecastUrl = $"https://api.open-meteo.com/v1/forecast" +
                $"?latitude={lat.ToString(System.Globalization.CultureInfo.InvariantCulture)}" +
                $"&longitude={lon.ToString(System.Globalization.CultureInfo.InvariantCulture)}" +
                $"&current=temperature_2m,weather_code" +
                $"&daily=sunrise,sunset" +
                $"&timezone=auto" +
                $"&forecast_days=1";

            var forecastResponse = await httpClient.GetFromJsonAsync<JsonElement>(forecastUrl);

            var current = forecastResponse.GetProperty("current");
            var temp = (int)Math.Round(current.GetProperty("temperature_2m").GetDouble());
            var code = current.GetProperty("weather_code").GetInt32();

            var (emoji, description) = WmoWeatherCode.Resolve(code);
            var summary = $"{emoji} {temp}°C, {description}";

            var daily = forecastResponse.GetProperty("daily");
            var sunriseRaw = daily.GetProperty("sunrise")[0].GetString() ?? "";
            var sunsetRaw = daily.GetProperty("sunset")[0].GetString() ?? "";

            // Sunrise/sunset come back as "2026-04-12T06:30" - extract just HH:mm
            var sunrise = sunriseRaw.Length >= 16 ? sunriseRaw[11..16] : sunriseRaw;
            var sunset = sunsetRaw.Length >= 16 ? sunsetRaw[11..16] : sunsetRaw;

            return new WeatherInfo(City: city, Summary: summary, Sunrise: sunrise, Sunset: sunset);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to fetch weather for {city}. Returning null weather info. Error: {ex.Message}");
            return new WeatherInfo(City: city, Summary: null, Sunrise: null, Sunset: null);
        }
    }
}
