using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MnestixCore.AasGenerator.Interfaces;

namespace MnestixCore.AasGenerator.Pipelines.Core;

internal sealed class PipelineBuilder<TContext>
{
    private readonly List<Type> _stepTypes = new();

    public PipelineBuilder<TContext> Use<TStep>() where TStep : class, IPipelineStep<TContext>, new()
    {
        _stepTypes.Add(typeof(TStep));
        return this;
    }

    public IPipeline<TContext> Build()
    {
        var steps = new List<IPipelineStep<TContext>>();
        foreach (var stepType in _stepTypes)
        {
            var step = (IPipelineStep<TContext>)Activator.CreateInstance(stepType)!;
            steps.Add(step);
        }
        return new Pipeline<TContext>(steps);
    }
}
