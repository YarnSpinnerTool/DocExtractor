using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Xml.Linq;

#nullable enable

namespace DocExtractor
{

    public static class JSONRenderer
    {
        private record DocumentationPageData
        {
            public required string ID { get; init; }
            public required string Title { get; set; }

            public required string TypeName { get; set; }

            public string? ContainingTypeID { get; set; }

            public string? BaseTypeID { get; set; }

            public ObsoleteInfo? Obsolete { get; set; } = null;

            internal record ObsoleteInfo
            {
                public string? Message { get; set; }
                public static ObsoleteInfo? GetObsoleteInfo(DocumentedSymbol symbol)
                {
                    var obsoleteAttribute = symbol.Symbol.GetAttributes().FirstOrDefault(a => a.AttributeClass?.GetDocumentationCommentId() == "T:System.ObsoleteAttribute");

                    if (obsoleteAttribute == null)
                    {
                        return null;
                    }
                    return obsoleteAttribute.ConstructorArguments.Length switch
                    {
                        > 0 => new() { Message = (obsoleteAttribute.ConstructorArguments[0].Value?.ToString()) },
                        _ => new(),
                    };
                }
            }

            public string? Summary { get; set; }

            public string? DeclarationSyntax { get; set; }

            public string? Remarks { get; set; }

            public List<ParameterInfo> Parameters { get; set; } = [];

            internal record ParameterInfo
            {
                public required string Name { get; set; }
                public string? TypeID { get; set; }
                public required string TypeName { get; set; }
                public string? Description { get; set; }
            }

            public List<TypeParameterInfo> TypeParameters { get; set; } = [];

            internal record TypeParameterInfo
            {
                public required string Name { get; set; }
                public string? Description { get; set; }
            }

            public string? Returns { get; set; }

            public List<ExceptionInfo> Exceptions { get; set; } = [];

            internal record ExceptionInfo
            {
                public required string TypeName { get; set; }
                public string? TypeID { get; set; }
                public string? Description { get; set; }

            }

            public List<ChildInfo> Children { get; set; } = [];

            internal record ChildInfo
            {
                public required string ID { get; set; }
                public required string TypeName { get; set; }
            }

            public List<SeeAlsoInfo> SeeAlso { get; set; } = [];
            internal record SeeAlsoInfo
            {
                public string? URL { get; set; }
                public string? TypeID { get; set; }
            }

        }

        public record File(string Name, string Contents, string DocumentationID);
        internal static IEnumerable<File> GenerateJSONForDocXML(IEnumerable<DocumentedSymbol> documentedSymbols, DefaultDictionary<string, DocumentedSymbol> symbolDict)
        {
            var output = new Dictionary<string, string>();

            var options = new JsonSerializerOptions
            {
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault,
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            };

            foreach (var member in documentedSymbols)
            {
                var symbol = symbolDict[member.DocumentationID];


                var xml = XElement.Parse(symbol.DocumentationXml);

                var data = new DocumentationPageData
                {
                    ID = member.DocumentationID,
                    Title = symbol.FullDisplayName,
                    TypeName = HTMLRenderer.GetTypeName(symbol),
                    ContainingTypeID = documentedSymbols.FirstOrDefault(s => symbol.ContainerID == s.DocumentationID)?.DocumentationID,
                    BaseTypeID = symbol.BaseTypeID,
                    Obsolete = DocumentationPageData.ObsoleteInfo.GetObsoleteInfo(symbol),
                    Summary = GetDocumentationNodeAsHTML(xml, "summary"),
                    Remarks = GetDocumentationNodeAsHTML(xml, "remarks"),

                    DeclarationSyntax = symbol.Declaration,

                    Parameters = [.. xml.Elements("param").Select(paramNode => {
                        return new DocumentationPageData.ParameterInfo
                        {
                            Name = paramNode.Attribute("name")?.Value ?? "(unknown)",
                            TypeName = paramNode.Attribute("typeName")?.Value ?? "(unknown)",
                            TypeID = paramNode.Attribute("typeID")?.Value,
                            Description = GetDocumentationNodeAsHTML(paramNode),
                        }; })],

                    TypeParameters = [.. xml.Elements("typeparam").Select(typeParamNode => {
                        return new DocumentationPageData.TypeParameterInfo {
                            Name = typeParamNode.Attribute("name")?.Value ?? "(unknown)",
                            Description = GetDocumentationNodeAsHTML(typeParamNode)
                        }; })],
                    Returns = GetDocumentationNodeAsHTML(xml, "returns"),
                    Exceptions = [..xml.Elements("except").Select(exceptNode => {
                        var cref = exceptNode.Attribute("cref")?.Value;
                        var exceptionSymbol = symbolDict[cref];

                        return new DocumentationPageData.ExceptionInfo {
                            TypeName  = exceptionSymbol.DocumentationID,
                            Description = GetDocumentationNodeAsHTML(exceptNode)
                        };
                    })],

                    Children = [.. symbolDict
                        .Values
                        .Where(s => s.ContainerID == symbol.DocumentationID)
                        .Select(s => new DocumentationPageData.ChildInfo {
                            ID = s.DocumentationID,
                            TypeName = HTMLRenderer.GetTypeName(s)
                        })
                    ],

                    // TODO: seealso
                };

                yield return new File(
                    symbol.AnchorName + ".json",
                    JsonSerializer.Serialize(data, options),
                    symbol.DocumentationID
                );
            }
            string? GetDocumentationNodeAsHTML(XElement xml, string? childNodeName = null)
            {
                if (childNodeName != null)
                {
                    if (xml.Element(childNodeName) is not XElement summaryElement)
                    {
                        // No child node with this name
                        return null;
                    }

                    xml = summaryElement;
                }

                XElement? html = CreateHTMLFromXMLTags(xml, symbolDict);
                return string.Join("", html.DescendantNodes()).Trim().ToString();
            }
        }


        /// <summary>
        /// Returns a clone of <paramref name="input"/>, but with tags
        /// replaced with HTML elements.
        /// </summary>
        /// <param name="input">The input XML.</param>
        /// <returns>An HTML version of <paramref name="input"/>.</returns>
        public static XElement CreateHTMLFromXMLTags(XElement input, IDictionary<string, DocumentedSymbol> symbols)
        {
            XElement output = new XElement(input);

            // Replace all CDATA with HTML-encoded versions of the same content
            foreach (var cData in output.Descendants().OfType<XNode>().OfType<XCData>().ToList())
            {
                cData.ReplaceWith(System.Web.HttpUtility.HtmlEncode(cData.Value));
            }

            foreach (var codeBlock in output.Descendants("code"))
            {
                codeBlock.Name = "pre";
            }

            foreach (var inlineCode in output.Descendants("c"))
            {
                inlineCode.Name = "code";
            }

            // <see langword="true"/> -> <code>true</code>
            foreach (var see in output.Descendants("see").Where(n => n.Attribute("langword") != null))
            {
                see.Name = "code";
                see.Add(see.Attribute("langword")?.Value);
                see.RemoveAttributes();
            }

            // <see cref="T:SomeType"> -> <a
            // href="T:SomeType">T:SomeType</a>
            //
            // <see href="http://github.com">GitHub</see> -> <a
            // href="http://github.com">GitHub</a>
            foreach (var see in output.Descendants("see"))
            {
                see.Name = "a";

                var cref = see.Attribute("cref");
                var href = see.Attribute("href");

                see.RemoveAttributes();

                if (cref != null)
                {
                    var otherTypeID = symbols[cref.Value].DocumentationID;

                    if (otherTypeID != null)
                    {
                        see.SetAttributeValue("href", otherTypeID);
                    }
                    else
                    {
                        see.Name = "code";
                    }
                    see.Add(symbols[cref.Value].DisplayName);
                }
                else if (href != null)
                {
                    see.SetAttributeValue("href", href.Value);
                }
            }

            foreach (var para in output.Descendants("para"))
            {
                para.Name = "p";
            }

            foreach (var list in output.Descendants("list"))
            {
                string listType = list.Attribute("type")?.Value ?? "bullet";

                if (listType == "table")
                {
                    // TODO: handle definition lists and tables
                    break;
                }

                switch (listType)
                {
                    case "bullet":
                        list.Name = "ul";
                        break;
                    case "number":
                        list.Name = "ol";
                        break;
                }
                foreach (var item in list.Elements("item"))
                {
                    item.Name = "li";
                }
            }

            foreach (var paramRef in output.Descendants("paramref"))
            {
                paramRef.Name = "code";
                paramRef.Add(paramRef.Attribute("name")?.Value);
                paramRef.Attribute("name")?.Remove();
            }

            foreach (var paramRef in output.Descendants("typeparamref"))
            {
                paramRef.Name = "code";
                paramRef.Add(paramRef.Attribute("name")?.Value);
                paramRef.Attribute("name")?.Remove();
            }

            foreach (var example in output.Descendants("example"))
            {
                example.Name = "div";
                example.Add(new XAttribute("class", "example"));
            }

            return output;
        }

    }
}