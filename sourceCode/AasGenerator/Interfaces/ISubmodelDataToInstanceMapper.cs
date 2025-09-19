using Newtonsoft.Json.Linq;

namespace MnestixCore.AasGenerator.Interfaces;
// Shall be deleted once the other definitions are removed
// Is replaced by SubmodelDataToInstanceMapper
public interface ISubmodelDataToInstanceMapper
{
    /// <summary>
    /// maps 'data' into 'submodelTemplate' according to the MappingInfo provided by the submodelTemplate.
    /// The MappingInfo needs to be in a format so that it can be passed to Newtonsoft SelectToken()
    /// <see href="https://www.newtonsoft.com/json/help/html/SelectToken.htm"/>
    /// </summary>
    /// <param name="submodelTemplate">The template that will be the base of the submodel</param>
    /// <param name="data">the data json, where the data for the submodel will be looked up according to the mapping info in the template</param>
    /// <param name="language">when the mapper encounters a multi language property (modelType='MultiLanguageProperty'), it convert the given value to a multi language property with given language</param>
    /// <param name="newSubmodelId"></param>
    /// <returns></returns>
    JObject CreateSubmodelInstanceFromDataJson(JObject submodelTemplate, JObject data, string language, string newSubmodelId);
}