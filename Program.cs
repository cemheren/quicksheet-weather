using System.Collections.Concurrent;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
/// QuickSheet Weather Extension — reads JSON-lines from stdin, writes JSON-lines to stdout.
/// Registers the "wthr" prefix and returns a 7-day forecast from Open-Meteo (free, no key).
/// Results are cached per location for 1 hour.
/// </summary>
class Program
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false
    };

    private static readonly HttpClient Http = new();

    private static readonly ConcurrentDictionary<string, (DateTime FetchedAt, ForecastData Data)> Cache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly TimeSpan CacheDuration = TimeSpan.FromHours(1);

    static void Main()
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;

        string? line;
        while ((line = Console.ReadLine()) != null)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;

            try
            {
                using var doc = JsonDocument.Parse(line);
                string? type = doc.RootElement.TryGetProperty("type", out var tp) ? tp.GetString() : null;

                switch (type)
                {
                    case "init":
                        HandleInit();
                        break;
                    case "activate":
                        HandleActivate(doc.RootElement);
                        break;
                }
            }
            catch (Exception ex)
            {
                SendError("", $"Parse error: {ex.Message}");
            }
        }
    }

    static void HandleInit()
    {
        var register = new
        {
            type = "register",
            prefix = "wthr",
            name = "Weather Forecast",
            version = "1.0.0"
        };
        SendJson(register);
        SendLog("Weather extension registered with prefix 'wthr'");
    }

    static void HandleActivate(JsonElement root)
    {
        string id = root.TryGetProperty("id", out var idProp) ? idProp.GetString() ?? "" : "";
        int gridCols = root.TryGetProperty("gridCols", out var gc) ? gc.GetInt32() : 2;
        int gridRows = root.TryGetProperty("gridRows", out var gr) ? gr.GetInt32() : 7;

        string[] extParams = [];
        if (root.TryGetProperty("params", out var paramsProp) && paramsProp.ValueKind == JsonValueKind.Array)
        {
            extParams = paramsProp.EnumerateArray()
                .Select(p => p.GetString() ?? "")
                .ToArray();
        }

        string location = extParams.Length > 0 ? extParams[0] : "New York";
        SendLog($"Generating forecast for '{location}', grid {gridCols}x{gridRows}");

        try
        {
            var forecast = GetForecast(location);
            var cells = BuildCells(forecast, gridCols, gridRows);

            SendJson(new { type = "write", id, cells });
        }
        catch (Exception ex)
        {
            SendError(id, $"Weather fetch failed: {ex.Message}");
        }
    }

    static ForecastData GetForecast(string location)
    {
        // Check cache
        if (Cache.TryGetValue(location, out var cached) && DateTime.UtcNow - cached.FetchedAt < CacheDuration)
        {
            SendLog($"Using cached forecast for '{location}'");
            return cached.Data;
        }

        SendLog($"Fetching live forecast for '{location}'");

        // Geocode location name to lat/lon via Open-Meteo
        var (lat, lon) = Geocode(location);

        // Fetch 7-day forecast
        string forecastUrl = $"https://api.open-meteo.com/v1/forecast?latitude={lat}&longitude={lon}" +
            "&daily=temperature_2m_max,temperature_2m_min,weathercode&temperature_unit=fahrenheit&timezone=auto";

        var response = Http.GetFromJsonAsync<JsonElement>(forecastUrl).GetAwaiter().GetResult();
        var daily = response.GetProperty("daily");

        var dates = daily.GetProperty("time").EnumerateArray().Select(d => d.GetString()!).ToArray();
        var maxTemps = daily.GetProperty("temperature_2m_max").EnumerateArray().Select(t => t.GetDouble()).ToArray();
        var minTemps = daily.GetProperty("temperature_2m_min").EnumerateArray().Select(t => t.GetDouble()).ToArray();
        var codes = daily.GetProperty("weathercode").EnumerateArray().Select(c => c.GetInt32()).ToArray();

        var data = new ForecastData
        {
            Days = dates.Select((date, i) => new ForecastDay
            {
                Date = DateOnly.Parse(date),
                MaxTemp = maxTemps[i],
                MinTemp = minTemps[i],
                WeatherCode = codes[i]
            }).ToArray()
        };

        Cache[location] = (DateTime.UtcNow, data);
        return data;
    }

    static (double Lat, double Lon) Geocode(string location)
    {
        string url = $"https://geocoding-api.open-meteo.com/v1/search?name={Uri.EscapeDataString(location)}&count=1";
        var response = Http.GetFromJsonAsync<JsonElement>(url).GetAwaiter().GetResult();

        if (!response.TryGetProperty("results", out var results) || results.GetArrayLength() == 0)
            throw new Exception($"Location '{location}' not found");

        var first = results[0];
        return (first.GetProperty("latitude").GetDouble(), first.GetProperty("longitude").GetDouble());
    }

    static object[] BuildCells(ForecastData forecast, int cols, int rows)
    {
        var cells = new List<object>();
        int count = Math.Min(rows, forecast.Days.Length);

        for (int r = 0; r < count; r++)
        {
            var day = forecast.Days[r];
            if (cols >= 1)
                cells.Add(new { r, c = 0, v = day.Date.ToString("ddd") });
            if (cols >= 2)
                cells.Add(new { r, c = 1, v = $"{WeatherIcon(day.WeatherCode)} {day.MaxTemp:F0}°/{day.MinTemp:F0}°F" });
        }

        return cells.ToArray();
    }

    static string WeatherIcon(int code) => code switch
    {
        0 => "☀️",
        1 or 2 => "🌤️",
        3 => "🌥️",
        45 or 48 => "🌫️",
        51 or 53 or 55 or 61 or 63 or 65 or 80 or 81 or 82 => "🌧️",
        71 or 73 or 75 or 77 or 85 or 86 => "🌨️",
        95 or 96 or 99 => "⛈️",
        _ => "🌡️"
    };

    static void SendJson(object obj)
    {
        string json = JsonSerializer.Serialize(obj, JsonOpts);
        Console.WriteLine(json);
        Console.Out.Flush();
    }

    static void SendError(string id, string message)
    {
        SendJson(new { type = "error", id, message });
    }

    static void SendLog(string message)
    {
        SendJson(new { type = "log", level = "info", message });
    }
}

record ForecastData
{
    public ForecastDay[] Days { get; init; } = [];
}

record ForecastDay
{
    public DateOnly Date { get; init; }
    public double MaxTemp { get; init; }
    public double MinTemp { get; init; }
    public int WeatherCode { get; init; }
}
