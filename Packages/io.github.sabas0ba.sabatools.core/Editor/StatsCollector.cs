// Walks a hierarchy and fills the numeric half of a StatsSnapshot.
// Read-only: nothing here touches the scene.
using System.Collections.Generic;
using UnityEngine;

namespace SabaTools.Inspect.Editors
{
    internal static class StatsCollector
    {
        internal static void Collect(GameObject[] roots, StatsSnapshot stats)
        {
            var uniqueMaterials = new HashSet<Material>();
            var uniqueShaders = new HashSet<Shader>();
            var bones = new HashSet<Transform>();

            foreach (GameObject root in roots)
            {
                if (root == null)
                {
                    continue;
                }

                stats.GameObjectCount += root.GetComponentsInChildren<Transform>(true).Length;

                foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>(true))
                {
                    stats.Triangles += TriangleCount(filter.sharedMesh);
                }

                foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
                {
                    if (renderer is MeshRenderer)
                    {
                        stats.MeshRendererCount++;
                    }
                    else if (renderer is SkinnedMeshRenderer skinned)
                    {
                        stats.SkinnedMeshRendererCount++;
                        // Skinned meshes have no MeshFilter, so their triangles
                        // are counted here rather than in the loop above.
                        stats.Triangles += TriangleCount(skinned.sharedMesh);

                        Transform[] rendererBones = skinned.bones;
                        if (rendererBones != null)
                        {
                            foreach (Transform bone in rendererBones)
                            {
                                if (bone != null)
                                {
                                    bones.Add(bone);
                                }
                            }
                        }
                    }

                    CollectMaterials(renderer, stats, uniqueMaterials, uniqueShaders);
                }

                foreach (Component component in root.GetComponentsInChildren<Component>(true))
                {
                    if (component == null)
                    {
                        continue;
                    }
                    if (component is ParticleSystem) stats.ParticleSystemCount++;
                    else if (component is TrailRenderer) stats.TrailRendererCount++;
                    else if (component is LineRenderer) stats.LineRendererCount++;
                    else if (component is Cloth) stats.ClothCount++;
                    else if (component is ReflectionProbe) stats.ReflectionProbeCount++;
                    else if (component is AudioSource) stats.AudioSourceCount++;
                    else if (component is Animator) stats.AnimatorCount++;
                    else if (component is Camera) stats.CameraCount++;
                    else if (component is UnityEngine.Animations.IConstraint)
                    {
                        // Unity's own constraints. VRChat's are counted
                        // separately by VrcComponentCensus into the same field:
                        // the checklist cares about how many constraints run,
                        // not which SDK they came from.
                        stats.ConstraintCount++;
                    }
                    else if (component is Light light)
                    {
                        stats.LightCount++;
                        // bakingOutput is the runtime-facing record of what the
                        // bake produced, so this counts lights that still cost
                        // something at runtime. Light.lightmapBakeType — the
                        // authoring setting — is editor-only API and is not in
                        // the reference assemblies the offline build compiles
                        // against, so it cannot be used here.
                        if (!light.bakingOutput.isBaked)
                        {
                            stats.RealtimeLightCount++;
                        }
                    }
                }
            }

            stats.UniqueMaterialCount = uniqueMaterials.Count;
            stats.UniqueShaderCount = uniqueShaders.Count;
            stats.BoneCount = bones.Count;
        }

        private static long TriangleCount(Mesh mesh)
        {
            if (mesh == null)
            {
                return 0;
            }
            long indices = 0;
            for (int i = 0; i < mesh.subMeshCount; i++)
            {
                indices += (long)mesh.GetIndexCount(i);
            }
            return indices / 3;
        }

        private static void CollectMaterials(
            Renderer renderer, StatsSnapshot stats,
            HashSet<Material> uniqueMaterials, HashSet<Shader> uniqueShaders)
        {
            Material[] materials = renderer.sharedMaterials;
            if (materials == null)
            {
                return;
            }
            foreach (Material material in materials)
            {
                stats.MaterialSlotCount++;
                if (material == null)
                {
                    stats.EmptyMaterialSlotCount++;
                    continue;
                }
                uniqueMaterials.Add(material);
                if (material.shader != null)
                {
                    uniqueShaders.Add(material.shader);
                }
            }
        }
    }
}
