using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace DocExtractor
{
    public static class Extensions {
        public static ImmutableArray<ISymbol> ExplicitOrImplicitInterfaceImplementations(this ISymbol symbol)
        {
            if (symbol.Kind is not SymbolKind.Method and not SymbolKind.Property and not SymbolKind.Event)
                return ImmutableArray<ISymbol>.Empty;

            var containingType = symbol.ContainingType;
            var query = from iface in containingType.AllInterfaces
                        from interfaceMember in iface.GetMembers()
                        let impl = containingType.FindImplementationForInterfaceMember(interfaceMember)
                        where symbol.Equals(impl)
                        select interfaceMember;
            return query.ToImmutableArray();
        }

        public static ImmutableArray<ITypeParameterSymbol> GetAllTypeParameters(this ISymbol? symbol)
        {
            List<ITypeParameterSymbol> results = new List<ITypeParameterSymbol>();
            
            while (symbol != null)
            {
                results.AddRange(symbol.GetTypeParameters());
                symbol = symbol.ContainingType;
            }

            return results.ToImmutableArray();
        }

        public static ImmutableArray<ITypeParameterSymbol> GetTypeParameters(this ISymbol? symbol)
            => symbol switch
            {
                IMethodSymbol m => m.TypeParameters,
                INamedTypeSymbol nt => nt.TypeParameters,
                _ => ImmutableArray<ITypeParameterSymbol>.Empty,
            };

        public static ImmutableArray<ISymbol> ExplicitInterfaceImplementations(this ISymbol symbol)
        => symbol switch
        {
            IEventSymbol @event => ImmutableArray<ISymbol>.CastUp(@event.ExplicitInterfaceImplementations),
            IMethodSymbol method => ImmutableArray<ISymbol>.CastUp(method.ExplicitInterfaceImplementations),
            IPropertySymbol property => ImmutableArray<ISymbol>.CastUp(property.ExplicitInterfaceImplementations),
            _ => ImmutableArray.Create<ISymbol>(),
        };

        public static ISymbol? GetOverriddenMember(this ISymbol? symbol)
            => symbol switch
            {
                IMethodSymbol method => method.OverriddenMethod,
                IPropertySymbol property => property.OverriddenProperty,
                IEventSymbol @event => @event.OverriddenEvent,
                _ => null,
            };

        public static ImmutableArray<ITypeSymbol> GetTypeArguments(this ISymbol? symbol)
            => symbol switch
            {
                IMethodSymbol m => m.TypeArguments,
                INamedTypeSymbol nt => nt.TypeArguments,
                _ => ImmutableArray.Create<ITypeSymbol>(),
            };

        public static ImmutableArray<ITypeSymbol> GetAllTypeArguments(this ISymbol symbol)
        {
            var results = new List<ITypeSymbol>();
            results.AddRange(symbol.GetTypeArguments());

            var containingType = symbol.ContainingType;
            while (containingType != null)
            {
                results.AddRange(containingType.GetTypeArguments());
                containingType = containingType.ContainingType;
            }

            return results.ToImmutableArray();
        }
    }

}
