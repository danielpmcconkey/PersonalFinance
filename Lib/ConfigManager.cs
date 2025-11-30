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
    
    /// <summary>
    /// warning: shouldForceDbCacheUpdate is not threadsafe. only use it outside of multi-threaded contexts 
    /// </summary>
    private static string ReadSetting(string key, bool shouldForceDbCacheUpdate = false)
    {
        // try local override from config file first
        var value = _root[key];
        if (value is not null) return value;
        
        // see if we need to update the db cache first
        if (shouldForceDbCacheUpdate) _cache = FetchDbCache();
        
        // try to reach from db cache
        if (_cache is null) _cache = FetchDbCache(); // don't move this into the constructor because creating the context needs config from files
        if(!_cache.TryGetValue(key, out value)) throw new InvalidDataException();
        return value;
    }
    
    /// <summary>
    /// warning: shouldForceDbCacheUpdate is not threadsafe. only use it outside of multi-threaded contexts 
    /// </summary>
    public static bool ReadBoolSetting(string key, bool shouldForceDbCacheUpdate = false)
    {
        return bool.Parse(ReadSetting(key, shouldForceDbCacheUpdate));
    }
    
    /// <summary>
    /// warning: shouldForceDbCacheUpdate is not threadsafe. only use it outside of multi-threaded contexts 
    /// </summary>
    public static int ReadIntSetting(string key, bool shouldForceDbCacheUpdate = false)
    {
        return int.Parse(ReadSetting(key, shouldForceDbCacheUpdate));
    }
    
    /// <summary>
    /// warning: shouldForceDbCacheUpdate is not threadsafe. only use it outside of multi-threaded contexts 
    /// </summary>
    public static decimal ReadDecimalSetting(string key, bool shouldForceDbCacheUpdate = false)
    {
        return decimal.Parse(ReadSetting(key, shouldForceDbCacheUpdate));
    }
    
    /// <summary>
    /// warning: shouldForceDbCacheUpdate is not threadsafe. only use it outside of multi-threaded contexts 
    /// </summary>
    public static long ReadLongSetting(string key, bool shouldForceDbCacheUpdate = false)
    {
        return Int64.Parse(ReadSetting(key, shouldForceDbCacheUpdate));
    }

    
    /// <summary>
    /// warning: shouldForceDbCacheUpdate is not threadsafe. only use it outside of multi-threaded contexts 
    /// </summary>
    public static string ReadStringSetting(string key, bool shouldForceDbCacheUpdate = false)
    {
        return ReadSetting(key, shouldForceDbCacheUpdate);
    }
    
    /// <summary>
    /// warning: shouldForceDbCacheUpdate is not threadsafe. only use it outside of multi-threaded contexts 
    /// </summary>
    public static NodaTime.LocalDateTime ReadDateSetting(string key, bool shouldForceDbCacheUpdate = false)
    {
        var dt = DateTime.Parse(ReadSetting(key, shouldForceDbCacheUpdate));
        return NodaTime.LocalDateTime.FromDateTime(dt);
    }
}