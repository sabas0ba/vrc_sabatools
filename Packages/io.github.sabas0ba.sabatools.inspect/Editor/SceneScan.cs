// Orchestrates one inspection run: the collectors fill a StatsSnapshot, the
// pure rules turn it into findings, and the result carries a path -> object
// map so the window can ping what a finding refers to.
using System.Collections.Generic;
using UnityEngine;

namespace SabaTools.Inspect.Editors
{
    internal sealed class ScanResult
    {
        internal InspectionReport Report;
        internal StatsSnapshot Stats;
        internal List<TextureEntry> Textures;
        internal readonly Dictionary<string, Object> Locations = new Dictionary<string, Object>();
    }

    internal static class SceneScan
    {
        /// <summary>How many textures the statistics table lists individually.</summary>
        internal const int TextureRowsInReport = 10;

        internal static ScanResult Run(
            GameObject[] roots, string targetName, InspectMode requestedMode, string generatedAt)
        {
            var stats = new StatsSnapshot();
            var result = new ScanResult
            {
                Stats = stats,
                // The report carries the same snapshot instance, so a caller
                // using the public API can read the raw numbers rather than
                // parsing the formatted rows back out.
                Report = new InspectionReport
                {
                    TargetName = targetName,
                    GeneratedAt = generatedAt,
                    Snapshot = stats,
                },
            };

            StatsCollector.Collect(roots, result.Stats);
            foreach (GameObject root in roots)
            {
                if (root != null)
                {
                    VrcComponentCensus.Collect(
                        root.GetComponentsInChildren<Component>(true), result.Stats);
                }
            }
            result.Textures = TextureUsageCollector.Collect(roots, result.Stats);

            // Runs before Evaluate: the aggregate findings quote counts this
            // scanner is the one to fill in.
            var referenceItems = new List<InspectionItem>();
            MissingReferenceScanner.Collect(roots, result.Stats, referenceItems, result.Locations);

            InspectMode mode = Resolve(requestedMode, result.Stats);
            result.Report.Mode = mode;
            result.Report.Items.AddRange(ChecklistRules.Evaluate(result.Stats, mode));
            result.Report.Items.AddRange(referenceItems);

            FillStatRows(result);
            return result;
        }

        /// <summary>
        /// Auto resolves from what was found: an avatar descriptor wins over a
        /// scene descriptor, because a world scene rarely contains an avatar
        /// while an avatar project's scene may contain both. With neither —
        /// the SDK being absent included — the result is Generic.
        /// </summary>
        internal static InspectMode Resolve(InspectMode requested, StatsSnapshot stats)
        {
            if (requested != InspectMode.Auto)
            {
                return requested;
            }
            if (stats.HasAvatarDescriptor)
            {
                return InspectMode.Avatar;
            }
            if (stats.HasSceneDescriptor)
            {
                return InspectMode.World;
            }
            return InspectMode.Generic;
        }

        private static void FillStatRows(ScanResult result)
        {
            StatsSnapshot stats = result.Stats;
            List<StatRow> rows = result.Report.Rows;

            rows.Add(new StatRow("Geometry", "GameObjects", stats.GameObjectCount.ToString()));
            rows.Add(new StatRow("Geometry", "Triangles", stats.Triangles.ToString("N0")));
            rows.Add(new StatRow("Geometry", "Mesh Renderers", stats.MeshRendererCount.ToString()));
            rows.Add(new StatRow("Geometry", "Skinned Mesh Renderers",
                stats.SkinnedMeshRendererCount.ToString()));
            rows.Add(new StatRow("Geometry", "Bones (unique)", stats.BoneCount.ToString()));

            rows.Add(new StatRow("Materials", "Material Slots", stats.MaterialSlotCount.ToString()));
            rows.Add(new StatRow("Materials", "Unique Materials", stats.UniqueMaterialCount.ToString()));
            rows.Add(new StatRow("Materials", "Unique Shaders", stats.UniqueShaderCount.ToString()));

            rows.Add(new StatRow("Textures", "Textures", stats.TextureCount.ToString()));
            rows.Add(new StatRow("Textures", "Estimated GPU Memory",
                TextureMemoryEstimate.FormatBytes(stats.TextureMemoryBytes)));
            int listed = 0;
            foreach (TextureEntry entry in result.Textures)
            {
                if (listed >= TextureRowsInReport)
                {
                    rows.Add(new StatRow("Textures",
                        "(" + (result.Textures.Count - TextureRowsInReport) + " more not listed)",
                        string.Empty));
                    break;
                }
                listed++;
                rows.Add(new StatRow("Textures", entry.Name + " (" + entry.Detail + ")",
                    TextureMemoryEstimate.FormatBytes(entry.Bytes)));
            }

            if (stats.PhysBoneCount > 0 || stats.PhysBoneColliderCount > 0
                || stats.ContactCount > 0 || stats.ConstraintCount > 0)
            {
                rows.Add(new StatRow("Dynamics", "PhysBones", stats.PhysBoneCount.ToString()));
                rows.Add(new StatRow("Dynamics", "PhysBone Colliders",
                    stats.PhysBoneColliderCount.ToString()));
                rows.Add(new StatRow("Dynamics", "Contacts", stats.ContactCount.ToString()));
                rows.Add(new StatRow("Dynamics", "Constraints", stats.ConstraintCount.ToString()));
            }

            rows.Add(new StatRow("Components", "Particle Systems", stats.ParticleSystemCount.ToString()));
            rows.Add(new StatRow("Components", "Trail / Line Renderers",
                stats.TrailRendererCount + " / " + stats.LineRendererCount));
            rows.Add(new StatRow("Components", "Cloth", stats.ClothCount.ToString()));
            rows.Add(new StatRow("Components", "Lights (realtime / total)",
                stats.RealtimeLightCount + " / " + stats.LightCount));
            rows.Add(new StatRow("Components", "Reflection Probes", stats.ReflectionProbeCount.ToString()));
            rows.Add(new StatRow("Components", "Audio Sources", stats.AudioSourceCount.ToString()));
            rows.Add(new StatRow("Components", "Animators", stats.AnimatorCount.ToString()));
            rows.Add(new StatRow("Components", "Cameras", stats.CameraCount.ToString()));

            if (result.Report.Mode == InspectMode.Avatar)
            {
                const string group = "Performance (PC, reference values)";
                rows.Add(new StatRow(group, "Overall Rank",
                    ChecklistRules.OverallAvatarRank(stats).ToString()));
                foreach (RankedMetric metric in ChecklistRules.AvatarMetrics(stats))
                {
                    rows.Add(new StatRow(group, metric.Label,
                        metric.Value.ToString("N0") + " → " + metric.Rank()));
                }
            }
        }
    }
}
