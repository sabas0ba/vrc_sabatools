using System;

namespace SabaTools.AvatarMaterials
{
    public enum FallbackShaderKind
    {
        Standard,
        Toon,
        ToonCutout,
        ToonOutline,
        Unlit,
        Transparent,
        Cutout,
        VertexLit,
        Particle,
        Sprite,
        Matcap,
        MobileToon,
        ToonStandard,
        ToonStandardOutline,
        Hidden,
    }

    /// <summary>
    /// Pure decision logic for the documented VRChat shader fallback rules.
    /// Shader lookup and material copying stay in the Unity-dependent layer.
    /// </summary>
    public static class AvatarShaderRules
    {
        private static readonly string[] QuestAvatarShaders =
        {
            "VRChat/Mobile/Toon Standard",
            "VRChat/Mobile/Standard Lite",
            "VRChat/Mobile/Bumped Diffuse",
            "VRChat/Mobile/Bumped Mapped Specular",
            "VRChat/Mobile/Diffuse",
            "VRChat/Mobile/MatCap Lit",
            "VRChat/Mobile/Toon Lit",
            "VRChat/Mobile/Particles/Additive",
            "VRChat/Mobile/Particles/Multiply",
        };

        public static FallbackShaderKind ResolveFallback(
            string fallbackTag,
            string shaderName,
            bool hasRamp,
            bool alphaBlendEnabled,
            bool alphaTestEnabled)
        {
            if (!string.IsNullOrEmpty(fallbackTag))
            {
                FallbackShaderKind tagged = ResolveTag(fallbackTag);
                if (tagged != FallbackShaderKind.Standard
                    || string.Equals(fallbackTag, "Standard", StringComparison.Ordinal))
                {
                    return tagged;
                }

                // The documented behavior for an unrecognized explicit tag is Standard.
                return FallbackShaderKind.Standard;
            }

            string name = shaderName ?? string.Empty;
            bool toon = name.Contains("Toon") || hasRamp;
            bool transparent = name.Contains("Transparent") || alphaBlendEnabled;
            bool fade = name.Contains("Fade");
            bool cutout = name.Contains("Cutout") || alphaTestEnabled;

            if (name.Contains("Particle")) return FallbackShaderKind.Particle;
            if (name.Contains("Sprite")) return FallbackShaderKind.Sprite;
            if (name.Contains("MatCap")) return FallbackShaderKind.Matcap;
            if (toon && cutout) return FallbackShaderKind.ToonCutout;
            if (toon && name.Contains("Outline")) return FallbackShaderKind.ToonOutline;
            if (toon && (transparent || fade)) return FallbackShaderKind.Transparent;
            if (toon) return FallbackShaderKind.Toon;
            if (cutout) return FallbackShaderKind.Cutout;
            if (transparent || fade) return FallbackShaderKind.Transparent;
            if (name.Contains("Unlit")) return FallbackShaderKind.Unlit;
            if (name.Contains("VertexLit")) return FallbackShaderKind.VertexLit;
            return FallbackShaderKind.Standard;
        }

        public static bool IsQuestAvatarShader(string shaderName)
        {
            if (string.IsNullOrEmpty(shaderName))
            {
                return false;
            }

            foreach (string allowed in QuestAvatarShaders)
            {
                if (string.Equals(shaderName, allowed, StringComparison.Ordinal))
                {
                    return true;
                }
            }
            return false;
        }

        private static FallbackShaderKind ResolveTag(string tag)
        {
            if (string.Equals(tag, "toonstandard", StringComparison.OrdinalIgnoreCase))
            {
                return FallbackShaderKind.ToonStandard;
            }
            if (string.Equals(tag, "toonstandardoutline", StringComparison.OrdinalIgnoreCase))
            {
                return FallbackShaderKind.ToonStandardOutline;
            }
            if (string.Equals(tag, "Hidden", StringComparison.OrdinalIgnoreCase))
            {
                return FallbackShaderKind.Hidden;
            }
            if (tag.IndexOf("MobileToon", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return FallbackShaderKind.MobileToon;
            }

            bool toon = tag.IndexOf("Toon", StringComparison.OrdinalIgnoreCase) >= 0;
            bool unlit = tag.IndexOf("Unlit", StringComparison.OrdinalIgnoreCase) >= 0;
            bool cutout = tag.IndexOf("Cutout", StringComparison.OrdinalIgnoreCase) >= 0;
            bool transparent = tag.IndexOf("Transparent", StringComparison.OrdinalIgnoreCase) >= 0;
            bool fade = tag.IndexOf("Fade", StringComparison.OrdinalIgnoreCase) >= 0;

            if (toon && cutout) return FallbackShaderKind.ToonCutout;
            if (toon && tag.IndexOf("Outline", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return FallbackShaderKind.ToonOutline;
            }
            if (toon && (transparent || fade)) return FallbackShaderKind.Transparent;
            if (toon) return FallbackShaderKind.Toon;
            if (unlit && (transparent || fade)) return FallbackShaderKind.Transparent;
            if (unlit && cutout) return FallbackShaderKind.Cutout;
            if (unlit) return FallbackShaderKind.Unlit;
            if (cutout) return FallbackShaderKind.Cutout;
            if (transparent || fade) return FallbackShaderKind.Transparent;
            if (tag.IndexOf("VertexLit", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return FallbackShaderKind.VertexLit;
            }
            if (tag.IndexOf("Particle", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return FallbackShaderKind.Particle;
            }
            if (tag.IndexOf("Sprite", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return FallbackShaderKind.Sprite;
            }
            if (tag.IndexOf("Matcap", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return FallbackShaderKind.Matcap;
            }
            return FallbackShaderKind.Standard;
        }
    }
}
