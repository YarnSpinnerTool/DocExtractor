using System;
using System.Collections.Generic;

namespace DocExtractor
{
    [Serializable]
    class Configuration
    {
        public List<string> Projects { get; set; } = [];
        public List<string> ExcludeRegexes { get; set; } = [];
        public OutputFormat OutputFormat { get; set; } = OutputFormat.HTML;
        public string OutputFolder { get; set; } = null;
        public string PathPrefix { get; set; } = ".";
        public string TocPathPrefix { get; set; } = ".";

        public Dictionary<string, string> NamespaceSummaries { get; set; } = [];

        /// <summary>
        /// Gets or sets the indent level of the generated summary file.
        /// </summary>
        /// <remarks>
        /// If <see cref="OutputFormat"/> is <see
        /// cref="OutputFormat.Markdown"/>, the generated SUMMARY.md
        /// file will be indented by this number of spaces.
        /// </remarks>
        public int SummaryIndentLevel { get; set; } = 2;

        public List<string> PreprocessorSymbols { get; set; } = [];

        [System.Text.Json.Serialization.JsonPropertyName("msBuildPath")]
        public string MSBuildPath { get; set; } = null;
    }

}
