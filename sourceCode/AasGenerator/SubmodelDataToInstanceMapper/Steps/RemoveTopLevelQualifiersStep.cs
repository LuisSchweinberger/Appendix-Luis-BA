using System;
using System.Threading;
using System.Threading.Tasks;
using MnestixCore.AasGenerator.Interfaces;
using Newtonsoft.Json.Linq;

namespace MnestixCore.AasGenerator.Pipelines.Steps;

public sealed class RemoveTopLevelQualifiersAasGeneratorPipelineStep : IPipelineStep<SubmodelMappingContext>
{
    public Task<SubmodelMappingContext> ExecuteAsync(SubmodelMappingContext ctx)
    {
        ctx.Log($"Started RemoveTopLevelQualifiersStep");
        RemoveTopLevelQualifiers(ctx.SubmodelInstance);
        ctx.Log($"Finished RemoveTopLevelQualifiersStep");
        return Task.FromResult(ctx);
    }

    private static void RemoveTopLevelQualifiers(JObject submodel)
    {
        submodel["qualifiers"]?.Replace(new JArray());
    }
}
