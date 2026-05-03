using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
/// QuickSheet Weather Extension — reads JSON-lines from stdin, writes JSON-lines to stdout.
/// Registers the "wthr" prefix and returns mock 7-day forecast data.
/// </summary>
class Program
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false
    };

    static void Main()
    {
        // Ensure stdout is not buffered
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
        // Respond with register message
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

        string location = extParams.Length > 0 ? extParams[0] : "unknown";
        SendLog($"Generating forecast for '{location}', grid {gridCols}x{gridRows}");

        // Generate mock weather data
        var cells = GenerateMockForecast(location, gridCols, gridRows);

        var write = new
        {
            type = "write",
            id,
            cells
        };
        SendJson(write);
    }

    static object[] GenerateMockForecast(string location, int cols, int rows)
    {
        string[] days = ["Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun"];
        string[] conditions = ["☀️ 75°F", "🌤️ 72°F", "🌧️ 65°F", "⛈️ 60°F", "🌤️ 70°F", "☀️ 78°F", "🌥️ 68°F"];

        var cells = new List<object>();

        for (int r = 0; r < Math.Min(rows, 7); r++)
        {
            if (cols >= 1)
                cells.Add(new { r, c = 0, v = days[r % days.Length] });
            if (cols >= 2)
                cells.Add(new { r, c = 1, v = conditions[r % conditions.Length] });
        }

        return cells.ToArray();
    }

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
