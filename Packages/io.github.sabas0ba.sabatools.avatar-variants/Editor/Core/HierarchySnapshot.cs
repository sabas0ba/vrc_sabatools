using System.Collections.Generic;

namespace SabaTools.AvatarVariants
{
    /// <summary>
    /// One GameObject of a captured hierarchy, with its components and children
    /// in their original order.
    ///
    /// A snapshot holds no Unity objects. The collector that builds it writes
    /// every value as a string, and encodes an object reference so that the same
    /// target yields the same string in two hierarchies: a reference to an
    /// object inside the captured root is written as that object's path from
    /// the root, and a reference to an asset as its GUID and file id. Two
    /// duplicates of one avatar therefore compare equal wherever nothing was
    /// edited, although each refers to its own bones.
    /// </summary>
    public sealed class SnapshotNode
    {
        public SnapshotNode(string name)
        {
            Name = name ?? string.Empty;
        }

        public string Name { get; }

        public List<SnapshotComponent> Components { get; } = new List<SnapshotComponent>();

        public List<SnapshotNode> Children { get; } = new List<SnapshotNode>();
    }

    /// <summary>
    /// One component of a node. The GameObject's own settings (active state,
    /// layer, tag) are captured as a component too, so that they are compared
    /// the same way.
    /// </summary>
    public sealed class SnapshotComponent
    {
        public SnapshotComponent(string typeName)
        {
            TypeName = typeName ?? string.Empty;
        }

        public string TypeName { get; }

        public List<SnapshotProperty> Properties { get; } = new List<SnapshotProperty>();
    }

    public readonly struct SnapshotProperty
    {
        public SnapshotProperty(string path, string value)
        {
            Path = path ?? string.Empty;
            Value = value ?? string.Empty;
        }

        /// <summary>The serialized property path, unique within its component.</summary>
        public string Path { get; }

        public string Value { get; }
    }
}
