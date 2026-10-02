using System;
using System.IO;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Model.Serialization;

namespace Jellyfin.Plugin.JellyMetaLocal.Tests;

public class MockApplicationPaths : IApplicationPaths
{
    public string ProgramDataPath => Path.GetTempPath();
    public string WebPath => Path.GetTempPath();
    public string ProgramSystemPath => Path.GetTempPath();
    public string DataPath => Path.GetTempPath();
    public string PluginsPath => Path.GetTempPath();
    public string PluginConfigurationsPath => Path.GetTempPath();
    public string LogDirectoryPath => Path.GetTempPath();
    public string ConfigurationDirectoryPath => Path.GetTempPath();
    public string SystemConfigurationFilePath => Path.GetTempPath();
    public string CachePath => Path.GetTempPath();
    public string TempDirectory => Path.GetTempPath();
    public string VirtualDataPath => Path.GetTempPath();
    public string ImageCachePath => Path.GetTempPath();
    public string TrickplayPath => Path.GetTempPath();
    public string BackupPath => Path.GetTempPath();
    public void MakeSanityCheckOrThrow() { }
    public void CreateAndCheckMarker(string path, string marker, bool delete) { }
}

public class MockXmlSerializer : IXmlSerializer
{
    public object DeserializeFromBytes(Type type, byte[] buffer) => Activator.CreateInstance(type)!;
    public object DeserializeFromFile(Type type, string file) => Activator.CreateInstance(type)!;
    public object DeserializeFromStream(Type type, Stream stream) => Activator.CreateInstance(type)!;
    public object DeserializeFromString(Type type, string text) => Activator.CreateInstance(type)!;
    public void SerializeToFile(object obj, string file) { }
    public void SerializeToStream(object obj, Stream stream) { }
}
