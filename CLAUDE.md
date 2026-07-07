# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What This Is

HtmlToOpenXml is a .NET library that converts HTML strings into OpenXml (Word document) elements. It produces `IList<OpenXmlCompositeElement>` that callers insert into a `MainDocumentPart`. The HTML parser is AngleSharp (W3C-compliant, replaced regex-based parsing in v3).

## Commands

```powershell
# Restore
dotnet restore

# Build
dotnet build --configuration Release

# Test (all frameworks)
dotnet test --configuration Release --verbosity normal

# Test (single framework)
dotnet test --framework net8.0 --configuration Release

# Test (single test by name)
dotnet test --filter "FullyQualifiedName~TableTests" --configuration Release

# Pack NuGet
dotnet pack src/Html2OpenXml/HtmlToOpenXml.csproj --configuration Release --output ./nupkg
```

Target frameworks: `net462`, `netstandard2.0`, `net8.0`, `net10.0`. Tests cover `net462`, `net8.0`, `net10.0`.

CI runs on push to `dev` and on PRs (`.github/workflows/dotnet.yml`). NuGet publish triggers on version tags pushed to `master` (`.github/workflows/publish_nuget.yml`).

## Architecture

### Entry Point

`HtmlConverter` (root of `src/Html2OpenXml/`) is the public API. Callers construct it with a `MainDocumentPart` and optional `IWebRequest` for image fetching, then call `Parse(string html)` or `ParseAsync(string html, CancellationToken)`.

### Data Flow

```
HTML string
  → AngleSharp DOM
  → ParsingContext (holds cascading styles, bookmarks, property bag)
  → HtmlDomExpression tree (one Expression subclass per tag type)
  → OpenXml elements appended to MainDocumentPart
  → returned IList<OpenXmlCompositeElement>
```

### Expression Pattern

Each HTML element type maps to an `*Expression` class under `src/Html2OpenXml/Expressions/`. All inherit `HtmlElementExpression : HtmlDomExpression`. Key method is `CascadeStyles()` which propagates CSS through nesting.

Notable sub-hierarchies:
- `Expressions/Table/` — `TableExpression`, `TableCellExpression`, `TableColExpression`, `TableCaptionExpression` (colspan/rowspan handled here)
- `Expressions/Numbering/` — `ListExpression`, `HeadingElementExpression`
- `Expressions/Image/` — `ImageExpression`, `SvgExpression`

### State Management

`ParsingContext` is the single context object threaded through the parse. It holds:
- Document styles (`WordDocumentStyle`)
- Bookmarks
- Cascading style state
- Property bag for cross-expression communication

`WordDocumentStyle` manages font registration, predefined styles, and style lookup/creation in the document.

### Image Loading

`IImageLoader` / `DefaultWebRequest` handle image downloading. The loader caches by URL so the same image isn't re-fetched across header, body, and footer sections. `IWebRequest` is the HTTP client abstraction — pass a custom implementation to `HtmlConverter` for mocked or authenticated requests.

### Conditional Compilation

`NET5_0_OR_GREATER` symbol is defined for the `net8.0`/`net10.0` targets. Use it for version-specific code paths; avoid adding new framework-specific branches without necessity.

## Test Conventions

- Framework: NUnit 4 + NUnit3TestAdapter, Moq for HTTP mocking
- Base class: `HtmlConverterTestBase` in `test/HtmlToOpenXml.Tests/`
- Each HTML feature has its own `*Tests.cs` file (e.g., `TableTests.cs`, `HeadingTests.cs`)
- `test/HtmlToOpenXml.Tests/Utilities/OpenXmlExtensions.cs` — helper methods for asserting OpenXml structure
- `MockHttpMessageHandler` used for image-loading tests that need HTTP
- Embedded resources in `test/HtmlToOpenXml.Tests/Resources/` for test fixtures

## Branch Strategy

- `dev` — active development, PRs target here
- `master` — releases only; pushing a version tag here triggers NuGet publish
