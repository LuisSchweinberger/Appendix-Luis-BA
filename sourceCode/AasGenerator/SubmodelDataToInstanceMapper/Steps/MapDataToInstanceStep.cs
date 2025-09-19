using System;
using System.Threading;
using System.Threading.Tasks;
using MnestixCore.AasGenerator.Interfaces;
using MnestixCore.Errors;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace MnestixCore.AasGenerator.Pipelines.Steps;

public sealed class MapDataToInstanceAasGeneratorPipelineStep : IPipelineStep<SubmodelMappingContext>
{
    public Task<SubmodelMappingContext> ExecuteAsync(SubmodelMappingContext ctx)
    {
        ctx.Log($"Started MapDataToInstanceStep");
        MapDataToInstance(ctx);
        ctx.Log($"Finished MapDataToInstanceStep");
        return Task.FromResult(ctx);
    }

    private static JArray ConvertToMultiLanguageProperty(JToken text, string language)
    {
        return new JArray
        {
            new JObject
            {
                {"text", text},
                {"language", language}
            }
        };
    }

    private static void AssignJsonValueToTemplate(JToken templateValue, JToken dataFromMappingPath, JToken modelType, string language)
    {
        if (modelType.Value<string>() == "MultiLanguageProperty")
        {
            templateValue.Replace(ConvertToMultiLanguageProperty(dataFromMappingPath, language));
            return;
        }

        templateValue.Replace(dataFromMappingPath);
    }

    private static JToken? SelectTokenFromDataJson(JToken dataJson, string mappingPath, SubmodelMappingContext ctx)
    {
        try
        {

            return dataJson.SelectToken(mappingPath);
        }
        catch (JsonException e)
        {
            throw new SubmodelDataToInstanceMapperException($"Error while trying to get data from mapping path {mappingPath}: " + e.Message, e, ctx);
        }
    }

    private static void CheckIfValueKeyExists(JToken templateValue)
    {
        /*
        As per the v3 standard, "value" in MultiLanguageProperty has Cardinality "0..1,"
        indicating potential absence in custom submodel data. We handle this by creating
        an empty value for the key "value" during mapping, ensuring smooth mapping without exceptions.
        */
        var parent = templateValue.Parent?.Parent?.Parent;
        if (parent?["value"] != null) return;
        if (parent != null) parent["value"] = "";
    }

    private static JToken? GetCardinalityQualifier(JToken qualifier)
    {
        // qualifier.parent is the "qualifiers" array
        return qualifier.Parent?.SelectToken("[?(@.type=='SMT/Cardinality')]");
    }

    private static void MapDataToInstance(SubmodelMappingContext ctx)
    {
        var submodelInstance = ctx.SubmodelInstance;
        var data = ctx.Data;
        var language = ctx.Language;
        
        var qualifiers = submodelInstance.SelectTokens("$..qualifiers[?(@.type=='SMT/MappingInfo')]");

        foreach (var qualifier in qualifiers)
        {
            ctx.Qualifier = qualifier;
            var modelType = qualifier.Parent?.Parent?.Parent?["modelType"] ?? throw new SubmodelDataToInstanceMapperException("could not find matching modelType field of a qualify object", ctx);
            if (modelType.Value<string>() == "MultiLanguageProperty") CheckIfValueKeyExists(qualifier);
            var templateValue = qualifier.Parent?.Parent?.Parent?["value"] ?? throw new SubmodelDataToInstanceMapperException("could not find matching value field of a qualify object", ctx);
            var mappingPath = qualifier["value"]?.Value<string>() ?? throw new SubmodelDataToInstanceMapperException("Mapping Info cannot be null", ctx);
            var isMandatory = GetCardinalityQualifier(qualifier)?["value"]?.Value<string>()?.StartsWith("One") ?? false;
            var dataFromMappingPath = SelectTokenFromDataJson(data, mappingPath, ctx);

            // If no data is found and the mapping is mandatory an error will be thrown
            if (dataFromMappingPath == null)
            {
                if (isMandatory)
                {
                    throw new SubmodelDataToInstanceMapperException($"Mandatory mapping '{mappingPath}' not found.", ctx);
                }
                else
                {
                    continue;
                }
            }
            AssignJsonValueToTemplate(templateValue, dataFromMappingPath, modelType, language);
            ctx.Log($"Succesfully mapped data from path '{mappingPath}'");


        }
    }
}
