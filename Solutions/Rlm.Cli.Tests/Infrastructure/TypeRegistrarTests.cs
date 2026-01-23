// <copyright file="TypeRegistrarTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Microsoft.Extensions.DependencyInjection;
using Rlm.Cli.Infrastructure;
using Shouldly;
using Spectre.Console.Cli;

namespace Rlm.Cli.Tests.Infrastructure;

[TestClass]
public sealed class TypeRegistrarTests
{
    [TestMethod]
    public void Register_ServiceAndImplementation_RegistersInContainer()
    {
        // Arrange
        ServiceCollection services = new();
        TypeRegistrar registrar = new(services);

        // Act
        registrar.Register(typeof(ITestService), typeof(TestService));
        ITypeResolver resolver = registrar.Build();

        // Assert
        object? service = resolver.Resolve(typeof(ITestService));
        service.ShouldNotBeNull();
        service.ShouldBeOfType<TestService>();
    }

    [TestMethod]
    public void RegisterInstance_RegistersSpecificInstance()
    {
        // Arrange
        ServiceCollection services = new();
        TypeRegistrar registrar = new(services);
        TestService instance = new();

        // Act
        registrar.RegisterInstance(typeof(ITestService), instance);
        ITypeResolver resolver = registrar.Build();

        // Assert
        object? resolved = resolver.Resolve(typeof(ITestService));
        resolved.ShouldBeSameAs(instance);
    }

    [TestMethod]
    public void RegisterLazy_RegistersFactoryFunction()
    {
        // Arrange
        ServiceCollection services = new();
        TypeRegistrar registrar = new(services);
        int callCount = 0;

        // Act
        registrar.RegisterLazy(typeof(ITestService), () =>
        {
            callCount++;
            return new TestService();
        });
        ITypeResolver resolver = registrar.Build();

        // First resolve
        object? service1 = resolver.Resolve(typeof(ITestService));
        // Second resolve (should return same instance due to singleton)
        object? service2 = resolver.Resolve(typeof(ITestService));

        // Assert
        service1.ShouldNotBeNull();
        service2.ShouldBeSameAs(service1);
        callCount.ShouldBe(1); // Factory only called once
    }

    [TestMethod]
    public void Build_ReturnsTypeResolver()
    {
        // Arrange
        ServiceCollection services = new();
        TypeRegistrar registrar = new(services);

        // Act
        ITypeResolver resolver = registrar.Build();

        // Assert
        resolver.ShouldNotBeNull();
        resolver.ShouldBeOfType<TypeResolver>();
    }

    [TestMethod]
    public void TypeResolver_Resolve_NullType_ReturnsNull()
    {
        // Arrange
        ServiceCollection services = new();
        ServiceProvider provider = services.BuildServiceProvider();
        TypeResolver resolver = new(provider);

        // Act
        object? result = resolver.Resolve(null);

        // Assert
        result.ShouldBeNull();
    }

    [TestMethod]
    public void TypeResolver_Resolve_UnregisteredType_ReturnsNull()
    {
        // Arrange
        ServiceCollection services = new();
        ServiceProvider provider = services.BuildServiceProvider();
        TypeResolver resolver = new(provider);

        // Act
        object? result = resolver.Resolve(typeof(ITestService));

        // Assert
        result.ShouldBeNull();
    }

    [TestMethod]
    public void TypeResolver_Resolve_RegisteredType_ReturnsInstance()
    {
        // Arrange
        ServiceCollection services = new();
        services.AddSingleton<ITestService, TestService>();
        ServiceProvider provider = services.BuildServiceProvider();
        TypeResolver resolver = new(provider);

        // Act
        object? result = resolver.Resolve(typeof(ITestService));

        // Assert
        result.ShouldNotBeNull();
        result.ShouldBeOfType<TestService>();
    }

    [TestMethod]
    public void Register_MultipleServices_AllResolve()
    {
        // Arrange
        ServiceCollection services = new();
        TypeRegistrar registrar = new(services);

        // Act
        registrar.Register(typeof(ITestService), typeof(TestService));
        registrar.Register(typeof(IAnotherService), typeof(AnotherService));
        ITypeResolver resolver = registrar.Build();

        // Assert
        resolver.Resolve(typeof(ITestService)).ShouldBeOfType<TestService>();
        resolver.Resolve(typeof(IAnotherService)).ShouldBeOfType<AnotherService>();
    }

    [TestMethod]
    public void RegisterInstance_OverwritesPreviousRegistration()
    {
        // Arrange
        ServiceCollection services = new();
        TypeRegistrar registrar = new(services);
        TestService instance1 = new();
        TestService instance2 = new();

        // Act
        registrar.RegisterInstance(typeof(ITestService), instance1);
        registrar.RegisterInstance(typeof(ITestService), instance2);
        ITypeResolver resolver = registrar.Build();

        // Assert - second registration should be returned (last wins)
        object? resolved = resolver.Resolve(typeof(ITestService));
        resolved.ShouldBeSameAs(instance2);
    }

    // Test interfaces and implementations
    private interface ITestService { }
    private sealed class TestService : ITestService { }
    private interface IAnotherService { }
    private sealed class AnotherService : IAnotherService { }
}