// <copyright file="RlmDocumentBuilder.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Rlm.Cli.Core.Documents;

namespace Rlm.Cli.Tests.Builders;

/// <summary>
/// Builder for creating RlmDocument test instances.
/// </summary>
public sealed class RlmDocumentBuilder
{
    private string id = "test-document";
    private string content = "This is test content for the document.";
    private DocumentMetadata? metadata;

    public RlmDocumentBuilder WithId(string id)
    {
        this.id = id;
        return this;
    }

    public RlmDocumentBuilder WithContent(string content)
    {
        this.content = content;
        return this;
    }

    public RlmDocumentBuilder WithMetadata(DocumentMetadata metadata)
    {
        this.metadata = metadata;
        return this;
    }

    public RlmDocumentBuilder WithMetadata(Action<DocumentMetadataBuilder> configure)
    {
        DocumentMetadataBuilder builder = DocumentMetadataBuilder.Default();
        configure(builder);
        metadata = builder.Build();
        return this;
    }

    public RlmDocument Build()
    {
        DocumentMetadata metadata = this.metadata ?? new DocumentMetadataBuilder()
            .WithSource($"/test/{id}.txt")
            .WithTotalLength(content.Length)
            .WithLineCount(content.Split('\n').Length)
            .Build();

        return new()
        {
            Id = id,
            Content = content,
            Metadata = metadata
        };
    }

    public static RlmDocumentBuilder Default() => new();

    /// <summary>
    /// Creates a document with markdown content for semantic chunking tests.
    /// </summary>
    public static RlmDocumentBuilder WithMarkdownContent() => new RlmDocumentBuilder()
        .WithId("markdown-doc")
        .WithContent("""
            # Introduction

            This is the introduction section.

            ## Background

            Some background information here.

            ### Details

            More detailed information.

            ## Methodology

            The methodology section.

            # Conclusion

            Final conclusions here.
            """);

    /// <summary>
    /// Creates a document with content suitable for filter chunking tests.
    /// </summary>
    public static RlmDocumentBuilder WithSearchableContent() => new RlmDocumentBuilder()
        .WithId("searchable-doc")
        .WithContent("""
            Start of document.

            Some random text that doesn't match.

            Contact us at email@example.com for support.

            More unrelated content here.

            Another email address: test@domain.org in this section.

            End of document.
            """);
}