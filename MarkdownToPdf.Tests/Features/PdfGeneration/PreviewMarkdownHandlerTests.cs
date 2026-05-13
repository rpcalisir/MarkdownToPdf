using FluentAssertions;
using MarkdownToPdf.Web.Features.PdfGeneration.Preview;
using Xunit;

namespace MarkdownToPdf.Tests.Features.PdfGeneration;

public sealed class PreviewMarkdownHandlerTests
{
    private readonly PreviewMarkdownHandler _handler = new();

    [Fact]
    public async Task Handler_ShouldStripMaliciousHtmlTags_ToPreventXSS()
    {
        // Arrange: A malicious user tries to inject a script tag via the HTMX preview
        var maliciousInput = "# Hello \n <script>alert('hacked');</script>";
        var command = new PreviewMarkdownCommand(maliciousInput);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert: Verifies the pipeline configuration is working perfectly
        result.IsSuccess.Should().BeTrue();

        // The script tags should be HTML-encoded safely, not rendered natively into the DOM
        result.Value.Should().NotContain("<script>");
        result.Value.Should().Contain("&lt;script&gt;");
    }

    [Fact]
    public async Task Handler_ShouldParseAdvancedMarkdown_LikeTables()
    {
        // Arrange
        var input = "| Header |\n|---| \n| Cell |";
        var command = new PreviewMarkdownCommand(input);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Contain("<table>");
        result.Value.Should().Contain("<thead>");
        result.Value.Should().Contain("Header");
    }

    [Fact]
    public async Task Handler_ShouldParseStandardMarkdown_ToValidHtml()
    {
        // Arrange: A standard markdown string with a header and bold text
        var validInput = "# Enterprise Boilerplate\nThis text is **highly** scalable.";
        var command = new PreviewMarkdownCommand(validInput);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert: Verify the HTML output matches expected semantic tags
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNullOrEmpty();

        // Ensure Markdig correctly rendered the H1 and Strong tags
        result.Value.Should().Contain("<h1");
        result.Value.Should().Contain("Enterprise Boilerplate");
        result.Value.Should().Contain("<strong>highly</strong>");
    }
}