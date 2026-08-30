using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SabaTools.AvatarMaterials.Editors
{
    internal static class AvatarMaterialEditorActions
    {
        internal static void SetMaterial(Renderer renderer, int slotIndex, Material material)
        {
            if (renderer == null)
            {
                return;
            }

            Material[] materials = renderer.sharedMaterials;
            if (slotIndex < 0 || slotIndex >= materials.Length || materials[slotIndex] == material)
            {
                return;
            }

            Undo.RecordObject(renderer, "Assign Avatar Material");
            materials[slotIndex] = material;
            renderer.sharedMaterials = materials;
            if (EditorUtility.IsPersistent(renderer))
            {
                EditorUtility.SetDirty(renderer);
            }
            else
            {
                PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
                if (renderer.gameObject.scene.IsValid())
                {
                    EditorSceneManager.MarkSceneDirty(renderer.gameObject.scene);
                }
            }
        }

        internal static void SetTexture(Material material, string propertyName, Texture texture)
        {
            if (material == null || string.IsNullOrEmpty(propertyName)
                || !material.HasProperty(propertyName)
                || material.GetTexture(propertyName) == texture)
            {
                return;
            }

            Undo.RecordObject(material, "Assign Material Texture");
            material.SetTexture(propertyName, texture);
            EditorUtility.SetDirty(material);
        }

        internal static void SetTextureScaleOffset(
            Material material, string propertyName, Vector2 scale, Vector2 offset)
        {
            if (material == null || string.IsNullOrEmpty(propertyName)
                || !material.HasProperty(propertyName))
            {
                return;
            }

            if (material.GetTextureScale(propertyName) == scale
                && material.GetTextureOffset(propertyName) == offset)
            {
                return;
            }

            Undo.RecordObject(material, "Edit Material Texture Transform");
            material.SetTextureScale(propertyName, scale);
            material.SetTextureOffset(propertyName, offset);
            EditorUtility.SetDirty(material);
        }

        internal static void SetRenderQueue(Material material, int renderQueue)
        {
            if (material == null || material.renderQueue == renderQueue)
            {
                return;
            }

            Undo.RecordObject(material, "Edit Material Render Queue");
            material.renderQueue = renderQueue;
            EditorUtility.SetDirty(material);
        }
    }
}
