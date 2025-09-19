using Newtonsoft.Json.Linq;

namespace MnestixCore.AasGenerator.Interfaces;

public interface IAasGenerator
{
    /// <summary>
    /// supplies data to the shell with given aasId. It will fetch templates with given ids, populate them with data according to their mapping info
    /// using given data json as data source. After that it will store the templates under their idShort in the shell.
    /// See <see cref="ISubmodelDataToInstanceMapper"/> for more Information about mapping.
    /// </summary>
    /// <param name="base64EncodedAasId">the shell where the submodel will be added to</param>
    /// <param name="submodelTemplateIds">the templates where data will be mapped to according to their mapping info.
    /// After that, they will be added the shell under their idShort
    /// </param>
    /// <param name="data">the json where the data will be looked up from the templates according to mapping info</param>
    /// <param name="language">the language that will be used when encountering a multi language property</param>
    /// <returns>a list of results for each template</returns>
    Task<IEnumerable<AasGeneratorResult>> AddDataToAasAsync(string base64EncodedAasId, IEnumerable<string> submodelTemplateIds, JObject data, string language);
}