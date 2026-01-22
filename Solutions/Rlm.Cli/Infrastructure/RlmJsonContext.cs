// <copyright file="RlmJsonContext.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text.Json.Serialization;
using Rlm.Cli.Core.Chunking;
using Rlm.Cli.Core.Documents;
using Rlm.Cli.Core.Output;
using Rlm.Cli.Core.Session;
using Rlm.Cli.Core.Validation;

namespace Rlm.Cli.Infrastructure;

/// <summary>
/// JSON source generation context for AOT serialization support.
/// </summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(RlmSession))]
[JsonSerializable(typeof(DocumentMetadata))]
[JsonSerializable(typeof(ContentChunk))]
[JsonSerializable(typeof(List<ContentChunk>))]
[JsonSerializable(typeof(Dictionary<string, string>))]
[JsonSerializable(typeof(SessionInfoOutput))]
[JsonSerializable(typeof(ChunkOutput))]
[JsonSerializable(typeof(AggregateOutput))]
[JsonSerializable(typeof(ValidationResult))]
[JsonSerializable(typeof(List<string>))]
[JsonSerializable(typeof(IReadOnlyList<string>))]
[JsonSerializable(typeof(IReadOnlyDictionary<string, string>))]
public partial class RlmJsonContext : JsonSerializerContext;
