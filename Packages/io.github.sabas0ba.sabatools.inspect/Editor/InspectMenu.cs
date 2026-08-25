// Menu entry points.
using UnityEditor;
using UnityEngine;

namespace SabaTools.Inspect.Editors
{
    internal static class InspectMenu
    {
        [MenuItem("Tools/SabaTools/Inspect Window")]
        private static void OpenWindow()
        {
            InspectWindow.Open();
        }

        [MenuItem("Tools/SabaTools/Inspect Selection")]
        private static void InspectSelection()
        {
            GameObject selected = Selection.activeGameObject;
            if (selected == null)
            {
                return;
            }
            InspectWindow.Open().ScanTarget(selected);
        }

        [MenuItem("Tools/SabaTools/Inspect Selection", true)]
        private static bool ValidateInspectSelection()
        {
            return Selection.activeGameObject != null;
        }
    }
}
