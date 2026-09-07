using System.Collections.Generic;
using UnityEngine;

namespace SabaTools.AvatarMaterials.Editors
{
    internal enum RenderQueueRangePreset
    {
        All,
        Background,
        Geometry,
        AlphaTest,
        GeometryLast,
        Transparent,
        Overlay,
        Custom,
    }

    internal enum RenderQueueVisibilityMode
    {
        ShowAll,
        OnlySelectedRange,
        ExcludeSelectedRange,
    }

    internal sealed class RenderQueueEntry
    {
        internal MaterialSlotEntry Slot;
        internal int ShaderQueue;
        internal int EffectiveQueue;
        internal string RenderType;
        internal int? ZWrite;
        internal int? ZTest;
        internal int? Cull;
        internal int? SrcBlend;
        internal int? DstBlend;
        internal readonly List<string> Issues = new List<string>();
    }

    internal sealed class RendererBoundsEntry
    {
        internal Renderer Renderer;
        internal string RendererPath;
        internal Bounds LocalBounds;
        internal Bounds WorldBounds;
        internal int SubMeshCount;
        internal bool UpdateWhenOffscreen;
        internal readonly List<string> Issues = new List<string>();
    }

    internal sealed class BoundaryProbeResult
    {
        internal int DistanceIndex;
        internal int DirectionIndex;
        internal Vector3 CameraPosition;
        internal Quaternion CameraRotation;
        internal int VisibleRendererCount;
        internal int TotalRendererCount;
        internal readonly HashSet<string> VisibleRendererPaths = new HashSet<string>();

        internal bool AllVisible => TotalRendererCount > 0
            && VisibleRendererCount == TotalRendererCount;

        internal bool NoneVisible => VisibleRendererCount == 0;
    }

    internal static class AvatarRenderDiagnostics
    {
        internal const int OpaqueLastQueue = 2500;

        internal static List<RenderQueueEntry> CollectRenderQueues(
            List<MaterialSlotEntry> slots)
        {
            var result = new List<RenderQueueEntry>();
            var rendererIssuesAdded = new HashSet<Renderer>();
            foreach (MaterialSlotEntry slot in slots)
            {
                var entry = new RenderQueueEntry
                {
                    Slot = slot,
                };
                Material material = slot.Material;
                if (material == null || material.shader == null)
                {
                    entry.ShaderQueue = -1;
                    entry.EffectiveQueue = -1;
                    entry.RenderType = "Missing";
                    entry.Issues.Add("Material or shader is missing.");
                    result.Add(entry);
                    continue;
                }

                entry.ShaderQueue = material.shader.renderQueue;
                entry.EffectiveQueue = material.renderQueue;
                entry.RenderType = material.GetTag("RenderType", false, string.Empty);
                entry.ZWrite = ReadInt(material, "_ZWrite");
                entry.ZTest = ReadInt(material, "_ZTest");
                entry.Cull = ReadInt(material, "_Cull");
                entry.SrcBlend = ReadInt(material, "_SrcBlend");
                entry.DstBlend = ReadInt(material, "_DstBlend");
                ValidateMaterial(entry);

                if (rendererIssuesAdded.Add(slot.Renderer))
                {
                    ValidateRendererMaterialSlots(slot.Renderer, entry.Issues);
                }
                result.Add(entry);
            }
            return result;
        }

        internal static List<RendererBoundsEntry> CollectRendererBounds(GameObject root)
        {
            var result = new List<RendererBoundsEntry>();
            if (root == null)
            {
                return result;
            }

            Bounds avatarBounds = CalculateAvatarBounds(root);
            float avatarMagnitude = Mathf.Max(avatarBounds.extents.magnitude, 0.01f);
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                if (!(renderer is MeshRenderer) && !(renderer is SkinnedMeshRenderer))
                {
                    continue;
                }

                var entry = new RendererBoundsEntry
                {
                    Renderer = renderer,
                    RendererPath = AvatarMaterialCatalog.RelativePath(
                        root.transform, renderer.transform),
                    LocalBounds = renderer.localBounds,
                    WorldBounds = renderer.bounds,
                    SubMeshCount = GetSubMeshCount(renderer),
                    UpdateWhenOffscreen = renderer is SkinnedMeshRenderer skinned
                        && skinned.updateWhenOffscreen,
                };

                if (entry.SubMeshCount < 0)
                {
                    entry.Issues.Add("Mesh is missing.");
                }
                if (entry.WorldBounds.size.x <= 0.0001f
                    || entry.WorldBounds.size.y <= 0.0001f
                    || entry.WorldBounds.size.z <= 0.0001f)
                {
                    entry.Issues.Add("World bounds has a zero or near-zero axis.");
                }
                if (entry.WorldBounds.extents.magnitude > avatarMagnitude * 4f)
                {
                    entry.Issues.Add("Bounds is more than four times the avatar extent.");
                }
                if (Vector3.Distance(entry.WorldBounds.center, avatarBounds.center)
                    > avatarMagnitude * 4f)
                {
                    entry.Issues.Add("Bounds center is far outside the avatar aggregate bounds.");
                }
                if (entry.UpdateWhenOffscreen)
                {
                    entry.Issues.Add("updateWhenOffscreen is enabled; local bounds can be recomputed each frame.");
                }
                result.Add(entry);
            }

            result.Sort((left, right) => string.CompareOrdinal(
                left.RendererPath, right.RendererPath));
            return result;
        }

        internal static List<BoundaryProbeResult> EvaluateBoundaryProbes(
            List<RendererBoundsEntry> renderers,
            Vector3 viewPoint,
            float[] distances,
            float fieldOfView,
            float aspect)
        {
            var result = new List<BoundaryProbeResult>();
            for (int distanceIndex = 0; distanceIndex < distances.Length; distanceIndex++)
            {
                for (int directionIndex = 0; directionIndex < 16; directionIndex++)
                {
                    Vector3 direction = GetProbeDirection(directionIndex);
                    Vector3 position = viewPoint + direction * distances[distanceIndex];
                    Quaternion rotation = Quaternion.LookRotation(viewPoint - position, Vector3.up);
                    Matrix4x4 view = Matrix4x4.TRS(
                        position, rotation, new Vector3(1f, 1f, -1f)).inverse;
                    Matrix4x4 projection = Matrix4x4.Perspective(
                        fieldOfView, aspect, 0.01f, Mathf.Max(distances[distanceIndex] * 20f, 100f));
                    Plane[] planes = GeometryUtility.CalculateFrustumPlanes(projection * view);
                    var probe = new BoundaryProbeResult
                    {
                        DistanceIndex = distanceIndex,
                        DirectionIndex = directionIndex,
                        CameraPosition = position,
                        CameraRotation = rotation,
                        TotalRendererCount = renderers.Count,
                    };
                    foreach (RendererBoundsEntry renderer in renderers)
                    {
                        if (GeometryUtility.TestPlanesAABB(planes, renderer.WorldBounds))
                        {
                            probe.VisibleRendererCount++;
                            probe.VisibleRendererPaths.Add(renderer.RendererPath);
                        }
                    }
                    result.Add(probe);
                }
            }
            return result;
        }

        internal static Vector2Int ResolveQueueRange(
            RenderQueueRangePreset preset, int customMinimum, int customMaximum)
        {
            switch (preset)
            {
                case RenderQueueRangePreset.Background:
                    return new Vector2Int(0, 1499);
                case RenderQueueRangePreset.Geometry:
                    return new Vector2Int(1500, 2449);
                case RenderQueueRangePreset.AlphaTest:
                    return new Vector2Int(2450, 2499);
                case RenderQueueRangePreset.GeometryLast:
                    return new Vector2Int(2500, 2500);
                case RenderQueueRangePreset.Transparent:
                    return new Vector2Int(2501, 3999);
                case RenderQueueRangePreset.Overlay:
                    return new Vector2Int(4000, 5000);
                case RenderQueueRangePreset.Custom:
                    return new Vector2Int(
                        Mathf.Clamp(Mathf.Min(customMinimum, customMaximum), 0, 5000),
                        Mathf.Clamp(Mathf.Max(customMinimum, customMaximum), 0, 5000));
                default:
                    return new Vector2Int(0, 5000);
            }
        }

        internal static string QueueName(int queue)
        {
            if (queue < 0)
            {
                return "Invalid";
            }
            if (queue < 1500)
            {
                return "Background";
            }
            if (queue < 2450)
            {
                return "Geometry";
            }
            if (queue < 2500)
            {
                return "AlphaTest";
            }
            if (queue == 2500)
            {
                return "GeometryLast";
            }
            if (queue < 4000)
            {
                return "Transparent";
            }
            return queue <= 5000 ? "Overlay" : "Out of range";
        }

        internal static Vector3 GetProbeDirection(int index)
        {
            if (index < 8)
            {
                float angle = index * 45f * Mathf.Deg2Rad;
                return new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle));
            }

            bool upper = index < 12;
            int diagonalIndex = upper ? index - 8 : index - 12;
            float diagonalAngle = (diagonalIndex * 90f + 45f) * Mathf.Deg2Rad;
            return new Vector3(
                Mathf.Sin(diagonalAngle), upper ? 0.55f : -0.55f, Mathf.Cos(diagonalAngle)).normalized;
        }

        private static void ValidateMaterial(RenderQueueEntry entry)
        {
            if (entry.EffectiveQueue < 0 || entry.EffectiveQueue > 5000)
            {
                entry.Issues.Add("Effective render queue is outside Unity's 0..5000 range.");
                return;
            }

            bool transparentTag = entry.RenderType.IndexOf(
                "Transparent", System.StringComparison.OrdinalIgnoreCase) >= 0;
            bool cutoutTag = entry.RenderType.IndexOf(
                "Cutout", System.StringComparison.OrdinalIgnoreCase) >= 0;
            bool transparentQueue = entry.EffectiveQueue > OpaqueLastQueue;
            if (transparentTag && !transparentQueue)
            {
                entry.Issues.Add("Transparent RenderType is in the opaque queue range.");
            }
            if (!transparentTag && !cutoutTag && transparentQueue)
            {
                entry.Issues.Add("Opaque RenderType is in the transparent queue range.");
            }
            if (transparentQueue && entry.ZWrite.HasValue && entry.ZWrite.Value != 0)
            {
                entry.Issues.Add("Transparent material writes depth; verify overlap and sorting intentionally.");
            }
            if (!transparentQueue && entry.ZWrite.HasValue && entry.ZWrite.Value == 0)
            {
                entry.Issues.Add("Opaque-range material does not write depth.");
            }
        }

        private static void ValidateRendererMaterialSlots(
            Renderer renderer, List<string> issues)
        {
            int subMeshCount = GetSubMeshCount(renderer);
            if (subMeshCount < 0)
            {
                issues.Add("Renderer has no mesh.");
                return;
            }
            if (renderer.sharedMaterials.Length != subMeshCount)
            {
                issues.Add(
                    "Material slot count (" + renderer.sharedMaterials.Length
                    + ") differs from submesh count (" + subMeshCount + ").");
            }
        }

        private static int GetSubMeshCount(Renderer renderer)
        {
            if (renderer is SkinnedMeshRenderer skinned)
            {
                return skinned.sharedMesh != null ? skinned.sharedMesh.subMeshCount : -1;
            }
            if (renderer is MeshRenderer meshRenderer)
            {
                MeshFilter filter = meshRenderer.GetComponent<MeshFilter>();
                return filter != null && filter.sharedMesh != null
                    ? filter.sharedMesh.subMeshCount
                    : -1;
            }
            return -1;
        }

        private static int? ReadInt(Material material, string propertyName)
        {
            return material.HasProperty(propertyName)
                ? Mathf.RoundToInt(material.GetFloat(propertyName))
                : (int?)null;
        }

        private static Bounds CalculateAvatarBounds(GameObject root)
        {
            bool found = false;
            Bounds result = new Bounds(root.transform.position, Vector3.zero);
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                if (!(renderer is MeshRenderer) && !(renderer is SkinnedMeshRenderer))
                {
                    continue;
                }
                if (!found)
                {
                    result = renderer.bounds;
                    found = true;
                }
                else
                {
                    result.Encapsulate(renderer.bounds);
                }
            }
            return result;
        }
    }
}
