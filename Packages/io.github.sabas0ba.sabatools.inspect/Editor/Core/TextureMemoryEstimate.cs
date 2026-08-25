// GPU memory estimation for textures. Pure: formats arrive by name (what
// UnityEngine.TextureFormat.ToString() produces on the Unity side) so this
// file has no UnityEngine reference and the table is testable offline.
using System.Collections.Generic;

namespace SabaTools.Inspect
{
    /// <summary>
    /// Estimates the GPU memory a texture occupies from its dimensions and
    /// format. These are estimates: Unity pads small compressed textures, and
    /// the exact mip chain sum depends on per-level rounding — the factor used
    /// here is the geometric-series bound of 4/3.
    /// </summary>
    public static class TextureMemoryEstimate
    {
        // Bits per pixel by UnityEngine.TextureFormat name. ASTC entries are
        // 128 bits per block divided by the block area.
        private static readonly Dictionary<string, double> BitsPerPixel =
            new Dictionary<string, double>
            {
                { "Alpha8", 8 },
                { "ARGB4444", 16 },
                { "RGB24", 24 },
                { "RGBA32", 32 },
                { "ARGB32", 32 },
                { "RGB565", 16 },
                { "R16", 16 },
                { "DXT1", 4 },
                { "DXT5", 8 },
                { "RGBA4444", 16 },
                { "BGRA32", 32 },
                { "RHalf", 16 },
                { "RGHalf", 32 },
                { "RGBAHalf", 64 },
                { "RFloat", 32 },
                { "RGFloat", 64 },
                { "RGBAFloat", 128 },
                { "RGB9e5Float", 32 },
                { "BC4", 4 },
                { "BC5", 8 },
                { "BC6H", 8 },
                { "BC7", 8 },
                { "DXT1Crunched", 4 },
                { "DXT5Crunched", 8 },
                { "ETC_RGB4", 4 },
                { "EAC_R", 4 },
                { "EAC_R_SIGNED", 4 },
                { "EAC_RG", 8 },
                { "EAC_RG_SIGNED", 8 },
                { "ETC2_RGB", 4 },
                { "ETC2_RGBA1", 4 },
                { "ETC2_RGBA8", 8 },
                { "ETC_RGB4Crunched", 4 },
                { "ETC2_RGBA8Crunched", 8 },
                { "ASTC_4x4", 8 },
                { "ASTC_5x5", 128.0 / 25.0 },
                { "ASTC_6x6", 128.0 / 36.0 },
                { "ASTC_8x8", 2 },
                { "ASTC_10x10", 1.28 },
                { "ASTC_12x12", 128.0 / 144.0 },
                { "ASTC_HDR_4x4", 8 },
                { "ASTC_HDR_5x5", 128.0 / 25.0 },
                { "ASTC_HDR_6x6", 128.0 / 36.0 },
                { "ASTC_HDR_8x8", 2 },
                { "ASTC_HDR_10x10", 1.28 },
                { "ASTC_HDR_12x12", 128.0 / 144.0 },
                { "R8", 8 },
                { "RG16", 16 },
                { "RG32", 32 },
                { "RGB48", 48 },
                { "RGBA64", 64 },
            };

        /// <summary>Fallback bpp for formats the table does not know.</summary>
        public const double UnknownFormatBitsPerPixel = 32;

        /// <summary>
        /// Looks up the bits per pixel for a format name. Returns false — and
        /// the 32 bpp fallback — for names the table does not know, so a Unity
        /// version with new formats degrades to a conservative estimate rather
        /// than reporting zero.
        /// </summary>
        public static bool TryGetBitsPerPixel(string formatName, out double bitsPerPixel)
        {
            if (formatName != null && BitsPerPixel.TryGetValue(formatName, out bitsPerPixel))
            {
                return true;
            }
            bitsPerPixel = UnknownFormatBitsPerPixel;
            return false;
        }

        /// <summary>
        /// Estimated bytes for a texture. <paramref name="faces"/> is 1 for
        /// Texture2D and 6 for a cubemap; a full mip chain multiplies the base
        /// level by 4/3.
        /// </summary>
        public static long EstimateBytes(int width, int height, double bitsPerPixel, bool mipmaps, int faces)
        {
            if (width <= 0 || height <= 0 || faces <= 0 || bitsPerPixel <= 0)
            {
                return 0;
            }
            double bytes = (double)width * height * faces * bitsPerPixel / 8.0;
            if (mipmaps)
            {
                bytes = bytes * 4.0 / 3.0;
            }
            return (long)bytes;
        }

        /// <summary>"512.0 KB", "12.3 MB" — binary units, one or two decimals.</summary>
        public static string FormatBytes(long bytes)
        {
            const double kb = 1024.0;
            const double mb = kb * 1024.0;
            const double gb = mb * 1024.0;
            if (bytes >= gb)
            {
                return (bytes / gb).ToString("0.00") + " GB";
            }
            if (bytes >= mb)
            {
                return (bytes / mb).ToString("0.0") + " MB";
            }
            if (bytes >= kb)
            {
                return (bytes / kb).ToString("0.0") + " KB";
            }
            return bytes + " B";
        }
    }
}
