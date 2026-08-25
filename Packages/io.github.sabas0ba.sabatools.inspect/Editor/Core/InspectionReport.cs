// Pure data model for an inspection run. No UnityEngine or UnityEditor
// references: everything under Editor/Core compiles and runs on a plain .NET
// runtime, which is what lets .github/verify execute these rules without
// Unity.
using System.Collections.Generic;
using System.Text;

namespace SabaTools.Inspect
{
    public enum InspectionSeverity
    {
        Info = 0,
        Warning = 1,
        Error = 2,
    }

    /// <summary>What kind of content the checklist judges the target as.</summary>
    public enum InspectMode
    {
        /// <summary>Pick Avatar or World from the components found on the target.</summary>
        Auto = 0,
        Avatar = 1,
        World = 2,
        /// <summary>No VRChat assumptions; statistics and reference checks only.</summary>
        Generic = 3,
    }

    /// <summary>
    /// One finding. <see cref="Path"/> is a hierarchy path and may be empty,
    /// which is how the aggregate rows in the Summary category are marked.
    /// </summary>
    public sealed class InspectionItem
    {
        public string Category;
        public InspectionSeverity Severity;
        public string Message;
        public string Path;

        public InspectionItem(string category, InspectionSeverity severity, string message, string path)
        {
            Category = category;
            Severity = severity;
            Message = message;
            Path = path ?? string.Empty;
        }
    }

    /// <summary>One row of the statistics table.</summary>
    public struct StatRow
    {
        public string Group;
        public string Label;
        public string Value;

        public StatRow(string group, string label, string value)
        {
            Group = group;
            Label = label;
            Value = value;
        }
    }

    /// <summary>
    /// The result of one inspection: a statistics table plus a list of
    /// findings. The window renders it and the Markdown export writes it out
    /// verbatim, so the report carries no editor state.
    /// </summary>
    public sealed class InspectionReport
    {
        /// <summary>Name of the inspected root object or scene.</summary>
        public string TargetName = string.Empty;

        /// <summary>The mode the checklist actually ran as (never Auto).</summary>
        public InspectMode Mode = InspectMode.Generic;

        /// <summary>
        /// Timestamp text supplied by the caller. Kept as an opaque string so
        /// this assembly never reads a clock, which is what keeps the offline
        /// tests deterministic.
        /// </summary>
        public string GeneratedAt = string.Empty;

        public readonly List<StatRow> Rows = new List<StatRow>();

        /// <summary>The raw numbers the rows and the checklist were built from.</summary>
        public StatsSnapshot Snapshot = new StatsSnapshot();
        public readonly List<InspectionItem> Items = new List<InspectionItem>();

        public int Count(InspectionSeverity severity)
        {
            int count = 0;
            for (int i = 0; i < Items.Count; i++)
            {
                if (Items[i].Severity == severity)
                {
                    count++;
                }
            }
            return count;
        }

        public int ErrorCount => Count(InspectionSeverity.Error);
        public int WarningCount => Count(InspectionSeverity.Warning);

        /// <summary>
        /// The report as a Markdown document: a header, the statistics grouped
        /// into tables, then the findings.
        /// </summary>
        public string ToMarkdown()
        {
            var text = new StringBuilder();
            text.Append("# SabaTools Inspect Report\n\n");
            text.Append("| | |\n|---|---|\n");
            text.Append("| Target | ").Append(Escape(TargetName)).Append(" |\n");
            text.Append("| Mode | ").Append(Mode).Append(" |\n");
            if (GeneratedAt.Length > 0)
            {
                text.Append("| Generated | ").Append(Escape(GeneratedAt)).Append(" |\n");
            }
            text.Append("| Findings | ")
                .Append(ErrorCount).Append(" error(s), ")
                .Append(WarningCount).Append(" warning(s) |\n\n");

            string group = null;
            foreach (StatRow row in Rows)
            {
                if (row.Group != group)
                {
                    group = row.Group;
                    text.Append("## ").Append(Escape(group)).Append("\n\n");
                    text.Append("| Item | Value |\n|---|---|\n");
                }
                text.Append("| ").Append(Escape(row.Label))
                    .Append(" | ").Append(Escape(row.Value)).Append(" |\n");
            }

            if (Items.Count > 0)
            {
                text.Append("\n## Findings\n\n");
                foreach (InspectionItem item in Items)
                {
                    text.Append("- **").Append(item.Severity).Append("** [")
                        .Append(Escape(item.Category)).Append("] ")
                        .Append(Escape(item.Message));
                    if (item.Path.Length > 0)
                    {
                        text.Append(" — `").Append(item.Path).Append('`');
                    }
                    text.Append('\n');
                }
            }

            return text.ToString();
        }

        private static string Escape(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }
            return value.Replace("|", "\\|");
        }
    }
}
