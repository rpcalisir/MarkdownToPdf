using FluentAssertions;
using MarkdownToPdf.Web.Features.PdfGeneration;
using MarkdownToPdf.Web.Features.PdfGeneration.Jobs;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Text.RegularExpressions;
using Xunit;

namespace MarkdownToPdf.Tests.Features.PdfGeneration;

public sealed class GeneratePdfIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public GeneratePdfIntegrationTests(WebApplicationFactory<Program> factory)
    {
        var clientOptions = new WebApplicationFactoryClientOptions { AllowAutoRedirect = false };

        _factory = factory.WithWebHostBuilder(builder =>
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
        });

        _client = _factory.CreateClient(clientOptions);
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

    [Fact]
    public async Task Post_GeneratePdf_ShouldReturnHtmlToast_WhenValidationFails()
    {
        var token = await GetAntiforgeryTokenAsync();
        var content = new FormUrlEncodedContent([
            new KeyValuePair<string, string>("MarkdownText", ""),
            new KeyValuePair<string, string>("__RequestVerificationToken", token)
        ]);

        var response = await _client.PostAsync("/api/pdf/generate", content);
        var responseString = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType?.MediaType.Should().Be("text/html");
        responseString.Should().Contain("Oops! Please type or paste some Markdown");
    }

    [Fact]
    public async Task Post_GeneratePdf_ShouldReturnAcceptedAndPollUntilDownloadIframeIsRendered_WhenMarkdownIsValid()
    {
        var token = await GetAntiforgeryTokenAsync();
        var content = new FormUrlEncodedContent([
            new KeyValuePair<string, string>("MarkdownText", "# Valid Markdown Test"),
            new KeyValuePair<string, string>("__RequestVerificationToken", token)
        ]);

        var generateResponse = await _client.PostAsync("/api/pdf/generate", content);
        var htmlResponse = await generateResponse.Content.ReadAsStringAsync();

        generateResponse.StatusCode.Should().Be(HttpStatusCode.Accepted);
        htmlResponse.Should().Contain("hx-trigger=\"every 2s\"");

        var match = Regex.Match(htmlResponse, @"/api/pdf/status/([a-fA-F0-9\-]+)");
        match.Success.Should().BeTrue();
        var jobId = match.Groups[1].Value;

        HttpResponseMessage? statusResponse = null;
        string? statusHtml = null;
        for (int i = 0; i < 50; i++)
        {
            statusResponse = await _client.GetAsync($"/api/pdf/status/{jobId}");
            statusHtml = await statusResponse.Content.ReadAsStringAsync();

            // ARCHITECTURAL FIX: Poll until the backend returns the Success UI containing the PDF Ready message
            if (statusHtml.Contains("PDF Ready"))
            {
                break;
            }
            await Task.Delay(100);
        }

        statusHtml.Should().NotBeNull();
        statusHtml.Should().Contain("PDF Ready");

        // Extract the URL from the invisible iframe src
        var matchUrl = Regex.Match(statusHtml!, @"<iframe src=""([^""]+)""");
        matchUrl.Success.Should().BeTrue();
        var downloadUrl = matchUrl.Groups[1].Value;

        var downloadResponse = await _client.GetAsync(downloadUrl);

        downloadResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        downloadResponse.Content.Headers.ContentType?.MediaType.Should().Be("application/pdf");

        var fakeIdUrl = $"/api/pdf/download/{Guid.NewGuid()}";
        var invalidDownloadResponse = await _client.GetAsync(fakeIdUrl);

        invalidDownloadResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Get_Status_ShouldReturnProcessingAlert_WhenJobIsPending()
    {
        var jobId = Guid.NewGuid();

        var cache = _factory.Services.GetRequiredService<IMemoryCache>();
        cache.Set(jobId, new PdfJobState(jobId, JobStatus.Pending));

        var response = await _client.GetAsync($"/api/pdf/status/{jobId}");
        var html = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        html.Should().Contain("Your document is being processed securely");
    }
}