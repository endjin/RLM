// <copyright file="ResultBufferTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Rlm.Cli.Core.Session;
using Shouldly;

namespace Rlm.Cli.Tests.Core.Session;

[TestClass]
public sealed class ResultBufferTests
{
    [TestMethod]
    public void Store_NewKey_AddsToBuffer()
    {
        // Arrange
        ResultBuffer buffer = new();

        // Act
        buffer.Store("key1", "value1");

        // Assert
        buffer.Count.ShouldBe(1);
        buffer.Get("key1").ShouldBe("value1");
    }

    [TestMethod]
    public void Store_ExistingKey_OverwritesValue()
    {
        // Arrange
        ResultBuffer buffer = new();
        buffer.Store("key1", "old value");

        // Act
        buffer.Store("key1", "new value");

        // Assert
        buffer.Count.ShouldBe(1);
        buffer.Get("key1").ShouldBe("new value");
    }

    [TestMethod]
    public void Get_ExistingKey_ReturnsValue()
    {
        // Arrange
        ResultBuffer buffer = new();
        buffer.Store("key1", "value1");

        // Act
        string? result = buffer.Get("key1");

        // Assert
        result.ShouldBe("value1");
    }

    [TestMethod]
    public void Get_NonExistingKey_ReturnsNull()
    {
        // Arrange
        ResultBuffer buffer = new();

        // Act
        string? result = buffer.Get("nonexistent");

        // Assert
        result.ShouldBeNull();
    }

    [TestMethod]
    public void GetAll_ReturnsAllStoredResults()
    {
        // Arrange
        ResultBuffer buffer = new();
        buffer.Store("key1", "value1");
        buffer.Store("key2", "value2");

        // Act
        IReadOnlyDictionary<string, string> all = buffer.GetAll();

        // Assert
        all.Count.ShouldBe(2);
        all["key1"].ShouldBe("value1");
        all["key2"].ShouldBe("value2");
    }

    [TestMethod]
    public void GetAll_EmptyBuffer_ReturnsEmptyDictionary()
    {
        // Arrange
        ResultBuffer buffer = new();

        // Act
        IReadOnlyDictionary<string, string> all = buffer.GetAll();

        // Assert
        all.ShouldBeEmpty();
    }

    [TestMethod]
    public void GetCombined_DefaultSeparator_CombinesWithDoubleDash()
    {
        // Arrange
        ResultBuffer buffer = new();
        buffer.Store("chunk_0", "Result 0");
        buffer.Store("chunk_1", "Result 1");

        // Act
        string combined = buffer.GetCombined();

        // Assert
        combined.ShouldContain("[chunk_0]");
        combined.ShouldContain("Result 0");
        combined.ShouldContain("[chunk_1]");
        combined.ShouldContain("Result 1");
        combined.ShouldContain("---");
    }

    [TestMethod]
    public void GetCombined_CustomSeparator_UsesSeparator()
    {
        // Arrange
        ResultBuffer buffer = new();
        buffer.Store("a", "1");
        buffer.Store("b", "2");

        // Act
        string combined = buffer.GetCombined("|||");

        // Assert
        combined.ShouldContain("|||");
    }

    [TestMethod]
    public void GetCombined_EmptyBuffer_ReturnsEmptyString()
    {
        // Arrange
        ResultBuffer buffer = new();

        // Act
        string combined = buffer.GetCombined();

        // Assert
        combined.ShouldBeEmpty();
    }

    [TestMethod]
    public void Count_EmptyBuffer_ReturnsZero()
    {
        // Arrange
        ResultBuffer buffer = new();

        // Act & Assert
        buffer.Count.ShouldBe(0);
    }

    [TestMethod]
    public void Count_MultipleItems_ReturnsCorrectCount()
    {
        // Arrange
        ResultBuffer buffer = new();
        buffer.Store("key1", "value1");
        buffer.Store("key2", "value2");
        buffer.Store("key3", "value3");

        // Act & Assert
        buffer.Count.ShouldBe(3);
    }

    [TestMethod]
    public void HasResults_EmptyBuffer_ReturnsFalse()
    {
        // Arrange
        ResultBuffer buffer = new();

        // Act & Assert
        buffer.HasResults.ShouldBeFalse();
    }

    [TestMethod]
    public void HasResults_WithItems_ReturnsTrue()
    {
        // Arrange
        ResultBuffer buffer = new();
        buffer.Store("key", "value");

        // Act & Assert
        buffer.HasResults.ShouldBeTrue();
    }

    [TestMethod]
    public void Clear_RemovesAllResults()
    {
        // Arrange
        ResultBuffer buffer = new();
        buffer.Store("key1", "value1");
        buffer.Store("key2", "value2");

        // Act
        buffer.Clear();

        // Assert
        buffer.Count.ShouldBe(0);
        buffer.HasResults.ShouldBeFalse();
    }

    [TestMethod]
    public void Remove_ExistingKey_RemovesAndReturnsTrue()
    {
        // Arrange
        ResultBuffer buffer = new();
        buffer.Store("key1", "value1");

        // Act
        bool removed = buffer.Remove("key1");

        // Assert
        removed.ShouldBeTrue();
        buffer.Get("key1").ShouldBeNull();
        buffer.Count.ShouldBe(0);
    }

    [TestMethod]
    public void Remove_NonExistingKey_ReturnsFalse()
    {
        // Arrange
        ResultBuffer buffer = new();

        // Act
        bool removed = buffer.Remove("nonexistent");

        // Assert
        removed.ShouldBeFalse();
    }

    [TestMethod]
    public void GetAll_ReturnsReadOnlyDictionary()
    {
        // Arrange
        ResultBuffer buffer = new();
        buffer.Store("key", "value");

        // Act
        IReadOnlyDictionary<string, string> all = buffer.GetAll();

        // Assert - verify it's a read-only interface
        all.ShouldBeOfType<Dictionary<string, string>>();
    }
}