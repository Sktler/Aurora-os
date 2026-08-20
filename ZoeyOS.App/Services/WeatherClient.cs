using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace ZoeyOS.App.Services
{
    /// <summary>
    /// Real-time weather via Open-Meteo (open-meteo.com) - completely free, no API key,
    /// no account, no rate-limit worries for personal use. Geocodes a place name first,
    /// then pulls current conditions for those coordinates.
    /// </summary>
    public class WeatherClient
    {
        private readonly HttpClient _http = new();

        public async Task<string> GetCurrentWeatherAsync(string place)
        {
            if (string.IsNullOrWhiteSpace(place))
                return "No location given.";

            try
            {
                var geoUrl = $"https://geocoding-api.open-meteo.com/v1/search?name={Uri.EscapeDataString(place)}&count=1";
                var geoText = await _http.GetStringAsync(geoUrl);
                using var geoDoc = JsonDocument.Parse(geoText);

                if (!geoDoc.RootElement.TryGetProperty("results", out var results) || results.GetArrayLength() == 0)
                    return $"Couldn't find a location matching \"{place}\".";

                var first = results[0];
                var lat = first.GetProperty("latitude").GetDouble();
                var lon = first.GetProperty("longitude").GetDouble();
                var resolvedName = first.TryGetProperty("name", out var n) ? n.GetString() : place;
                var country = first.TryGetProperty("country", out var c) ? c.GetString() : "";

                var weatherUrl = $"https://api.open-meteo.com/v1/forecast?latitude={lat}&longitude={lon}" +
                                  "&current=temperature_2m,apparent_temperature,relative_humidity_2m,precipitation,weather_code,wind_speed_10m" +
                                  "&temperature_unit=fahrenheit&wind_speed_unit=mph&precipitation_unit=inch";
                var weatherText = await _http.GetStringAsync(weatherUrl);
                using var weatherDoc = JsonDocument.Parse(weatherText);

                var current = weatherDoc.RootElement.GetProperty("current");
                var temp = current.GetProperty("temperature_2m").GetDouble();
                var feelsLike = current.GetProperty("apparent_temperature").GetDouble();
                var humidity = current.GetProperty("relative_humidity_2m").GetDouble();
                var wind = current.GetProperty("wind_speed_10m").GetDouble();
                var precip = current.GetProperty("precipitation").GetDouble();
                var code = current.GetProperty("weather_code").GetInt32();

                return $"Weather in {resolvedName}, {country}: {DescribeCode(code)}, {temp:0}°F (feels like {feelsLike:0}°F), " +
                       $"{humidity:0}% humidity, wind {wind:0} mph, precipitation {precip:0.00} in.";
            }
            catch (Exception ex)
            {
                return $"Couldn't get the weather: {ex.Message}";
            }
        }

        // WMO weather codes (used by Open-Meteo) - the common ones.
        private static string DescribeCode(int code) => code switch
        {
            0 => "clear sky",
            1 or 2 or 3 => "partly cloudy",
            45 or 48 => "foggy",
            51 or 53 or 55 => "drizzle",
            56 or 57 => "freezing drizzle",
            61 or 63 or 65 => "rain",
            66 or 67 => "freezing rain",
            71 or 73 or 75 => "snow",
            77 => "snow grains",
            80 or 81 or 82 => "rain showers",
            85 or 86 => "snow showers",
            95 => "thunderstorm",
            96 or 99 => "thunderstorm with hail",
            _ => "unknown conditions"
        };
    }
}
