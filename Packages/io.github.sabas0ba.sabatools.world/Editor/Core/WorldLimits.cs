// World heuristics that are just numbers, kept away from the SDK types so they
// can be executed and tested without Unity. See the core package's Editor/Core
// for why the split exists.
namespace SabaTools.Inspect.World
{
    /// <summary>
    /// Thresholds for the world checks. Unlike the avatar performance ranks,
    /// VRChat publishes no hard limits for most of these, so these are this
    /// package's own judgement rather than quoted values — the findings that
    /// use them say so.
    /// </summary>
    public static class WorldLimits
    {
        /// <summary>
        /// How far below the lowest geometry the respawn plane should sit. A
        /// respawn height above the floor teleports players standing on it.
        /// </summary>
        public const float RespawnClearance = 1f;

        /// <summary>Mirrors past this count are worth pointing out.</summary>
        public const int MirrorAdvisoryCount = 2;

        /// <summary>
        /// True when a respawn plane would catch players who are standing on
        /// the world's own floor.
        /// </summary>
        public static bool RespawnIsAboveGeometry(float respawnHeightY, float lowestGeometryY)
        {
            return respawnHeightY > lowestGeometryY;
        }

        /// <summary>
        /// The respawn height this world should have for its lowest geometry.
        /// Offered in the finding so the number does not have to be guessed.
        /// </summary>
        public static float SuggestedRespawnHeight(float lowestGeometryY)
        {
            return lowestGeometryY - RespawnClearance;
        }
    }
}
