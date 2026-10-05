using System;
using System.Collections.Generic;

namespace SabaTools.AvatarVariants
{
    public enum HierarchyChangeKind
    {
        PropertyChanged,
        ComponentAdded,
        ComponentRemoved,
        GameObjectAdded,
        GameObjectRemoved,

        /// <summary>
        /// The children both hierarchies share appear in a different order.
        /// </summary>
        ChildrenReordered,
    }

    /// <summary>
    /// One difference between a base hierarchy and a derived one.
    /// </summary>
    public sealed class HierarchyChange
    {
        internal HierarchyChange(HierarchyChangeKind kind, string path, string parentPath)
        {
            Kind = kind;
            Path = path;
            ParentPath = parentPath;
        }

        public HierarchyChangeKind Kind { get; }

        /// <summary>
        /// Path of the GameObject from the root, with the root itself as the
        /// empty string. A sibling that shares its name with an earlier one
        /// carries its occurrence as a suffix: <c>Ribbon</c>, <c>Ribbon[1]</c>.
        /// </summary>
        public string Path { get; }

        /// <summary>Path of the parent, or null for the root.</summary>
        public string ParentPath { get; }

        /// <summary>Set for component and property changes.</summary>
        public string ComponentType { get; internal set; }

        /// <summary>
        /// Which component of <see cref="ComponentType"/> on the GameObject,
        /// counted from zero.
        /// </summary>
        public int ComponentIndex { get; internal set; }

        /// <summary>Set for property changes.</summary>
        public string PropertyPath { get; internal set; }

        /// <summary>Null when the property exists only in the derived hierarchy.</summary>
        public string BaseValue { get; internal set; }

        /// <summary>Null when the property exists only in the base hierarchy.</summary>
        public string DerivedValue { get; internal set; }

        /// <summary>Null for an added GameObject.</summary>
        public SnapshotNode BaseNode { get; internal set; }

        /// <summary>Null for a removed GameObject.</summary>
        public SnapshotNode DerivedNode { get; internal set; }

        /// <summary>
        /// Path of the nearest GameObject, this one included, that was paired
        /// with its counterpart only by its position among siblings of the same
        /// name. Null when every pairing on the way down was by a unique name.
        /// </summary>
        public string AmbiguousPath { get; internal set; }
    }

    public sealed class HierarchyDiff
    {
        internal HierarchyDiff(List<HierarchyChange> changes)
        {
            Changes = changes;
        }

        public IReadOnlyList<HierarchyChange> Changes { get; }

        public bool IsIdentical
        {
            get { return Changes.Count == 0; }
        }

        public int Count(HierarchyChangeKind kind)
        {
            int count = 0;
            foreach (HierarchyChange change in Changes)
            {
                if (change.Kind == kind)
                {
                    count++;
                }
            }
            return count;
        }
    }

    /// <summary>
    /// Compares two snapshots. GameObjects are paired by name under paired
    /// parents and components by type and order of appearance, which is how a
    /// duplicate lines up with its original as long as nothing was renamed.
    ///
    /// The name of the root and where the root is placed are not compared: two
    /// avatars standing side by side differ in both without having been edited,
    /// and a prefab instance keeps both as overrides regardless.
    /// </summary>
    public static class HierarchyDiffer
    {
        private static readonly string[] RootPlacementPrefixes =
        {
            "m_LocalPosition",
            "m_LocalRotation",
            "m_LocalEulerAnglesHint",
        };

        public static HierarchyDiff Compare(SnapshotNode baseRoot, SnapshotNode derivedRoot)
        {
            if (baseRoot == null)
            {
                throw new ArgumentNullException(nameof(baseRoot));
            }
            if (derivedRoot == null)
            {
                throw new ArgumentNullException(nameof(derivedRoot));
            }

            var changes = new List<HierarchyChange>();
            CompareNodes(baseRoot, derivedRoot, string.Empty, null, null, changes);
            return new HierarchyDiff(changes);
        }

        private static void CompareNodes(
            SnapshotNode baseNode,
            SnapshotNode derivedNode,
            string path,
            string parentPath,
            string ambiguousPath,
            List<HierarchyChange> changes)
        {
            CompareComponents(baseNode, derivedNode, path, parentPath, ambiguousPath, changes);

            List<string> baseKeys = ChildKeys(baseNode, out HashSet<string> baseRepeated);
            List<string> derivedKeys = ChildKeys(derivedNode, out HashSet<string> derivedRepeated);
            var derivedByKey = new Dictionary<string, SnapshotNode>();
            for (int index = 0; index < derivedKeys.Count; index++)
            {
                derivedByKey[derivedKeys[index]] = derivedNode.Children[index];
            }

            var sharedInBaseOrder = new List<string>();
            var baseKeySet = new HashSet<string>(baseKeys);
            for (int index = 0; index < baseKeys.Count; index++)
            {
                string key = baseKeys[index];
                SnapshotNode baseChild = baseNode.Children[index];
                string childPath = path.Length == 0 ? key : path + "/" + key;
                string childAmbiguous =
                    baseRepeated.Contains(baseChild.Name) || derivedRepeated.Contains(baseChild.Name)
                        ? childPath
                        : ambiguousPath;

                if (derivedByKey.TryGetValue(key, out SnapshotNode derivedChild))
                {
                    sharedInBaseOrder.Add(key);
                    CompareNodes(baseChild, derivedChild, childPath, path, childAmbiguous, changes);
                }
                else
                {
                    changes.Add(new HierarchyChange(HierarchyChangeKind.GameObjectRemoved, childPath, path)
                    {
                        BaseNode = baseChild,
                        AmbiguousPath = childAmbiguous,
                    });
                }
            }

            var sharedInDerivedOrder = new List<string>();
            for (int index = 0; index < derivedKeys.Count; index++)
            {
                string key = derivedKeys[index];
                if (baseKeySet.Contains(key))
                {
                    sharedInDerivedOrder.Add(key);
                    continue;
                }

                SnapshotNode derivedChild = derivedNode.Children[index];
                string childPath = path.Length == 0 ? key : path + "/" + key;
                changes.Add(new HierarchyChange(HierarchyChangeKind.GameObjectAdded, childPath, path)
                {
                    DerivedNode = derivedChild,
                    AmbiguousPath =
                        baseRepeated.Contains(derivedChild.Name) || derivedRepeated.Contains(derivedChild.Name)
                            ? childPath
                            : ambiguousPath,
                });
            }

            if (!SameSequence(sharedInBaseOrder, sharedInDerivedOrder))
            {
                changes.Add(new HierarchyChange(HierarchyChangeKind.ChildrenReordered, path, parentPath)
                {
                    BaseNode = baseNode,
                    DerivedNode = derivedNode,
                    AmbiguousPath = ambiguousPath,
                });
            }
        }

        private static void CompareComponents(
            SnapshotNode baseNode,
            SnapshotNode derivedNode,
            string path,
            string parentPath,
            string ambiguousPath,
            List<HierarchyChange> changes)
        {
            List<int> baseIndices = OccurrenceIndices(baseNode.Components);
            List<int> derivedIndices = OccurrenceIndices(derivedNode.Components);
            var matchedDerived = new bool[derivedNode.Components.Count];

            for (int index = 0; index < baseNode.Components.Count; index++)
            {
                SnapshotComponent baseComponent = baseNode.Components[index];
                int counterpart = -1;
                for (int candidate = 0; candidate < derivedNode.Components.Count; candidate++)
                {
                    if (derivedNode.Components[candidate].TypeName == baseComponent.TypeName
                        && derivedIndices[candidate] == baseIndices[index])
                    {
                        counterpart = candidate;
                        break;
                    }
                }

                if (counterpart < 0)
                {
                    changes.Add(new HierarchyChange(HierarchyChangeKind.ComponentRemoved, path, parentPath)
                    {
                        ComponentType = baseComponent.TypeName,
                        ComponentIndex = baseIndices[index],
                        BaseNode = baseNode,
                        DerivedNode = derivedNode,
                        AmbiguousPath = ambiguousPath,
                    });
                    continue;
                }

                matchedDerived[counterpart] = true;
                CompareProperties(
                    baseNode, derivedNode, baseComponent, derivedNode.Components[counterpart],
                    baseIndices[index], path, parentPath, ambiguousPath, changes);
            }

            for (int index = 0; index < derivedNode.Components.Count; index++)
            {
                if (matchedDerived[index])
                {
                    continue;
                }
                changes.Add(new HierarchyChange(HierarchyChangeKind.ComponentAdded, path, parentPath)
                {
                    ComponentType = derivedNode.Components[index].TypeName,
                    ComponentIndex = derivedIndices[index],
                    BaseNode = baseNode,
                    DerivedNode = derivedNode,
                    AmbiguousPath = ambiguousPath,
                });
            }
        }

        private static void CompareProperties(
            SnapshotNode baseNode,
            SnapshotNode derivedNode,
            SnapshotComponent baseComponent,
            SnapshotComponent derivedComponent,
            int componentIndex,
            string path,
            string parentPath,
            string ambiguousPath,
            List<HierarchyChange> changes)
        {
            bool skipPlacement = parentPath == null && IsTransform(baseComponent.TypeName);

            var derivedValues = new Dictionary<string, string>();
            foreach (SnapshotProperty property in derivedComponent.Properties)
            {
                derivedValues[property.Path] = property.Value;
            }

            var basePaths = new HashSet<string>();
            foreach (SnapshotProperty property in baseComponent.Properties)
            {
                basePaths.Add(property.Path);
                if (skipPlacement && IsRootPlacement(property.Path))
                {
                    continue;
                }
                bool present = derivedValues.TryGetValue(property.Path, out string derivedValue);
                if (present && derivedValue == property.Value)
                {
                    continue;
                }
                changes.Add(PropertyChange(
                    baseNode, derivedNode, baseComponent.TypeName, componentIndex, path, parentPath,
                    ambiguousPath, property.Path, property.Value, present ? derivedValue : null));
            }

            foreach (SnapshotProperty property in derivedComponent.Properties)
            {
                if (basePaths.Contains(property.Path)
                    || (skipPlacement && IsRootPlacement(property.Path)))
                {
                    continue;
                }
                changes.Add(PropertyChange(
                    baseNode, derivedNode, baseComponent.TypeName, componentIndex, path, parentPath,
                    ambiguousPath, property.Path, null, property.Value));
            }
        }

        private static HierarchyChange PropertyChange(
            SnapshotNode baseNode,
            SnapshotNode derivedNode,
            string componentType,
            int componentIndex,
            string path,
            string parentPath,
            string ambiguousPath,
            string propertyPath,
            string baseValue,
            string derivedValue)
        {
            return new HierarchyChange(HierarchyChangeKind.PropertyChanged, path, parentPath)
            {
                ComponentType = componentType,
                ComponentIndex = componentIndex,
                PropertyPath = propertyPath,
                BaseValue = baseValue,
                DerivedValue = derivedValue,
                BaseNode = baseNode,
                DerivedNode = derivedNode,
                AmbiguousPath = ambiguousPath,
            };
        }

        /// <summary>
        /// One key per child: its name, followed by its occurrence when an
        /// earlier sibling already has that name.
        /// </summary>
        private static List<string> ChildKeys(SnapshotNode node, out HashSet<string> repeatedNames)
        {
            var seen = new Dictionary<string, int>();
            var keys = new List<string>(node.Children.Count);
            repeatedNames = new HashSet<string>();
            foreach (SnapshotNode child in node.Children)
            {
                seen.TryGetValue(child.Name, out int occurrence);
                seen[child.Name] = occurrence + 1;
                if (occurrence > 0)
                {
                    repeatedNames.Add(child.Name);
                }
                keys.Add(occurrence == 0 ? child.Name : child.Name + "[" + occurrence + "]");
            }
            return keys;
        }

        /// <summary>
        /// For each component, how many earlier components share its type.
        /// </summary>
        private static List<int> OccurrenceIndices(List<SnapshotComponent> components)
        {
            var seen = new Dictionary<string, int>();
            var indices = new List<int>(components.Count);
            foreach (SnapshotComponent component in components)
            {
                seen.TryGetValue(component.TypeName, out int occurrence);
                seen[component.TypeName] = occurrence + 1;
                indices.Add(occurrence);
            }
            return indices;
        }

        private static bool IsTransform(string typeName)
        {
            return typeName == "UnityEngine.Transform" || typeName == "UnityEngine.RectTransform";
        }

        private static bool IsRootPlacement(string propertyPath)
        {
            foreach (string prefix in RootPlacementPrefixes)
            {
                if (propertyPath.StartsWith(prefix, StringComparison.Ordinal))
                {
                    return true;
                }
            }
            return false;
        }

        private static bool SameSequence(List<string> first, List<string> second)
        {
            if (first.Count != second.Count)
            {
                return false;
            }
            for (int index = 0; index < first.Count; index++)
            {
                if (first[index] != second[index])
                {
                    return false;
                }
            }
            return true;
        }
    }
}
