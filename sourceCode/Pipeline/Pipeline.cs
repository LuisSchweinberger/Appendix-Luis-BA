using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MnestixCore.AasGenerator.Interfaces;

namespace MnestixCore.AasGenerator.Pipelines.Core;

internal sealed class Pipeline<TContext> : IPipeline<TContext>
{
    private readonly IReadOnlyList<IPipelineStep<TContext>> _steps;

    public Pipeline(IReadOnlyList<IPipelineStep<TContext>> steps)
    {
        _steps = steps;
    }

    public async Task<TContext> RunAsync(TContext context)
    {
        var current = context;
        foreach (var step in _steps)
        {
            current = await step.ExecuteAsync(current).ConfigureAwait(false);
        }
        return current;
    }
}
