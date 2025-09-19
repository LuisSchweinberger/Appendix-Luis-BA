using System;
using System.Threading;
using System.Threading.Tasks;
using MnestixCore.AasGenerator.Interfaces;
using MnestixCore.Errors;
using Newtonsoft.Json.Linq;

namespace MnestixCore.AasGenerator.Pipelines.Steps;

public sealed class ReplaceIdentificationAasGeneratorPipelineStep : IPipelineStep<SubmodelMappingContext>
{
    public Task<SubmodelMappingContext> ExecuteAsync(SubmodelMappingContext ctx)
    {
        ctx.Log($"Started ReplaceIdentificationStep");
        ReplaceIdentification(ctx.SubmodelInstance, ctx.NewSubmodelId, ctx);
        ctx.Log($"Finished ReplaceIdentificationStep");
        return Task.FromResult(ctx);
    }

    private static void ReplaceIdentification(JObject submodel, string newSubmodelId, SubmodelMappingContext ctx)
    {
        var id = submodel["id"] ?? throw new SubmodelDataToInstanceMapperException("Could not find id property in template", ctx);
        id.Replace(newSubmodelId);
    }
}
