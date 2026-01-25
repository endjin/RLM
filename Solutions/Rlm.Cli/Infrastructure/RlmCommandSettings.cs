// <copyright file="RlmCommandSettings.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.ComponentModel;
using Spectre.Console.Cli;

namespace Rlm.Cli.Infrastructure;

/// <summary>
/// Base settings for all RLM commands, providing common options like session ID.
/// </summary>
public abstract class RlmCommandSettings : CommandSettings
{
    [CommandOption("--session <ID>")]
    [Description("Session identifier or path for state isolation.")]
    public string? SessionId { get; set; }
}