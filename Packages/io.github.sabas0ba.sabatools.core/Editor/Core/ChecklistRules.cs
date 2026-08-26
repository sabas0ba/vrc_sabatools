// Pure evaluation of a StatsSnapshot into findings. No Unity references;
// exercised directly by the offline tests in .github/verify/offline.
using System.Collections.Generic;

namespace SabaTools.Inspect
{
    /// <summary>VRChat's five performance ranks, ordered best to worst.</summary>
    public enum PerfRank
    {
        Excellent = 0,
        Good = 1,
        Medium = 2,
        Poor = 3,
        VeryPoor = 4,
    }

    /// <summary>
    /// One ranked avatar metric: the value is compared against the per-rank
    /// inclusive upper limits.
    /// </summary>
    public struct RankedMetric
    {
        public string Label;
        public long Value;
        public long Excellent;
        public long Good;
        public long Medium;
        public long Poor;

        public RankedMetric(string label, long value, long excellent, long good, long medium, long poor)
        {
            Label = label;
            Value = value;
            Excellent = excellent;
            Good = good;
            Medium = medium;
            Poor = poor;
        }

        public PerfRank Rank()
        {
            if (Value <= Excellent) return PerfRank.Excellent;
            if (Value <= Good) return PerfRank.Good;
            if (Value <= Medium) return PerfRank.Medium;
            if (Value <= Poor) return PerfRank.Poor;
            return PerfRank.VeryPoor;
        }
    }

    /// <summary>
    /// Turns a snapshot into checklist findings.
    /// <para>
    /// The avatar limits mirror VRChat's PC avatar performance ranks as
    /// published at
    /// https://creators.vrchat.com/avatars/avatar-performance-ranking-system/
    /// as of 2026-08. They are reference values: VRChat revises them, and this
    /// package's CI does not check them against the live document. The
    /// authoritative numbers are the ones there and in the SDK's own
    /// validation — the findings say so rather than presenting a verdict.
    /// </para>
    /// </summary>
    public static class ChecklistRules
    {
        /// <summary>The ranked PC avatar metrics for a snapshot.</summary>
        public static List<RankedMetric> AvatarMetrics(StatsSnapshot stats)
        {
            return new List<RankedMetric>
            {
                new RankedMetric("Triangles", stats.Triangles, 32000, 70000, 140000, 260000),
                new RankedMetric("Texture Memory (bytes)", stats.TextureMemoryBytes,
                    40L * 1024 * 1024, 75L * 1024 * 1024, 110L * 1024 * 1024, 150L * 1024 * 1024),
                new RankedMetric("Skinned Mesh Renderers", stats.SkinnedMeshRendererCount, 1, 2, 8, 16),
                new RankedMetric("Mesh Renderers", stats.MeshRendererCount, 4, 8, 16, 24),
                new RankedMetric("Material Slots", stats.MaterialSlotCount, 4, 8, 16, 32),
                new RankedMetric("Bones", stats.BoneCount, 75, 150, 256, 400),
                new RankedMetric("PhysBone Components", stats.PhysBoneCount, 4, 8, 16, 32),
                new RankedMetric("PhysBone Colliders", stats.PhysBoneColliderCount, 4, 8, 16, 32),
            };
        }

        /// <summary>The worst rank across all avatar metrics.</summary>
        public static PerfRank OverallAvatarRank(StatsSnapshot stats)
        {
            PerfRank worst = PerfRank.Excellent;
            foreach (RankedMetric metric in AvatarMetrics(stats))
            {
                PerfRank rank = metric.Rank();
                if (rank > worst)
                {
                    worst = rank;
                }
            }
            return worst;
        }

        /// <summary>
        /// Evaluates the snapshot. Broken references are always reported;
        /// everything else depends on the mode. The items produced here are
        /// aggregates with no path — the per-object findings come from the
        /// scanner, which is the half that needs Unity.
        /// </summary>
        /// <param name="moduleHandled">
        /// True when an SDK-aware module is running for this mode. The
        /// descriptor checks below are then suppressed: they can only detect a
        /// descriptor by type name, and saying "not found (or the SDK is not
        /// installed)" next to a module that read the real type is worse than
        /// saying nothing.
        /// </param>
        public static List<InspectionItem> Evaluate(
            StatsSnapshot stats, InspectMode mode, bool moduleHandled = false)
        {
            var items = new List<InspectionItem>();

            AddBrokenReferenceItems(stats, items);

            if (mode == InspectMode.Avatar)
            {
                AddAvatarItems(stats, items, moduleHandled);
            }
            else if (mode == InspectMode.World)
            {
                AddWorldItems(stats, items, moduleHandled);
            }

            if (stats.UnknownTextureFormatCount > 0)
            {
                items.Add(new InspectionItem(
                    "Textures", InspectionSeverity.Info,
                    stats.UnknownTextureFormatCount + " texture(s) use a format the memory " +
                    "table does not know; a 32 bpp fallback was assumed for them.",
                    string.Empty));
            }

            return items;
        }

        private static void AddBrokenReferenceItems(StatsSnapshot stats, List<InspectionItem> items)
        {
            if (stats.MissingScriptCount > 0)
            {
                items.Add(new InspectionItem("Summary", InspectionSeverity.Error,
                    stats.MissingScriptCount + " missing script(s). The component's type " +
                    "no longer exists in the project.", string.Empty));
            }
            if (stats.MissingPrefabCount > 0)
            {
                items.Add(new InspectionItem("Summary", InspectionSeverity.Error,
                    stats.MissingPrefabCount + " prefab instance(s) whose prefab asset is missing.",
                    string.Empty));
            }
            if (stats.MissingMeshCount > 0)
            {
                items.Add(new InspectionItem("Summary", InspectionSeverity.Error,
                    stats.MissingMeshCount + " renderer(s)/filter(s) with no mesh assigned.",
                    string.Empty));
            }
            if (stats.MissingBoneCount > 0)
            {
                items.Add(new InspectionItem("Summary", InspectionSeverity.Warning,
                    stats.MissingBoneCount + " skinned mesh bone slot(s) are null.",
                    string.Empty));
            }
            if (stats.EmptyMaterialSlotCount > 0)
            {
                items.Add(new InspectionItem("Summary", InspectionSeverity.Warning,
                    stats.EmptyMaterialSlotCount + " material slot(s) are empty; those " +
                    "submeshes render magenta.", string.Empty));
            }
            if (stats.MissingReferenceCount > 0)
            {
                items.Add(new InspectionItem("Summary", InspectionSeverity.Warning,
                    stats.MissingReferenceCount + " serialized field(s) point at an object " +
                    "that no longer exists (shown as \"Missing\" in the Inspector).",
                    string.Empty));
            }
        }

        private static void AddAvatarItems(
            StatsSnapshot stats, List<InspectionItem> items, bool moduleHandled)
        {
            if (!moduleHandled && !stats.HasAvatarDescriptor)
            {
                items.Add(new InspectionItem("Avatar", InspectionSeverity.Warning,
                    "No VRCAvatarDescriptor found on the target. Uploading requires one " +
                    "(or the VRChat SDK is not installed in this project).", string.Empty));
            }

            foreach (RankedMetric metric in AvatarMetrics(stats))
            {
                PerfRank rank = metric.Rank();
                if (rank < PerfRank.Poor)
                {
                    continue;
                }
                InspectionSeverity severity = rank == PerfRank.VeryPoor
                    ? InspectionSeverity.Warning
                    : InspectionSeverity.Info;
                items.Add(new InspectionItem("Performance", severity,
                    metric.Label + " = " + metric.Value + " ranks " + rank +
                    " on PC (reference values; see the VRChat documentation for current limits).",
                    string.Empty));
            }
        }

        private static void AddWorldItems(
            StatsSnapshot stats, List<InspectionItem> items, bool moduleHandled)
        {
            if (!moduleHandled && !stats.HasSceneDescriptor)
            {
                items.Add(new InspectionItem("World", InspectionSeverity.Warning,
                    "No VRCSceneDescriptor found on the target. A world scene needs one " +
                    "(or the VRChat SDK is not installed in this project).", string.Empty));
            }
            if (stats.RealtimeLightCount > 0)
            {
                items.Add(new InspectionItem("World", InspectionSeverity.Info,
                    stats.RealtimeLightCount + " realtime (non-baked) light(s). Realtime " +
                    "lighting is a common world performance cost; consider baking.",
                    string.Empty));
            }
            if (stats.CameraCount > 0)
            {
                items.Add(new InspectionItem("World", InspectionSeverity.Info,
                    stats.CameraCount + " Camera component(s) under the target. Extra " +
                    "enabled cameras render every frame.", string.Empty));
            }
        }
    }
}
