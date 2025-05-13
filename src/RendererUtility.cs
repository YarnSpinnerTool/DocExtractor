using System.Collections.Generic;
using System.Linq;

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
                if (key == null)
                {
                    key = "(null)";
                }

                DocumentedSymbol documentedSymbol = new DocumentedSymbol
                {
                    AnchorName = null,
                    DisplayName = key.Substring(2), // skip the "!:" or similar prefix
                    FullDisplayName = key.Substring(2),
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

    }
}