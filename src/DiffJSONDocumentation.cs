using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

#nullable enable

namespace DocExtractor
{
    public class DocumentationDiffResult
    {
        [Description("The IDs of symbols that were added in the new version.")]
        public List<string> Added { get; set; } = [];
        [Description("The IDs symbols that were removed from the old version.")]
        public List<string> Removed { get; set; } = [];
        [Description("The IDs symbols that have become deprecated in the new version, mapped to the reason for their obsoletion (or null if none was provided)")]
        public Dictionary<string, string?> Deprecated { get; set; } = [];
    }


    public static class DiffJSONDocumentation
    {
        internal static DocumentationDiffResult CreateDiffResult(FileInfo from, FileInfo to)
        {
            var fromDocs = JsonSerializer.Deserialize<JSONRenderer.DocumentationData>(from.OpenRead(), JSONRenderer.SerializerOptions);
            var toDocs = JsonSerializer.Deserialize<JSONRenderer.DocumentationData>(to.OpenRead(), JSONRenderer.SerializerOptions);

            if (fromDocs == null)
            {
                throw new ArgumentException($"Failed to load {from.FullName}");
            }
            if (toDocs == null)
            {
                throw new ArgumentException($"Failed to load {to.FullName}");
            }

            if (fromDocs.Engine != toDocs.Engine)
            {
                throw new ArgumentException($"Docs don't target the same engine? ({from.FullName} targets {fromDocs.Engine}, {to.FullName} targets {toDocs.Engine})");
            }

            HashSet<string> docsInFrom = [.. fromDocs.DocumentedSymbols.Values.Select(s => s.ID)];
            HashSet<string> docsInTo = [.. toDocs.DocumentedSymbols.Values.Select(s => s.ID)];

            HashSet<string> deprecatedDocsInFrom = [.. fromDocs.DocumentedSymbols.Values
                .Where(s => s.Obsolete != null)
                .Select(s => s.ID)
            ];

            HashSet<string> deprecatedDocsInTo = [.. toDocs.DocumentedSymbols.Values
                .Where(s => s.Obsolete != null)
                .Select(s => s.ID)
            ];

            // Docs that are in 'from' and not in 'to' were removed
            HashSet<string> removedDocs = [.. docsInFrom];
            removedDocs.RemoveWhere(s => docsInTo.Contains(s));

            // Docs that are in 'to' and not in 'from' were added
            HashSet<string> addedDocs = [.. docsInTo];
            addedDocs.RemoveWhere(s => docsInFrom.Contains(s));

            // Docs that are deprecated in to and not in from have become newly deprecated
            HashSet<string> newlyDeprecatedDocs = [.. deprecatedDocsInTo];
            deprecatedDocsInTo.RemoveWhere(s => deprecatedDocsInFrom.Contains(s));

            return new DocumentationDiffResult
            {
                Added = [.. addedDocs],
                Removed = [.. removedDocs],
                Deprecated = newlyDeprecatedDocs.ToDictionary(s => s, s => toDocs.DocumentedSymbols[s].Obsolete?.Message)
            };
        }
    }
}