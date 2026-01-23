// <copyright file="RlmJsonContextTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text.Json;
using Rlm.Cli.Core.Chunking;
using Rlm.Cli.Core.Documents;
using Rlm.Cli.Core.Session;
using Rlm.Cli.Infrastructure;
using Rlm.Cli.Tests.Builders;
using Shouldly;

namespace Rlm.Cli.Tests.Infrastructure;

[TestClass]
public sealed class RlmJsonContextTests
{
    [TestMethod]
    public void Serialize_RlmSession_ProducesValidJson()
    {
        // Arrange
        RlmSession session = RlmSessionBuilder.WithStoredResults().Build();

        // Act
        string json = JsonSerializer.Serialize(session, RlmJsonContext.Default.RlmSession);

        // Assert
        json.ShouldNotBeNullOrEmpty();
        json.ShouldContain("content");
        json.ShouldContain("chunkBuffer");
        json.ShouldContain("results");
    }

    [TestMethod]
    public void Deserialize_ValidJson_ProducesRlmSession()
    {
        // Arrange
        string json = """
                      {
                        "content": "Test content",
                        "chunkBuffer": [],
                        "currentChunkIndex": 0,
                        "results": {"key": "value"}
                      }
                      """;

        // Act
        RlmSession? session = JsonSerializer.Deserialize(json, RlmJsonContext.Default.RlmSession);

        // Assert
        session.ShouldNotBeNull();
        session.Content.ShouldBe("Test content");
        session.Results["key"].ShouldBe("value");
    }

    [TestMethod]
    public void RoundTrip_RlmSession_PreservesData()
    {
        // Arrange
        RlmSession original = RlmSessionBuilder.Default()
            .WithContent("Round trip content")
            .WithMetadata(m => m
                .WithSource("/test/path.txt")
                .WithTotalLength(100)
                .WithLineCount(5))
            .WithChunks(2)
            .WithResult("key1", "value1")
            .Build();

        // Act
        string json = JsonSerializer.Serialize(original, RlmJsonContext.Default.RlmSession);
        RlmSession? deserialized = JsonSerializer.Deserialize(json, RlmJsonContext.Default.RlmSession);

        // Assert
        deserialized.ShouldNotBeNull();
        deserialized.Content.ShouldBe(original.Content);
        deserialized.Metadata!.Source.ShouldBe(original.Metadata!.Source);
        deserialized.ChunkBuffer.Count.ShouldBe(original.ChunkBuffer.Count);
        deserialized.Results["key1"].ShouldBe("value1");
    }

    [TestMethod]
    public void Serialize_UsesCamelCase()
    {
        // Arrange
        RlmSession session = RlmSessionBuilder.WithLoadedDocument().Build();

        // Act
        string json = JsonSerializer.Serialize(session, RlmJsonContext.Default.RlmSession);

        // Assert - verify camelCase property names are used
        json.ShouldContain("\"content\":");
        json.ShouldContain("\"chunkBuffer\":");
        json.ShouldContain("\"currentChunkIndex\":");
        // PascalCase versions should not appear (use regex to match exact case)
        json.IndexOf("\"Content\":", StringComparison.Ordinal).ShouldBe(-1);
        json.IndexOf("\"ChunkBuffer\":", StringComparison.Ordinal).ShouldBe(-1);
    }

    [TestMethod]
    public void Serialize_IgnoresNullValues()
    {
        // Arrange
        RlmSession session = new(); // Content and Metadata are null

        // Act
        string json = JsonSerializer.Serialize(session, RlmJsonContext.Default.RlmSession);

        // Assert
        // When null values are ignored, the key won't appear
        json.ShouldNotContain("\"content\":null");
        json.ShouldNotContain("\"metadata\":null");
    }

    [TestMethod]
    public void Serialize_DocumentMetadata_PreservesAllProperties()
    {
        // Arrange
        DateTimeOffset loadedAt = new(2025, 1, 15, 10, 30, 0, TimeSpan.Zero);
        DocumentMetadata metadata = DocumentMetadataBuilder.Default()
            .WithSource("/test/doc.txt")
            .WithTotalLength(500)
            .WithTokenEstimate(125)
            .WithLineCount(25)
            .WithLoadedAt(loadedAt)
            .Build();

        // Act
        string json = JsonSerializer.Serialize(metadata, RlmJsonContext.Default.DocumentMetadata);
        DocumentMetadata? deserialized = JsonSerializer.Deserialize(json, RlmJsonContext.Default.DocumentMetadata);

        // Assert
        deserialized.ShouldNotBeNull();
        deserialized.Source.ShouldBe(metadata.Source);
        deserialized.TotalLength.ShouldBe(metadata.TotalLength);
        deserialized.TokenEstimate.ShouldBe(metadata.TokenEstimate);
        deserialized.LineCount.ShouldBe(metadata.LineCount);
        deserialized.LoadedAt.ShouldBe(loadedAt);
    }

    [TestMethod]
    public void Serialize_ContentChunk_PreservesMetadataDictionary()
    {
        // Arrange
        ContentChunk chunk = ContentChunkBuilder.WithUniformMetadata("doc-1", 5)
            .WithIndex(2)
            .WithContent("Chunk content here")
            .WithPositions(100, 118)
            .Build();

        // Act
        string json = JsonSerializer.Serialize(chunk, RlmJsonContext.Default.ContentChunk);
        ContentChunk? deserialized = JsonSerializer.Deserialize(json, RlmJsonContext.Default.ContentChunk);

        // Assert
        deserialized.ShouldNotBeNull();
        deserialized.Index.ShouldBe(2);
        deserialized.Content.ShouldBe("Chunk content here");
        deserialized.StartPosition.ShouldBe(100);
        deserialized.EndPosition.ShouldBe(118);
        deserialized.Metadata["documentId"].ShouldBe("doc-1");
        deserialized.Metadata["totalChunks"].ShouldBe("5");
        deserialized.Metadata["strategy"].ShouldBe("uniform");
    }

    [TestMethod]
    public void Serialize_ListOfContentChunks_Works()
    {
        // Arrange
        List<ContentChunk> chunks =
        [
            ContentChunkBuilder.Default().WithIndex(0).WithContent("Chunk 0").Build(),
            ContentChunkBuilder.Default().WithIndex(1).WithContent("Chunk 1").Build()
        ];

        // Act
        string json = JsonSerializer.Serialize(chunks, RlmJsonContext.Default.ListContentChunk);
        List<ContentChunk>? deserialized = JsonSerializer.Deserialize(json, RlmJsonContext.Default.ListContentChunk);

        // Assert
        deserialized.ShouldNotBeNull();
        deserialized.Count.ShouldBe(2);
        deserialized[0].Content.ShouldBe("Chunk 0");
        deserialized[1].Content.ShouldBe("Chunk 1");
    }

    [TestMethod]
    public void Serialize_DictionaryStringString_Works()
    {
        // Arrange
        Dictionary<string, string> dict = new()
        {
            ["key1"] = "value1",
            ["key2"] = "value2"
        };

        // Act
        string json = JsonSerializer.Serialize(dict, RlmJsonContext.Default.DictionaryStringString);
        Dictionary<string, string>? deserialized = JsonSerializer.Deserialize(json, RlmJsonContext.Default.DictionaryStringString);

        // Assert
        deserialized.ShouldNotBeNull();
        deserialized.Count.ShouldBe(2);
        deserialized["key1"].ShouldBe("value1");
        deserialized["key2"].ShouldBe("value2");
    }

    [TestMethod]
    public void Serialize_FormatsWithIndentation()
    {
        // Arrange
        RlmSession session = new();

        // Act
        string json = JsonSerializer.Serialize(session, RlmJsonContext.Default.RlmSession);

        // Assert
        json.ShouldContain("\n"); // Indented output contains newlines
    }
}