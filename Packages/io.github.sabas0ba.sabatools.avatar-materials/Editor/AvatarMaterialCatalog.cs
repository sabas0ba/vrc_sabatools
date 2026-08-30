using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace SabaTools.AvatarMaterials.Editors
{
    internal sealed class MaterialSlotEntry
    {
        internal Renderer Renderer;
        internal int SlotIndex;
        internal string RendererPath;
        internal Material Material;
    }

    internal sealed class TexturePropertyEntry
    {
        internal string Name;
        internal string DisplayName;
        internal Texture Texture;
    }

    internal sealed class TexturePropertyUsage
    {
        internal Material Material;
        internal string PropertyName;
        internal string DisplayName;
        internal int MaterialSlotCount;
    }

    internal sealed class TextureUsageEntry
    {
        internal Texture Texture;
        internal int MaterialSlotCount;
        internal string AssetPath;
        internal string SourceHash;
        internal int DuplicateSourceCount;
        internal readonly List<string> DuplicateSourcePaths = new List<string>();
        internal readonly List<TexturePropertyUsage> Properties =
            new List<TexturePropertyUsage>();
    }

    internal static class AvatarMaterialCatalog
    {
        internal static List<MaterialSlotEntry> CollectSlots(GameObject root)
        {
            var result = new List<MaterialSlotEntry>();
            if (root == null)
            {
                return result;
            }

            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                Material[] materials = renderer.sharedMaterials;
                for (int index = 0; index < materials.Length; index++)
                {
                    result.Add(new MaterialSlotEntry
                    {
                        Renderer = renderer,
                        SlotIndex = index,
                        RendererPath = RelativePath(root.transform, renderer.transform),
                        Material = materials[index],
                    });
                }
            }

            result.Sort((left, right) =>
            {
                int path = string.CompareOrdinal(left.RendererPath, right.RendererPath);
                return path != 0 ? path : left.SlotIndex.CompareTo(right.SlotIndex);
            });
            return result;
        }

        internal static List<TexturePropertyEntry> CollectTextures(Material material)
        {
            var result = new List<TexturePropertyEntry>();
            if (material == null || material.shader == null)
            {
                return result;
            }

            Shader shader = material.shader;
            int propertyCount = shader.GetPropertyCount();
            for (int index = 0; index < propertyCount; index++)
            {
                if (shader.GetPropertyType(index) != ShaderPropertyType.Texture)
                {
                    continue;
                }

                string propertyName = shader.GetPropertyName(index);
                result.Add(new TexturePropertyEntry
                {
                    Name = propertyName,
                    DisplayName = shader.GetPropertyDescription(index),
                    Texture = material.GetTexture(propertyName),
                });
            }
            return result;
        }

        internal static List<TextureUsageEntry> CollectTextureInventory(
            List<MaterialSlotEntry> slots)
        {
            var resultByTexture = new Dictionary<Texture, TextureUsageEntry>();
            var materialSlotCounts = new Dictionary<Material, int>();

            foreach (MaterialSlotEntry slot in slots)
            {
                if (slot.Material == null)
                {
                    continue;
                }

                if (!materialSlotCounts.ContainsKey(slot.Material))
                {
                    materialSlotCounts.Add(slot.Material, 0);
                }
                materialSlotCounts[slot.Material]++;
            }

            foreach (KeyValuePair<Material, int> materialAndCount in materialSlotCounts)
            {
                var texturesUsedByMaterial = new HashSet<Texture>();
                foreach (TexturePropertyEntry property in CollectTextures(materialAndCount.Key))
                {
                    if (property.Texture == null)
                    {
                        continue;
                    }

                    if (!resultByTexture.TryGetValue(
                            property.Texture, out TextureUsageEntry textureUsage))
                    {
                        textureUsage = new TextureUsageEntry
                        {
                            Texture = property.Texture,
                        };
                        resultByTexture.Add(property.Texture, textureUsage);
                    }

                    textureUsage.Properties.Add(new TexturePropertyUsage
                    {
                        Material = materialAndCount.Key,
                        PropertyName = property.Name,
                        DisplayName = property.DisplayName,
                        MaterialSlotCount = materialAndCount.Value,
                    });
                    texturesUsedByMaterial.Add(property.Texture);
                }

                foreach (Texture texture in texturesUsedByMaterial)
                {
                    resultByTexture[texture].MaterialSlotCount += materialAndCount.Value;
                }
            }

            var result = new List<TextureUsageEntry>(resultByTexture.Values);
            result.Sort((left, right) => string.Compare(
                left.Texture != null ? left.Texture.name : string.Empty,
                right.Texture != null ? right.Texture.name : string.Empty,
                System.StringComparison.OrdinalIgnoreCase));
            foreach (TextureUsageEntry entry in result)
            {
                entry.AssetPath = entry.Texture != null
                    ? AssetDatabase.GetAssetPath(entry.Texture)
                    : string.Empty;
                entry.SourceHash = CalculateSourceHash(entry.AssetPath);
                entry.Properties.Sort((left, right) =>
                {
                    int materialName = string.Compare(
                        left.Material != null ? left.Material.name : string.Empty,
                        right.Material != null ? right.Material.name : string.Empty,
                        System.StringComparison.OrdinalIgnoreCase);
                    return materialName != 0
                        ? materialName
                        : string.CompareOrdinal(left.PropertyName, right.PropertyName);
                });
            }
            AnnotateDuplicateSources(result);
            return result;
        }

        private static void AnnotateDuplicateSources(List<TextureUsageEntry> entries)
        {
            var groups = new Dictionary<string, List<TextureUsageEntry>>();
            foreach (TextureUsageEntry entry in entries)
            {
                if (string.IsNullOrEmpty(entry.SourceHash))
                {
                    continue;
                }
                if (!groups.TryGetValue(entry.SourceHash, out List<TextureUsageEntry> group))
                {
                    group = new List<TextureUsageEntry>();
                    groups.Add(entry.SourceHash, group);
                }
                group.Add(entry);
            }

            foreach (List<TextureUsageEntry> group in groups.Values)
            {
                if (group.Count < 2)
                {
                    continue;
                }
                foreach (TextureUsageEntry entry in group)
                {
                    entry.DuplicateSourceCount = group.Count;
                    foreach (TextureUsageEntry duplicate in group)
                    {
                        if (duplicate != entry && !string.IsNullOrEmpty(duplicate.AssetPath))
                        {
                            entry.DuplicateSourcePaths.Add(duplicate.AssetPath);
                        }
                    }
                }
            }
        }

        private static string CalculateSourceHash(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath))
            {
                return string.Empty;
            }
            string projectRoot = Path.GetDirectoryName(Application.dataPath);
            string sourcePath = Path.IsPathRooted(assetPath)
                ? assetPath
                : Path.GetFullPath(Path.Combine(projectRoot ?? string.Empty, assetPath));
            if (!File.Exists(sourcePath))
            {
                return string.Empty;
            }
            try
            {
                using (FileStream stream = File.OpenRead(sourcePath))
                using (SHA256 sha = SHA256.Create())
                {
                    return System.BitConverter.ToString(sha.ComputeHash(stream))
                        .Replace("-", string.Empty).ToLowerInvariant();
                }
            }
            catch (IOException)
            {
                return string.Empty;
            }
            catch (System.UnauthorizedAccessException)
            {
                return string.Empty;
            }
        }

        internal static int CountUses(List<MaterialSlotEntry> slots, Material material)
        {
            int count = 0;
            foreach (MaterialSlotEntry slot in slots)
            {
                if (slot.Material == material)
                {
                    count++;
                }
            }
            return count;
        }

        internal static string RelativePath(Transform root, Transform target)
        {
            if (target == root)
            {
                return root.name;
            }

            var names = new List<string>();
            Transform current = target;
            while (current != null && current != root)
            {
                names.Add(current.name);
                current = current.parent;
            }
            names.Add(root.name);
            names.Reverse();
            return string.Join("/", names);
        }
    }
}
