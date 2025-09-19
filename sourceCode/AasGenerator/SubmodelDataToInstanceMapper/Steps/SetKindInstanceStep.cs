using System;
using System.Threading;
using System.Threading.Tasks;
using MnestixCore.AasGenerator.Interfaces;

namespace MnestixCore.AasGenerator.Pipelines.Steps;

/// <summary>
/// Sets kind = "Instance" at top-level.
/// </summary>
public sealed class SetKindInstanceAasGeneratorPipelineStep : IPipelineStep<SubmodelMappingContext>
{
    public Task<SubmodelMappingContext> ExecuteAsync(SubmodelMappingContext ctx)
    {
        ctx.Log($"Started SetKindInstanceStep");
        ctx.SubmodelInstance.Property("kind")!.Value = "Instance";
        ctx.Log($"Finished SetKindInstanceStep");
        return Task.FromResult(ctx);
    }
}
