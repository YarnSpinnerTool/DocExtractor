using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;

#nullable enable

namespace DocExtractor
{

    public static class JSONRenderer
    {
        internal record DocumentationData
        {
            [Description("The engine that this API is intended for")]
            public required string Engine { get; set; }

            [Description("The version of this API")]
            public required string Version { get; set; }

            [Description("The git SHAs of the projects used to compile this documentation")]
            public Dictionary<string, string> ProjectVersions { get; set; } = [];

            [Description("The documented API items")]
            public Dictionary<string, DocumentationPageData> DocumentedSymbols { get; set; } = [];
        }

        public record DocumentationPageData
        {
            // Required for deserialization
            public DocumentationPageData() { }

            [Description("The Type ID of the symbol.")]
            public required string ID { get; init; }

            [Description("The user-facing title of the symbol.")]
            public required string Title { get; set; }

            [Description("The kind of this symbol: method, class, property, and so on.")]
            public required string TypeKind { get; set; }

            [Description("The ID of the type that contains this symbol, if any. (Most types will have a container, except for root namespaces.)")]
            public string? ContainingTypeID { get; set; }

            [Description("The ID of the type that this symbol inherits from, if any.")]
            public string? BaseTypeID { get; set; }

            [Description("The list of type IDs for interfaces that this symbol implements. Only relevant to class and struct symbols.")]
            public List<string> ImplementedInterfaceTypeIDs { get; set; } = [];

            [Description("If null, the symbol is not obsolete; if not null, the symbol is obsolete.")]
            public ObsoleteInfo? Obsolete { get; set; } = null;

            public record ObsoleteInfo
            {
                [Description("If not null, contains descriptive information about why a symbol is obsolete.")]
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

            [Description("The user-facing summary of the symbol, as HTML.")]
            public string? Summary { get; set; }

            [Description("The syntax of the declaration for this symbol.")]
            public string? DeclarationSyntax { get; set; }

            [Description("User-facing additional remarks for this symbol.")]
            public string? Remarks { get; set; }

            [Description("The collection of parameters that this symbol receives. Only relevant for methods.")]
            public List<ParameterInfo> Parameters { get; set; } = [];

            public record ParameterInfo
            {
                [Description("The name of the parameter, as it appears in source code.")]
                public required string Name { get; set; }
                [Description("The type ID for this parameter.")]
                public string? TypeID { get; set; }
                [Description("The user-facing name for the type of this parameter.")]
                public required string TypeName { get; set; }
                [Description("The user-facing description for this parameter.")]
                public string? Description { get; set; }
            }

            [Description("The type parameters for this symbol.")]
            public List<TypeParameterInfo> TypeParameters { get; set; } = [];

            public record TypeParameterInfo
            {
                [Description("The name of this symbol.")]
                public required string Name { get; set; }
                [Description("The user-facing description for this symbol.")]
                public string? Description { get; set; }
            }

            [Description("Information about the return type and value for this symbol. Only relevant for methods.")]
            public ReturnInfo? Returns { get; set; }

            public record ReturnInfo
            {
                [Description("The type ID for the returned value.")]
                public required string TypeID { get; set; }
                [Description("The user-facing description of the return value.")]
                public string? Description { get; set; }
                [Description("The user-facing name of the type of the return value.")]
                public string? TypeName { get; set; }
            }

            [Description("Information about the type of this symbol. Only relevant for fields and properties.")]
            public FieldOrPropertyTypeInfo? FieldOrPropertyType { get; set; }

            public record FieldOrPropertyTypeInfo
            {
                [Description("The type ID for this symbol.")]
                public required string? TypeID { get; set; }
                [Description("The user-facing name of the type of this symbol.")]
                public required string TypeName { get; set; }
            }

            [Description("The collection of exceptions that can be thrown by invoking this symbol. Only relevant for methods and properties.")]
            public List<ExceptionInfo> Exceptions { get; set; } = [];

            public record ExceptionInfo
            {
                [Description("The type ID for the type of the exception.")]
                public required string TypeID { get; set; }
                [Description("The user-facing name of the type of this exception.")]
                public string? TypeName { get; set; }
                [Description("The user-facing description of when this exception can be thrown.")]
                public string? Description { get; set; }
            }

            [Description("The collection of symbols that are children of this symbol.")]
            public List<ChildInfo> Children { get; set; } = [];

            public record ChildInfo
            {
                [Description("The ID of this symbol.")]
                public required string ID { get; set; }

                [Description("The kind of symbol (class, method, etc).")]
                public required string TypeKind { get; set; }
                [Description("The user-facing summary of this symbol. Equivalent to the 'summary' field.")]
                public string? Description { get; set; }
                [Description("The name of this symbol")]
                public required string Name { get; set; }
            }

            [Description("The collection of symbols or links marked as 'see also' in the source code.")]
            public List<SeeAlsoInfo> SeeAlso { get; set; } = [];


            public record SeeAlsoInfo
            {
                [Description("A URL to show.")]
                public string? URL { get; set; }
                [Description("A documented type to refer to.")]
                public string? TypeID { get; set; }
            }
        }

        public static JsonSerializerOptions SerializerOptions = new()
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault,
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        };

        public record File(string Name, string Contents, string DocumentationID);
        internal static DocumentationData GenerateJSONForDocXML(string engineName, string version, IEnumerable<DocumentedSymbol> documentedSymbols, DefaultDictionary<string, DocumentedSymbol> symbolDict)
        {

            var output = new DocumentationData()
            {
                Engine = engineName,
                Version = version,
            };

            foreach (var member in documentedSymbols)
            {
                var symbol = symbolDict[member.DocumentationID];

                var xml = XElement.Parse(symbol.DocumentationXml);

                DocumentationPageData.FieldOrPropertyTypeInfo? typeInfo = null;
                DocumentationPageData.ReturnInfo? returnInfo = null;

                var propertyOrTypeSymbol = RendererUtility.GetPropertyOrFieldType(symbol.Symbol);

                if (propertyOrTypeSymbol?.GetDocumentationCommentId() is string fieldOrPropTypeID)
                {
                    typeInfo = new DocumentationPageData.FieldOrPropertyTypeInfo
                    {
                        TypeID = fieldOrPropTypeID,
                        TypeName = propertyOrTypeSymbol.Name,
                    };
                }

                var returnTypeSymbol = RendererUtility.GetMethodReturnType(symbol.Symbol);
                if (returnTypeSymbol?.GetDocumentationCommentId() is string methodReturnTypeID)
                {
                    returnInfo = new DocumentationPageData.ReturnInfo
                    {
                        TypeID = methodReturnTypeID,
                        TypeName = returnTypeSymbol.Name,
                        Description = GetDocumentationNodeAsHTML(xml, "returns")
                    };
                }

                var data = new DocumentationPageData
                {
                    ID = member.DocumentationID,
                    Title = symbol.FullDisplayName,
                    TypeKind = HTMLRenderer.GetTypeName(symbol).ToLowerInvariant(),
                    ContainingTypeID = documentedSymbols.FirstOrDefault(s => symbol.ContainerID == s.DocumentationID)?.DocumentationID,
                    BaseTypeID = symbol.BaseTypeID,
                    Obsolete = DocumentationPageData.ObsoleteInfo.GetObsoleteInfo(symbol),
                    Summary = GetDocumentationNodeAsHTML(xml, "summary"),
                    Remarks = GetDocumentationNodeAsHTML(xml, "remarks"),

                    ImplementedInterfaceTypeIDs = (symbol.Symbol is INamedTypeSymbol namedTypeSymbol) ?
                        [.. namedTypeSymbol.AllInterfaces.Select(i => i.GetDocumentationCommentId()!).Where(t => t != null)]
                        : [],

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

                    FieldOrPropertyType = typeInfo,

                    Returns = returnInfo,

                    // Returns = GetDocumentationNodeAsHTML(xml, "returns"),
                    Exceptions = [..xml.Elements("exception").Select(exceptNode => {
                        var cref = exceptNode.Attribute("cref")?.Value;
                        if (cref == null) {
                            return new DocumentationPageData.ExceptionInfo {
                                TypeID = "!:Unknown",
                                TypeName  = "unknown",
                                Description = GetDocumentationNodeAsHTML(exceptNode)
                            };
                        }

                        var exceptionSymbol = symbolDict[cref];

                        return new DocumentationPageData.ExceptionInfo {

                            TypeID  = exceptionSymbol.DocumentationID,
                            TypeName = exceptionSymbol.Symbol?.Name ?? exceptionSymbol.DocumentationID[2..],
                            Description = GetDocumentationNodeAsHTML(exceptNode)
                        };
                    })],

                    Children = [.. symbolDict
                        .Values
                        .Where(s => s.ContainerID == symbol.DocumentationID)
                        .Select(s => new DocumentationPageData.ChildInfo {
                            ID = s.DocumentationID,
                            Name = s.Symbol.Name,
                            TypeKind = HTMLRenderer.GetTypeName(s)
                        })
                    ],

                    SeeAlso = [
                        ..xml.Elements("seealso").Select(seeAlsoNode => {
                            if (seeAlsoNode.Attribute("href") is XAttribute hrefNode) {
                                return new DocumentationPageData.SeeAlsoInfo {
                                    URL = hrefNode.Value
                                };
                            } else if (seeAlsoNode.Attribute("cref") is XAttribute crefNode) {
                                var seeAlsoSymbol = symbolDict[crefNode.Value.ToString()];
                                return new DocumentationPageData.SeeAlsoInfo {
                                    TypeID = seeAlsoSymbol.DocumentationID
                                };
                            } else {
                                return  new DocumentationPageData.SeeAlsoInfo {
                                    TypeID = "!:Unknown"
                                };
                            }
                        })
                    ]
                };

                output.DocumentedSymbols.TryAdd(
                    symbol.DocumentationID,
                    data
                );
            }

            return output;

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

    public static class JsonNodeExtensions
    {
        public static JsonNode? WithSortedKeys(this JsonNode? node)
        {
            if (node is JsonObject jsonObject)
            {
                var sortedObject = new JsonObject();

                foreach (var kv in jsonObject.OrderBy(p => p.Key, System.StringComparer.Ordinal))
                {
                    sortedObject.Add(kv.Key, kv.Value.WithSortedKeys());
                }
                return sortedObject;
            }
            else if (node is JsonArray jsonArray)
            {
                var sortedArray = new JsonArray();

                foreach (var item in jsonArray)
                {
                    sortedArray.Add(item.WithSortedKeys());
                }
                return sortedArray;
            }
            else
            {
                return node?.DeepClone();
            }
        }
    }
}