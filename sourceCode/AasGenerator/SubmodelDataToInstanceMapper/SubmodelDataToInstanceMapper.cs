using System;
using System.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MnestixCore.AasGenerator.Interfaces;
using MnestixCore.AasGenerator.Pipelines;
using MnestixCore.AasGenerator.Pipelines.Steps;
using Newtonsoft.Json.Linq;

namespace MnestixCore.AasGenerator;

/// <summary>
/// Pipeline-based implementation of ISubmodelDataToInstanceMapper.
/// </summary>
public sealed class SubmodelDataToInstanceMapper : ISubmodelDataToInstanceMapper
{
    private readonly IServiceProvider _serviceProvider;

    public SubmodelDataToInstanceMapper(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public JObject CreateSubmodelInstanceFromDataJson(JObject submodelTemplate, JObject data, string language, string newSubmodelId)
    {
        var logger = _serviceProvider.GetRequiredService<ILogger<SubmodelMappingContext>>();
        var context = new SubmodelMappingContext(submodelTemplate, data, language, newSubmodelId, logger);

        // Build pipeline with all the steps in the correct order
        var pipeline = new MnestixCore.AasGenerator.Pipelines.Core.PipelineBuilder<SubmodelMappingContext>()
            .Use<DeepCloneTemplateAasGeneratorPipelineStep>()
            .Use<SetKindInstanceAasGeneratorPipelineStep>()
            .Use<DuplicateCollectionsAasGeneratorPipelineStep>()
            .Use<MapDataToInstanceAasGeneratorPipelineStep>()
            .Use<RemoveTopLevelQualifiersAasGeneratorPipelineStep>()
            .Use<ReplaceIdentificationAasGeneratorPipelineStep>()
            .Build();

        var resultCtx = pipeline.RunAsync(context).GetAwaiter().GetResult();
        return resultCtx.SubmodelInstance;
    }
}
