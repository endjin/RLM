// <copyright file="SyntacticValidator.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using System.Text.RegularExpressions;
using Rlm.Cli.Core.Documents;

namespace Rlm.Cli.Core.Validation;

/// <summary>
/// Validates document syntax including UTF-8 encoding, balanced code blocks,
/// and detects potential binary content.
/// </summary>
public sealed partial class SyntacticValidator : IDocumentValidator
{
    public Task<ValidationResult> ValidateAsync(RlmDocument document, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        List<string> errors = [];
        List<string> warnings = [];

        // Check for binary content (null bytes or high concentration of non-printable characters)
        if (ContainsBinaryContent(document.Content))
        {
            errors.Add("Document appears to contain binary content. Only text documents are supported.");
            return Task.FromResult(ValidationResult.Failure([.. errors], [.. warnings]));
        }

        // Check for valid UTF-8 encoding
        if (!IsValidUtf8(document.Content))
        {
            warnings.Add("Document contains potentially invalid UTF-8 sequences.");
        }

        // Check for balanced code blocks in markdown
        if (IsMarkdown(document))
        {
            int openCodeBlocks = CodeBlockStartRegex().Matches(document.Content).Count;
            int closeCodeBlocks = CodeBlockEndRegex().Matches(document.Content).Count;

            if (openCodeBlocks != closeCodeBlocks)
            {
                warnings.Add($"Unbalanced code blocks detected: {openCodeBlocks} opening, {closeCodeBlocks} closing.");
            }
        }

        // Check for extremely long lines (potential minified content)
        int maxLineLength = GetMaxLineLength(document.Content);
        if (maxLineLength > 10000)
        {
            warnings.Add($"Document contains very long lines (max: {maxLineLength:N0} chars). This may be minified content.");
        }

        return Task.FromResult(errors.Count > 0
            ? ValidationResult.Failure([.. errors], [.. warnings])
            : warnings.Count > 0
                ? ValidationResult.SuccessWithWarnings([.. warnings])
                : ValidationResult.Success());
    }

    private static bool ContainsBinaryContent(string content)
    {
        if (string.IsNullOrEmpty(content))
        {
            return false;
        }

        // Check for null bytes (definite binary indicator)
        if (content.Contains('\0'))
        {
            return true;
        }

        // Check concentration of non-printable characters
        int nonPrintableCount = 0;
        int sampleSize = Math.Min(content.Length, 1000);

        for (int i = 0; i < sampleSize; i++)
        {
            char c = content[i];
            if (c < 32 && c != '\t' && c != '\n' && c != '\r')
            {
                nonPrintableCount++;
            }
        }

        // If more than 10% non-printable, likely binary
        return nonPrintableCount > sampleSize * 0.1;
    }

    private static bool IsValidUtf8(string content)
    {
        try
        {
            // Try to encode and decode - invalid sequences will throw
            byte[] bytes = Encoding.UTF8.GetBytes(content);
            _ = Encoding.UTF8.GetString(bytes);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool IsMarkdown(RlmDocument document)
    {
        return document.Metadata.ContentType == "text/markdown" ||
               document.Metadata.Source.EndsWith(".md", StringComparison.OrdinalIgnoreCase) ||
               document.Metadata.Source.EndsWith(".markdown", StringComparison.OrdinalIgnoreCase);
    }

    private static int GetMaxLineLength(string content)
    {
        int maxLength = 0;
        int currentLength = 0;

        foreach (char c in content)
        {
            if (c == '\n')
            {
                maxLength = Math.Max(maxLength, currentLength);
                currentLength = 0;
            }
            else
            {
                currentLength++;
            }
        }

        return Math.Max(maxLength, currentLength);
    }

    [GeneratedRegex(@"^```\w*\s*$", RegexOptions.Multiline | RegexOptions.Compiled)]
    private static partial Regex CodeBlockStartRegex();

    [GeneratedRegex(@"^```\s*$", RegexOptions.Multiline | RegexOptions.Compiled)]
    private static partial Regex CodeBlockEndRegex();
}
