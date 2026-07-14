using System.IO;
using System.Text;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace DocMgr.Infrastructure.Schema;

/// <summary>
/// SchemaDictionary.yaml 读写。
/// </summary>
public static class SchemaDictionaryYaml
{
    private static readonly ISerializer Serializer = new SerializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitNull)
        .Build();

    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    /// <summary>
    /// 从磁盘加载字典；文件不存在时返回空文档。
    /// </summary>
    public static SchemaDictionaryDocument LoadOrCreate(string path)
    {
        if (!File.Exists(path))
        {
            return new SchemaDictionaryDocument();
        }

        var yaml = File.ReadAllText(path, Encoding.UTF8);
        var document = Deserializer.Deserialize<SchemaDictionaryDocument>(yaml);
        document.Tables ??= new Dictionary<string, SchemaDictionaryTableEntry>(StringComparer.Ordinal);
        return document;
    }

    /// <summary>
    /// 保存字典到磁盘。
    /// </summary>
    public static void Save(string path, SchemaDictionaryDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        document.GeneratedAt = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss'Z'");
        var yaml = Serializer.Serialize(document);
        File.WriteAllText(path, yaml, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }
}
