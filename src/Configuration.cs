using System;
using System.Collections.Generic;

namespace DocExtractor
{
    [Serializable]
    class Configuration
    {
        public List<string> Projects { get; set; } = new List<string>();
        public List<string> ExcludeRegexes { get; set; } = new List<string>();
        public OutputFormat OutputFormat { get; set; } = OutputFormat.HTML;
        public string OutputFolder { get; set; } = null;
        public string PathPrefix { get; set; } = ".";
        public Dictionary<string, string> NamespaceSummaries { get; set; } = null;

        public List<string> PreprocessorSymbols { get; set; } = new List<string>();

        [System.Text.Json.Serialization.JsonPropertyName("msBuildPath")]
        public string MSBuildPath { get; set; } = null;
    }

}
