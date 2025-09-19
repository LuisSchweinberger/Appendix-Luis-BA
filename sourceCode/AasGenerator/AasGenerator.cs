using System.Text;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MnestixCore.AasGenerator.Interfaces;
using MnestixCore.Dtos;
using MnestixCore.Dtos.AppSettingsOptions;
using MnestixCore.Errors;
using MnestixCore.IdGenerator.Interfaces;
using MnestixCore.RepoProxyClient.Interfaces;
using MnestixCore.TemplateBuilder.Interfaces;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

namespace MnestixCore.AasGenerator;

public class AasGenerator : IAasGenerator
{
    private readonly ISubmodelDataToInstanceMapper _dataToInstanceMapper;
    private readonly IRepoProxyClient _repoProxyClient;
    private readonly ICustomTemplateSubmodelsProvider _customTemplateSubmodelsProvider;
    private readonly IAasIdGeneratorService _idGenerator;
    private readonly RepoProxyOptions _repoProxyOptions;
    private readonly ILogger<AasGenerator> _logger;

    public AasGenerator(
        ISubmodelDataToInstanceMapper dataToInstanceMapper,
        IRepoProxyClient repoProxyClient,
        ICustomTemplateSubmodelsProvider customTemplateSubmodelsProvider,
        IAasIdGeneratorService idGenerator,
        IOptions<RepoProxyOptions> repoProxyOptions,
        ILogger<AasGenerator> logger)
    {
        _dataToInstanceMapper = dataToInstanceMapper;
        _repoProxyClient = repoProxyClient;
        _customTemplateSubmodelsProvider = customTemplateSubmodelsProvider;
        _idGenerator = idGenerator;
        _repoProxyOptions = repoProxyOptions.Value ?? throw new ArgumentNullException(nameof(repoProxyOptions));
        _logger = logger;
    }

    public async Task<IEnumerable<AasGeneratorResult>> AddDataToAasAsync(string base64EncodedAasId, IEnumerable<string> submodelTemplateIds, JObject data, string language)
    {
        var submodelTemplateResults = submodelTemplateIds.Select(async customTemplateId =>
        {
            var (templateError, subModelTemplate) = await TryGetTemplateFromCustomTemplateProviderAsync(customTemplateId);
            if (templateError != null)
            {
                return templateError;
            }

            var (shortIdError, subModelShortId) = TryGetIdShortFromTemplate(subModelTemplate!, customTemplateId);
            if (shortIdError != null)
            {
                return shortIdError;
            }

            var (idGeneratorError, newSubmodelId) = await TryGenerateSubmodelIdAsync(customTemplateId);
            if (idGeneratorError != null)
            {
                return idGeneratorError;
            }

            var (mappingError, instance) = TryMapDataToInstance(subModelTemplate!, data, language, customTemplateId, newSubmodelId!);
            if (mappingError != null)
            {
                return mappingError;
            }

            var errorWhileAdding = await TryAddSubmodelToAasAsync(base64EncodedAasId, instance!, customTemplateId);
            if (errorWhileAdding != null)
            {
                return errorWhileAdding;
            }

            // when everything went through, we can return a success for this custom template id
            return new AasGeneratorResult
            {
                Success = true,
                TemplateId = customTemplateId,
                GeneratedSubmodelId = newSubmodelId!
            };
        });

        return await Task.WhenAll(submodelTemplateResults);
    }

    private async Task<(AasGeneratorResult? Error, JObject? Result)> TryGetTemplateFromCustomTemplateProviderAsync(string customTemplateId)
    {
        var base64CustomTemplateId = Base64UrlTextEncoder.Encode(Encoding.UTF8.GetBytes(customTemplateId));

        try
        {
            var subModelTemplate = await _customTemplateSubmodelsProvider.GetCustomTemplateSubmodelAsync(base64CustomTemplateId);
            return (null, subModelTemplate);
        }
        catch (Exception e)
        {
            var error = new AasGeneratorResult
            {
                Message = "Failed to fetch template from custom template provider: " + e.Message,
                TemplateId = customTemplateId,
                Success = false
            };
            _logger.LogError(e, $"Failed to fetch template from custom template provider. TemplateId: {customTemplateId}, Message: {e.Message}");
            return (error, null);
        }
    }

    private (AasGeneratorResult? Error, JObject? Result) TryMapDataToInstance(JObject submodelTemplate, JObject data, string language, string customTemplateId, string newSubmodelId)
    {
        try
        {
            var instance = _dataToInstanceMapper.CreateSubmodelInstanceFromDataJson(submodelTemplate, data, language, newSubmodelId);
            return (null, instance);
        }
        catch (SubmodelDataToInstanceMapperException e)
        {
            var error = new AasGeneratorResult
            {
                Success = false,
                TemplateId = customTemplateId,
                Message = e.Message,
                ErrorInfo = new AasGeneratorErrorInfo
                {
                    Logs = e.Context?.Logs,
                    Qualifier = e.Context?.Qualifier.ToString(Formatting.None),
                    QualifierPath = e.Context?.Qualifier.Path
                }
            };
            _logger.LogError(e, $"Failed to map data to instance. TemplateId: {customTemplateId}, Message: {e.Message}, ErrorInfo: {error.ErrorInfo}");
            return (error, null);
        }
    }

    private (AasGeneratorResult? Error, string? Result) TryGetIdShortFromTemplate(JObject subModelTemplate, string customTemplateId)
    {
        var subModelShortId = subModelTemplate["idShort"]?.Value<string>();
        if (subModelShortId == null)
        {
            var error = new AasGeneratorResult
            {
                Success = false,
                TemplateId = customTemplateId,
                Message = $"template shortId of {customTemplateId} needs to be not null"
            };
            return (error, null);
        }

        return (null, subModelShortId);
    }

    private async Task<AasGeneratorResult?> TryAddSubmodelToAasAsync(string base64EncodedAasId, JObject submodel, string customTemplateId)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(base64EncodedAasId))
            {
                return new AasGeneratorResult
                {
                    Success = false,
                    TemplateId = customTemplateId,
                    Message = "The aas id cannot be empty!"
                };
            }

            var submodelId = submodel["id"]?.Value<string>();
            if (string.IsNullOrWhiteSpace(submodelId))
            {
                return new AasGeneratorResult
                {
                    Success = false,
                    TemplateId = customTemplateId,
                    Message = "The submodel id cannot be empty!"
                };
            }
            await _repoProxyClient.PostAsync(_repoProxyOptions.SubmodelPath, submodel.ToString());

            var submodelReference =
                new SubmodelReference(new List<Key> { new("Submodel", submodelId) }, "ModelReference");
            var submodelReferenceJson = JsonConvert.SerializeObject(submodelReference, new JsonSerializerSettings
            {
                ContractResolver = new CamelCasePropertyNamesContractResolver()
            });
            await _repoProxyClient.PostAsync($"{_repoProxyOptions.SubmodelReferencePath}/{base64EncodedAasId}", submodelReferenceJson);

            return null;
        }
        catch (RepoProxyException e)
        {
            var error = new AasGeneratorResult
            {
                Success = false,
                TemplateId = customTemplateId,
                Message = e.Message
            };
            _logger.LogError(e, $"Failed to add submodel to AAS. TemplateId: {customTemplateId}, AasId: {base64EncodedAasId}, Message: {e.Message}");
            return error;
        }
    }

    private async Task<(AasGeneratorResult?, string?)> TryGenerateSubmodelIdAsync(string templateId)
    {
        try
        {
            var ids = await _idGenerator.GenerateSubmodelIdsAsync();
            return (null, ids.First());
        }
        catch (Exception e)
        {
            var error = new AasGeneratorResult
            {
                Success = false,
                TemplateId = templateId,
                Message = "could not generate submodel id"
            };
            _logger.LogError(e, $"Could not generate submodel id. TemplateId: {templateId}, Message: {e.Message}");
            return (error, null);
        }
    }
}