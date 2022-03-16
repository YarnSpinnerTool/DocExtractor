using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DocExtractor
{
    /// <summary>
    /// A walker that reports on declarations.
    /// </summary>
    public class DeclarationWalker : CSharpSyntaxWalker
    {
        private SemanticModel model;
        private SymbolHandler onSymbolHandled;

        /// <summary>
        /// A method for handling the discovery of a declaration symbol.
        /// </summary>
        /// <param name="symbol">The symbol that was encountered</param>
        public delegate void SymbolHandler(SyntaxNode node, ISymbol symbol);


        /// <summary>
        ///
        /// </summary>
        /// <param name="model"></param>
        /// <param name="handler"></param>
        public DeclarationWalker(SemanticModel model, SymbolHandler handler) : base()
        {
            this.model = model;
            this.onSymbolHandled = handler;
        }

        /// <inheritdoc/>
        public override void Visit(SyntaxNode node)
        {
            base.Visit(node);

            if (node is MemberDeclarationSyntax member)
            {
                if (member is FieldDeclarationSyntax field)
                {
                    // Fields can declare _multiple_ variables, so we need
                    // to hit each one
                    foreach (var variable in field.Declaration.Variables)
                    {
                        var variableSymbol = model.GetDeclaredSymbol(variable);
                        if (variableSymbol != null)
                        {
                            // This is a declaration symbol. Pass it back
                            // to our handler.
                            onSymbolHandled?.Invoke(node, variableSymbol);
                        }
                    }
                }
                else
                {
                    var memberSymbol = model.GetDeclaredSymbol(member);
                    if (memberSymbol != null)
                    {
                        // This is a declaration symbol. Pass it back to
                        // our handler.
                        onSymbolHandled?.Invoke(node, memberSymbol);
                    }
                }


            }

        }
    }

}
