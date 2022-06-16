using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using StarterKit.Api.Models.Status;
using StarterKit.IntegrationTests.Shared;
using Xunit;

namespace StarterKit.IntegrationTests.Api.Controllers
{
  [Collection(TestConstants.Collections.ControllerTests)]
  public class StatusControllerTests : ControllerTestBase
  {
    public StatusControllerTests(TestBaseFixture fixture) : base(fixture)
    {
      BasePath = "v1/status";
    }

    [Fact]
    public async void GetApiStatus_ShouldReturnOk()
    {
      //Arrange
      var url = $"{BasePath}/health";

      //Act
      var response = await GetAsync(url);
      var mappedResponse = await ParseResultAsync<StatusResponse>(response);

      //Assert
      Assert.Equal(ReasonPhrases.GetReasonPhrase(StatusCodes.Status200OK), response.ReasonPhrase);
      Assert.Equal(Status.ok, mappedResponse.Status);
    }
  }
}
