// Runs the package's decision-making code outside Unity.
//
// Everything under Editor/Core is written without a UnityEngine reference
// precisely so this is possible: the rank thresholds, the checklist, the
// memory estimate and the Markdown writer are compiled here against the plain
// .NET runtime and executed, so a pull request exercises the logic rather than
// only parsing it.
//
// What this does NOT cover: the collectors and the window, which need Unity's
// object model. Those are compile-checked in verify.sh against real
// UnityEngine reference assemblies, and run for real in the Unity workflow.
using System;
using System.Collections.Generic;
using SabaTools.Inspect;
using SabaTools.Inspect.Avatar;
using SabaTools.Inspect.World;

internal static class InspectRulesTests
{
    private static int _failures;

    private static int Main()
    {
        RankThresholdsAreInclusiveUpperBounds();
        AvatarMetricDefinitionsMatchPublishedSnapshot();
        OverallRankIsTheWorstMetric();
        CleanGenericSnapshotProducesNoFindings();
        BrokenReferencesAreReportedInEveryMode();
        AvatarModeReportsAMissingDescriptor();
        AvatarModeOnlyReportsPoorOrWorse();
        PoorAndVeryPoorUseDifferentSeverities();
        WorldModeReportsRealtimeLightsAndCameras();
        UnknownTextureFormatIsDisclosed();
        TextureMemoryMatchesTheFormatTable();
        TextureFormatFamiliesRemainCovered();
        UnknownFormatFallsBackToThirtyTwoBits();
        DegenerateTextureSizesEstimateZero();
        ByteFormattingUsesBinaryUnits();
        MarkdownCarriesStatsAndFindings();
        MarkdownEscapesTableSeparators();
        EmptyMarkdownOmitsFindingsSection();
        SeverityCountsMatchTheItems();
        AModuleTakesOverTheDescriptorDiagnosis();
        ExpressionParameterBitsAreCostedPerType();
        NearlyFullIsAboutRoomForOneMoreParameter();
        AvatarLimitConstantsRemainStable();
        RespawnHeightIsComparedAgainstGeometry();
        WorldLimitConstantsRemainStable();

        if (_failures > 0)
        {
            Console.Error.WriteLine($"FAILED: {_failures} check(s)");
            return 1;
        }
        Console.WriteLine("ok: all offline checks passed");
        return 0;
    }

    // -----------------------------------------------------------------------
    // Ranking
    // -----------------------------------------------------------------------

    private static void RankThresholdsAreInclusiveUpperBounds()
    {
        // 32000 triangles is Excellent and 32001 is Good: the published limits
        // are inclusive, and an off-by-one here would mislabel every avatar
        // sitting exactly on a boundary.
        AreEqual(PerfRank.Excellent, TriangleRank(0), "zero triangles");
        AreEqual(PerfRank.Excellent, TriangleRank(32000), "at the Excellent limit");
        AreEqual(PerfRank.Good, TriangleRank(32001), "one over the Excellent limit");
        AreEqual(PerfRank.Good, TriangleRank(70000), "at the Good limit");
        AreEqual(PerfRank.Medium, TriangleRank(70001), "one over the Good limit");
        AreEqual(PerfRank.Medium, TriangleRank(140000), "at the Medium limit");
        AreEqual(PerfRank.Poor, TriangleRank(140001), "one over the Medium limit");
        AreEqual(PerfRank.Poor, TriangleRank(260000), "at the Poor limit");
        AreEqual(PerfRank.VeryPoor, TriangleRank(260001), "one over the Poor limit");
    }

    private static PerfRank TriangleRank(long triangles)
    {
        var stats = new StatsSnapshot { Triangles = triangles };
        foreach (RankedMetric metric in ChecklistRules.AvatarMetrics(stats))
        {
            if (metric.Label == "Triangles")
            {
                return metric.Rank();
            }
        }
        throw new InvalidOperationException("no Triangles metric");
    }

    private static void AvatarMetricDefinitionsMatchPublishedSnapshot()
    {
        var stats = new StatsSnapshot
        {
            Triangles = 1,
            TextureMemoryBytes = 2,
            SkinnedMeshRendererCount = 3,
            MeshRendererCount = 4,
            MaterialSlotCount = 5,
            BoneCount = 6,
            PhysBoneCount = 7,
            PhysBoneColliderCount = 8,
        };

        List<RankedMetric> metrics = ChecklistRules.AvatarMetrics(stats);
        AreEqual(8, metrics.Count, "number of ranked avatar metrics");
        AssertMetric(metrics[0], "Triangles", 1, 32000, 70000, 140000, 260000);
        AssertMetric(metrics[1], "Texture Memory (bytes)", 2,
            40L * 1024 * 1024, 75L * 1024 * 1024,
            110L * 1024 * 1024, 150L * 1024 * 1024);
        AssertMetric(metrics[2], "Skinned Mesh Renderers", 3, 1, 2, 8, 16);
        AssertMetric(metrics[3], "Mesh Renderers", 4, 4, 8, 16, 24);
        AssertMetric(metrics[4], "Material Slots", 5, 4, 8, 16, 32);
        AssertMetric(metrics[5], "Bones", 6, 75, 150, 256, 400);
        AssertMetric(metrics[6], "PhysBone Components", 7, 4, 8, 16, 32);
        AssertMetric(metrics[7], "PhysBone Colliders", 8, 4, 8, 16, 32);
    }

    private static void OverallRankIsTheWorstMetric()
    {
        // Everything Excellent except one metric: the overall rank has to
        // follow the worst one, not an average.
        var stats = new StatsSnapshot { SkinnedMeshRendererCount = 1, MeshRendererCount = 1 };
        AreEqual(PerfRank.Excellent, ChecklistRules.OverallAvatarRank(stats), "baseline");

        stats.BoneCount = 401;
        AreEqual(PerfRank.VeryPoor, ChecklistRules.OverallAvatarRank(stats), "one bad metric dominates");
    }

    // -----------------------------------------------------------------------
    // Checklist
    // -----------------------------------------------------------------------

    private static void CleanGenericSnapshotProducesNoFindings()
    {
        var stats = new StatsSnapshot { Triangles = 1000, MaterialSlotCount = 1 };
        List<InspectionItem> items = ChecklistRules.Evaluate(stats, InspectMode.Generic);
        AreEqual(0, items.Count, "a clean generic snapshot should be silent");
    }

    private static void BrokenReferencesAreReportedInEveryMode()
    {
        foreach (InspectMode mode in new[] { InspectMode.Generic, InspectMode.Avatar, InspectMode.World })
        {
            var stats = new StatsSnapshot
            {
                MissingScriptCount = 2,
                MissingPrefabCount = 1,
                MissingMeshCount = 1,
                MissingBoneCount = 3,
                EmptyMaterialSlotCount = 1,
                MissingReferenceCount = 4,
                HasAvatarDescriptor = true,
                HasSceneDescriptor = true,
                SkinnedMeshRendererCount = 1,
            };
            List<InspectionItem> items = ChecklistRules.Evaluate(stats, mode);
            AreEqual(3, CountOf(items, InspectionSeverity.Error), $"errors in {mode} mode");
            IsTrue(CountOf(items, InspectionSeverity.Warning) >= 3, $"warnings in {mode} mode");
        }
    }

    private static void AvatarModeReportsAMissingDescriptor()
    {
        var stats = new StatsSnapshot { SkinnedMeshRendererCount = 1 };
        IsTrue(Mentions(ChecklistRules.Evaluate(stats, InspectMode.Avatar), "VRCAvatarDescriptor"),
            "avatar mode should note a missing descriptor");
        IsFalse(Mentions(ChecklistRules.Evaluate(stats, InspectMode.Generic), "VRCAvatarDescriptor"),
            "generic mode makes no VRChat assumptions");
    }

    private static void AvatarModeOnlyReportsPoorOrWorse()
    {
        // A Medium avatar is a normal avatar. Reporting it would make the
        // findings list noise, and noise is what stops the real errors being
        // read.
        var medium = new StatsSnapshot
        {
            HasAvatarDescriptor = true,
            Triangles = 100000,
            SkinnedMeshRendererCount = 3,
        };
        AreEqual(0, ChecklistRules.Evaluate(medium, InspectMode.Avatar).Count,
            "Medium metrics should not produce findings");

        var poor = new StatsSnapshot
        {
            HasAvatarDescriptor = true,
            Triangles = 200000,
            SkinnedMeshRendererCount = 1,
        };
        IsTrue(Mentions(ChecklistRules.Evaluate(poor, InspectMode.Avatar), "Poor"),
            "Poor metrics should be reported");
    }

    private static void PoorAndVeryPoorUseDifferentSeverities()
    {
        var poor = new StatsSnapshot
        {
            HasAvatarDescriptor = true,
            Triangles = 140001,
        };
        List<InspectionItem> poorItems = ChecklistRules.Evaluate(poor, InspectMode.Avatar);
        AreEqual(1, CountOf(poorItems, InspectionSeverity.Info), "Poor is advisory");
        AreEqual(0, CountOf(poorItems, InspectionSeverity.Warning), "Poor is not a warning");

        var veryPoor = new StatsSnapshot
        {
            HasAvatarDescriptor = true,
            Triangles = 260001,
        };
        List<InspectionItem> veryPoorItems = ChecklistRules.Evaluate(veryPoor, InspectMode.Avatar);
        AreEqual(0, CountOf(veryPoorItems, InspectionSeverity.Info), "VeryPoor is not advisory");
        AreEqual(1, CountOf(veryPoorItems, InspectionSeverity.Warning), "VeryPoor is a warning");
    }

    private static void WorldModeReportsRealtimeLightsAndCameras()
    {
        var stats = new StatsSnapshot
        {
            HasSceneDescriptor = true,
            RealtimeLightCount = 4,
            LightCount = 10,
            CameraCount = 2,
        };
        List<InspectionItem> items = ChecklistRules.Evaluate(stats, InspectMode.World);
        IsTrue(Mentions(items, "realtime"), "world mode should note realtime lights");
        IsTrue(Mentions(items, "Camera"), "world mode should note extra cameras");
        AreEqual(0, CountOf(items, InspectionSeverity.Warning), "these are advisory, not warnings");
    }

    private static void UnknownTextureFormatIsDisclosed()
    {
        // A silent fallback would make the memory figure look authoritative
        // when part of it was guessed.
        var stats = new StatsSnapshot { UnknownTextureFormatCount = 2 };
        IsTrue(Mentions(ChecklistRules.Evaluate(stats, InspectMode.Generic), "32 bpp"),
            "the fallback should be disclosed");
    }

    // -----------------------------------------------------------------------
    // Texture memory
    // -----------------------------------------------------------------------

    private static void TextureMemoryMatchesTheFormatTable()
    {
        IsTrue(TextureMemoryEstimate.TryGetBitsPerPixel("DXT5", out double dxt5), "DXT5 is known");
        AreEqual(8.0, dxt5, "DXT5 is 8 bpp");

        // 2048 * 2048 * 8 bits / 8 = 4 MiB.
        long noMips = TextureMemoryEstimate.EstimateBytes(2048, 2048, dxt5, false, 1);
        AreEqual(4L * 1024 * 1024, noMips, "2048^2 DXT5 without mips");

        long withMips = TextureMemoryEstimate.EstimateBytes(2048, 2048, dxt5, true, 1);
        AreEqual(noMips * 4 / 3, withMips, "the mip chain adds the 4/3 bound");

        long cube = TextureMemoryEstimate.EstimateBytes(256, 256, dxt5, false, 6);
        AreEqual(TextureMemoryEstimate.EstimateBytes(256, 256, dxt5, false, 1) * 6, cube,
            "a cubemap is six faces");
    }

    private static void TextureFormatFamiliesRemainCovered()
    {
        var expected = new Dictionary<string, double>
        {
            { "RGBA32", 32 },
            { "DXT1", 4 },
            { "BC7", 8 },
            { "ETC2_RGBA8", 8 },
            { "ASTC_4x4", 8 },
            { "ASTC_8x8", 2 },
            { "ASTC_HDR_12x12", 128.0 / 144.0 },
            { "RGBAFloat", 128 },
        };

        foreach (KeyValuePair<string, double> pair in expected)
        {
            IsTrue(TextureMemoryEstimate.TryGetBitsPerPixel(pair.Key, out double actual),
                pair.Key + " remains a known format");
            AreEqual(pair.Value, actual, pair.Key + " bits per pixel");
        }
    }

    private static void UnknownFormatFallsBackToThirtyTwoBits()
    {
        IsFalse(TextureMemoryEstimate.TryGetBitsPerPixel("SomeFutureFormat", out double bpp),
            "an unknown format reports itself as unknown");
        AreEqual(TextureMemoryEstimate.UnknownFormatBitsPerPixel, bpp, "the fallback is used");

        IsFalse(TextureMemoryEstimate.TryGetBitsPerPixel(null, out double nullBpp), "null is unknown");
        AreEqual(TextureMemoryEstimate.UnknownFormatBitsPerPixel, nullBpp, "null takes the fallback too");
    }

    private static void DegenerateTextureSizesEstimateZero()
    {
        AreEqual(0L, TextureMemoryEstimate.EstimateBytes(0, 512, 8, false, 1), "zero width");
        AreEqual(0L, TextureMemoryEstimate.EstimateBytes(512, -1, 8, false, 1), "negative height");
        AreEqual(0L, TextureMemoryEstimate.EstimateBytes(512, 512, 8, false, 0), "zero faces");
        AreEqual(0L, TextureMemoryEstimate.EstimateBytes(512, 512, 0, false, 1), "zero bpp");
        AreEqual(0L, TextureMemoryEstimate.EstimateBytes(512, 512, -1, false, 1), "negative bpp");
    }

    private static void ByteFormattingUsesBinaryUnits()
    {
        AreEqual("512 B", TextureMemoryEstimate.FormatBytes(512), "bytes");
        AreEqual("1.0 KB", TextureMemoryEstimate.FormatBytes(1024), "kibibytes");
        AreEqual("1.0 MB", TextureMemoryEstimate.FormatBytes(1024 * 1024), "mebibytes");
        AreEqual("1.00 GB", TextureMemoryEstimate.FormatBytes(1024L * 1024 * 1024), "gibibytes");
    }

    // -----------------------------------------------------------------------
    // Report
    // -----------------------------------------------------------------------

    private static void MarkdownCarriesStatsAndFindings()
    {
        var report = new InspectionReport
        {
            TargetName = "Avatar",
            Mode = InspectMode.Avatar,
            GeneratedAt = "2026-08-25 12:00",
        };
        report.Rows.Add(new StatRow("Geometry", "Triangles", "1,234"));
        report.Items.Add(new InspectionItem("References", InspectionSeverity.Error,
            "Missing script.", "Avatar/Body"));

        string markdown = report.ToMarkdown();
        Contains(markdown, "# SabaTools Inspect Report", "title");
        Contains(markdown, "2026-08-25 12:00", "timestamp");
        Contains(markdown, "## Geometry", "stat group heading");
        Contains(markdown, "| Triangles | 1,234 |", "stat row");
        Contains(markdown, "## Findings", "findings heading");
        Contains(markdown, "`Avatar/Body`", "finding path");
        Contains(markdown, "1 error(s), 0 warning(s)", "counts");
    }

    private static void MarkdownEscapesTableSeparators()
    {
        // An object called "left|right" would otherwise split the row and
        // silently corrupt every cell after it.
        var report = new InspectionReport { TargetName = "left|right" };
        Contains(report.ToMarkdown(), "left\\|right", "pipes are escaped");
    }

    private static void EmptyMarkdownOmitsFindingsSection()
    {
        var report = new InspectionReport { TargetName = "Clean" };
        string markdown = report.ToMarkdown();

        Contains(markdown, "0 error(s), 0 warning(s)", "empty finding counts");
        DoesNotContain(markdown, "## Findings", "empty findings section");
        DoesNotContain(markdown, "| Generated |", "empty timestamp row");
    }

    private static void SeverityCountsMatchTheItems()
    {
        var report = new InspectionReport();
        report.Items.Add(new InspectionItem("a", InspectionSeverity.Error, "e", null));
        report.Items.Add(new InspectionItem("a", InspectionSeverity.Warning, "w", null));
        report.Items.Add(new InspectionItem("a", InspectionSeverity.Warning, "w", null));
        report.Items.Add(new InspectionItem("a", InspectionSeverity.Info, "i", null));

        AreEqual(1, report.ErrorCount, "errors");
        AreEqual(2, report.WarningCount, "warnings");
        AreEqual(1, report.Count(InspectionSeverity.Info), "infos");
        AreEqual(string.Empty, report.Items[0].Path, "a null path becomes empty");
    }

    // -----------------------------------------------------------------------
    // Module hand-off
    // -----------------------------------------------------------------------

    private static void AModuleTakesOverTheDescriptorDiagnosis()
    {
        // With an SDK module running, the core checklist must stop guessing at
        // descriptors by type name — otherwise every avatar without one gets
        // told twice, once vaguely and once precisely.
        var stats = new StatsSnapshot { SkinnedMeshRendererCount = 1 };

        IsTrue(Mentions(ChecklistRules.Evaluate(stats, InspectMode.Avatar, moduleHandled: false),
            "VRCAvatarDescriptor"), "without a module the core check speaks");
        IsFalse(Mentions(ChecklistRules.Evaluate(stats, InspectMode.Avatar, moduleHandled: true),
            "VRCAvatarDescriptor"), "with a module the core check stays quiet");

        var world = new StatsSnapshot();
        IsTrue(Mentions(ChecklistRules.Evaluate(world, InspectMode.World, moduleHandled: false),
            "VRCSceneDescriptor"), "without a module the core check speaks");
        IsFalse(Mentions(ChecklistRules.Evaluate(world, InspectMode.World, moduleHandled: true),
            "VRCSceneDescriptor"), "with a module the core check stays quiet");

        // Broken references are core's own finding and belong in the report
        // whether or not a module is running.
        var broken = new StatsSnapshot { MissingScriptCount = 1 };
        AreEqual(1, CountOf(ChecklistRules.Evaluate(broken, InspectMode.Avatar, true),
            InspectionSeverity.Error), "reference errors survive the hand-off");
    }

    // -----------------------------------------------------------------------
    // Avatar limits (sabatools.avatar)
    // -----------------------------------------------------------------------

    private static void ExpressionParameterBitsAreCostedPerType()
    {
        AreEqual(1, AvatarLimits.BitsFor("Bool"), "a bool costs one bit");
        AreEqual(8, AvatarLimits.BitsFor("Int"), "an int costs eight");
        AreEqual(8, AvatarLimits.BitsFor("Float"), "a float costs eight");

        // An SDK that adds a value type must be over-costed, never under: a
        // tool that reports room the avatar does not have is worse than one
        // that reports less room than it has.
        AreEqual(8, AvatarLimits.BitsFor("SomeFutureType"), "unknown types cost the widest");
        AreEqual(8, AvatarLimits.BitsFor(null), "a null type name costs the widest");

        AreEqual(256, AvatarLimits.ExpressionParameterBits, "the published budget");
        AreEqual(1.0, AvatarLimits.BudgetFraction(256), "a full budget is 1.0");
        AreEqual(0.5, AvatarLimits.BudgetFraction(128), "half a budget is 0.5");
    }

    private static void NearlyFullIsAboutRoomForOneMoreParameter()
    {
        IsFalse(AvatarLimits.IsNearlyFull(0), "an empty budget is not nearly full");
        IsFalse(AvatarLimits.IsNearlyFull(248), "exactly one Int still fits");
        IsTrue(AvatarLimits.IsNearlyFull(249), "one Int no longer fits");
        IsTrue(AvatarLimits.IsNearlyFull(256), "a full budget is nearly full");
        IsFalse(AvatarLimits.IsNearlyFull(257), "over budget is an error, not a warning");
    }

    private static void AvatarLimitConstantsRemainStable()
    {
        AreEqual(256, AvatarLimits.ExpressionParameterBits, "expression parameter bit limit");
        AreEqual(8, AvatarLimits.ControlsPerMenu, "controls per expression menu");
    }

    // -----------------------------------------------------------------------
    // World limits (sabatools.world)
    // -----------------------------------------------------------------------

    private static void RespawnHeightIsComparedAgainstGeometry()
    {
        IsTrue(WorldLimits.RespawnIsAboveGeometry(respawnHeightY: 0f, lowestGeometryY: -10f),
            "a respawn plane above the floor catches standing players");
        IsFalse(WorldLimits.RespawnIsAboveGeometry(respawnHeightY: -100f, lowestGeometryY: -10f),
            "a respawn plane below the floor is correct");
        IsFalse(WorldLimits.RespawnIsAboveGeometry(-10f, -10f),
            "exactly level is not above");

        AreEqual(-11.0, WorldLimits.SuggestedRespawnHeight(-10f),
            "the suggestion clears the lowest geometry");
    }

    private static void WorldLimitConstantsRemainStable()
    {
        AreEqual(1.0, WorldLimits.RespawnClearance, "respawn clearance");
        AreEqual(2, WorldLimits.MirrorAdvisoryCount, "mirror advisory count");
    }

    // -----------------------------------------------------------------------
    // Assertions
    // -----------------------------------------------------------------------

    private static int CountOf(List<InspectionItem> items, InspectionSeverity severity)
    {
        int count = 0;
        foreach (InspectionItem item in items)
        {
            if (item.Severity == severity)
            {
                count++;
            }
        }
        return count;
    }

    private static void AssertMetric(
        RankedMetric metric, string label, long value,
        long excellent, long good, long medium, long poor)
    {
        AreEqual(label, metric.Label, label + " label");
        AreEqual(value, metric.Value, label + " value");
        AreEqual(excellent, metric.Excellent, label + " Excellent threshold");
        AreEqual(good, metric.Good, label + " Good threshold");
        AreEqual(medium, metric.Medium, label + " Medium threshold");
        AreEqual(poor, metric.Poor, label + " Poor threshold");
    }

    private static bool Mentions(List<InspectionItem> items, string fragment)
    {
        foreach (InspectionItem item in items)
        {
            if (item.Message.Contains(fragment))
            {
                return true;
            }
        }
        return false;
    }

    private static void Fail(string what, string detail)
    {
        _failures++;
        Console.Error.WriteLine($"fail: {what} ({detail})");
    }

    private static void AreEqual(object expected, object actual, string what)
    {
        if (!Equals(expected, actual))
        {
            Fail(what, $"expected {expected}, got {actual}");
        }
    }

    private static void AreEqual(double expected, double actual, string what)
    {
        if (Math.Abs(expected - actual) > 1e-9)
        {
            Fail(what, $"expected {expected}, got {actual}");
        }
    }

    private static void IsTrue(bool condition, string what)
    {
        if (!condition)
        {
            Fail(what, "expected true");
        }
    }

    private static void IsFalse(bool condition, string what)
    {
        if (condition)
        {
            Fail(what, "expected false");
        }
    }

    private static void Contains(string haystack, string needle, string what)
    {
        if (haystack == null || !haystack.Contains(needle))
        {
            Fail(what, $"missing {needle}");
        }
    }

    private static void DoesNotContain(string haystack, string needle, string what)
    {
        if (haystack != null && haystack.Contains(needle))
        {
            Fail(what, $"unexpected {needle}");
        }
    }
}
