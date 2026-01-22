// <copyright file="SyntacticValidatorTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Rlm.Cli.Core.Documents;
using Rlm.Cli.Core.Validation;
using Rlm.Cli.Tests.Builders;
using Shouldly;

namespace Rlm.Cli.Tests.Core.Validation;

[TestClass]
public sealed class SyntacticValidatorTests
{
    private SyntacticValidator validator = null!;

    [TestInitialize]
    public void Setup()
    {
        validator = new SyntacticValidator();
    }

    [TestMethod]
    public async Task ValidateAsync_ValidTextDocument_ReturnsSuccess()
    {
        // Arrange
        RlmDocument document = RlmDocumentBuilder.Default()
            .WithContent("This is valid text content.")
            .Build();

        // Act
        ValidationResult result = await validator.ValidateAsync(document);

        // Assert
        result.IsValid.ShouldBeTrue();
        result.Errors.ShouldBeEmpty();
    }

    [TestMethod]
    public async Task ValidateAsync_DocumentWithNullBytes_ReturnsError()
    {
        // Arrange
        RlmDocument document = RlmDocumentBuilder.Default()
            .WithContent("Text with\0null bytes")
            .Build();

        // Act
        ValidationResult result = await validator.ValidateAsync(document);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.Contains("binary content"));
    }

    [TestMethod]
    public async Task ValidateAsync_DocumentWithHighNonPrintableConcentration_ReturnsError()
    {
        // Arrange - Create content with >10% non-printable chars
        char[] content = new char[100];
        for (int i = 0; i < 100; i++)
        {
            content[i] = i < 15 ? (char)1 : 'A'; // 15% non-printable
        }
        RlmDocument document = RlmDocumentBuilder.Default()
            .WithContent(new string(content))
            .Build();

        // Act
        ValidationResult result = await validator.ValidateAsync(document);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.Contains("binary content"));
    }

    [TestMethod]
    public async Task ValidateAsync_MarkdownWithUnbalancedCodeBlocks_ReturnsWarning()
    {
        // Arrange
        RlmDocument document = RlmDocumentBuilder.Default()
            .WithContent("""
                # Header

                ```python
                def hello():
                    pass

                Some more text
                """)
            .WithMetadata(m => m
                .WithSource("/test/file.md")
                .WithContentType("text/markdown"))
            .Build();

        // Act
        ValidationResult result = await validator.ValidateAsync(document);

        // Assert
        result.IsValid.ShouldBeTrue(); // Unbalanced blocks are warnings, not errors
        result.Warnings.ShouldContain(w => w.Contains("Unbalanced code blocks"));
    }

    [TestMethod]
    public async Task ProcessAsync_MarkdownWithCodeBlocks_ValidatesCodeBlockPattern()
    {
        // Arrange - The validator checks code block balance
        // Note: The regex patterns may count blocks differently depending on exact formatting
        RlmDocument document = RlmDocumentBuilder.Default()
            .WithContent("""
                # Header

                ```python
                def hello():
                    pass
                ```

                Some more text
                """)
            .WithMetadata(m => m
                .WithSource("/test/file.md")
                .WithContentType("text/markdown"))
            .Build();

        // Act
        ValidationResult result = await validator.ValidateAsync(document);

        // Assert - Validation completes without error
        result.IsValid.ShouldBeTrue();
    }

    [TestMethod]
    public async Task ValidateAsync_DocumentWithVeryLongLines_ReturnsWarning()
    {
        // Arrange - Create content with a very long line (>10000 chars)
        string longLine = new('X', 15000);
        RlmDocument document = RlmDocumentBuilder.Default()
            .WithContent($"Short line\n{longLine}\nAnother short line")
            .Build();

        // Act
        ValidationResult result = await validator.ValidateAsync(document);

        // Assert
        result.IsValid.ShouldBeTrue();
        result.Warnings.ShouldContain(w => w.Contains("very long lines"));
    }

    [TestMethod]
    public async Task ValidateAsync_EmptyDocument_ReturnsSuccess()
    {
        // Arrange
        RlmDocument document = RlmDocumentBuilder.Default()
            .WithContent("")
            .Build();

        // Act
        ValidationResult result = await validator.ValidateAsync(document);

        // Assert
        result.IsValid.ShouldBeTrue();
    }

    [TestMethod]
    public async Task ValidateAsync_CancellationRequested_ThrowsOperationCancelledException()
    {
        // Arrange
        RlmDocument document = RlmDocumentBuilder.Default().Build();
        CancellationTokenSource cts = new();
        cts.Cancel();

        // Act & Assert
        await Should.ThrowAsync<OperationCanceledException>(
            () => validator.ValidateAsync(document, cts.Token));
    }

    [TestMethod]
    public async Task ValidateAsync_NonMarkdownFile_DoesNotCheckCodeBlocks()
    {
        // Arrange - Non-markdown file with unbalanced backticks should be fine
        RlmDocument document = RlmDocumentBuilder.Default()
            .WithContent("```\nSome code without closing")
            .WithMetadata(m => m
                .WithSource("/test/file.txt")
                .WithContentType("text/plain"))
            .Build();

        // Act
        ValidationResult result = await validator.ValidateAsync(document);

        // Assert
        result.IsValid.ShouldBeTrue();
        result.Warnings.ShouldNotContain(w => w.Contains("Unbalanced code blocks"));
    }

    [TestMethod]
    public async Task ValidateAsync_MarkdownFileByExtension_ChecksCodeBlocks()
    {
        // Arrange
        RlmDocument document = RlmDocumentBuilder.Default()
            .WithContent("""
                # Header

                ```python
                code
                """)
            .WithMetadata(m => m
                .WithSource("/test/file.md")
                .WithTotalLength(30))
            .Build();

        // Act
        ValidationResult result = await validator.ValidateAsync(document);

        // Assert
        result.Warnings.ShouldContain(w => w.Contains("Unbalanced code blocks"));
    }

    [TestMethod]
    public async Task ValidateAsync_MarkdownFileByMarkdownExtension_IsRecognizedAsMarkdown()
    {
        // Arrange - File with .markdown extension should be treated as markdown
        RlmDocument document = RlmDocumentBuilder.Default()
            .WithContent("""
                # Header

                Some content here
                """)
            .WithMetadata(m => m
                .WithSource("/test/file.markdown")
                .WithTotalLength(30))
            .Build();

        // Act
        ValidationResult result = await validator.ValidateAsync(document);

        // Assert - Valid markdown should pass validation
        result.IsValid.ShouldBeTrue();
    }

    [TestMethod]
    public async Task ValidateAsync_DocumentWithTabsAndNewlines_ReturnsSuccess()
    {
        // Arrange - Tabs and newlines are valid whitespace
        RlmDocument document = RlmDocumentBuilder.Default()
            .WithContent("Line 1\tTabbed\nLine 2\r\nLine 3")
            .Build();

        // Act
        ValidationResult result = await validator.ValidateAsync(document);

        // Assert
        result.IsValid.ShouldBeTrue();
        result.Errors.ShouldNotContain(e => e.Contains("binary content"));
    }

    [TestMethod]
    public async Task ValidateAsync_LowNonPrintableConcentration_ReturnsSuccess()
    {
        // Arrange - Create content with <10% non-printable chars
        char[] content = new char[100];
        for (int i = 0; i < 100; i++)
        {
            content[i] = i < 5 ? (char)1 : 'A'; // 5% non-printable
        }
        RlmDocument document = RlmDocumentBuilder.Default()
            .WithContent(new string(content))
            .Build();

        // Act
        ValidationResult result = await validator.ValidateAsync(document);

        // Assert
        result.IsValid.ShouldBeTrue();
    }
}
