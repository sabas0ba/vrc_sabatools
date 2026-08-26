// Avatar checks that need the real Avatars SDK types.
//
// This assembly is the reason the package split exists: it references
// com.vrchat.avatars, so it can only be installed in an avatar project. The
// core package stays free of that dependency and works everywhere.
//
// On enum comparisons: fields are read through their real types, but enum
// VALUES are compared by name (ToString()). The field names below are stable
// SDK API; the enum member sets have grown over releases, and matching by name
// means a new member is unrecognised rather than a compile error. Where an
// exact value matters the check says so.
using System.Collections.Generic;
using SabaTools.Inspect;
using SabaTools.Inspect.Editors;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace SabaTools.Inspect.Avatar.Editors
{
    public sealed class AvatarInspectionModule : InspectionModule
    {
        private const string Category = "Avatar";

        public override string DisplayName => "SabaTools Inspect for Avatars";

        public override InspectMode Mode => InspectMode.Avatar;

        // Ahead of a world module: an avatar can sit inside a world scene
        // while being the thing the author is actually working on.
        public override int Order => -10;

        public override bool Detect(GameObject[] roots)
        {
            return FindDescriptors(roots).Count > 0;
        }

        public override void Inspect(InspectionContext context)
        {
            List<VRCAvatarDescriptor> descriptors = FindDescriptors(context.Roots);

            if (descriptors.Count == 0)
            {
                context.Add(InspectionSeverity.Warning, Category,
                    "No VRCAvatarDescriptor found. An avatar cannot be uploaded without one.");
                return;
            }

            if (descriptors.Count > 1)
            {
                context.Add(InspectionSeverity.Error, Category,
                    descriptors.Count + " VRCAvatarDescriptor components were found. " +
                    "The SDK uploads one avatar per descriptor; nested descriptors are " +
                    "almost always a mistake.", descriptors[1]);
            }

            foreach (VRCAvatarDescriptor descriptor in descriptors)
            {
                CheckViewPosition(context, descriptor);
                CheckExpressionParameters(context, descriptor);
                CheckExpressionsMenu(context, descriptor);
                CheckPlayableLayers(context, descriptor);
                CheckLipSync(context, descriptor);
                CheckEyeLook(context, descriptor);
            }

            ReportPhysBones(context);
        }

        private static List<VRCAvatarDescriptor> FindDescriptors(GameObject[] roots)
        {
            var found = new List<VRCAvatarDescriptor>();
            foreach (GameObject root in roots)
            {
                if (root == null)
                {
                    continue;
                }
                found.AddRange(root.GetComponentsInChildren<VRCAvatarDescriptor>(true));
            }
            return found;
        }

        // -------------------------------------------------------------------

        private static void CheckViewPosition(InspectionContext context, VRCAvatarDescriptor descriptor)
        {
            Vector3 view = descriptor.ViewPosition;
            context.AddRow("Avatar", "View Position", view.ToString("F3"));

            // The default descriptor value is the origin, and an avatar left
            // that way puts the camera in the floor. This is the single most
            // common thing to forget.
            if (view == Vector3.zero)
            {
                context.Add(InspectionSeverity.Error, Category,
                    "ViewPosition is (0, 0, 0), which puts the viewpoint at the avatar's " +
                    "origin. Set it to the eyes.", descriptor);
                return;
            }

            if (view.y <= 0f)
            {
                context.Add(InspectionSeverity.Error, Category,
                    "ViewPosition.y is " + view.y.ToString("F3") + ", at or below the " +
                    "avatar's feet.", descriptor);
            }
            else if (view.y > 3f)
            {
                context.Add(InspectionSeverity.Warning, Category,
                    "ViewPosition.y is " + view.y.ToString("F3") + " m. That is far above " +
                    "human scale; check the avatar's import scale.", descriptor);
            }

            if (Mathf.Abs(view.x) > 0.15f)
            {
                context.Add(InspectionSeverity.Warning, Category,
                    "ViewPosition.x is " + view.x.ToString("F3") + ", noticeably off the " +
                    "centre line. The view will sit to one side of the head.", descriptor);
            }
        }

        // -------------------------------------------------------------------

        private static void CheckExpressionParameters(
            InspectionContext context, VRCAvatarDescriptor descriptor)
        {
            VRCExpressionParameters parameters = descriptor.expressionParameters;
            if (parameters == null)
            {
                if (descriptor.expressionsMenu != null)
                {
                    context.Add(InspectionSeverity.Error, Category,
                        "An Expressions Menu is assigned but Expression Parameters is empty. " +
                        "The menu's controls have nothing to drive.", descriptor);
                }
                return;
            }

            // Computed here rather than through the SDK's own cost method, so
            // the arithmetic lives in AvatarLimits and stays testable without
            // Unity. The per-type costs are the published ones.
            int bits = 0;
            var seen = new HashSet<string>();
            foreach (VRCExpressionParameters.Parameter parameter in parameters.parameters)
            {
                if (parameter == null || string.IsNullOrEmpty(parameter.name))
                {
                    continue;
                }
                bits += AvatarLimits.BitsFor(parameter.valueType.ToString());
                if (!seen.Add(parameter.name))
                {
                    context.Add(InspectionSeverity.Error, Category,
                        "Expression parameter '" + parameter.name + "' is declared more than " +
                        "once. The duplicate still costs bits and the animator binds only one.",
                        descriptor);
                }
            }

            context.AddRow("Avatar", "Expression Parameter Bits",
                bits + " / " + AvatarLimits.ExpressionParameterBits +
                " (" + Mathf.RoundToInt(AvatarLimits.BudgetFraction(bits) * 100f) + "%)");

            if (bits > AvatarLimits.ExpressionParameterBits)
            {
                context.Add(InspectionSeverity.Error, Category,
                    "Expression parameters use " + bits + " bits, over the " +
                    AvatarLimits.ExpressionParameterBits + " bit limit. The avatar will be " +
                    "rejected on upload.", descriptor);
            }
            else if (AvatarLimits.IsNearlyFull(bits))
            {
                context.Add(InspectionSeverity.Warning, Category,
                    "Expression parameters use " + bits + " of " +
                    AvatarLimits.ExpressionParameterBits + " bits. There is no room left for " +
                    "another Int or Float.", descriptor);
            }
        }

        // -------------------------------------------------------------------

        private static void CheckExpressionsMenu(
            InspectionContext context, VRCAvatarDescriptor descriptor)
        {
            VRCExpressionsMenu menu = descriptor.expressionsMenu;
            if (menu == null)
            {
                return;
            }

            var visited = new HashSet<VRCExpressionsMenu>();
            int menuCount = 0;
            WalkMenu(context, descriptor, menu, visited, ref menuCount, depth: 0);
            context.AddRow("Avatar", "Expression Menus", menuCount.ToString());
        }

        /// <summary>
        /// Walks the menu tree. <paramref name="visited"/> is what stops a
        /// submenu that points back at an ancestor from recursing forever --
        /// the SDK allows that assignment, and it is easy to make by dragging
        /// the wrong asset.
        /// </summary>
        private static void WalkMenu(
            InspectionContext context, VRCAvatarDescriptor descriptor, VRCExpressionsMenu menu,
            HashSet<VRCExpressionsMenu> visited, ref int menuCount, int depth)
        {
            if (menu == null || !visited.Add(menu))
            {
                if (menu != null)
                {
                    context.Add(InspectionSeverity.Error, Category,
                        "The Expressions Menu '" + menu.name + "' is reachable from itself. " +
                        "A menu cycle locks the in-game menu up.", descriptor);
                }
                return;
            }

            menuCount++;

            if (menu.controls != null && menu.controls.Count > AvatarLimits.ControlsPerMenu)
            {
                context.Add(InspectionSeverity.Error, Category,
                    "The Expressions Menu '" + menu.name + "' has " + menu.controls.Count +
                    " controls; the limit is " + AvatarLimits.ControlsPerMenu + ".", descriptor);
            }

            if (depth >= 8)
            {
                context.Add(InspectionSeverity.Warning, Category,
                    "The Expressions Menu nests at least 8 levels deep at '" + menu.name +
                    "'. Reaching the bottom in game takes eight selections.", descriptor);
                return;
            }

            if (menu.controls == null)
            {
                return;
            }

            foreach (VRCExpressionsMenu.Control control in menu.controls)
            {
                if (control == null)
                {
                    continue;
                }
                if (control.type.ToString() == "SubMenu")
                {
                    if (control.subMenu == null)
                    {
                        context.Add(InspectionSeverity.Warning, Category,
                            "The control '" + control.name + "' in menu '" + menu.name +
                            "' is a SubMenu with nothing assigned.", descriptor);
                        continue;
                    }
                    WalkMenu(context, descriptor, control.subMenu, visited, ref menuCount, depth + 1);
                }
            }
        }

        // -------------------------------------------------------------------

        private static void CheckPlayableLayers(
            InspectionContext context, VRCAvatarDescriptor descriptor)
        {
            if (!descriptor.customizeAnimationLayers)
            {
                context.AddRow("Avatar", "Custom Playable Layers", "0 (using defaults)");
                return;
            }

            int custom = 0;
            CountLayers(context, descriptor, descriptor.baseAnimationLayers, ref custom);
            CountLayers(context, descriptor, descriptor.specialAnimationLayers, ref custom);
            context.AddRow("Avatar", "Custom Playable Layers", custom.ToString());
        }

        private static void CountLayers(
            InspectionContext context, VRCAvatarDescriptor descriptor,
            VRCAvatarDescriptor.CustomAnimLayer[] layers, ref int custom)
        {
            if (layers == null)
            {
                return;
            }
            foreach (VRCAvatarDescriptor.CustomAnimLayer layer in layers)
            {
                if (layer.isDefault)
                {
                    continue;
                }
                if (layer.animatorController == null)
                {
                    // "Not default, no controller" is the state left behind by
                    // unticking Default and then never assigning anything; the
                    // layer silently does nothing in game.
                    context.Add(InspectionSeverity.Warning, Category,
                        "The " + layer.type + " playable layer is marked custom but has no " +
                        "animator controller assigned.", descriptor);
                    continue;
                }
                custom++;
            }
        }

        // -------------------------------------------------------------------

        private static void CheckLipSync(InspectionContext context, VRCAvatarDescriptor descriptor)
        {
            string mode = descriptor.lipSync.ToString();
            context.AddRow("Avatar", "Lip Sync", mode);

            if (mode == "Default" || mode == "VisemeBlendShape")
            {
                if (descriptor.VisemeSkinnedMesh == null)
                {
                    context.Add(InspectionSeverity.Warning, Category,
                        "Lip sync is set to " + mode + " but no face mesh is assigned, so the " +
                        "avatar will not move its mouth.", descriptor);
                }
                else if (descriptor.VisemeBlendShapes == null
                         || descriptor.VisemeBlendShapes.Length == 0)
                {
                    context.Add(InspectionSeverity.Warning, Category,
                        "Lip sync is set to " + mode + " but no visemes are mapped.", descriptor);
                }
            }
        }

        private static void CheckEyeLook(InspectionContext context, VRCAvatarDescriptor descriptor)
        {
            context.AddRow("Avatar", "Eye Look", descriptor.enableEyeLook ? "Enabled" : "Disabled");

            if (!descriptor.enableEyeLook)
            {
                return;
            }

            // Only the eye bones are read. The rest of CustomEyeLookSettings
            // (eyelid type, blend shape indices, rotation states) has grown
            // over SDK releases, and a check that reached into it would be the
            // first thing to break on an SDK bump for little gain.
            VRCAvatarDescriptor.CustomEyeLookSettings eyes = descriptor.customEyeLookSettings;
            if (eyes.leftEye == null && eyes.rightEye == null)
            {
                context.Add(InspectionSeverity.Warning, Category,
                    "Eye Look is enabled but neither eye bone is assigned, so the eyes will " +
                    "not track.", descriptor);
            }
        }

        // -------------------------------------------------------------------

        private static void ReportPhysBones(InspectionContext context)
        {
            // The counts themselves come from the core census, which matches on
            // type name and so works with or without this package. What is
            // added here is the transform reach, which needs the real
            // component: it is what actually costs at runtime, and two avatars
            // with the same component count can differ by an order of
            // magnitude.
            StatsSnapshot stats = context.Stats;
            if (stats.PhysBoneCount == 0)
            {
                return;
            }

            int affected = 0;
            foreach (GameObject root in context.Roots)
            {
                if (root == null)
                {
                    continue;
                }
                foreach (VRC.SDK3.Dynamics.PhysBone.Components.VRCPhysBone bone in
                         root.GetComponentsInChildren<VRC.SDK3.Dynamics.PhysBone.Components.VRCPhysBone>(true))
                {
                    affected += AffectedTransforms(bone);
                }
            }

            context.AddRow("Avatar", "PhysBone Affected Transforms (estimate)", affected.ToString());
        }

        /// <summary>
        /// Transforms below a PhysBone's root, minus the ignored subtrees.
        /// An estimate: the SDK's own count also folds in endpoint handling and
        /// multi-child branching rules, which are not reproduced here.
        /// </summary>
        private static int AffectedTransforms(
            VRC.SDK3.Dynamics.PhysBone.Components.VRCPhysBone bone)
        {
            Transform root = bone.rootTransform != null ? bone.rootTransform : bone.transform;
            if (root == null)
            {
                return 0;
            }

            int count = 0;
            foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
            {
                if (transform == root)
                {
                    continue;
                }
                if (IsIgnored(bone, transform))
                {
                    continue;
                }
                count++;
            }
            return count;
        }

        private static bool IsIgnored(
            VRC.SDK3.Dynamics.PhysBone.Components.VRCPhysBone bone, Transform transform)
        {
            if (bone.ignoreTransforms == null)
            {
                return false;
            }
            foreach (Transform ignored in bone.ignoreTransforms)
            {
                if (ignored == null)
                {
                    continue;
                }
                if (transform == ignored || transform.IsChildOf(ignored))
                {
                    return true;
                }
            }
            return false;
        }
    }
}
