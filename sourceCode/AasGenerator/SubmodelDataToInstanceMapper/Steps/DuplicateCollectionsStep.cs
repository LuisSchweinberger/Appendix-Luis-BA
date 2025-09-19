using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using MnestixCore.AasGenerator.Interfaces;
using MnestixCore.Errors;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ZstdSharp.Unsafe;

namespace MnestixCore.AasGenerator.Pipelines.Steps;

public sealed class DuplicateCollectionsAasGeneratorPipelineStep : IPipelineStep<SubmodelMappingContext>
{
    public Task<SubmodelMappingContext> ExecuteAsync(SubmodelMappingContext ctx)
    {
        ctx.Log($"Started DuplicateCollectionsStep");
        DuplicateCollectionElements(ctx);
        ctx.Log($"Finished DuplicateCollectionsStep");
        return Task.FromResult(ctx);
    }

    private static IEnumerable<JToken> SelectTokensFromDataJson(JToken dataJson, string mappingPath, bool isMandatory, SubmodelMappingContext ctx)
    {
        try
        {
            IEnumerable<JToken> tokens = dataJson.SelectTokens(mappingPath, isMandatory) ?? throw new SubmodelDataToInstanceMapperException($"could not find {mappingPath} in data json", ctx);
            // Force evaluation of the enumerable to catch exceptions during JSON path evaluation
            return tokens.ToList();
        }
        catch (JsonException e)
        {
            throw new SubmodelDataToInstanceMapperException($"Error while trying to get data from mapping path {mappingPath}: ", e, ctx);
        }
    }
    private static JToken? GetCardinalityQualifier(JToken qualifier)
    {
        // qualifier.parent is the "qualifiers" array
        return qualifier.Parent?.SelectToken("[?(@.type=='SMT/Cardinality')]");
    }

    private static void DuplicateCollectionElements(SubmodelMappingContext ctx)
    {
        var submodelInstance = ctx.SubmodelInstance;
        var data = ctx.Data;
        var language = ctx.Language;

        var qualifiers = submodelInstance.SelectTokens("$..qualifiers[?(@.type=='SMT/CollectionMappingInfo')]");

        if (!qualifiers.Any())
        {
            ctx.Qualifier = new JObject(); // TODO: Make this null later
            submodelInstance.SelectTokens("$..qualifiers[?(@.type=='_SMT/CollectionMappingInfo')]")
                .ToList()
                .ForEach(q => q["type"]?.Replace("SMT/CollectionMappingInfo"));
            return;
        }

        var sortedQualifiers = qualifiers
            .OrderBy(q => Regex.Matches(q["value"]?.Value<string>() ?? string.Empty, @"\[\*\]").Count)
            .ToList();

        ctx.Qualifier = sortedQualifiers[0];


        var modelType = ctx.Qualifier.Parent?.Parent?.Parent?["modelType"] ?? throw new SubmodelDataToInstanceMapperException("could not find matching modelType field of a qualify object", ctx);
        if (modelType.Value<string>() != "SubmodelElementCollection") throw new SubmodelDataToInstanceMapperException($"Expected modelType 'SubmodelElementCollection', but found '{modelType.Value<string>()}'.", ctx);
        var elementToBeDuplicated = ctx.Qualifier.Parent?.Parent?.Parent ?? throw new SubmodelDataToInstanceMapperException("could not find matching value field of a qualify object", ctx);
        var mappingPath = ctx.Qualifier["value"]?.Value<string>() ?? throw new SubmodelDataToInstanceMapperException("Mapping Info cannot be null", ctx);

        var isMandatory = GetCardinalityQualifier(ctx.Qualifier)?["value"]?.Value<string>()?.StartsWith("One") ?? false;
        var collectionLength = SelectTokensFromDataJson(data, mappingPath.Replace("[*]", "[0]").TrimEnd('[', '0', ']') + "[*]", isMandatory, ctx).Count();

        var listIdentifier = mappingPath.EndsWith("[*]") ? mappingPath.Substring(0, mappingPath.Length - 3) : mappingPath;

        for (var i = 0; i < collectionLength; i++)
        {
            var newElement = elementToBeDuplicated.DeepClone();

            // Delete the qualifier that triggered this duplication to avoid infinite loops
            newElement.SelectTokens("$..qualifiers[?(@.type=='SMT/CollectionMappingInfo')]")
                .ToList()
                .ForEach(q =>
                {
                    if (q["value"]?.Value<string>() == mappingPath)
                    {
                        q.Remove();
                    }
                });

            var idShortToken = newElement["idShort"];
            if (idShortToken is JValue idVal && idVal.Type == JTokenType.String)
            {
                idVal.Value = $"{idVal.Value}_{i}";
            }

            var iteratedQualifiers = newElement
                    .SelectTokens("$..qualifiers[?(@.type=='SMT/MappingInfo' || @.type=='SMT/CollectionMappingInfo')]")
                    .Where(q =>
                    {
                        var v = q["value"]?.Value<string>();
                        return v != null && v.StartsWith(listIdentifier, StringComparison.Ordinal);
                    })
                    .ToList();


            foreach (var iteratedQualifer in iteratedQualifiers)
            {
                var iteratedMappingPath = iteratedQualifer["value"]?.Value<string>()
                                          ?? throw new SubmodelDataToInstanceMapperException("Mapping Info cannot be null", ctx);
                iteratedMappingPath = iteratedMappingPath.Replace($"{listIdentifier}[*]", $"{listIdentifier}[{i}]");
                iteratedQualifer["value"] = iteratedMappingPath;
            }

            elementToBeDuplicated.Parent!.Add(newElement);
        }
        //TODO: Implement ConceptQualifiers instead of just temporaryly replacing the type
        ctx.Qualifier["type"]?.Replace("_SMT/CollectionMappingInfo");

        elementToBeDuplicated.Remove();

        ctx.Log($"Succesfully duplicated {collectionLength} elements for collection with mapping path '{mappingPath}'");

        DuplicateCollectionElements(ctx);
    }
}
