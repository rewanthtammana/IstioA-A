using System.Text.Json;

public static class JsonCfg
{
    public static JsonSerializerOptions Options = new JsonSerializerOptions
    {
        PropertyNameCaseInsensitive = true
    };
}