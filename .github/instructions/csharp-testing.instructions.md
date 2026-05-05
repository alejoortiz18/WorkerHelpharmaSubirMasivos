---
description: "Use when writing, reviewing, or generating unit tests, integration tests, or test doubles in C#. Covers xUnit, FluentAssertions, NSubstitute, mocking patterns, test naming, Arrange-Act-Assert, async tests, and testing Worker Services or services with ILogger."
applyTo: "**/*Tests*/**/*.cs,**/*.Tests.cs,**/*Test*.cs"
---

# C# Testing Guidelines

## Stack (use these, no others)

| Purpose | Package |
|---------|---------|
| Test runner | `xUnit` |
| Assertions | `FluentAssertions` |
| Mocking / faking | `NSubstitute` |
| Code coverage | `coverlet.collector` |
| Logging in tests | `Microsoft.Extensions.Logging.Abstractions` |

## Test Project Setup

Every test project `.csproj` must include:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <IsPackable>false</IsPackable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.*" />
    <PackageReference Include="xunit" Version="2.*" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.*" />
    <PackageReference Include="FluentAssertions" Version="6.*" />
    <PackageReference Include="NSubstitute" Version="5.*" />
    <PackageReference Include="coverlet.collector" Version="6.*">
      <IncludeAssets>runtime; build; native; contentfiles; analyzers</IncludeAssets>
      <PrivateAssets>all</PrivateAssets>
    </PackageReference>
    <PackageReference Include="Microsoft.Extensions.Logging.Abstractions" Version="10.*" />
  </ItemGroup>
</Project>
```

## Test Naming Convention

**Format**: `MethodName_StateUnderTest_ExpectedBehavior`

```csharp
// Good
[Fact]
public void ProcesarPdf_ArchivoNoExiste_RetornaNull() { }

[Fact]
public void ProcesarPdf_CodigoValido_RetornaDocumentoProcesado() { }

[Theory]
[InlineData("ABC123", "ABC", "123")]
[InlineData("XY9999", "XY", "9999")]
public void ProcesarPdf_CodigoConFormato_ExtaePrefijoYNumero(string codigo, string prefijo, string numero) { }
```

## Arrange-Act-Assert (AAA) Pattern

Always separate the three phases with blank lines:

```csharp
using FluentAssertions;
using NSubstitute;
using Xunit;                                         // siempre requerido — ImplicitUsings no lo incluye

[Fact]
public void MethodName_Scenario_ExpectedResult()
{
    // Arrange
    var logger = Substitute.For<ILogger<MyService>>();
    var sut = new MyService(logger);

    // Act
    var result = sut.DoSomething("input");

    // Assert
    result.Should().NotBeNull();
    result.Value.Should().Be("expected");
}
```

## Mocking with NSubstitute

```csharp
// Create substitute
var repository = Substitute.For<IOrderRepository>();

// Setup return value
repository.GetByIdAsync(1, Arg.Any<CancellationToken>())
          .Returns(new Order { Id = 1, Status = "Active" });

// Verify call was made
await repository.Received(1).GetByIdAsync(1, Arg.Any<CancellationToken>());

// Verify call was NOT made
repository.DidNotReceive().Delete(Arg.Any<Order>());
```

### Mocking ILogger (use NullLogger, never mock directly)

```csharp
// Correct — use NullLogger for clean tests
var logger = new NullLogger<MyService>();
var sut = new MyService(logger);

// Only if you need to verify log calls
var logger = Substitute.For<ILogger<MyService>>();
// Then verify:
logger.Received().Log(
    LogLevel.Warning,
    Arg.Any<EventId>(),
    Arg.Is<object>(o => o.ToString()!.Contains("Archivo no existe")),
    null,
    Arg.Any<Func<object, Exception?, string>>());
```

## FluentAssertions Cheat Sheet

```csharp
// Nulls
result.Should().BeNull();
result.Should().NotBeNull();

// Equality
result.Value.Should().Be(42);
result.Name.Should().Be("expected");

// Collections
list.Should().HaveCount(3);
list.Should().Contain(x => x.Id == 1);
list.Should().BeEmpty();
list.Should().NotBeEmpty();

// Strings
text.Should().StartWith("ABC");
text.Should().Contain("barcode");
text.Should().MatchRegex(@"^[A-Z]+\d+$");

// Exceptions
act.Should().Throw<ArgumentNullException>()
   .WithMessage("*rutaPdf*");

// Async exceptions
await act.Should().ThrowAsync<InvalidOperationException>();

// Boolean
flag.Should().BeTrue();
flag.Should().BeFalse();

// Numeric
value.Should().BeGreaterThan(0);
value.Should().BeInRange(1, 100);

// Types
obj.Should().BeOfType<DocumentoProcesadoDto>();
obj.Should().BeAssignableTo<IDisposable>();
```

## Async Tests

```csharp
[Fact]
public async Task GetOrderAsync_ValidId_ReturnsOrder()
{
    // Arrange
    var repository = Substitute.For<IOrderRepository>();
    repository.GetByIdAsync(1, Arg.Any<CancellationToken>())
              .Returns(new Order { Id = 1 });
    var sut = new OrderService(repository);

    // Act
    var result = await sut.GetOrderAsync(1, CancellationToken.None);

    // Assert
    result.Should().NotBeNull();
    result!.Id.Should().Be(1);
}
```

## Testing Worker Services (BackgroundService)

```csharp
[Fact]
public async Task ExecuteAsync_ProcessesFiles_CallsServiceOnce()
{
    // Arrange
    using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));
    var service = Substitute.For<IFileProcessingService>();
    var worker = new MyWorker(service, new NullLogger<MyWorker>());

    // Act
    await worker.StartAsync(cts.Token);
    await Task.Delay(100);
    await worker.StopAsync(CancellationToken.None);

    // Assert
    await service.Received().ProcessAsync(Arg.Any<CancellationToken>());
}
```

## Test Data — Avoid Magic Values

Use private constants or helper builders:

```csharp
private const string ValidPdfPath = "TestData/valid_barcode.pdf";
private const string ExpectedPrefix = "ABC";
private const string ExpectedNumber = "12345";

// Or a builder for complex objects
private static DocumentoProcesadoDto BuildDocumento(string prefijo = "ABC", string numero = "123")
    => new() { Prefijo = prefijo, Numero = numero, NombreArchivo = $"{prefijo}{numero}.pdf" };
```

## Test Organization

```
Tests/
  Services/
    BarcodeRegionServiceTests.cs
    SoporteApiServiceTests.cs
  Infrastructure/
    FileManagerTests.cs
  Business/
    OrderBusinessTests.cs
  TestData/            ← sample files needed by tests
    valid_barcode.pdf
```

## Anti-patterns to Avoid

- Never test private methods directly — test via public surface
- Never use `Thread.Sleep` in tests — use `CancellationTokenSource` with timeout
- Never share mutable state between tests — each `[Fact]` is independent
- Never assert on implementation details — assert on observable output
- Never use `Moq` (use NSubstitute instead)
