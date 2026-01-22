// <copyright file="TypeRegistrar.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Microsoft.Extensions.DependencyInjection;
using Spectre.Console.Cli;

namespace Rlm.Cli.Infrastructure;

/// <summary>
/// Implements Spectre.Console.Cli type registration using Microsoft.Extensions.DependencyInjection.
/// </summary>
public sealed class TypeRegistrar(IServiceCollection services) : ITypeRegistrar
{
    public ITypeResolver Build() => new TypeResolver(services.BuildServiceProvider());

    public void Register(Type service, Type implementation)
        => services.AddSingleton(service, implementation);

    public void RegisterInstance(Type service, object implementation)
        => services.AddSingleton(service, implementation);

    public void RegisterLazy(Type service, Func<object> factory)
        => services.AddSingleton(service, _ => factory());
}

/// <summary>
/// Resolves types from the DI container.
/// </summary>
public sealed class TypeResolver(IServiceProvider provider) : ITypeResolver
{
    public object? Resolve(Type? type)
        => type is null ? null : provider.GetService(type);
}