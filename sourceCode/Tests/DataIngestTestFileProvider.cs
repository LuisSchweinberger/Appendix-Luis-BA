using System.IO;
using System.Reflection;
using Newtonsoft.Json.Linq;

namespace Core.Tests.AasGenerator;

public static class DataIngestTestFileProvider
{
    private static string GetBasePath()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var assemblyLocation = assembly.Location;
        var assemblyDirectory = Path.GetDirectoryName(assemblyLocation)!;
        return Path.Combine(assemblyDirectory, "AasGenerator", "TestJsons");
    }

    public static JObject GetTemplateSubmodel(string testCase)
    {
        var filePath = Path.Combine(GetBasePath(), testCase, "TemplateSubmodel.json");
        var content = File.ReadAllText(filePath);
        return JObject.Parse(content);
    }

    public static JObject GetData(string testCase)
    {
        var filePath = Path.Combine(GetBasePath(), testCase, "Data.json");
        var content = File.ReadAllText(filePath);
        return JObject.Parse(content);
    }

    public static JObject? GetExpectedResult(string testCase)
    {
        var filePath = Path.Combine(GetBasePath(), testCase, "ExpectedResult.json");
        if (!File.Exists(filePath)) return null;
        
        var content = File.ReadAllText(filePath);
        if (string.IsNullOrWhiteSpace(content) || content.Trim() == "null") return null;
        
        return JObject.Parse(content);
    }
}
