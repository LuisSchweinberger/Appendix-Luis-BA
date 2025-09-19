using System;
using System.Threading;
using System.Threading.Tasks;
using MnestixCore.AasGenerator.Interfaces;
using Newtonsoft.Json.Linq;

namespace MnestixCore.AasGenerator.Pipelines.Steps;

/// <summary>
/// Clones the template into a fresh instance we can mutate.
/// </summary>
public sealed class YetAnotherRuleAasGeneratorPipelineStep : IPipelineStep<SubmodelMappingContext>
{
    public Task<SubmodelMappingContext> ExecuteAsync(SubmodelMappingContext ctx)
    {
        ctx.Log($"Started YetAnotherRuleStep");
        // Implement your logic here
        ctx.Log($"Finished YetAnotherRuleStep");
        return Task.FromResult(ctx);
    }
}
