using FluentAssertions;
using MarkdownToPdf.Web.Features.PdfGeneration;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Text.RegularExpressions;
using Xunit;

namespace MarkdownToPdf.Tests.Features.PdfGeneration;

public sealed class RateLimitingIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public RateLimitingIntegrationTests(WebApplicationFactory<Program> factory)
    {
        var clientOptions = new WebApplicationFactoryClientOptions { AllowAutoRedirect = false };

        _client = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IPdfService));
                if (descriptor != null)
                {
                    services.Remove(descriptor);
                }

                services.AddSingleton<IPdfService, FakePdfService>();
            });
        }).CreateClient(clientOptions);
    }

    private async Task<string> GetAntiforgeryTokenAsync()
    {
        var getResponse = await _client.GetAsync("/");
        var html = await getResponse.Content.ReadAsStringAsync();

        var tokenMatch = Regex.Match(html, @"name=""__RequestVerificationToken"" type=""hidden"" value=""([^""]+)""");
        var token = tokenMatch.Success ? tokenMatch.Groups[1].Value : "";

        var cookies = getResponse.Headers.GetValues("Set-Cookie");
        _client.DefaultRequestHeaders.Add("Cookie", cookies);

        return token;
    }

    // DRY Helper to instantiate the validation payload
    private static FormUrlEncodedContent CreatePdfRequestPayload(string token) =>
        new FormUrlEncodedContent([
            new KeyValuePair<string, string>("MarkdownText", "# Rate Limit Test"),
            new KeyValuePair<string, string>("__RequestVerificationToken", token)
        ]);

    [Fact]
    public async Task Post_GeneratePdf_ShouldReturn429TooManyRequests_WhenLimitExceeded()
    {
        var token = await GetAntiforgeryTokenAsync();

        for (int i = 0; i < 3; i++)
        {
            var validResponse = await _client.PostAsync("/api/pdf/generate", CreatePdfRequestPayload(token));

            // ARCHITECTURAL FIX: Because of the Background Worker Queue, successful initial requests 
            // now immediately return 202 Accepted instead of 200 OK.
            validResponse.StatusCode.Should().Be(HttpStatusCode.Accepted);
        }

        var rateLimitedResponse = await _client.PostAsync("/api/pdf/generate", CreatePdfRequestPayload(token));
        var responseString = await rateLimitedResponse.Content.ReadAsStringAsync();

        rateLimitedResponse.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        rateLimitedResponse.Headers.Contains("Retry-After").Should().BeTrue();

        rateLimitedResponse.Content.Headers.ContentType?.MediaType.Should().Be("text/html");

        // Verifies the user-friendly UI message is rendered
        // TEST FIX: Razor automatically HTML-encodes apostrophes (You've -> You&#x27;ve) during component rendering. 
        // Asserting on the substring bypasses encoding mismatches while maintaining strict test integrity.
        responseString.Should().Contain("reached your request limit");
    }
}