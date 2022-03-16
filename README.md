# DocExtractor

DocExtractor is a tool that reads C# source code, extracts documentation from its comments, and produces HTML or Markdown content. DocExtractor is used to generate the [API documentation for Yarn Spinner](https://docs.yarnspinner.dev/api/csharp).

DocExtractor uses Roslyn to read the source code, and performs a _partial compilation_ of your source code. DocExtractor is thus able to build documentation even when the project doesn't fully build; for example, if your source code contains errors, or references to assemblies that you don't have, then DocExtractor will still be able to produce useful output.

DocExtractor is **not an officially supported Yarn Spinner project**. We use it internally, but we can't offer any support for its use. In particular, our current focus is on producing Markdown content for GitBook sites; other use cases are less maintained.

## Usage

First, download and install the .NET runtime for your platform.

Next, clone this repository.

DocExtractor is configured via JSON files. To create a new configuration file, use the `create` command:

```bash
dotnet run --project src/DocExtractor.csproj -- create MyDocumentation.json
```

This will create a new configuration file. To build documentation, use the `build` command and specify the configuration file:

```bash
dotnet run --project src/DocExtractor.csproj -- build MyDocumentation.json
```

## Configuration

The configuration file is a JSON file that supports comments.

### `projects`

An array of paths to the `.csproj` files you want to generate documentation for.

```jsonc
"projects": [
    "../YarnSpinner/YarnSpinner/YarnSpinner.csproj",
    "../YarnSpinner/YarnSpinner.Compiler/YarnSpinner.Compiler.csproj",
    "../YarnSpinner-Unity/YarnSpinner.Unity.csproj"
],
```

### `excludeRegexes`

An array of regular expressions. Any symbols in the source code that match any of these expressions will not be included in the documentation.

```jsonc
"excludeRegexes": [
    // Exclude certain namespaces that aren't intended for public consumption
    "^Yarn\\.Analysis",
    "^Yarn\\.Dialogue\\.Analyse",
    "^Yarn\\.Compiler\\.Graph",
    "^Yarn\\.Compiler\\.YarnSpinner(V1)?(Parser|Lexer)",
    "^Yarn\\.Compiler\\.IYarnSpinner(V1)?(Parser|Lexer)",
    "^CLDRPlurals"
]
```

### `outputFormat`

A string. Valid values are `html` or `markdown`. Specifies the output format for the generated documentation.

* `html` mode will generate a single HTML file containing the documentation.
* `markdown` mode will generate a markdown file for each symbol, as well as a `SUMMARY.md` file that contains a table of contents. The generated markdown is designed to be used by GitBook, but other platforms can make use of it as well.

```jsonc
"outputFormat": "markdown"
```

### `outputFolder`

A string containing the path to the folder that the generated source code should be placed.

```jsonc
"outputFolder": "/Users/desplesda/Work/YSDocs/api/csharp/"
```

### `pathPrefix`

(Only used in Markdown mode). A string containing a prefix to use for all inter-document links.

```jsonc
"pathPrefix": "/api/csharp"
```

### `namespaceSummaries`

A dictionary mapping the symbol names of namespaces to descriptions of those namespaces. (This is specified in this configuration file because C# doesn't have a way to associate XML documentation comments with namespaces.)

```jsonc
"namespaceSummaries": {
    "Yarn": "Contains classes for working with compiled Yarn programs.",
    "Yarn.Markup": "Contains classes for working with markup in Yarn lines.",
    "Yarn.Unity": "Contains classes for working with Yarn Spinner in the Unity game engine.",
}
```

### `preprocessorSymbols`

An array of strings containing preprocessor definitions. These symbols will be defined during DocExtractor's compilation of the source code.

```jsonc
"preprocessorSymbols": [
    "USE_INPUTSYSTEM",
    "ENABLE_INPUT_SYSTEM",
    "ENABLE_LEGACY_INPUT_MANAGER",
    "USE_ADDRESSABLES"
],
```

### `msBuildPath`

A string containing the path to MSBuild. If this is null, DocExtractor will attempt to locate MSBuild on the system automatically.

```jsonc
"msBuildPath": "/usr/local/share/dotnet/x64/sdk/5.0.404"
```
