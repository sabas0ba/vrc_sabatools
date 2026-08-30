using System;
using System.IO;
using SabaTools.AvatarMaterials.Editors;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using VRC.SDK3.Avatars.Components;

public static class AvatarMaterialsDemoSetup
{
    private const string DemoFolder = "Assets/AvatarMaterialStudioDemo";
    private const string ScenePath = DemoFolder + "/AvatarMaterialStudioDemo.unity";

    [MenuItem("Tools/SabaTools/Open Material Studio Demo")]
    public static void Open()
    {
        GameObject avatar = BuildDemo();
        Selection.activeGameObject = avatar;
        if (SceneView.lastActiveSceneView != null)
        {
            SceneView.lastActiveSceneView.FrameSelected();
        }
        AvatarMaterialStudioWindow.Open();
    }

    public static void BuildForVerification()
    {
        BuildDemo();
    }

    private static GameObject BuildDemo()
    {
        EnsureFolder();
        return OpenOrCreateScene();
    }

    private static GameObject OpenOrCreateScene()
    {
        Scene scene;
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
        {
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name == "DemoAvatar")
                {
                    UnityEngine.Object.DestroyImmediate(root);
                }
            }
        }
        else
        {
            scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        var avatar = new GameObject("DemoAvatar");
        VRCAvatarDescriptor descriptor = avatar.AddComponent<VRCAvatarDescriptor>();
        descriptor.ViewPosition = new Vector3(0f, 1.66f, -0.06f);

        Func<int, int, Color> checkerPixels = (x, y) =>
        {
            bool light = (x / 4 + y / 4) % 2 == 0;
            return light ? new Color(0.82f, 0.93f, 1f) : new Color(0.06f, 0.2f, 0.45f);
        };
        Texture2D checker = CreatePngTexture("CheckerSourceA", checkerPixels);
        Texture2D duplicateChecker = CreatePngTexture("CheckerSourceB", checkerPixels);
        Texture2D emissionMask = CreateTexture("EmissionMask", (x, y) =>
            ((x + y) / 3) % 2 == 0 ? Color.white : new Color(0.02f, 0.02f, 0.02f));
        Texture2D matCap = CreateTexture("MatCap", (x, y) =>
        {
            Vector2 position = new Vector2(x / 15f, y / 15f) - Vector2.one * 0.5f;
            float highlight = Mathf.Clamp01(1f - position.magnitude * 1.7f);
            return Color.Lerp(new Color(0.04f, 0.08f, 0.16f), Color.white, highlight * highlight);
        });
        Texture2D shadow = CreateTexture("ShadowRamp", (x, y) =>
            x < 7 ? new Color(0.12f, 0.16f, 0.35f) : new Color(0.65f, 0.85f, 1f));
        Texture2D outlineMask = CreateTexture("OutlineWidthMask", (x, y) =>
            Color.Lerp(Color.black, Color.white, y / 15f));

        Material bodyMaterial = CreateMaterial("Body", "lilToon", material =>
        {
            material.SetOverrideTag("VRCFallback", "ToonStandard");
            SetColor(material, "_Color", new Color(0.3f, 0.65f, 0.95f));
            SetTexture(material, "_MainTex", duplicateChecker);
            SetFloat(material, "_UseShadow", 1f);
            SetFloat(material, "_ShadowStrength", 0.75f);
            SetColor(material, "_ShadowColor", new Color(0.35f, 0.45f, 0.8f));
            SetTexture(material, "_ShadowColorTex", shadow);
            SetFloat(material, "_UseEmission", 1f);
            SetColor(material, "_EmissionColor", new Color(0.05f, 0.8f, 1.4f));
            SetTexture(material, "_EmissionMap", emissionMask);
            SetFloat(material, "_EmissionBlend", 0.6f);
            SetFloat(material, "_UseMatCap", 1f);
            SetColor(material, "_MatCapColor", new Color(0.4f, 0.7f, 1f));
            SetTexture(material, "_MatCapTex", matCap);
            SetFloat(material, "_MatCapBlend", 0.35f);
            SetFloat(material, "_UseRim", 1f);
            SetColor(material, "_RimColor", new Color(0.1f, 0.7f, 1.5f));
            SetFloat(material, "_RimBorder", 0.55f);
            SetFloat(material, "_RimBlur", 0.25f);
            SetFloat(material, "_LightMinLimit", 0.02f);
            SetFloat(material, "_LightMaxLimit", 2f);
        });
        Material accentMaterial = CreateMaterial("Accent", "Hidden/lilToonOutline", material =>
        {
            material.SetOverrideTag("VRCFallback", "ToonStandardOutline");
            SetColor(material, "_Color", new Color(0.95f, 0.18f, 0.48f));
            SetTexture(material, "_MainTex", checker);
            SetFloat(material, "_UseShadow", 1f);
            SetFloat(material, "_ShadowStrength", 0.9f);
            SetFloat(material, "_UseEmission", 1f);
            SetColor(material, "_EmissionColor", new Color(1.2f, 0.05f, 0.35f));
            SetTexture(material, "_EmissionMap", emissionMask);
            SetColor(material, "_OutlineColor", new Color(0.03f, 0.01f, 0.08f));
            SetTexture(material, "_OutlineTex", checker);
            SetTexture(material, "_OutlineWidthMask", outlineMask);
            SetFloat(material, "_OutlineWidth", 0.08f);
            SetFloat(material, "_OutlineEnableLighting", 0.4f);
            SetFloat(material, "_LightMinLimit", 0f);
            SetFloat(material, "_LightMaxLimit", 1.5f);
        });
        Material garmentMaterial = CreateMaterial(
            "TransparentGarment", "Hidden/lilToonTransparent", material =>
        {
            material.SetOverrideTag("VRCFallback", "Transparent");
            material.SetOverrideTag("RenderType", "Transparent");
            material.renderQueue = 3000;
            SetColor(material, "_Color", new Color(0.15f, 0.85f, 1f, 0.38f));
            SetTexture(material, "_MainTex", checker);
            SetFloat(material, "_TransparentMode", 2f);
            SetFloat(material, "_ZWrite", 0f);
            SetFloat(material, "_UseShadow", 1f);
            SetFloat(material, "_UseRim", 1f);
            SetColor(material, "_RimColor", new Color(0.2f, 0.9f, 1.5f));
            SetFloat(material, "_RimBorder", 0.4f);
        });

        CreatePart("Torso", PrimitiveType.Capsule, avatar.transform,
            new Vector3(0f, 1.12f, 0f), new Vector3(0.38f, 0.43f, 0.25f),
            Vector3.zero, bodyMaterial);
        CreatePart("Pelvis", PrimitiveType.Sphere, avatar.transform,
            new Vector3(0f, 0.76f, 0f), new Vector3(0.4f, 0.24f, 0.28f),
            Vector3.zero, bodyMaterial);
        CreatePart("Neck", PrimitiveType.Cylinder, avatar.transform,
            new Vector3(0f, 1.48f, 0f), new Vector3(0.12f, 0.09f, 0.12f),
            Vector3.zero, bodyMaterial);
        CreatePart("Head", PrimitiveType.Sphere, avatar.transform,
            new Vector3(0f, 1.72f, 0f), new Vector3(0.31f, 0.36f, 0.3f),
            Vector3.zero, bodyMaterial);
        CreatePart("Hair", PrimitiveType.Sphere, avatar.transform,
            new Vector3(0f, 1.83f, 0.055f), new Vector3(0.325f, 0.27f, 0.31f),
            Vector3.zero, accentMaterial);
        CreatePart("Visor", PrimitiveType.Cube, avatar.transform,
            new Vector3(0f, 1.73f, -0.285f), new Vector3(0.27f, 0.08f, 0.025f),
            Vector3.zero, accentMaterial);

        CreatePart("LeftArm", PrimitiveType.Capsule, avatar.transform,
            new Vector3(-0.48f, 1.1f, 0f), new Vector3(0.12f, 0.4f, 0.13f),
            new Vector3(0f, 0f, -8f), bodyMaterial);
        CreatePart("RightArm", PrimitiveType.Capsule, avatar.transform,
            new Vector3(0.48f, 1.1f, 0f), new Vector3(0.12f, 0.4f, 0.13f),
            new Vector3(0f, 0f, 8f), bodyMaterial);
        CreatePart("LeftHand", PrimitiveType.Sphere, avatar.transform,
            new Vector3(-0.55f, 0.69f, 0f), Vector3.one * 0.14f,
            Vector3.zero, accentMaterial);
        CreatePart("RightHand", PrimitiveType.Sphere, avatar.transform,
            new Vector3(0.55f, 0.69f, 0f), Vector3.one * 0.14f,
            Vector3.zero, accentMaterial);

        CreatePart("LeftLeg", PrimitiveType.Capsule, avatar.transform,
            new Vector3(-0.19f, 0.38f, 0f), new Vector3(0.16f, 0.4f, 0.18f),
            Vector3.zero, bodyMaterial);
        CreatePart("RightLeg", PrimitiveType.Capsule, avatar.transform,
            new Vector3(0.19f, 0.38f, 0f), new Vector3(0.16f, 0.4f, 0.18f),
            Vector3.zero, bodyMaterial);
        CreatePart("LeftBoot", PrimitiveType.Cube, avatar.transform,
            new Vector3(-0.19f, 0.07f, -0.08f), new Vector3(0.17f, 0.09f, 0.27f),
            Vector3.zero, accentMaterial);
        CreatePart("RightBoot", PrimitiveType.Cube, avatar.transform,
            new Vector3(0.19f, 0.07f, -0.08f), new Vector3(0.17f, 0.09f, 0.27f),
            Vector3.zero, accentMaterial);

        CreatePart("TransparentJacket", PrimitiveType.Cube, avatar.transform,
            new Vector3(0f, 1.13f, 0f), new Vector3(0.44f, 0.43f, 0.3f),
            Vector3.zero, garmentMaterial);
        CreatePart("ShoulderAccessory", PrimitiveType.Cube, avatar.transform,
            new Vector3(0.49f, 1.36f, 0f), new Vector3(0.16f, 0.1f, 0.22f),
            new Vector3(0f, 0f, -12f), accentMaterial);

        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.SaveAssets();
        return avatar;
    }

    private static GameObject CreatePart(
        string name,
        PrimitiveType primitive,
        Transform parent,
        Vector3 localPosition,
        Vector3 localScale,
        Vector3 localEulerAngles,
        Material material)
    {
        GameObject part = GameObject.CreatePrimitive(primitive);
        part.name = name;
        part.transform.SetParent(parent);
        part.transform.localPosition = localPosition;
        part.transform.localEulerAngles = localEulerAngles;
        part.transform.localScale = localScale;
        part.GetComponent<Renderer>().sharedMaterial = material;
        Collider collider = part.GetComponent<Collider>();
        if (collider != null)
        {
            UnityEngine.Object.DestroyImmediate(collider);
        }
        return part;
    }

    private static Material CreateMaterial(string name, string shaderName, Action<Material> configure)
    {
        string path = DemoFolder + "/" + name + ".mat";
        Shader shader = Shader.Find(shaderName);
        if (shader == null)
        {
            throw new InvalidOperationException(
                shaderName + " was not found. Install the pinned lilToon package first.");
        }

        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(shader) { name = name };
            AssetDatabase.CreateAsset(material, path);
        }
        else
        {
            material.shader = shader;
        }

        configure(material);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static Texture2D CreateTexture(string name, Func<int, int, Color> getPixel)
    {
        string path = DemoFolder + "/" + name + ".asset";
        Texture2D existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        Texture2D texture = existing;
        if (texture == null)
        {
            texture = new Texture2D(16, 16, TextureFormat.RGBA32, false)
            {
                name = name,
                filterMode = FilterMode.Bilinear,
            };
            AssetDatabase.CreateAsset(texture, path);
        }

        for (int y = 0; y < texture.height; y++)
        {
            for (int x = 0; x < texture.width; x++)
            {
                texture.SetPixel(x, y, getPixel(x, y));
            }
        }
        texture.Apply();
        EditorUtility.SetDirty(texture);
        return texture;
    }

    private static Texture2D CreatePngTexture(string name, Func<int, int, Color> getPixel)
    {
        string assetPath = DemoFolder + "/" + name + ".png";
        var source = new Texture2D(16, 16, TextureFormat.RGBA32, false);
        for (int y = 0; y < source.height; y++)
        {
            for (int x = 0; x < source.width; x++)
            {
                source.SetPixel(x, y, getPixel(x, y));
            }
        }
        source.Apply();
        byte[] bytes = source.EncodeToPNG();
        UnityEngine.Object.DestroyImmediate(source);

        string projectRoot = Path.GetDirectoryName(Application.dataPath);
        string sourcePath = Path.GetFullPath(Path.Combine(projectRoot ?? string.Empty, assetPath));
        File.WriteAllBytes(sourcePath, bytes);
        AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
        return AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
    }

    private static void SetFloat(Material material, string property, float value)
    {
        if (material.HasProperty(property))
        {
            material.SetFloat(property, value);
        }
    }

    private static void SetColor(Material material, string property, Color value)
    {
        if (material.HasProperty(property))
        {
            material.SetColor(property, value);
        }
    }

    private static void SetTexture(Material material, string property, Texture value)
    {
        if (material.HasProperty(property))
        {
            material.SetTexture(property, value);
        }
    }

    private static void EnsureFolder()
    {
        if (!AssetDatabase.IsValidFolder(DemoFolder))
        {
            AssetDatabase.CreateFolder("Assets", "AvatarMaterialStudioDemo");
        }
    }
}
