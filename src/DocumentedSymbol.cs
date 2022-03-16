namespace DocExtractor
{
    using System;
    using System.Collections.Generic;
    using Microsoft.CodeAnalysis;

    public class DocumentedSymbol
    {
        public ISymbol Symbol { get; set; }
        public string DocumentationXml { get; set; }
        public SyntaxNode Syntax { get; set; }
        public string DocumentationID { get; set; }
        public string ContainerID { get; set; }

        public string DisplayName { get; internal set; }
        public string FullDisplayName { get; internal set; }
        public string AnchorName { get; internal set; }

        public bool ContainsUndocumentedElements => UndocumentedElementNames.Count > 0;
        public string Declaration { get; internal set; }
        public string BaseTypeID { get; internal set; }

        public List<string> UndocumentedElementNames { get; } = new List<string>();

        public override bool Equals(object obj)
        {
            return obj is DocumentedSymbol symbol &&
                   symbol.DocumentationID == this.DocumentationID;
        }

        public override int GetHashCode()
        {
            return DocumentationID.GetHashCode();
        }
    }
}