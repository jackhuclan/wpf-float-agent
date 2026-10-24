using System.ComponentModel;
using System.Globalization;
using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace Floaticon.Tools;

/// <summary>
/// 天气查询工具（数据来源 Open-Meteo，免费且无需 API Key）。
/// 方法通过 <see cref="Microsoft.Extensions.AI.AIFunctionFactory"/> 包装成 get_weather 函数，
/// 由 IAIToolService 注册后交给 Agent 自动调用。
/// </summary>
public sealed class WeatherTool
{
    private const string GeocodingEndpoint = "https://geocoding-api.open-meteo.com/v1/search";
    private const string ForecastEndpoint = "https://api.open-meteo.com/v1/forecast";

    private readonly HttpClient _httpClient;

    public WeatherTool(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    /// <summary>查询城市实时天气与未来三天预报。</summary>
    [Description("查询指定城市当前的实时天气和未来三天天气预报。当用户询问天气、气温、是否下雨、是否需要带伞时调用。")]
    public async Task<string> GetWeatherAsync(
        [Description("城市名称，中文或英文均可，例如：北京、上海、Shenzhen")] string city,
        [Description("温度单位：celsius（摄氏度，默认）或 fahrenheit（华氏度）")] string? unit = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(city))
        {
            return "缺少城市名称，无法查询天气。";
        }

        var fahrenheit = string.Equals(unit?.Trim(), "fahrenheit", StringComparison.OrdinalIgnoreCase)
                         || string.Equals(unit?.Trim(), "f", StringComparison.OrdinalIgnoreCase);

        try
        {
            var location = await GeocodeAsync(city.Trim(), cancellationToken).ConfigureAwait(false);
            if (location is null)
            {
                return $"没有找到城市“{city}”的坐标，请换一个更常见的城市名称再试。";
            }

            return await QueryForecastAsync(location.Value, fahrenheit, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (HttpRequestException ex)
        {
            return $"天气服务请求失败：{ex.Message}（请检查网络连接）";
        }
        catch (JsonException)
        {
            return "天气服务返回的数据无法解析，请稍后重试。";
        }
    }

    private async Task<Location?> GeocodeAsync(string city, CancellationToken cancellationToken)
    {
        var url = $"{GeocodingEndpoint}?name={Uri.EscapeDataString(city)}&count=1&language=zh&format=json";
        using var document = await GetJsonAsync(url, cancellationToken).ConfigureAwait(false);

        if (!document.RootElement.TryGetProperty("results", out var results)
            || results.ValueKind != JsonValueKind.Array
            || results.GetArrayLength() == 0)
        {
            return null;
        }

        var first = results[0];
        return new Location(
            first.GetProperty("latitude").GetDouble(),
            first.GetProperty("longitude").GetDouble(),
            first.TryGetProperty("name", out var name) ? name.GetString() ?? city : city,
            first.TryGetProperty("country", out var country) ? country.GetString() : null,
            first.TryGetProperty("admin1", out var admin1) ? admin1.GetString() : null,
            first.TryGetProperty("timezone", out var tz) ? tz.GetString() : null);
    }

    private async Task<string> QueryForecastAsync(Location location, bool fahrenheit, CancellationToken cancellationToken)
    {
        var unitParam = fahrenheit ? "fahrenheit" : "celsius";
        var url = $"{ForecastEndpoint}?latitude={location.Latitude.ToString(CultureInfo.InvariantCulture)}" +
                  $"&longitude={location.Longitude.ToString(CultureInfo.InvariantCulture)}" +
                  "&current=temperature_2m,relative_humidity_2m,apparent_temperature,precipitation,weather_code,wind_speed_10m" +
                  "&daily=weather_code,temperature_2m_max,temperature_2m_min,precipitation_probability_max" +
                  $"&timezone=auto&forecast_days=4&temperature_unit={unitParam}&wind_speed_unit=kmh";

        using var document = await GetJsonAsync(url, cancellationToken).ConfigureAwait(false);
        var root = document.RootElement;

        var unitSymbol = fahrenheit ? "°F" : "°C";
        var builder = new StringBuilder();

        var placeName = string.Join(" ", new[] { location.Country, location.Admin1, location.Name }
            .Where(part => !string.IsNullOrWhiteSpace(part)));

        builder.AppendLine($"**{placeName} 天气**");
        if (!string.IsNullOrWhiteSpace(location.TimeZone))
        {
            builder.AppendLine($"时区：{location.TimeZone}");
        }

        if (root.TryGetProperty("current", out var current))
        {
            builder.AppendLine();
            builder.AppendLine($"实时：{Describe(current.GetProperty("weather_code").GetInt32())}，" +
                               $"气温 {Format(current, "temperature_2m")}{unitSymbol}" +
                               $"（体感 {Format(current, "apparent_temperature")}{unitSymbol}），" +
                               $"湿度 {Format(current, "relative_humidity_2m")}%，" +
                               $"风速 {Format(current, "wind_speed_10m")} km/h，" +
                               $"降水 {Format(current, "precipitation")} mm。");
        }

        if (root.TryGetProperty("daily", out var daily)
            && daily.TryGetProperty("time", out var dates))
        {
            builder.AppendLine();
            builder.AppendLine("未来几天：");

            var weatherCodes = daily.GetProperty("weather_code");
            var maxes = daily.GetProperty("temperature_2m_max");
            var mins = daily.GetProperty("temperature_2m_min");
            var pops = daily.GetProperty("precipitation_probability_max");

            for (var i = 0; i < dates.GetArrayLength(); i++)
            {
                builder.AppendLine(
                    $"- {dates[i].GetString()}：{Describe(ValueAt(weatherCodes, i))}，" +
                    $"{ValueAtText(mins, i)}{unitSymbol} ~ {ValueAtText(maxes, i)}{unitSymbol}，" +
                    $"降水概率 {ValueAtText(pops, i)}%");
            }
        }

        builder.AppendLine();
        builder.Append("数据来源：Open-Meteo");
        return builder.ToString().TrimEnd();
    }

    private async Task<JsonDocument> GetJsonAsync(string url, CancellationToken cancellationToken)
    {
        using var response = await _httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        return await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    private static string Format(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value))
        {
            return "-";
        }

        return value.ValueKind switch
        {
            JsonValueKind.Number => Math.Round(value.GetDouble()).ToString("0", CultureInfo.InvariantCulture),
            JsonValueKind.String => value.GetString() ?? "-",
            _ => "-"
        };
    }

    private static int ValueAt(JsonElement array, int index)
        => index < array.GetArrayLength() && array[index].ValueKind == JsonValueKind.Number
            ? array[index].GetInt32()
            : -1;

    private static string ValueAtText(JsonElement array, int index)
    {
        if (index >= array.GetArrayLength())
        {
            return "-";
        }

        var item = array[index];
        return item.ValueKind == JsonValueKind.Number
            ? Math.Round(item.GetDouble()).ToString("0", CultureInfo.InvariantCulture)
            : "-";
    }

    /// <summary>WMO 天气代码 → 中文描述。</summary>
    private static string Describe(int code) => code switch
    {
        0 => "晴",
        1 => "晴间多云",
        2 => "多云",
        3 => "阴",
        45 or 48 => "雾",
        51 or 53 or 55 => "毛毛雨",
        56 or 57 => "冻毛毛雨",
        61 => "小雨",
        63 => "中雨",
        65 => "大雨",
        66 or 67 => "冻雨",
        71 => "小雪",
        73 => "中雪",
        75 => "大雪",
        77 => "米雪",
        80 => "小阵雨",
        81 => "阵雨",
        82 => "强阵雨",
        85 or 86 => "阵雪",
        95 => "雷阵雨",
        96 or 99 => "雷暴伴冰雹",
        _ => "未知天气"
    };

    private readonly record struct Location(
        double Latitude,
        double Longitude,
        string Name,
        string? Country,
        string? Admin1,
        string? TimeZone);
}
