using Microsoft.Extensions.Configuration; 
using System.Reflection;
using Microsoft.EntityFrameworkCore;

namespace Lib;

public static class ConfigManager
{
    private static IConfigurationBuilder _builder;
    private static IConfigurationRoot _root;
    private static Dictionary<string, string>? _cache;

    static ConfigManager()
    {
        var executingAssemblyDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly()
            .Location);
        var appSettingsPath = $"{executingAssemblyDir}/appsettings.json";
        _builder = new ConfigurationBuilder()
            .AddJsonFile(appSettingsPath, optional: false, reloadOnChange: true);
        _root = _builder.Build();
    }

    private static Dictionary<string, string> FetchDbCache()
    {
        using var context = new PgContext();
        var configs = context.PgConfigValues
            .Select(x => new Tuple<string, string>(x.ConfigKey, x.ConfigValue))
            .ToList();
        Dictionary<string, string> dict = [];
        foreach (var config in configs)
        {
            dict.Add(config.Item1, config.Item2);
        }
        return dict;
    }
    private static string ReadSetting(string key)
    {
        // try local override from config file first
        var value = _root[key];
        if (value is not null) return value;
        
        // try to reach from db cache
        if (_cache is null) _cache = FetchDbCache(); // don't move this into the constructor because creating the context needs config from files
        if(!_cache.TryGetValue(key, out value)) throw new InvalidDataException();
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
    public static long ReadLongSetting(string key)
    {
        return Int64.Parse(ReadSetting(key));
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