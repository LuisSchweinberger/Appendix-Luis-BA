namespace MnestixCore.AasGenerator;

public class AasGeneratorErrorInfo
{
    public IList<string>? Logs { get; set; }
    public string? Qualifier { get; set; }
    public string? QualifierPath { get; set; }
}

public class AasGeneratorResult
{
    /// <summary>
    /// The template id this result is referencing to
    /// </summary>
    public string TemplateId { get; init; } = null!;
    /// <summary>
    /// indicates whether the creation of the submodel was successful
    /// </summary>
    public bool Success { get; init; }
    /// <summary>
    /// a error message that will be set if the creation of the submodel was not successful
    /// </summary>
    public string Message { get; init; } = "";
    /// <summary>
    /// the id of the new submodel if the creation was successful
    /// </summary>
    public string GeneratedSubmodelId { get; set; } = "";
    /// <summary>
    /// additional structured error information when Success is false
    /// </summary>
    public AasGeneratorErrorInfo? ErrorInfo { get; init; }
}
