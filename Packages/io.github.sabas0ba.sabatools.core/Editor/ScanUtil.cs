// Small helpers shared by the collectors.
using UnityEngine;

namespace SabaTools.Inspect.Editors
{
    internal static class ScanUtil
    {
        /// <summary>
        /// Hierarchy path of <paramref name="transform"/> up to the scene
        /// root, root name included, segments joined with '/'.
        /// </summary>
        internal static string HierarchyPath(Transform transform)
        {
            if (transform == null)
            {
                return string.Empty;
            }
            string path = transform.name;
            Transform current = transform.parent;
            while (current != null)
            {
                path = current.name + "/" + path;
                current = current.parent;
            }
            return path;
        }
    }
}
