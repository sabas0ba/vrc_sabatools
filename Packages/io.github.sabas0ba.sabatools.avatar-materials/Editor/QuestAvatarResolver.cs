using UnityEditor;
using UnityEngine;
using VRC.SDK3A.Editor;

namespace SabaTools.AvatarMaterials.Editors
{
    internal static class QuestAvatarResolver
    {
        internal static GameObject Resolve(GameObject avatarRoot, out bool usesOverride)
        {
            usesOverride = false;
            if (avatarRoot == null)
            {
                return null;
            }

            var options = PerPlatformOverrides.GetPlatformOverrides(avatarRoot);
            if (options == null)
            {
                return avatarRoot;
            }

            foreach (PerPlatformOverrides.Option option in options)
            {
                if (option.platform == BuildTarget.Android && option.avatar != null)
                {
                    usesOverride = true;
                    return option.avatar.gameObject;
                }
            }
            return avatarRoot;
        }
    }
}
