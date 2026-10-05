using System;
using System.Collections.Generic;

namespace SabaTools.AvatarVariants
{
    public enum ConversionIssueKind
    {
        /// <summary>
        /// A difference lies at or below siblings that share a name. Unity's
        /// conversion pairs such siblings without recording their differences
        /// (PrefabConversionBehaviourTests.EditsUnderDuplicateSiblingNamesAreLost),
        /// so the edit would be dropped.
        /// </summary>
        AmbiguousSiblings,

        /// <summary>
        /// A GameObject removed from one place has a counterpart of the same
        /// shape added under the same parent: probably a rename. The variant
        /// records it as a removal and an addition.
        /// </summary>
        LikelyRenamed,

        /// <summary>
        /// A GameObject removed from one place has a counterpart of the same
        /// name and shape added under another parent: probably a move. A
        /// variant cannot reparent what its base owns, so it is recorded as a
        /// removal and an addition.
        /// </summary>
        LikelyMoved,

        /// <summary>
        /// Children the two hierarchies share are in a different order.
        /// </summary>
        ChildrenReordered,
    }

    public enum ConversionIssueSeverity
    {
        /// <summary>The conversion can proceed; the result deserves a look.</summary>
        Warning,

        /// <summary>The conversion would lose an edit and must not be attempted.</summary>
        Blocking,
    }

    public sealed class ConversionIssue
    {
        internal ConversionIssue(
            ConversionIssueKind kind, ConversionIssueSeverity severity, string path, string relatedPath)
        {
            Kind = kind;
            Severity = severity;
            Path = path;
            RelatedPath = relatedPath;
        }

        public ConversionIssueKind Kind { get; }

        public ConversionIssueSeverity Severity { get; }

        /// <summary>
        /// The GameObject the issue is about. For a rename or a move, where it
        /// was in the base hierarchy.
        /// </summary>
        public string Path { get; }

        /// <summary>
        /// For a rename or a move, where the counterpart is in the derived
        /// hierarchy. Null otherwise.
        /// </summary>
        public string RelatedPath { get; }
    }

    /// <summary>
    /// Decides from a diff whether converting the derived hierarchy into a
    /// variant of the base would keep every edit, before anything is written.
    /// </summary>
    public static class ConversionPrecheck
    {
        public static List<ConversionIssue> Evaluate(HierarchyDiff diff)
        {
            if (diff == null)
            {
                throw new ArgumentNullException(nameof(diff));
            }

            var issues = new List<ConversionIssue>();
            AddAmbiguousSiblings(diff, issues);
            AddRenamesAndMoves(diff, issues);
            foreach (HierarchyChange change in diff.Changes)
            {
                if (change.Kind == HierarchyChangeKind.ChildrenReordered)
                {
                    issues.Add(new ConversionIssue(
                        ConversionIssueKind.ChildrenReordered,
                        ConversionIssueSeverity.Warning,
                        change.Path,
                        null));
                }
            }
            return issues;
        }

        public static bool CanConvert(IEnumerable<ConversionIssue> issues)
        {
            if (issues == null)
            {
                throw new ArgumentNullException(nameof(issues));
            }
            foreach (ConversionIssue issue in issues)
            {
                if (issue.Severity == ConversionIssueSeverity.Blocking)
                {
                    return false;
                }
            }
            return true;
        }

        private static void AddAmbiguousSiblings(HierarchyDiff diff, List<ConversionIssue> issues)
        {
            var reported = new HashSet<string>();
            foreach (HierarchyChange change in diff.Changes)
            {
                if (change.AmbiguousPath != null && reported.Add(change.AmbiguousPath))
                {
                    issues.Add(new ConversionIssue(
                        ConversionIssueKind.AmbiguousSiblings,
                        ConversionIssueSeverity.Blocking,
                        change.AmbiguousPath,
                        null));
                }
            }
        }

        private static void AddRenamesAndMoves(HierarchyDiff diff, List<ConversionIssue> issues)
        {
            var added = new List<HierarchyChange>();
            foreach (HierarchyChange change in diff.Changes)
            {
                if (change.Kind == HierarchyChangeKind.GameObjectAdded)
                {
                    added.Add(change);
                }
            }

            foreach (HierarchyChange removed in diff.Changes)
            {
                if (removed.Kind != HierarchyChangeKind.GameObjectRemoved)
                {
                    continue;
                }

                // A move is the stronger claim, since the name still agrees.
                int match = FindCounterpart(removed, added, moved: true);
                ConversionIssueKind kind = ConversionIssueKind.LikelyMoved;
                if (match < 0)
                {
                    match = FindCounterpart(removed, added, moved: false);
                    kind = ConversionIssueKind.LikelyRenamed;
                }
                if (match < 0)
                {
                    continue;
                }

                issues.Add(new ConversionIssue(
                    kind, ConversionIssueSeverity.Warning, removed.Path, added[match].Path));
                added.RemoveAt(match);
            }
        }

        private static int FindCounterpart(
            HierarchyChange removed, List<HierarchyChange> added, bool moved)
        {
            for (int index = 0; index < added.Count; index++)
            {
                HierarchyChange candidate = added[index];
                bool sameParent = candidate.ParentPath == removed.ParentPath;
                bool sameName = candidate.DerivedNode.Name == removed.BaseNode.Name;
                if (moved ? (sameParent || !sameName) : (!sameParent || sameName))
                {
                    continue;
                }
                if (SameShape(removed.BaseNode, candidate.DerivedNode))
                {
                    return index;
                }
            }
            return -1;
        }

        /// <summary>
        /// The same component types in the same order, and children of the same
        /// names in the same order. Property values are left out: a renamed
        /// object has usually been edited as well.
        /// </summary>
        private static bool SameShape(SnapshotNode first, SnapshotNode second)
        {
            if (first.Components.Count != second.Components.Count
                || first.Children.Count != second.Children.Count)
            {
                return false;
            }
            for (int index = 0; index < first.Components.Count; index++)
            {
                if (first.Components[index].TypeName != second.Components[index].TypeName)
                {
                    return false;
                }
            }
            for (int index = 0; index < first.Children.Count; index++)
            {
                if (first.Children[index].Name != second.Children[index].Name)
                {
                    return false;
                }
            }
            return true;
        }
    }
}
