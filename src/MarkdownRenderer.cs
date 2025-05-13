using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DocExtractor
{
    public static class MarkdownRenderer
    {
        public static string GenerateMarkdownTableOfContentsForDocXML(IEnumerable<DocumentedSymbol> documentedSymbols, string pathPrefix, int indentLevel)
        {
            var symbolDict = RendererUtility.CreateSymbolDictionary(documentedSymbols);

            var namespaces = documentedSymbols.Where(d => d.Syntax is NamespaceDeclarationSyntax);

            var stringBuilder = new System.Text.StringBuilder();

            string LineItem(int level, DocumentedSymbol symbol)
            {
                var label = $"{symbol.DisplayName}{(symbol.Syntax is NamespaceDeclarationSyntax ? " Namespace" : string.Empty)}";

                label = EscapeMarkdownCharacters(label);

                var pathPrefixWithoutLeadingSlash = pathPrefix.TrimStart('/');

                var path = $"{label}]({pathPrefixWithoutLeadingSlash}/{symbol.AnchorName}.md";

                var indent = new string(' ', indentLevel);

                return $"{indent}{new string(' ', level * 2)}* [{path})";
            }

            var linkStack = new Stack<(DocumentedSymbol Symbol, int Level)>();

            foreach (var @namespace in namespaces)
            {
                linkStack.Push((@namespace, 0));

                while (linkStack.Count > 0)
                {
                    var (Symbol, Level) = linkStack.Pop();

                    stringBuilder.AppendLine(LineItem(Level, Symbol));

                    var children = documentedSymbols
                        .Where(d => d.Syntax is not NamespaceDeclarationSyntax && d.ContainerID == Symbol.DocumentationID)
                        .OrderByDescending(d => d.DocumentationID)
;

                    foreach (var child in children)
                    {
                        linkStack.Push((child, Level + 1));
                    }
                }
            }

            return stringBuilder.ToString();
        }

        public static string EscapeMarkdownCharacters(string label)
        {
            var replacedCharacters = new string[] {
                    "\\", "<", ">", "(", ")", "#", "`", "[", "]",
                };

            foreach (var character in replacedCharacters)
            {
                label = label.Replace(character, "\\" + character);
            }

            return label;
        }

        static string GetMarkdownLink(DocumentedSymbol symbol, string pathPrefix, bool asCode = false, bool useFullDisplayName = false)
        {
            var displayName = useFullDisplayName ? symbol.FullDisplayName : symbol.DisplayName;

            return GetMarkdownLink(displayName, symbol, pathPrefix, asCode);
        }

        static string GetMarkdownLink(string label, DocumentedSymbol symbol, string pathPrefix, bool asCode = false)
        {
            var displayName = label;

            if (symbol.AnchorName == null)
            {
                return EscapeMarkdownCharacters(displayName);
            }
            else
            {
                displayName = EscapeMarkdownCharacters((string)displayName);
                var linkText = asCode ? $"`{displayName}`" : displayName;
                return $"[{linkText}]({pathPrefix}/{symbol.AnchorName}.md)";
            }
        }

        public static Dictionary<string, string> GenerateMarkdownForDocXML(IEnumerable<DocumentedSymbol> documentedSymbols, string pathPrefix)
        {
            var symbolDict = RendererUtility.CreateSymbolDictionary(documentedSymbols);

            var output = new Dictionary<string, string>();

            // var types = documentedSymbols.Where(s => s.DocumentationID.StartsWith("T:"));

            foreach (var member in documentedSymbols)
            {
                var symbol = symbolDict[member.DocumentationID];

                var path = symbol.AnchorName + ".md";

                var stringBuilder = new System.Text.StringBuilder();

                if (symbol.Syntax is NamespaceDeclarationSyntax)
                {
                    stringBuilder.AppendLine("# " + symbol.FullDisplayName + " Namespace");
                }
                else
                {
                    stringBuilder.AppendLine("# " + symbol.FullDisplayName);
                }

                stringBuilder.AppendLine();

                var typeName = HTMLRenderer.GetTypeName(symbol);

                if (symbol.Syntax is not NamespaceDeclarationSyntax)
                {
                    var parent = documentedSymbols.FirstOrDefault(s => symbol.ContainerID == s.DocumentationID);

                    if (parent != null)
                    {
                        stringBuilder.AppendLine($"{typeName} in {GetMarkdownLink(parent, pathPrefix)}");
                        stringBuilder.AppendLine();
                    }
                }

                if (symbol.BaseTypeID != null)
                {
                    stringBuilder.Append("Inherits from ");
                    if (symbolDict.TryGetValue(symbol.BaseTypeID, out var baseSymbol))
                    {
                        stringBuilder.AppendLine($"{GetMarkdownLink(baseSymbol, pathPrefix, true)}");
                    }
                    else
                    {
                        stringBuilder.AppendLine($"`{symbol.BaseTypeID.Substring(2)}`");
                    }
                    stringBuilder.AppendLine();
                }

                var obsoleteAttribute = symbol.Symbol.GetAttributes().FirstOrDefault(a => a.AttributeClass.GetDocumentationCommentId() == "T:System.ObsoleteAttribute");

                if (obsoleteAttribute != null)
                {
                    var message = new System.Text.StringBuilder();

                    message.Append($"This {typeName?.ToLowerInvariant() ?? "item"} is <b>obsolete</b> and may be removed from a future version of Yarn Spinner");

                    if (obsoleteAttribute.ConstructorArguments.Length > 0)
                    {
                        string reason = obsoleteAttribute.ConstructorArguments[0].Value.ToString();
                        message.Append(": " + reason);
                    }

                    if (message.ToString().EndsWith('.') == false)
                    {
                        message.Append('.');
                    }

                    stringBuilder.AppendLine(CreateCallout("warning", message.ToString()));
                    stringBuilder.AppendLine();
                }

                var xml = XElement.Parse(symbol.DocumentationXml);

                var summary = xml.Element("summary");

                if (summary != null)
                {
                    stringBuilder.AppendLine("## Summary");
                    stringBuilder.AppendLine();

                    string value = CreateMarkdownFromXMLTags(symbolDict, summary);

                    stringBuilder.AppendLine(value);
                    stringBuilder.AppendLine();
                }

                // Show the declaration, if this is not a namespace
                if (symbol.Syntax is not NamespaceDeclarationSyntax)
                {
                    stringBuilder.AppendLine("```csharp");
                    stringBuilder.AppendLine(symbol.Declaration);
                    stringBuilder.AppendLine("```");
                }

                stringBuilder.AppendLine();

                // Add remarks, if present
                if (xml.Element("remarks") != null)
                {
                    stringBuilder.AppendLine("## Remarks");
                    stringBuilder.AppendLine();

                    stringBuilder.AppendLine(CreateMarkdownFromXMLTags(symbolDict, xml.Element("remarks")));

                    stringBuilder.AppendLine();
                }

                var paramNodes = xml.Elements("param");
                if (paramNodes.Any())
                {
                    stringBuilder.AppendLine("## Parameters");
                    stringBuilder.AppendLine();

                    stringBuilder.AppendLine("|Name|Description|");
                    stringBuilder.AppendLine("|:---|:---|");

                    foreach (var param in paramNodes)
                    {
                        stringBuilder.Append("|");

                        var parameterTypeID = param.Attribute("typeID")?.Value;
                        var parameterTypeName = param.Attribute("typeName")?.Value;

                        DocumentedSymbol parameterTypeSymbol = null;
                        if (parameterTypeID != null)
                        {
                            symbolDict.TryGetValue(parameterTypeID, out parameterTypeSymbol);
                        }

                        if (parameterTypeSymbol != null)
                        {
                            stringBuilder.Append(GetMarkdownLink(parameterTypeName, parameterTypeSymbol, pathPrefix));
                        }
                        else if (parameterTypeName != null)
                        {
                            stringBuilder.Append($"`{parameterTypeName}`");
                        }
                        else
                        {
                            // We don't have a type symbol or even a type name
                            // for this parameter! Can't write a parameter type.
                        }

                        stringBuilder.Append(" ");

                        stringBuilder.Append(param.Attribute("name").Value);
                        stringBuilder.Append("|");
                        stringBuilder.Append(CreateMarkdownFromXMLTags(symbolDict, param).Replace("\n", " ").Trim());
                        stringBuilder.AppendLine("|");
                    }

                    stringBuilder.AppendLine();
                }

                var typeParamNodes = xml.Elements("typeparam");
                if (typeParamNodes.Any())
                {
                    stringBuilder.AppendLine("## Type Parameters");
                    stringBuilder.AppendLine();

                    stringBuilder.AppendLine("|Name|Description|");
                    stringBuilder.AppendLine("|:---|:---|");

                    foreach (var param in typeParamNodes)
                    {
                        stringBuilder.Append("|");
                        stringBuilder.Append(param.Attribute("name").Value);
                        stringBuilder.Append("|");
                        stringBuilder.Append(CreateMarkdownFromXMLTags(symbolDict, param).Replace("\n", " ").Trim());
                        stringBuilder.AppendLine("|");
                    }

                    stringBuilder.AppendLine();
                }

                // Generate a 'Returns' section only if we actually have content
                // for it
                XElement returns = xml.Element("returns");
                if (returns != null && returns.Nodes().Any())
                {
                    stringBuilder.AppendLine("## Returns");
                    stringBuilder.AppendLine();
                    stringBuilder.AppendLine(CreateMarkdownFromXMLTags(symbolDict, xml.Element("returns")));
                    stringBuilder.AppendLine();
                }

                var exceptions = xml.Elements("except");
                if (exceptions.Any())
                {
                    stringBuilder.AppendLine("## Exceptions");
                    stringBuilder.AppendLine();

                    stringBuilder.AppendLine("|Type|Description|");
                    stringBuilder.AppendLine("|:---|:---|");

                    foreach (var exception in exceptions)
                    {
                        var cref = exception.Attribute("cref")?.Value;
                        var exceptionSymbol = symbolDict[cref];

                        stringBuilder.Append("|");
                        stringBuilder.Append(GetMarkdownLink(exceptionSymbol, pathPrefix));
                        stringBuilder.Append("|");
                        stringBuilder.Append(CreateMarkdownFromXMLTags(symbolDict, exception).Replace("\n", " ").Trim());
                        stringBuilder.AppendLine("|");
                    }

                    stringBuilder.AppendLine();
                }

                var children = symbolDict
                    .Values
                    .Where(s => s.ContainerID == symbol.DocumentationID)
                    .GroupBy(s => (HTMLRenderer.GetTypeName(s, plural: true)));

                if (children.Any())
                {
                    foreach (var group in children.OrderBy(group => group.Key))
                    {
                        string groupName = group.Key;

                        stringBuilder.AppendLine($"## {groupName}");
                        stringBuilder.AppendLine();
                        stringBuilder.AppendLine($"|Name|Description|");
                        stringBuilder.AppendLine($"|:---|:---|");

                        foreach (var childSymbol in group.OrderBy(s => s.DocumentationID))
                        {
                            var name = childSymbol.DisplayName;
                            var link = $"{pathPrefix}/{childSymbol.AnchorName}.md";
                            var childDocs = XElement.Parse(childSymbol.DocumentationXml);
                            var childSummary = CreateMarkdownFromXMLTags(symbolDict, childDocs.Element("summary")).Replace("\n", " ").Trim();

                            stringBuilder.AppendLine($"|[{EscapeMarkdown(name)}]({link})|{EscapeMarkdown(childSummary)}|");
                        }

                        stringBuilder.AppendLine();
                    }
                }

                var seeAlsos = xml.Elements("seealso");
                if (seeAlsos.Any())
                {
                    stringBuilder.AppendLine("## See Also");
                    stringBuilder.AppendLine();

                    foreach (var seeAlso in seeAlsos)
                    {
                        var href = seeAlso.Attribute("href");
                        var cref = seeAlso.Attribute("cref");

                        if (cref != null)
                        {
                            var seeAlsoSymbol = symbolDict[cref.Value.ToString()];
                            stringBuilder.Append("* " + GetMarkdownLink(seeAlsoSymbol, pathPrefix, asCode: false, useFullDisplayName: true));

                            if (seeAlsoSymbol.DocumentationID.StartsWith("!:") == false)
                            {
                                var seeAlsoSummary = XElement.Parse(seeAlsoSymbol.DocumentationXml).Element("summary");

                                string seeAlsoSummaryText = CreateMarkdownFromXMLTags(symbolDict, seeAlsoSummary);

                                seeAlsoSummaryText = seeAlsoSummaryText.Replace("\n", " ").Trim();
                                stringBuilder.Append(": " + seeAlsoSummaryText);
                            }

                            stringBuilder.AppendLine();
                        }
                        else if (href != null)
                        {
                            var linkText = string.Join(" ", seeAlso.Nodes().Select(n => n.ToString()));

                            stringBuilder.AppendLine($"* [{EscapeMarkdown(linkText)}]({href})");
                        }
                    }

                    stringBuilder.AppendLine();
                }

                output[path] = stringBuilder.ToString();
            }

            return output;
        }

        private static string EscapeMarkdown(string input, bool escapeCharacters = false)
        {
            string v = System.Text.RegularExpressions.Regex.Replace(input, "^[ ]+", string.Empty, System.Text.RegularExpressions.RegexOptions.Multiline);

            if (escapeCharacters)
            {
                v = EscapeMarkdownCharacters(v);
            }

            return v;
        }

        private static string CreateMarkdownFromXMLTags(DefaultDictionary<string, DocumentedSymbol> symbolDict, XElement element)
        {
            IEnumerable<XNode> htmlNodes = HTMLRenderer.CreateHTMLFromXMLTags(element, symbolDict, (anchor) => anchor + ".md").Nodes();

            // Replace all <p style="x"> nodes with GitBook-style Hint blocks
            foreach (var node in htmlNodes)
            {
                if (node is XElement ele)
                {
                    foreach (var styledPTag in ele.DescendantsAndSelf("p").Where(e => e.Attribute("style") != null).ToList())
                    {
                        // Get the style attribute's value
                        XAttribute styleAttribute = styledPTag.Attribute("style");
                        var style = styleAttribute.Value;

                        // Remove the attribute from the HTML
                        styleAttribute.Remove();

                        var sb = new System.Text.StringBuilder();
                        sb
                            .Append(System.Environment.NewLine)
                            .Append("{% hint style=\"")
                            .Append(style)
                            .Append("\" %}")
                            .Append(System.Environment.NewLine);

                        var sb2 = new System.Text.StringBuilder();
                        sb2
                            .Append(System.Environment.NewLine)
                            .Append("{% endhint %}")
                            .Append(System.Environment.NewLine);

                        string startOfHint = sb.ToString();
                        string endOfHint = sb2.ToString();

                        styledPTag.AddFirst(startOfHint);
                        styledPTag.Add(endOfHint);
                    }
                }
            }

            string value = string.Join(" ", htmlNodes.Select(s => s.ToString()));

            // Strip leading whitespace from lines
            return EscapeMarkdown(value);
        }

        /// <summary>
        /// Returns Markdown for creating a callout of the specified type.
        /// </summary>
        /// <param name="type">The type of the callout. May be "info" or "warning".</param>
        /// <param name="label">The text to include in the callout.</param>
        /// <returns>The markdown for the callout.</returns>
        private static string CreateCallout(string type, string label)
        {
            var lines = new[] {
                $@"{{% hint style=""{type}"" %}}",
                label,
                "{% endhint %}"
            };
            return string.Join(System.Environment.NewLine, lines);
        }
    }
}