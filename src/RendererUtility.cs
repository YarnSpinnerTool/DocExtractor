using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.CodeAnalysis;

#nullable enable

namespace DocExtractor
{
    internal static class RendererUtility
    {
        private static readonly Dictionary<string, string> KnownTypes = new Dictionary<string, string>
        {
            {"System.String", "string"},
            {"System.Single", "float"},
            {"System.Double", "double"},
            {"System.Boolean", "bool"},
        };

        public static DefaultDictionary<string, DocumentedSymbol> CreateSymbolDictionary(IEnumerable<DocumentedSymbol> documentedSymbols)
        {
            var symbolDict = new DefaultDictionary<string, DocumentedSymbol>(documentedSymbols.Distinct().ToDictionary(s => s.DocumentationID), (key) =>
            {
                // We were asked for a symbol with a given key, but we
                // don't have one. Make one up using the key.
                key ??= "(null)";

                DocumentedSymbol documentedSymbol = new DocumentedSymbol
                {
                    AnchorName = null,
                    DisplayName = key[2..], // skip the "!:" or similar prefix
                    FullDisplayName = key[2..],
                    DocumentationID = key,
                    ContainerID = null,
                    DocumentationXml = $@"<member name=""{key}""><summary></summary></member>",
                };

                if (KnownTypes.TryGetValue(documentedSymbol.DisplayName, out var knownName))
                {
                    documentedSymbol.DisplayName = knownName;
                }

                return documentedSymbol;
            },
            (key) =>
            {
                if (key.Contains('~'))
                {
                    return key.Split('~', 1)[0];
                }
                else
                {
                    return null;
                }
            });
            return symbolDict;
        }

        public static ITypeSymbol? GetPropertyOrFieldType(ISymbol symbol)
        {
            return symbol switch
            {
                IPropertySymbol propertySymbol => propertySymbol.Type,
                IFieldSymbol fieldSymbol => fieldSymbol.Type,
                _ => null // not a property or a field
            };
        }

        public static ITypeSymbol? GetMethodReturnType(ISymbol symbol)
        {
            return symbol is IMethodSymbol methodSymbol
                ? methodSymbol.ReturnType
                : null;
        }

        public static string GetGitSha(string repoPath)
        {
            const string UnknownResult = "<UNKNOWN>";


            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = "git",
                Arguments = $"-C \"{repoPath}\" log -1 --pretty=format:%H",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            });

            if (process == null)
            {
                return UnknownResult;
            }

            string output = process.StandardOutput.ReadToEnd().Trim();
            process.WaitForExit();

            return process.ExitCode == 0
                ? output
                : UnknownResult;
        }

    }
}