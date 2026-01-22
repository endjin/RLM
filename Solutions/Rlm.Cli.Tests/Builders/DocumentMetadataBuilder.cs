// <copyright file="DocumentMetadataBuilder.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Rlm.Cli.Core.Documents;

namespace Rlm.Cli.Tests.Builders;

/// <summary>
/// Builder for creating DocumentMetadata test instances.
/// </summary>
public sealed class DocumentMetadataBuilder
{
    private string source = "/test/document.txt";
    private int totalLength = 1000;
    private int tokenEstimate = 250;
    private int lineCount = 50;
    private DateTimeOffset loadedAt = DateTimeOffset.UtcNow;
    private string? contentType;
    private string? title;

    public DocumentMetadataBuilder WithSource(string source)
    {
        this.source = source;
        return this;
    }

    public DocumentMetadataBuilder WithTotalLength(int totalLength)
    {
        this.totalLength = totalLength;
        tokenEstimate = totalLength / 4;
        return this;
    }

    public DocumentMetadataBuilder WithTokenEstimate(int tokenEstimate)
    {
        this.tokenEstimate = tokenEstimate;
        return this;
    }

    public DocumentMetadataBuilder WithLineCount(int lineCount)
    {
        this.lineCount = lineCount;
        return this;
    }

    public DocumentMetadataBuilder WithLoadedAt(DateTimeOffset loadedAt)
    {
        this.loadedAt = loadedAt;
        return this;
    }

    public DocumentMetadataBuilder WithContentType(string contentType)
    {
        this.contentType = contentType;
        return this;
    }

    public DocumentMetadataBuilder WithTitle(string title)
    {
        this.title = title;
        return this;
    }

    public DocumentMetadata Build() => new()
    {
        Source = source,
        TotalLength = totalLength,
        TokenEstimate = tokenEstimate,
        LineCount = lineCount,
        LoadedAt = loadedAt,
        ContentType = contentType,
        Title = title
    };

    public static DocumentMetadataBuilder Default() => new();
}