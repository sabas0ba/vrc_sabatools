// Finds broken references: missing scripts, missing prefab assets, unassigned
// meshes, null bone slots and serialized fields whose target object is gone.
// Read-only; every finding carries the hierarchy path so the window can ping
// the object it talks about.
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace SabaTools.Inspect.Editors
{
    internal static class MissingReferenceScanner
    {
        internal static void Collect(
            GameObject[] roots, StatsSnapshot stats,
            List<InspectionItem> items, Dictionary<string, Object> locations)
        {
            foreach (GameObject root in roots)
            {
                if (root == null)
                {
                    continue;
                }
                foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
                {
                    ScanGameObject(transform.gameObject, stats, items, locations);
                }
            }
        }

        private static void ScanGameObject(
            GameObject gameObject, StatsSnapshot stats,
            List<InspectionItem> items, Dictionary<string, Object> locations)
        {
            string path = ScanUtil.HierarchyPath(gameObject.transform);

            if (PrefabUtility.GetPrefabInstanceStatus(gameObject) == PrefabInstanceStatus.MissingAsset)
            {
                stats.MissingPrefabCount++;
                Report(items, locations, gameObject, InspectionSeverity.Error,
                    "Prefab asset is missing for this instance.", path);
            }

            foreach (Component component in gameObject.GetComponents<Component>())
            {
                if (component == null)
                {
                    stats.MissingScriptCount++;
                    Report(items, locations, gameObject, InspectionSeverity.Error,
                        "Missing script.", path);
                    continue;
                }

                if (component is MeshFilter filter && filter.sharedMesh == null)
                {
                    stats.MissingMeshCount++;
                    Report(items, locations, gameObject, InspectionSeverity.Error,
                        "MeshFilter has no mesh assigned.", path);
                }

                if (component is SkinnedMeshRenderer skinned)
                {
                    if (skinned.sharedMesh == null)
                    {
                        stats.MissingMeshCount++;
                        Report(items, locations, gameObject, InspectionSeverity.Error,
                            "SkinnedMeshRenderer has no mesh assigned.", path);
                    }

                    Transform[] bones = skinned.bones;
                    if (bones != null)
                    {
                        int nullBones = 0;
                        foreach (Transform bone in bones)
                        {
                            if (bone == null)
                            {
                                nullBones++;
                            }
                        }
                        if (nullBones > 0)
                        {
                            stats.MissingBoneCount += nullBones;
                            Report(items, locations, gameObject, InspectionSeverity.Warning,
                                nullBones + " bone slot(s) are null on this SkinnedMeshRenderer.",
                                path);
                        }
                    }
                }

                ScanSerializedReferences(component, stats, items, locations, path);
            }
        }

        /// <summary>
        /// A serialized object reference whose instance id is set but whose
        /// object cannot be resolved is exactly what the Inspector draws as
        /// "Missing". That pair of conditions is the only reliable way to tell
        /// it apart from a field the author left empty on purpose.
        /// </summary>
        private static void ScanSerializedReferences(
            Component component, StatsSnapshot stats,
            List<InspectionItem> items, Dictionary<string, Object> locations, string path)
        {
            var serialized = new SerializedObject(component);
            SerializedProperty property = serialized.GetIterator();
            while (property.Next(true))
            {
                if (property.propertyType != SerializedPropertyType.ObjectReference)
                {
                    continue;
                }
                if (property.objectReferenceValue == null
                    && property.objectReferenceInstanceIDValue != 0)
                {
                    stats.MissingReferenceCount++;
                    Report(items, locations, component.gameObject, InspectionSeverity.Warning,
                        component.GetType().Name + "." + property.propertyPath +
                        " references a missing object.", path);
                }
            }
        }

        private static void Report(
            List<InspectionItem> items, Dictionary<string, Object> locations,
            Object location, InspectionSeverity severity, string message, string path)
        {
            items.Add(new InspectionItem("References", severity, message, path));
            if (!locations.ContainsKey(path))
            {
                locations.Add(path, location);
            }
        }
    }
}
