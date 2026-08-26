// Collects the textures a hierarchy depends on and estimates their GPU
// memory. Goes through EditorUtility.CollectDependencies so it sees what a
// build would pull in through materials, not only what is directly assigned.
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace SabaTools.Inspect.Editors
{
    internal sealed class TextureEntry
    {
        internal Texture Texture;
        internal string Name;
        internal string Detail;   // "2048x2048 DXT5, mips"
        internal long Bytes;
        internal bool KnownFormat;
    }

    internal static class TextureUsageCollector
    {
        internal static List<TextureEntry> Collect(GameObject[] roots, StatsSnapshot stats)
        {
            var entries = new List<TextureEntry>();
            var seen = new HashSet<Texture>();

            var rootObjects = new List<Object>();
            foreach (GameObject root in roots)
            {
                if (root != null)
                {
                    rootObjects.Add(root);
                }
            }

            foreach (Object dependency in EditorUtility.CollectDependencies(rootObjects.ToArray()))
            {
                var texture = dependency as Texture;
                if (texture == null || !seen.Add(texture))
                {
                    continue;
                }
                entries.Add(Describe(texture));
            }

            entries.Sort((a, b) => b.Bytes.CompareTo(a.Bytes));

            stats.TextureCount = entries.Count;
            foreach (TextureEntry entry in entries)
            {
                stats.TextureMemoryBytes += entry.Bytes;
                if (!entry.KnownFormat)
                {
                    stats.UnknownTextureFormatCount++;
                }
            }
            return entries;
        }

        private static TextureEntry Describe(Texture texture)
        {
            string formatName;
            int faces = 1;
            bool mipmaps = texture.mipmapCount > 1;

            if (texture is Texture2D texture2D)
            {
                formatName = texture2D.format.ToString();
            }
            else if (texture is Cubemap cubemap)
            {
                formatName = cubemap.format.ToString();
                faces = 6;
            }
            else if (texture is RenderTexture renderTexture)
            {
                // RenderTextureFormat names overlap TextureFormat for the
                // common cases (ARGB32, RHalf, ...); the rest take the 32 bpp
                // fallback and are counted as unknown.
                formatName = renderTexture.format.ToString();
            }
            else
            {
                formatName = texture.GetType().Name;
            }

            bool known = TextureMemoryEstimate.TryGetBitsPerPixel(formatName, out double bitsPerPixel);
            long bytes = TextureMemoryEstimate.EstimateBytes(
                texture.width, texture.height, bitsPerPixel, mipmaps, faces);

            return new TextureEntry
            {
                Texture = texture,
                Name = texture.name,
                Detail = texture.width + "x" + texture.height + " " + formatName +
                         (mipmaps ? ", mips" : string.Empty),
                Bytes = bytes,
                KnownFormat = known,
            };
        }
    }
}
