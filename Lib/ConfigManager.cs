using Microsoft.Extensions.Configuration; 
using System.Reflection;
namespace Lib;

public static class ConfigManager
{
    private static IConfigurationBuilder _builder;
    private static IConfigurationRoot _root;

    static ConfigManager()
    {
        var executingAssemblyDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly()
            .Location);
        var appSettingsPath = $"{executingAssemblyDir}/appsettings.json";
        _builder = new ConfigurationBuilder()
            .AddJsonFile(appSettingsPath, optional: false, reloadOnChange: true);
        _root = _builder.Build();
    }
    private static string ReadSetting(string key)
    {
        var value = _root[key];
        if (value is null) throw new InvalidDataException();
        return value;
    }
    public static bool ReadBoolSetting(string key)
    {
        return bool.Parse(ReadSetting(key));
    }
    public static int ReadIntSetting(string key)
    {
        return int.Parse(ReadSetting(key));
    }
    public static decimal ReadDecimalSetting(string key)
    {
        return decimal.Parse(ReadSetting(key));
    }

    public static string ReadStringSetting(string key)
    {
        return ReadSetting(key);
    }
    public static NodaTime.LocalDateTime ReadDateSetting(string key)
    {
        var dt = DateTime.Parse(ReadSetting(key));
        return NodaTime.LocalDateTime.FromDateTime(dt);
    }
}