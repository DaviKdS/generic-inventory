using GenericInventory.Health.Dtos;
using GenericInventory.Tests;
using System.Net;
using System.Net.Http.Json;

namespace GenericInventory.Health.Controllers.Tests;

public class HealthControllerTests : IClassFixture<TestWebApplicationFactory>
{
   private readonly TestWebApplicationFactory _factory;
   private readonly HttpClient _client;

   public HealthControllerTests(TestWebApplicationFactory factory)
   {
      _factory = factory;
      _client = _factory.CreateClient();
   }

   [Fact]
   [Trait("Category", "Integration")]
   public async Task GetLiveness_ShouldReturnOkStatus()
   {
      // Act
      var response = await _client.GetAsync("/health/liveness");

      // Assert
      Assert.Equal(HttpStatusCode.OK, response.StatusCode);
      var result = await response.Content.ReadFromJsonAsync<HealthDto>();
      Assert.NotNull(result);
      Assert.Equal("OK", result.Status);
   }

   [Fact]
   [Trait("Category", "Integration")]
   public async Task GetReadiness_ShouldReturnOkStatus()
   {
      // Act
      var response = await _client.PutAsync("/health/readiness", null);

      // Assert
      Assert.Equal(HttpStatusCode.OK, response.StatusCode);
      var result = await response.Content.ReadFromJsonAsync<HealthDto>();
      Assert.NotNull(result);
      Assert.Equal("OK", result.Status);
   }
}