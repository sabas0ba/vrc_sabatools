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

internal static class InspectRulesTests
{
    private static int _failures;

    private static int Main()
    {
        RankThresholdsAreInclusiveUpperBounds();
        OverallRankIsTheWorstMetric();
        CleanGenericSnapshotProducesNoFindings();
        BrokenReferencesAreReportedInEveryMode();
        AvatarModeReportsAMissingDescriptor();
        AvatarModeOnlyReportsPoorOrWorse();
        WorldModeReportsRealtimeLightsAndCameras();
        UnknownTextureFormatIsDisclosed();
        TextureMemoryMatchesTheFormatTable();
        UnknownFormatFallsBackToThirtyTwoBits();
        DegenerateTextureSizesEstimateZero();
        ByteFormattingUsesBinaryUnits();
        MarkdownCarriesStatsAndFindings();
        MarkdownEscapesTableSeparators();
        SeverityCountsMatchTheItems();

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
}
