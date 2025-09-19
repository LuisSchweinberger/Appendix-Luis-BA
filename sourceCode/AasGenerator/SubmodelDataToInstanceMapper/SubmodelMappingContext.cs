using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace MnestixCore.AasGenerator.Pipelines;

public sealed class SubmodelMappingContext
{
    // Immutable inputs
    public JObject Template { get; }
    public JObject Data { get; }
    public string Language { get; }
    public string NewSubmodelId { get; }
    
    // Logger for diagnostics
    private readonly ILogger<SubmodelMappingContext> _logger;

    // Mutable working object
    public JObject SubmodelInstance { get; set; }

    public void Log(string message)
    {
        Logs.Add($"[{DateTime.UtcNow}] - {message}");
        _logger.LogInformation(message);
    }
    // Optional: diagnostics/logs for each step
    public IList<string> Logs { get; } = new List<string>();

    // Optional: The currently processed qualifiers (for error reporting)
    public JToken Qualifier { get; set; } = new JObject();

    public SubmodelMappingContext(JObject template, JObject data, string language, string newSubmodelId, ILogger<SubmodelMappingContext> logger)
    {
        Template = template;
        Data = data;
        Language = language;
        NewSubmodelId = newSubmodelId;
        _logger = logger;
        SubmodelInstance = new JObject();
    }
}
