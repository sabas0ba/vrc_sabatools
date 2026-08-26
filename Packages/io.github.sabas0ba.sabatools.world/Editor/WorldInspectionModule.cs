// World checks that need the real Worlds SDK types.
//
// This assembly references com.vrchat.worlds, so it can only be installed in a
// world project. The core package stays free of that dependency and works
// everywhere. See the avatar package for the mirror image of this arrangement.
using System.Collections.Generic;
using SabaTools.Inspect;
using SabaTools.Inspect.Editors;
using UnityEngine;
using VRC.SDK3.Components;

namespace SabaTools.Inspect.World.Editors
{
    public sealed class WorldInspectionModule : InspectionModule
    {
        private const string Category = "World";

        public override string DisplayName => "SabaTools Inspect for Worlds";

        public override InspectMode Mode => InspectMode.World;

        public override bool Detect(GameObject[] roots)
        {
            return FindDescriptors(roots).Count > 0;
        }

        public override void Inspect(InspectionContext context)
        {
            List<VRCSceneDescriptor> descriptors = FindDescriptors(context.Roots);

            if (descriptors.Count == 0)
            {
                context.Add(InspectionSeverity.Warning, Category,
                    "No VRCSceneDescriptor found. A world scene needs exactly one.");
                return;
            }

            if (descriptors.Count > 1)
            {
                context.Add(InspectionSeverity.Error, Category,
                    descriptors.Count + " VRCSceneDescriptor components were found. " +
                    "The SDK expects one per scene and the extras are ignored silently.",
                    descriptors[1]);
            }

            foreach (VRCSceneDescriptor descriptor in descriptors)
            {
                CheckSpawns(context, descriptor);
                CheckRespawnHeight(context, descriptor);
                CheckReferenceCamera(context, descriptor);
            }

            ReportSdkComponents(context);
        }

        private static List<VRCSceneDescriptor> FindDescriptors(GameObject[] roots)
        {
            var found = new List<VRCSceneDescriptor>();
            foreach (GameObject root in roots)
            {
                if (root == null)
                {
                    continue;
                }
                found.AddRange(root.GetComponentsInChildren<VRCSceneDescriptor>(true));
            }
            return found;
        }

        // -------------------------------------------------------------------

        private static void CheckSpawns(InspectionContext context, VRCSceneDescriptor descriptor)
        {
            Transform[] spawns = descriptor.spawns;
            int usable = 0;
            int empty = 0;

            if (spawns != null)
            {
                foreach (Transform spawn in spawns)
                {
                    if (spawn == null)
                    {
                        empty++;
                    }
                    else
                    {
                        usable++;
                    }
                }
            }

            context.AddRow("World", "Spawn Points", usable.ToString());

            if (usable == 0)
            {
                context.Add(InspectionSeverity.Error, Category,
                    "The scene descriptor has no usable spawn point. Players enter at the " +
                    "world origin, which is rarely where the world is.", descriptor);
                return;
            }

            if (empty > 0)
            {
                // An empty slot is not merely untidy: with the Random spawn
                // order, landing on one drops the player at the origin.
                context.Add(InspectionSeverity.Error, Category,
                    empty + " spawn slot(s) are empty. Depending on the spawn order, a " +
                    "player can be sent to the world origin instead of a spawn point.",
                    descriptor);
            }

            foreach (Transform spawn in spawns)
            {
                if (spawn == null)
                {
                    continue;
                }
                // A spawn rotated off the horizontal tilts the player's whole
                // frame on entry, which reads as a broken world rather than as
                // a stylistic choice.
                Vector3 euler = spawn.eulerAngles;
                float pitch = Mathf.DeltaAngle(0f, euler.x);
                float roll = Mathf.DeltaAngle(0f, euler.z);
                if (Mathf.Abs(pitch) > 1f || Mathf.Abs(roll) > 1f)
                {
                    context.Add(InspectionSeverity.Warning, Category,
                        "The spawn point '" + spawn.name + "' is tilted (pitch " +
                        pitch.ToString("F1") + "°, roll " + roll.ToString("F1") +
                        "°). Players spawn upright; only the yaw is used.", spawn);
                }
            }
        }

        // -------------------------------------------------------------------

        private static void CheckRespawnHeight(
            InspectionContext context, VRCSceneDescriptor descriptor)
        {
            float respawn = descriptor.RespawnHeightY;
            context.AddRow("World", "Respawn Height Y", respawn.ToString("F1"));

            if (!TryLowestGeometry(context.Roots, out float lowest))
            {
                return;
            }

            context.AddRow("World", "Lowest Geometry Y", lowest.ToString("F1"));

            if (WorldLimits.RespawnIsAboveGeometry(respawn, lowest))
            {
                context.Add(InspectionSeverity.Error, Category,
                    "RespawnHeightY (" + respawn.ToString("F1") + ") is above the lowest " +
                    "geometry in the scene (" + lowest.ToString("F1") + "). Players standing " +
                    "there are teleported to a spawn point. Try " +
                    WorldLimits.SuggestedRespawnHeight(lowest).ToString("F1") + ".", descriptor);
            }
        }

        /// <summary>
        /// The bottom of the renderer bounds across the scene. Renderers rather
        /// than colliders: a world's visible floor and its collision usually
        /// agree, and colliders alone miss worlds built on terrain.
        /// </summary>
        private static bool TryLowestGeometry(GameObject[] roots, out float lowest)
        {
            lowest = float.MaxValue;
            bool found = false;

            foreach (GameObject root in roots)
            {
                if (root == null)
                {
                    continue;
                }
                foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
                {
                    if (!renderer.enabled)
                    {
                        continue;
                    }
                    lowest = Mathf.Min(lowest, renderer.bounds.min.y);
                    found = true;
                }
            }

            if (!found)
            {
                lowest = 0f;
            }
            return found;
        }

        // -------------------------------------------------------------------

        private static void CheckReferenceCamera(
            InspectionContext context, VRCSceneDescriptor descriptor)
        {
            if (descriptor.ReferenceCamera == null)
            {
                // Not an error: without one the world simply uses VRChat's
                // defaults. It is worth saying because post-processing and the
                // clipping planes are configured through it, and their absence
                // is usually an oversight rather than a decision.
                context.Add(InspectionSeverity.Info, Category,
                    "No Reference Camera is assigned. The world will use VRChat's default " +
                    "clipping planes and no post-processing.", descriptor);
                return;
            }

            context.AddRow("World", "Reference Camera", descriptor.ReferenceCamera.name);

            var camera = descriptor.ReferenceCamera.GetComponent<Camera>();
            if (camera == null)
            {
                context.Add(InspectionSeverity.Error, Category,
                    "The Reference Camera object '" + descriptor.ReferenceCamera.name +
                    "' has no Camera component.", descriptor.ReferenceCamera);
                return;
            }

            if (camera.nearClipPlane > 0.05f)
            {
                context.Add(InspectionSeverity.Warning, Category,
                    "The Reference Camera's near clip plane is " +
                    camera.nearClipPlane.ToString("F3") + ". Anything above about 0.05 clips " +
                    "geometry a player can put their head into.", descriptor.ReferenceCamera);
            }
        }

        // -------------------------------------------------------------------

        private static void ReportSdkComponents(InspectionContext context)
        {
            int udon = 0, pickups = 0, stations = 0, mirrors = 0, activeMirrors = 0;

            foreach (GameObject root in context.Roots)
            {
                if (root == null)
                {
                    continue;
                }

                udon += root.GetComponentsInChildren<VRC.Udon.UdonBehaviour>(true).Length;
                pickups += root.GetComponentsInChildren<VRC.SDK3.Components.VRCPickup>(true).Length;
                stations += root.GetComponentsInChildren<VRC.SDK3.Components.VRCStation>(true).Length;

                foreach (VRC.SDK3.Components.VRCMirrorReflection mirror in
                         root.GetComponentsInChildren<VRC.SDK3.Components.VRCMirrorReflection>(true))
                {
                    mirrors++;
                    if (mirror.enabled && mirror.gameObject.activeInHierarchy)
                    {
                        activeMirrors++;
                    }
                }
            }

            context.AddRow("World", "Udon Behaviours", udon.ToString());
            context.AddRow("World", "Pickups", pickups.ToString());
            context.AddRow("World", "Stations", stations.ToString());
            context.AddRow("World", "Mirrors (active / total)", activeMirrors + " / " + mirrors);

            if (activeMirrors > 0)
            {
                // A mirror renders the scene again per eye. One left enabled on
                // load is the most common reason a world runs at half the frame
                // rate its geometry suggests.
                context.Add(InspectionSeverity.Warning, Category,
                    activeMirrors + " mirror(s) are enabled when the scene loads. Each one " +
                    "renders the world again per eye; the usual practice is to ship them " +
                    "disabled and let players turn them on.");
            }

            if (mirrors > WorldLimits.MirrorAdvisoryCount)
            {
                context.Add(InspectionSeverity.Info, Category,
                    mirrors + " mirrors are present. This is this package's own advisory " +
                    "threshold, not a VRChat limit.");
            }
        }
    }
}
