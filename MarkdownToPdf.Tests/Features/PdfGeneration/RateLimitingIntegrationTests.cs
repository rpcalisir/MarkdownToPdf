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

    // DRY PATTERN: Extract token generation to prevent test bloat and duplication
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

    [Fact]
    public async Task Post_GeneratePdf_ShouldReturn429TooManyRequests_WhenLimitExceeded()
    {
        // 1. Arrange: Establish session and obtain Antiforgery payload
        var token = await GetAntiforgeryTokenAsync();

        // 2. Act: Exhaust the permit limit (configured to 3 in settings)
        for (int i = 0; i < 3; i++)
        {
            // ARCHITECTURAL FIX: HttpContent streams are consumed upon sending. 
            // We must instantiate a new payload instance for every request in the loop.
            var validContent = new FormUrlEncodedContent([
                new KeyValuePair<string, string>("MarkdownText", "# Rate Limit Test"),
                new KeyValuePair<string, string>("__RequestVerificationToken", token)
            ]);

            var validResponse = await _client.PostAsync("/api/pdf/generate", validContent);
            validResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        // The 4th request within the same minute window should trigger the Rate Limiter
        var rateLimitContent = new FormUrlEncodedContent([
            new KeyValuePair<string, string>("MarkdownText", "# Rate Limit Test"),
            new KeyValuePair<string, string>("__RequestVerificationToken", token)
        ]);

        var rateLimitedResponse = await _client.PostAsync("/api/pdf/generate", rateLimitContent);
        var responseString = await rateLimitedResponse.Content.ReadAsStringAsync();

        // 3. Assert: Verify the rejection status and our Razor Component output
        rateLimitedResponse.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        rateLimitedResponse.Headers.Contains("Retry-After").Should().BeTrue();

        // Verifies the HTMX interceptor receives HTML (Razor Component) and not plain text
        rateLimitedResponse.Content.Headers.ContentType?.MediaType.Should().Be("text/html");
        responseString.Should().Contain("Rate limit exceeded. Please wait one minute");
    }
}