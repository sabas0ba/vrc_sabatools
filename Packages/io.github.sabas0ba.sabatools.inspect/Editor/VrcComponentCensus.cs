// Counts VRChat SDK components by type name.
//
// The SDK is deliberately not referenced: this package declares no VPM
// dependencies, so it has to compile in projects with no VRChat SDK at all,
// and in both avatar and world projects. An asmdef reference would make the
// SDK mandatory, and an optional assembly gated by defineConstraints still has
// to resolve those references at import time. Matching on the component's type
// name behind a namespace guard costs exactness but keeps the package
// universal; with the SDK absent every count is simply zero.
using UnityEngine;

namespace SabaTools.Inspect.Editors
{
    internal static class VrcComponentCensus
    {
        internal static void Collect(Component[] components, StatsSnapshot stats)
        {
            foreach (Component component in components)
            {
                if (component == null)
                {
                    continue; // missing script; MissingReferenceScanner reports these
                }

                System.Type type = component.GetType();
                string ns = type.Namespace ?? string.Empty;
                if (!ns.StartsWith("VRC"))
                {
                    continue;
                }

                switch (type.Name)
                {
                    case "VRCPhysBone":
                        stats.PhysBoneCount++;
                        break;
                    case "VRCPhysBoneCollider":
                        stats.PhysBoneColliderCount++;
                        break;
                    case "VRCContactSender":
                    case "VRCContactReceiver":
                        stats.ContactCount++;
                        break;
                    case "VRCAvatarDescriptor":
                        stats.HasAvatarDescriptor = true;
                        break;
                    case "VRCSceneDescriptor":
                        stats.HasSceneDescriptor = true;
                        break;
                    default:
                        // VRCParentConstraint, VRCPositionConstraint, ...
                        if (type.Name.EndsWith("Constraint"))
                        {
                            stats.ConstraintCount++;
                        }
                        break;
                }
            }
        }
    }
}
