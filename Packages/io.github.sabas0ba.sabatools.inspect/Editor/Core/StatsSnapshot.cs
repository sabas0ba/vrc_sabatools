// The numbers one inspection run collects, decoupled from how they were
// collected. The Unity-dependent collectors fill this in; the pure checklist
// rules read it. Plain fields, no behaviour — which is what lets the offline
// tests construct one directly.
namespace SabaTools.Inspect
{
    public sealed class StatsSnapshot
    {
        // Geometry
        public long Triangles;
        public int MeshRendererCount;
        public int SkinnedMeshRendererCount;
        public int BoneCount;

        // Materials
        public int MaterialSlotCount;
        public int UniqueMaterialCount;
        public int UniqueShaderCount;
        public int EmptyMaterialSlotCount;

        // Textures
        public int TextureCount;
        public long TextureMemoryBytes;
        public int UnknownTextureFormatCount;

        // VRChat dynamics, counted by component type name so the package works
        // with no SDK installed. Zero when the SDK is absent.
        public int PhysBoneCount;
        public int PhysBoneColliderCount;
        public int ContactCount;
        public int ConstraintCount;

        // Scene components
        public int ParticleSystemCount;
        public int TrailRendererCount;
        public int LineRendererCount;
        public int ClothCount;
        public int LightCount;
        public int RealtimeLightCount;
        public int ReflectionProbeCount;
        public int AudioSourceCount;
        public int AnimatorCount;
        public int CameraCount;

        // Broken references
        public int MissingScriptCount;
        public int MissingMeshCount;
        public int MissingBoneCount;
        public int MissingReferenceCount;
        public int MissingPrefabCount;

        // Presence flags used by mode auto-detection and the checklist
        public bool HasAvatarDescriptor;
        public bool HasSceneDescriptor;
        public int GameObjectCount;
    }
}
