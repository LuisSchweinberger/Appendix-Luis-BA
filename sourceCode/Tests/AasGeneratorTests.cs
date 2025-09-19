using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MnestixCore.AasGenerator;
using MnestixCore.AasGenerator.Interfaces;
using MnestixCore.AasGenerator.Pipelines;
using MnestixCore.Dtos.AppSettingsOptions;
using MnestixCore.Errors;
using MnestixCore.IdGenerator.Interfaces;
using MnestixCore.RepoProxyClient.Interfaces;
using MnestixCore.TemplateBuilder.Interfaces;
using FluentAssertions;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using RestSharp;
using System.Runtime.InteropServices;

namespace Core.Tests.AasGenerator;

public class AasGeneratorTests
{
    private MnestixCore.AasGenerator.AasGenerator _AasGenerator = null!;
    private ISubmodelDataToInstanceMapper _dataToInstanceMapper = null!;
    private Mock<IRepoProxyClient> _repoProxyClientMock = null!;
    private Mock<ICustomTemplateSubmodelsProvider> _templateSubmodelsProviderMock = null!;
    private Mock<IAasIdGeneratorService> _idGeneratorMock = null!;
    private Mock<ILogger<MnestixCore.AasGenerator.AasGenerator>> _loggerMock = null!;
    private readonly IOptions<RepoProxyOptions> _repoProxyOptions = Options.Create(new RepoProxyOptions());
    private const string SubmodelTemplatePath = "AasGenerator/TestJsons/CustomTemplateSubmodelWithMappingInfo.json";
    private const string NewSubmodelId = "TheNewSubmodelId";
    private const string TestSubmodelPath = "/submodels";
    private const string TestSubmodelReferencePath = "/aas/submodel-references";
    private const string TestBase64EncodedAasId = "dGVzdEFhc0lk"; // base64 encoded "testAasId"

    [SetUp]
    public void SetUp()
    {
        // Set up a real ServiceProvider for the SubmodelDataToInstanceMapper
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Debug));
        services.AddTransient<ILogger<SubmodelMappingContext>, Logger<SubmodelMappingContext>>();
        var serviceProvider = services.BuildServiceProvider();
        
        _dataToInstanceMapper = new SubmodelDataToInstanceMapper(serviceProvider);
        _repoProxyClientMock = new Mock<IRepoProxyClient>();
        _templateSubmodelsProviderMock = new Mock<ICustomTemplateSubmodelsProvider>();
        _idGeneratorMock = new Mock<IAasIdGeneratorService>();
        _loggerMock = new Mock<ILogger<MnestixCore.AasGenerator.AasGenerator>>();
        
        var repoProxyOptions = new RepoProxyOptions
        {
            SubmodelPath = TestSubmodelPath,
            SubmodelReferencePath = TestSubmodelReferencePath
        };
        
        _AasGenerator = new MnestixCore.AasGenerator.AasGenerator(
            _dataToInstanceMapper,
            _repoProxyClientMock.Object,
            _templateSubmodelsProviderMock.Object,
            _idGeneratorMock.Object,
            Options.Create(repoProxyOptions),
            _loggerMock.Object);

        _idGeneratorMock.Setup(x => x.GenerateSubmodelIdsAsync(It.IsAny<uint>())).ReturnsAsync(new List<string> { NewSubmodelId });
    }

    [Test]
    public async Task AddDataToAasAsync_WhenGivenAnEmptyListOfTemplateIds_ReturnsEmptyListOfResults()
    {
        // Arrange
        const string language = "de";
        const string aasId = "";
        var templateIds = Array.Empty<string>();
        var dataJson = new JObject();

        // Act
        var result = await _AasGenerator.AddDataToAasAsync(aasId, templateIds, dataJson, language);

        // Assert
        result.Should().BeEquivalentTo(Array.Empty<AasGeneratorResult>());
    }

            [Test]
    public async Task AddDataToAasAsync_MandatoryAndOptionalField_Success()
    {
        await RunDataIngestTest("MandatoryAndOptionalField");
    }
    
    [Test]
    public async Task AddDataToAasAsync_InputOnlyMandatoryField_Success()
    {
        await RunDataIngestTest("InputOnlyMandatoryField");
    }
    
    [Test]
    public async Task AddDataToAasAsync_InputOnlyOptionalField_ShouldFail()
    {
        await RunDataIngestFailureTest("InputOnlyOptionalField");
    }
    
    [Test]
    public async Task AddDataToAasAsync_InputList_Success()
    {
        await RunDataIngestTest("InputList");
    }
    
    [Test]
    public async Task AddDataToAasAsync_InputNestedList_Success()
    {
        await RunDataIngestTest("InputNestedList");
    }
    
    [Test]
    public async Task AddDataToAasAsync_InputListWithMandatoryListElementMissing_ShouldFail()
    {
        await RunDataIngestFailureTest("InputListWithMandatoryListElementMissing");
    }
    
    [Test]
    public async Task AddDataToAasAsync_InputListWithMandatoryListMissing_ShouldFail()
    {
        await RunDataIngestFailureTest("InputListWithMandatoryListMissing");
    }
    
    [Test]
    public async Task AddDataToAasAsync_InputListWithOptionalListElementMissing_Success()
    {
        await RunDataIngestTest("InputListWithOptionalListElementMissing");
    }
    
    [Test]
    public async Task AddDataToAasAsync_InputListWithOptionalListMissing_Success()
    {
        await RunDataIngestTest("InputListWithOptionalListMissing");
    }

    [Test, Ignore("Performance test depends on Hardware")]
    public async Task AddDataToAasAsync_InputList_PerformanceWith10kElements()
    {
        await RunPerformanceTestWith10kElements();
    }

    [Test, Ignore("This Feature isn't implemented yet")]
    public async Task AddDataToAasAsync_InputFilter_Success()
    {
        await RunDataIngestTest("InputFilter");
    }
    
    private async Task RunDataIngestTest(string testCaseName)
    {
        // Arrange
        var templateSubmodel = DataIngestTestFileProvider.GetTemplateSubmodel(testCaseName);
        var templateData = DataIngestTestFileProvider.GetData(testCaseName);
        var expectedResult = DataIngestTestFileProvider.GetExpectedResult(testCaseName);
        
        var aasId = "TestAasId";
        var templateIds = new List<string> { "urn:smtemplate:DemoTemplate" };
        
        string? capturedSubmodelContent = null;
        
        _repoProxyClientMock
            .Setup(x => x.PostAsync(It.Is<string>(path => path == TestSubmodelPath), It.IsAny<string>()))
            .Callback<string, string>((path, content) => capturedSubmodelContent = content)
            .ReturnsAsync("created");
            
        _repoProxyClientMock
            .Setup(x => x.PostAsync(It.Is<string>(path => path == TestSubmodelReferencePath), It.IsAny<string>()))
            .ReturnsAsync("created");
        
        _templateSubmodelsProviderMock
            .Setup(x => x.GetCustomTemplateSubmodelAsync(It.IsAny<string>()))
            .ReturnsAsync(templateSubmodel);
        
        _idGeneratorMock
            .Setup(x => x.GenerateSubmodelIdsAsync(It.IsAny<uint>()))
            .ReturnsAsync(new List<string> { "TheNewSubmodelId" });
        
        // This method is only for success cases - expectedResult should not be null
        expectedResult.Should().NotBeNull($"Test case '{testCaseName}' should have a valid expected result for success test");
        
        // The real implementation will process the template and data according to the mapping rules
        
        // Act
        var result = await _AasGenerator.AddDataToAasAsync(aasId, templateIds, templateData, "en");
        
        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(1);
        result.First().Success.Should().BeTrue();
        
        capturedSubmodelContent.Should().NotBeNull();
        var actualSubmodel = JObject.Parse(capturedSubmodelContent!);
        
        JToken.DeepEquals(actualSubmodel, expectedResult).Should().BeTrue(
            $"Test case '{testCaseName}' failed: Expected submodel content to match expected result \n Expected: {expectedResult}\n Actual: {actualSubmodel}");
    }
    
    private async Task RunDataIngestFailureTest(string testCaseName)
    {
        // Arrange
        var templateSubmodel = DataIngestTestFileProvider.GetTemplateSubmodel(testCaseName);
        var templateData = DataIngestTestFileProvider.GetData(testCaseName);
        var expectedResult = DataIngestTestFileProvider.GetExpectedResult(testCaseName);
        
        var aasId = "TestAasId";
        var templateIds = new List<string> { "urn:smtemplate:DemoTemplate" };
        
        _templateSubmodelsProviderMock
            .Setup(x => x.GetCustomTemplateSubmodelAsync(It.IsAny<string>()))
            .ReturnsAsync(templateSubmodel);
        
        _idGeneratorMock
            .Setup(x => x.GenerateSubmodelIdsAsync(It.IsAny<uint>()))
            .ReturnsAsync(new List<string> { "TheNewSubmodelId" });
        
        // This method is only for failure cases - expectedResult should be null
        expectedResult.Should().BeNull($"Test case '{testCaseName}' should have null expected result for failure test");
        
        // The real implementation will throw SubmodelDataToInstanceMapperException when validation fails
        
        // Act
        var result = await _AasGenerator.AddDataToAasAsync(aasId, templateIds, templateData, "en");
        
        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(1);
        result.First().Success.Should().BeFalse($"Test case '{testCaseName}' should fail due to missing mandatory data");
    }
    
    private async Task RunPerformanceTestWith10kElements()
    {
        // Arrange
        var templateSubmodel = DataIngestTestFileProvider.GetTemplateSubmodel("InputList");
        
        // Create test data with 10,000 contact persons and pets
        var contactPersons = new List<object>();
        var pets = new List<object>();
        
        for (int i = 0; i < 10000; i++)
        {
            contactPersons.Add(new
            {
                name = $"ContactPerson_{i}",
                email = $"person_{i}@example.com"
            });
            
            pets.Add(new
            {
                name = $"Pet_{i}",
                typeOfAnimal = i % 2 == 0 ? "Dog" : "Cat"
            });
        }
        
        var templateData = JObject.FromObject(new
        {
            sourceData = new
            {
                contactPersons = contactPersons,
                pets = pets
            }
        });
        
        var aasId = "TestAasId";
        var templateIds = new List<string> { "urn:smtemplate:DemoTemplate" };
        
        string? capturedSubmodelContent = null;
        
        _repoProxyClientMock
            .Setup(x => x.PostAsync(It.Is<string>(path => path == TestSubmodelPath), It.IsAny<string>()))
            .Callback<string, string>((path, content) => capturedSubmodelContent = content)
            .ReturnsAsync("created");
            
        _repoProxyClientMock
            .Setup(x => x.PostAsync(It.Is<string>(path => path == TestSubmodelReferencePath), It.IsAny<string>()))
            .ReturnsAsync("created");
        
        _templateSubmodelsProviderMock
            .Setup(x => x.GetCustomTemplateSubmodelAsync(It.IsAny<string>()))
            .ReturnsAsync(templateSubmodel);
        
        _idGeneratorMock
            .Setup(x => x.GenerateSubmodelIdsAsync(It.IsAny<uint>()))
            .ReturnsAsync(new List<string> { "TheNewSubmodelId" });
        
        // Act - Measure execution time
        var stopwatch = Stopwatch.StartNew();
        var result = await _AasGenerator.AddDataToAasAsync(aasId, templateIds, templateData, "en");
        stopwatch.Stop();
        
        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(1);
        result.First().Success.Should().BeTrue();
        
        capturedSubmodelContent.Should().NotBeNull();
        var actualSubmodel = JObject.Parse(capturedSubmodelContent!);
        
        // Verify that we processed all 10,000 elements
        var contactPersonsArray = actualSubmodel.SelectToken("$.submodelElements[?(@.idShort=='ContactPersons')].value") as JArray;
        var petsArray = actualSubmodel.SelectToken("$.submodelElements[?(@.idShort=='Pets')].value") as JArray;
        
        contactPersonsArray.Should().NotBeNull();
        contactPersonsArray!.Count.Should().Be(10000, "Should have processed all 10,000 contact persons");
        
        petsArray.Should().NotBeNull();
        petsArray!.Count.Should().Be(10000, "Should have processed all 10,000 pets");
        
        // Log performance results
        var elapsedMs = stopwatch.ElapsedMilliseconds;
        var elementsPerSecond = (20000.0 / elapsedMs) * 1000; // 20k total elements (10k contacts + 10k pets)
        
        TestContext.WriteLine($"Performance Test Results:");
        TestContext.WriteLine($"- Processed 20,000 elements (10,000 contact persons + 10,000 pets)");
        TestContext.WriteLine($"- Execution time: {elapsedMs} ms ({stopwatch.Elapsed.TotalSeconds:F2} seconds)");
        TestContext.WriteLine($"- Throughput: {elementsPerSecond:F0} elements/second");
        
        // Assert performance requirement (adjust threshold as needed)
        elapsedMs.Should().BeLessThan(30000, "Processing 20,000 elements should complete within 30 seconds");
    }
}