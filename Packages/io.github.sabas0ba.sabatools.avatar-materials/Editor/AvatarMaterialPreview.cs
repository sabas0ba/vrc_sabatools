using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace SabaTools.AvatarMaterials.Editors
{
    internal enum AvatarMaterialStudioLanguage
    {
        Japanese,
        English,
    }

    internal enum AvatarPreviewMode
    {
        Original,
        Fallback,
        Quest,
    }

    internal enum AvatarPreviewCameraMode
    {
        Orbit,
        FreeFly,
        SceneViewFollow,
    }

    internal struct AvatarPreviewRenderOptions
    {
        internal AvatarPreviewCameraMode CameraMode;
        internal RenderQueueVisibilityMode QueueVisibility;
        internal Vector2Int QueueRange;
        internal bool TransparencyProbe;
        internal int TransparencyProbeQueue;
        internal float TransparencyProbeAlpha;
        internal bool TransparencyProbeZWrite;
        internal float TransparencyProbeDistance;
        internal bool CameraOverride;
        internal Vector3 CameraPosition;
        internal Quaternion CameraRotation;
        internal float CameraFieldOfView;
        internal bool DrawBounds;
        internal bool DrawAllBounds;
        internal string SelectedRendererPath;
        internal bool DrawViewPoint;
        internal Vector3 ViewPoint;
        internal bool DrawRenderQueueLabels;
        internal bool DrawLightingGizmos;
    }

    internal enum AvatarLightingScenario
    {
        NoLights,
        AmbientMinimum,
        AmbientMaximum,
        DirectionalMinimum,
        DirectionalNeutral,
        DirectionalMaximum,
        DirectionalWarm,
        DirectionalCool,
        DualDirectional,
        DualColored,
        Backlight,
        PointNear,
        PointFar,
        DualPoint,
        DirectionalAndPoint,
        TopPoint,
        BottomPoint,
    }

    internal sealed class AvatarMaterialPreview
    {
        private readonly List<Material> _ownedMaterials = new List<Material>();

        private PreviewRenderUtility _utility;
        private GameObject _instance;
        private GameObject _transparencyProbe;
        private Material _transparencyProbeMaterial;
        private Bounds _bounds;
        private Vector2 _orbit = new Vector2(-25f, 10f);
        private Vector3 _orbitPan;
        private float _distance;
        private Vector3 _freePosition;
        private Vector2 _freeLook;
        private bool _freeCameraInitialized;
        private AvatarPreviewCameraMode _lastCameraMode;

        internal AvatarMaterialStudioLanguage Language { get; set; }
            = AvatarMaterialStudioLanguage.Japanese;
        internal string Status { get; private set; } = string.Empty;
        internal Vector3 CurrentCameraPosition { get; private set; }
        internal Quaternion CurrentCameraRotation { get; private set; } = Quaternion.identity;

        internal void Rebuild(GameObject source, AvatarPreviewMode mode)
        {
            EnsureUtility();
            DestroyInstance();
            Status = string.Empty;

            if (source == null)
            {
                return;
            }

            GameObject previewSource = source;
            bool questOverride = false;
            if (mode == AvatarPreviewMode.Quest)
            {
                previewSource = QuestAvatarResolver.Resolve(source, out questOverride);
            }

            _instance = Object.Instantiate(previewSource);
            _instance.name = previewSource.name + " (SabaTools Preview)";
            SetHideFlagsRecursively(_instance, HideFlags.HideAndDontSave);
            _instance.SetActive(true);
            _utility.AddSingleGO(_instance);

            int replaced = 0;
            if (mode == AvatarPreviewMode.Fallback)
            {
                replaced = ReplaceWithFallbackMaterials(_instance);
                Status = Language == AvatarMaterialStudioLanguage.Japanese
                    ? replaced + " 個のマテリアルスロットを Fallback 近似に置換しました。"
                    : replaced + " material slot(s) use fallback approximations.";
            }
            else if (mode == AvatarPreviewMode.Quest)
            {
                replaced = ReplaceUnsupportedQuestMaterials(_instance);
                if (questOverride)
                {
                    Status = replaced == 0
                        ? Text("Android プラットフォームOverride: ",
                            "Android per-platform override: ") + previewSource.name
                        : Text("Android プラットフォームOverride: ",
                            "Android per-platform override: ") + previewSource.name + "; "
                          + (Language == AvatarMaterialStudioLanguage.Japanese
                              ? replaced + " 個の非対応スロットをモバイルShaderで近似しました。"
                              : replaced + " unsupported slot(s) are approximated with a mobile shader.");
                }
                else
                {
                    Status = replaced == 0
                        ? Text("すべてのマテリアルスロットが Quest 対応Avatar Shaderです。",
                            "All material slots use documented Quest avatar shaders.")
                        : (Language == AvatarMaterialStudioLanguage.Japanese
                            ? replaced + " 個の非対応スロットをモバイルShaderで近似しました。"
                            : replaced + " unsupported slot(s) are approximated with a mobile shader.");
                }
            }

            _bounds = CalculateBounds(_instance);
            _distance = Mathf.Max(_bounds.extents.magnitude * 2.5f, 0.5f);
            ResetCamera();
        }

        internal void Draw(Rect rect, AvatarLightingScenario lighting)
        {
            Draw(rect, lighting, new AvatarPreviewRenderOptions
            {
                CameraMode = AvatarPreviewCameraMode.Orbit,
                QueueVisibility = RenderQueueVisibilityMode.ShowAll,
                QueueRange = new Vector2Int(0, 5000),
            });
        }

        internal void Draw(
            Rect rect, AvatarLightingScenario lighting, AvatarPreviewRenderOptions options)
        {
            if (Event.current.type == EventType.Repaint)
            {
                EditorGUI.DrawRect(rect, new Color(0.12f, 0.12f, 0.12f, 1f));
            }

            if (_utility == null || _instance == null || rect.width < 2f || rect.height < 2f)
            {
                GUI.Label(rect, Text("Preview対象のAvatarを選択してください。",
                    "Select an avatar to preview."), EditorStyles.centeredGreyMiniLabel);
                return;
            }

            HandleInput(rect, options.CameraMode, options.CameraOverride);
            ConfigureLighting(lighting);
            ConfigureQueueVisibility(options.QueueVisibility, options.QueueRange);
            ConfigureCamera(rect, options);
            ConfigureTransparencyProbe(options);

            _utility.BeginPreview(rect, GUIStyle.none);
            _utility.camera.Render();
            Texture result = _utility.EndPreview();
            GUI.DrawTexture(rect, result, ScaleMode.StretchToFill, false);
            if (options.DrawBounds || options.DrawRenderQueueLabels
                || options.DrawLightingGizmos)
            {
                DrawOverlays(rect, options);
            }
        }

        internal void ResetCamera()
        {
            _orbit = new Vector2(-25f, 10f);
            _orbitPan = Vector3.zero;
            Quaternion rotation = Quaternion.Euler(_orbit.y, _orbit.x, 0f);
            Vector3 centre = _bounds.center;
            _freePosition = centre + rotation * (Vector3.back * Mathf.Max(_distance, 0.5f));
            Quaternion look = Quaternion.LookRotation(centre - _freePosition, Vector3.up);
            _freeLook = new Vector2(look.eulerAngles.y, NormalizeAngle(look.eulerAngles.x));
            _freeCameraInitialized = true;
        }

        internal void Cleanup()
        {
            DestroyInstance();
            DestroyTransparencyProbe();
            if (_utility != null)
            {
                _utility.Cleanup();
                _utility = null;
            }
        }

        private void EnsureUtility()
        {
            if (_utility != null)
            {
                return;
            }

            _utility = new PreviewRenderUtility();
            _utility.camera.fieldOfView = 30f;
            _utility.camera.clearFlags = CameraClearFlags.Color;
            _utility.camera.backgroundColor = new Color(0.12f, 0.12f, 0.12f, 1f);
            CreateTransparencyProbe();
        }

        private void DestroyInstance()
        {
            if (_instance != null)
            {
                Object.DestroyImmediate(_instance);
                _instance = null;
            }

            foreach (Material material in _ownedMaterials)
            {
                if (material != null)
                {
                    Object.DestroyImmediate(material);
                }
            }
            _ownedMaterials.Clear();
        }

        private int ReplaceWithFallbackMaterials(GameObject root)
        {
            int replaced = 0;
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                Material[] materials = renderer.sharedMaterials;
                for (int index = 0; index < materials.Length; index++)
                {
                    Material replacement = CreateFallbackMaterial(materials[index]);
                    if (replacement != null)
                    {
                        materials[index] = replacement;
                        replaced++;
                    }
                }
                renderer.sharedMaterials = materials;
            }
            return replaced;
        }

        private int ReplaceUnsupportedQuestMaterials(GameObject root)
        {
            int replaced = 0;
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                Material[] materials = renderer.sharedMaterials;
                for (int index = 0; index < materials.Length; index++)
                {
                    Material source = materials[index];
                    string shaderName = source != null && source.shader != null
                        ? source.shader.name
                        : string.Empty;
                    if (AvatarShaderRules.IsQuestAvatarShader(shaderName))
                    {
                        continue;
                    }

                    Material replacement = CreateMaterial(
                        source,
                        "VRChat/Mobile/Toon Standard",
                        "VRChat/Mobile/Standard Lite",
                        "VRChat/Mobile/Diffuse",
                        "Standard");
                    if (replacement != null)
                    {
                        materials[index] = replacement;
                        replaced++;
                    }
                }
                renderer.sharedMaterials = materials;
            }
            return replaced;
        }

        private Material CreateFallbackMaterial(Material source)
        {
            if (source == null || source.shader == null)
            {
                return null;
            }

            Shader shader = source.shader;
            string tag = source.GetTag("VRCFallback", false, string.Empty);
            FallbackShaderKind kind = AvatarShaderRules.ResolveFallback(
                tag,
                shader.name,
                source.HasProperty("_Ramp"),
                source.IsKeywordEnabled("_ALPHABLEND_ON"),
                source.IsKeywordEnabled("_ALPHATEST_ON"));

            switch (kind)
            {
                case FallbackShaderKind.ToonStandard:
                    return CreateMaterial(source, "VRChat/Mobile/Toon Standard", "Toon/Lit", "Standard");
                case FallbackShaderKind.ToonStandardOutline:
                    return CreateMaterial(
                        source, "VRChat/Mobile/Toon Standard (Outline)", "Toon/Lit Outline", "Toon/Lit", "Standard");
                case FallbackShaderKind.ToonCutout:
                    return CreateMaterial(source, "Toon/Lit Cutout", "Unlit/Transparent Cutout", "Standard");
                case FallbackShaderKind.ToonOutline:
                    return CreateMaterial(source, "Toon/Lit Outline", "Toon/Lit", "Standard");
                case FallbackShaderKind.Toon:
                    return CreateMaterial(source, "Toon/Lit", "VRChat/Mobile/Toon Lit", "Standard");
                case FallbackShaderKind.Unlit:
                    return CreateMaterial(source, "Unlit/Texture", "Standard");
                case FallbackShaderKind.Transparent:
                    return CreateMaterial(source, "Unlit/Transparent", "Standard");
                case FallbackShaderKind.Cutout:
                    return CreateMaterial(source, "Unlit/Transparent Cutout", "Standard");
                case FallbackShaderKind.VertexLit:
                    return CreateMaterial(source, "Legacy Shaders/VertexLit", "Standard");
                case FallbackShaderKind.Particle:
                    return CreateMaterial(source, "Particles/Standard Unlit", "Particles/Alpha Blended", "Standard");
                case FallbackShaderKind.Sprite:
                    return CreateMaterial(source, "Sprites/Default", "Unlit/Transparent", "Standard");
                case FallbackShaderKind.Matcap:
                    return CreateMaterial(source, "MatCap/Vertex/Textured Lit", "Standard");
                case FallbackShaderKind.MobileToon:
                    return CreateMaterial(source, "VRChat/Mobile/Toon Lit", "Toon/Lit", "Standard");
                case FallbackShaderKind.Hidden:
                    return CreateMaterial(source, "Hidden/SabaTools/AvatarMaterialStudio/Invisible");
                default:
                    return CreateMaterial(source, "Standard");
            }
        }

        private Material CreateMaterial(Material source, params string[] shaderNames)
        {
            Shader shader = null;
            foreach (string shaderName in shaderNames)
            {
                shader = Shader.Find(shaderName);
                if (shader != null)
                {
                    break;
                }
            }
            if (shader == null)
            {
                return null;
            }

            var result = new Material(shader)
            {
                hideFlags = HideFlags.HideAndDontSave,
                name = (source != null ? source.name : "Missing") + " (Preview)",
            };
            if (source != null)
            {
                result.CopyMatchingPropertiesFromMaterial(source);
            }
            _ownedMaterials.Add(result);
            return result;
        }

        private void ConfigureLighting(AvatarLightingScenario scenario)
        {
            Light key = _utility.lights[0];
            Light fill = _utility.lights[1];
            float extent = Mathf.Max(_bounds.extents.magnitude, 0.5f);

            key.enabled = true;
            fill.enabled = true;
            key.type = LightType.Directional;
            fill.type = LightType.Directional;
            key.transform.rotation = Quaternion.Euler(35f, 35f, 0f);
            fill.transform.rotation = Quaternion.Euler(340f, 210f, 0f);
            key.transform.position = _bounds.center + new Vector3(extent, extent, -extent);
            fill.transform.position = _bounds.center + new Vector3(-extent, 0f, -extent);
            key.color = Color.white;
            fill.color = Color.white;
            key.intensity = 0f;
            fill.intensity = 0f;
            key.range = extent * 6f;
            fill.range = extent * 6f;
            key.shadows = LightShadows.None;
            fill.shadows = LightShadows.None;
            _utility.ambientColor = Color.black;

            switch (scenario)
            {
                case AvatarLightingScenario.NoLights:
                    key.enabled = false;
                    fill.enabled = false;
                    break;
                case AvatarLightingScenario.AmbientMinimum:
                    _utility.ambientColor = new Color(0.015f, 0.015f, 0.02f);
                    key.enabled = false;
                    fill.enabled = false;
                    break;
                case AvatarLightingScenario.AmbientMaximum:
                    _utility.ambientColor = Color.white;
                    key.enabled = false;
                    fill.enabled = false;
                    break;
                case AvatarLightingScenario.DirectionalMinimum:
                    _utility.ambientColor = new Color(0.01f, 0.01f, 0.015f);
                    key.intensity = 0.03f;
                    break;
                case AvatarLightingScenario.DirectionalNeutral:
                    _utility.ambientColor = new Color(0.18f, 0.18f, 0.18f);
                    key.intensity = 1f;
                    break;
                case AvatarLightingScenario.DirectionalMaximum:
                    _utility.ambientColor = new Color(0.35f, 0.35f, 0.35f);
                    key.intensity = 3f;
                    break;
                case AvatarLightingScenario.DirectionalWarm:
                    _utility.ambientColor = new Color(0.12f, 0.07f, 0.035f);
                    key.color = new Color(1f, 0.64f, 0.36f);
                    key.intensity = 1.2f;
                    break;
                case AvatarLightingScenario.DirectionalCool:
                    _utility.ambientColor = new Color(0.035f, 0.07f, 0.14f);
                    key.color = new Color(0.38f, 0.65f, 1f);
                    key.intensity = 1.2f;
                    break;
                case AvatarLightingScenario.DualDirectional:
                    _utility.ambientColor = new Color(0.1f, 0.1f, 0.1f);
                    key.intensity = 1f;
                    fill.color = new Color(0.65f, 0.72f, 0.85f);
                    fill.intensity = 0.7f;
                    break;
                case AvatarLightingScenario.DualColored:
                    _utility.ambientColor = new Color(0.035f, 0.035f, 0.035f);
                    key.color = new Color(1f, 0.12f, 0.08f);
                    key.intensity = 1.2f;
                    fill.color = new Color(0.05f, 0.25f, 1f);
                    fill.intensity = 1.2f;
                    break;
                case AvatarLightingScenario.Backlight:
                    _utility.ambientColor = new Color(0.02f, 0.02f, 0.025f);
                    key.transform.rotation = Quaternion.Euler(15f, 185f, 0f);
                    key.intensity = 1.8f;
                    fill.transform.rotation = Quaternion.Euler(25f, 15f, 0f);
                    fill.intensity = 0.08f;
                    break;
                case AvatarLightingScenario.PointNear:
                    _utility.ambientColor = new Color(0.035f, 0.035f, 0.035f);
                    ConfigurePoint(key, _bounds.center + new Vector3(
                        extent * 0.35f, extent * 0.15f, -extent * 0.55f),
                        extent * 2.5f, 2f, Color.white);
                    break;
                case AvatarLightingScenario.PointFar:
                    _utility.ambientColor = new Color(0.035f, 0.035f, 0.035f);
                    ConfigurePoint(key, _bounds.center + new Vector3(
                        extent * 2.5f, extent * 1.5f, -extent * 3f),
                        extent * 7f, 4f, Color.white);
                    break;
                case AvatarLightingScenario.DualPoint:
                    _utility.ambientColor = new Color(0.025f, 0.025f, 0.025f);
                    ConfigurePoint(key, _bounds.center + new Vector3(
                        extent * 0.8f, extent * 0.3f, -extent),
                        extent * 3.5f, 2.5f, new Color(1f, 0.25f, 0.12f));
                    ConfigurePoint(fill, _bounds.center + new Vector3(
                        -extent * 0.8f, -extent * 0.1f, -extent * 0.5f),
                        extent * 3.5f, 2.5f, new Color(0.12f, 0.35f, 1f));
                    break;
                case AvatarLightingScenario.DirectionalAndPoint:
                    _utility.ambientColor = new Color(0.08f, 0.08f, 0.08f);
                    key.intensity = 0.8f;
                    ConfigurePoint(fill, _bounds.center + new Vector3(
                        -extent * 0.65f, extent * 0.1f, -extent * 0.45f),
                        extent * 3f, 1.8f, new Color(0.35f, 0.55f, 1f));
                    break;
                case AvatarLightingScenario.TopPoint:
                    _utility.ambientColor = new Color(0.03f, 0.03f, 0.03f);
                    ConfigurePoint(key, _bounds.center + new Vector3(0f, extent * 1.4f, 0f),
                        extent * 3f, 2.3f, Color.white);
                    break;
                case AvatarLightingScenario.BottomPoint:
                    _utility.ambientColor = new Color(0.02f, 0.02f, 0.02f);
                    ConfigurePoint(key, _bounds.center + new Vector3(0f, -extent, -extent * 0.2f),
                        extent * 3f, 2.3f, new Color(0.65f, 0.8f, 1f));
                    break;
            }
        }

        private static void ConfigurePoint(
            Light light, Vector3 position, float range, float intensity, Color color)
        {
            light.type = LightType.Point;
            light.transform.position = position;
            light.range = range;
            light.intensity = intensity;
            light.color = color;
        }

        private void ConfigureQueueVisibility(
            RenderQueueVisibilityMode mode, Vector2Int range)
        {
            foreach (Renderer renderer in _instance.GetComponentsInChildren<Renderer>(true))
            {
                if (mode == RenderQueueVisibilityMode.ShowAll)
                {
                    renderer.forceRenderingOff = false;
                    continue;
                }

                bool inRange = false;
                foreach (Material material in renderer.sharedMaterials)
                {
                    if (material != null
                        && material.renderQueue >= range.x
                        && material.renderQueue <= range.y)
                    {
                        inRange = true;
                        break;
                    }
                }
                renderer.forceRenderingOff = mode == RenderQueueVisibilityMode.OnlySelectedRange
                    ? !inRange
                    : inRange;
            }
        }

        private void ConfigureCamera(Rect rect, AvatarPreviewRenderOptions options)
        {
            Camera camera = _utility.camera;
            camera.orthographic = false;

            if (options.CameraOverride)
            {
                camera.transform.SetPositionAndRotation(
                    options.CameraPosition, options.CameraRotation);
                camera.fieldOfView = Mathf.Clamp(options.CameraFieldOfView, 5f, 120f);
                camera.nearClipPlane = 0.01f;
                camera.farClipPlane = Mathf.Max(
                    Vector3.Distance(options.CameraPosition, _bounds.center) * 20f, 100f);
                CaptureCameraState(camera);
                return;
            }

            if (options.CameraMode == AvatarPreviewCameraMode.SceneViewFollow
                && SceneView.lastActiveSceneView != null
                && SceneView.lastActiveSceneView.camera != null)
            {
                Camera source = SceneView.lastActiveSceneView.camera;
                camera.transform.SetPositionAndRotation(
                    source.transform.position, source.transform.rotation);
                camera.fieldOfView = source.fieldOfView;
                camera.orthographic = source.orthographic;
                camera.orthographicSize = source.orthographicSize;
                camera.nearClipPlane = source.nearClipPlane;
                camera.farClipPlane = source.farClipPlane;
                _lastCameraMode = options.CameraMode;
                CaptureCameraState(camera);
                return;
            }

            if (options.CameraMode == AvatarPreviewCameraMode.FreeFly)
            {
                if (!_freeCameraInitialized)
                {
                    ResetCamera();
                }
                if (_lastCameraMode == AvatarPreviewCameraMode.Orbit)
                {
                    Quaternion orbitRotation = Quaternion.Euler(_orbit.y, _orbit.x, 0f);
                    Vector3 centre = _bounds.center + _orbitPan;
                    _freePosition = centre + orbitRotation * (Vector3.back * _distance);
                    Quaternion look = Quaternion.LookRotation(centre - _freePosition, Vector3.up);
                    _freeLook = new Vector2(
                        look.eulerAngles.y, NormalizeAngle(look.eulerAngles.x));
                }
                camera.transform.position = _freePosition;
                camera.transform.rotation = Quaternion.Euler(_freeLook.y, _freeLook.x, 0f);
            }
            else
            {
                Quaternion rotation = Quaternion.Euler(_orbit.y, _orbit.x, 0f);
                Vector3 centre = _bounds.center + _orbitPan;
                camera.transform.position = centre + rotation * (Vector3.back * _distance);
                camera.transform.rotation = Quaternion.LookRotation(
                    centre - camera.transform.position, Vector3.up);
            }

            camera.fieldOfView = 30f;
            camera.nearClipPlane = Mathf.Max(_distance * 0.01f, 0.01f);
            camera.farClipPlane = Mathf.Max(_distance * 10f, 100f);
            _lastCameraMode = options.CameraMode;
            CaptureCameraState(camera);
        }

        private void CaptureCameraState(Camera camera)
        {
            CurrentCameraPosition = camera.transform.position;
            CurrentCameraRotation = camera.transform.rotation;
        }

        private void ConfigureTransparencyProbe(AvatarPreviewRenderOptions options)
        {
            if (_transparencyProbe == null || _transparencyProbeMaterial == null)
            {
                return;
            }

            _transparencyProbe.SetActive(options.TransparencyProbe);
            if (!options.TransparencyProbe)
            {
                return;
            }

            Camera camera = _utility.camera;
            float distanceToAvatar = Mathf.Max(
                Vector3.Distance(camera.transform.position, _bounds.center), 0.1f);
            float distanceRatio = Mathf.Clamp(options.TransparencyProbeDistance, 0.1f, 0.9f);
            _transparencyProbe.transform.position = camera.transform.position
                + camera.transform.forward * (distanceToAvatar * distanceRatio);
            _transparencyProbe.transform.rotation = camera.transform.rotation
                * Quaternion.Euler(0f, 180f, 0f);
            float probeSize = Mathf.Max(_bounds.size.x, _bounds.size.y) * 1.15f;
            _transparencyProbe.transform.localScale = new Vector3(probeSize, probeSize, 1f);

            Color color = new Color(0.15f, 0.75f, 0.95f,
                Mathf.Clamp01(options.TransparencyProbeAlpha));
            if (_transparencyProbeMaterial.HasProperty("_Color"))
            {
                _transparencyProbeMaterial.SetColor("_Color", color);
            }
            if (_transparencyProbeMaterial.HasProperty("_ZWrite"))
            {
                _transparencyProbeMaterial.SetInt(
                    "_ZWrite", options.TransparencyProbeZWrite ? 1 : 0);
            }
            if (_transparencyProbeMaterial.HasProperty("_Cull"))
            {
                _transparencyProbeMaterial.SetInt("_Cull", 0);
            }
            _transparencyProbeMaterial.renderQueue = Mathf.Clamp(
                options.TransparencyProbeQueue, 0, 5000);
        }

        private void CreateTransparencyProbe()
        {
            _transparencyProbe = GameObject.CreatePrimitive(PrimitiveType.Quad);
            _transparencyProbe.name = "SabaTools Transparency Probe";
            Collider collider = _transparencyProbe.GetComponent<Collider>();
            if (collider != null)
            {
                Object.DestroyImmediate(collider);
            }
            SetHideFlagsRecursively(_transparencyProbe, HideFlags.HideAndDontSave);

            Shader shader = Shader.Find("Standard") ?? Shader.Find("Unlit/Transparent");
            if (shader != null)
            {
                _transparencyProbeMaterial = new Material(shader)
                {
                    name = "SabaTools Transparency Probe Material",
                    hideFlags = HideFlags.HideAndDontSave,
                };
                _transparencyProbeMaterial.SetOverrideTag("RenderType", "Transparent");
                if (_transparencyProbeMaterial.HasProperty("_Mode"))
                {
                    _transparencyProbeMaterial.SetFloat("_Mode", 3f);
                }
                if (_transparencyProbeMaterial.HasProperty("_SrcBlend"))
                {
                    _transparencyProbeMaterial.SetInt("_SrcBlend", 5);
                }
                if (_transparencyProbeMaterial.HasProperty("_DstBlend"))
                {
                    _transparencyProbeMaterial.SetInt("_DstBlend", 10);
                }
                _transparencyProbeMaterial.EnableKeyword("_ALPHABLEND_ON");
                _transparencyProbe.GetComponent<Renderer>().sharedMaterial =
                    _transparencyProbeMaterial;
            }
            _transparencyProbe.SetActive(false);
            _utility.AddSingleGO(_transparencyProbe);
        }

        private void DestroyTransparencyProbe()
        {
            if (_transparencyProbe != null)
            {
                Object.DestroyImmediate(_transparencyProbe);
                _transparencyProbe = null;
            }
            if (_transparencyProbeMaterial != null)
            {
                Object.DestroyImmediate(_transparencyProbeMaterial);
                _transparencyProbeMaterial = null;
            }
        }

        private void HandleInput(
            Rect rect, AvatarPreviewCameraMode cameraMode, bool cameraOverride)
        {
            Event current = Event.current;
            if (cameraOverride || cameraMode == AvatarPreviewCameraMode.SceneViewFollow)
            {
                return;
            }

            EditorGUIUtility.AddCursorRect(
                rect, cameraMode == AvatarPreviewCameraMode.FreeFly
                    ? MouseCursor.FPS
                    : MouseCursor.Orbit);
            if (current.type == EventType.ScrollWheel && rect.Contains(current.mousePosition))
            {
                if (cameraMode == AvatarPreviewCameraMode.FreeFly)
                {
                    float speed = Mathf.Max(_bounds.extents.magnitude * 0.15f, 0.05f);
                    _freePosition += Quaternion.Euler(_freeLook.y, _freeLook.x, 0f)
                        * Vector3.forward * (-current.delta.y * speed);
                }
                else
                {
                    _distance *= 1f + current.delta.y * 0.05f;
                    _distance = Mathf.Clamp(
                        _distance,
                        Mathf.Max(_bounds.extents.magnitude * 0.25f, 0.05f),
                        Mathf.Max(_bounds.extents.magnitude * 10f, 5f));
                }
                current.Use();
            }

            if (current.type == EventType.MouseDrag
                && rect.Contains(current.mousePosition))
            {
                if (cameraMode == AvatarPreviewCameraMode.FreeFly && current.button == 1)
                {
                    _freeLook.x += current.delta.x * 0.4f;
                    _freeLook.y = Mathf.Clamp(_freeLook.y - current.delta.y * 0.4f, -89f, 89f);
                    current.Use();
                }
                else if (current.button == 2)
                {
                    Quaternion rotation = cameraMode == AvatarPreviewCameraMode.FreeFly
                        ? Quaternion.Euler(_freeLook.y, _freeLook.x, 0f)
                        : Quaternion.Euler(_orbit.y, _orbit.x, 0f);
                    float scale = Mathf.Max(_distance * 0.002f, 0.001f);
                    Vector3 pan = rotation * new Vector3(
                        -current.delta.x * scale, current.delta.y * scale, 0f);
                    if (cameraMode == AvatarPreviewCameraMode.FreeFly)
                    {
                        _freePosition += pan;
                    }
                    else
                    {
                        _orbitPan += pan;
                    }
                    current.Use();
                }
                else if (cameraMode == AvatarPreviewCameraMode.Orbit && current.button == 0)
                {
                    _orbit.x += current.delta.x;
                    _orbit.y = Mathf.Clamp(_orbit.y - current.delta.y, -89f, 89f);
                    current.Use();
                }
            }
        }

        private void DrawOverlays(Rect rect, AvatarPreviewRenderOptions options)
        {
            GUI.BeginGroup(rect);
            Handles.BeginGUI();
            Rect localRect = new Rect(0f, 0f, rect.width, rect.height);
            if (options.DrawBounds)
            {
                DrawBoundsOverlay(localRect, options);
            }
            if (options.DrawRenderQueueLabels)
            {
                DrawRenderQueueOverlay(localRect);
            }
            if (options.DrawLightingGizmos)
            {
                DrawLightingOverlay(localRect);
            }
            Handles.EndGUI();
            GUI.EndGroup();
        }

        private void DrawBoundsOverlay(Rect rect, AvatarPreviewRenderOptions options)
        {
            foreach (Renderer renderer in _instance.GetComponentsInChildren<Renderer>(true))
            {
                if (!(renderer is MeshRenderer) && !(renderer is SkinnedMeshRenderer))
                {
                    continue;
                }
                string path = AvatarMaterialCatalog.RelativePath(
                    _instance.transform, renderer.transform);
                bool selected = string.Equals(
                    WithoutRoot(path), WithoutRoot(options.SelectedRendererPath),
                    System.StringComparison.Ordinal);
                if (!options.DrawAllBounds && !selected)
                {
                    continue;
                }
                Handles.color = selected
                    ? new Color(1f, 0.72f, 0.1f, 0.95f)
                    : new Color(0.15f, 1f, 0.45f, 0.65f);
                DrawWireBounds(rect, renderer.bounds);
            }

            if (options.DrawViewPoint
                && TryProject(rect, options.ViewPoint, out Vector2 viewPointPosition))
            {
                Handles.color = new Color(0.2f, 0.75f, 1f, 1f);
                Handles.DrawLine(
                    viewPointPosition + Vector2.left * 7f,
                    viewPointPosition + Vector2.right * 7f);
                Handles.DrawLine(
                    viewPointPosition + Vector2.up * 7f,
                    viewPointPosition + Vector2.down * 7f);
            }
        }

        private void DrawRenderQueueOverlay(Rect rect)
        {
            var labelStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                normal = { textColor = Color.white },
                wordWrap = false,
                clipping = TextClipping.Clip,
            };
            foreach (Renderer renderer in _instance.GetComponentsInChildren<Renderer>(true))
            {
                if (!(renderer is MeshRenderer) && !(renderer is SkinnedMeshRenderer))
                {
                    continue;
                }

                var queues = new List<string>();
                int representativeQueue = 2000;
                foreach (Material material in renderer.sharedMaterials)
                {
                    if (material == null)
                    {
                        continue;
                    }
                    representativeQueue = material.renderQueue;
                    queues.Add(material.renderQueue + " " + QueueCategory(material.renderQueue));
                }
                if (queues.Count == 0)
                {
                    continue;
                }

                Handles.color = QueueColor(representativeQueue);
                DrawWireBounds(rect, renderer.bounds);
                if (TryProject(rect, renderer.bounds.center, out Vector2 position))
                {
                    string path = WithoutRoot(AvatarMaterialCatalog.RelativePath(
                        _instance.transform, renderer.transform));
                    Handles.Label(position + new Vector2(4f, -4f),
                        path + "\n" + string.Join(", ", queues.ToArray()), labelStyle);
                }
            }
        }

        private void DrawLightingOverlay(Rect rect)
        {
            var labelStyle = new GUIStyle(EditorStyles.miniBoldLabel)
            {
                normal = { textColor = Color.white },
            };
            foreach (Light light in _utility.lights)
            {
                if (light == null || !light.enabled || light.intensity <= 0f)
                {
                    continue;
                }

                Handles.color = new Color(light.color.r, light.color.g, light.color.b, 1f);
                if (light.type == LightType.Point)
                {
                    if (!TryProject(rect, light.transform.position, out Vector2 lightPosition))
                    {
                        continue;
                    }
                    if (TryProject(rect, _bounds.center, out Vector2 targetPosition))
                    {
                        Handles.DrawDottedLine(lightPosition, targetPosition, 4f);
                    }
                    Handles.DrawWireDisc(lightPosition, Vector3.forward, 7f);
                    Handles.Label(lightPosition + new Vector2(8f, -8f),
                        Text("Point Light", "Point Light") + "  "
                        + light.intensity.ToString("0.00"), labelStyle);
                }
                else if (light.type == LightType.Directional)
                {
                    Vector3 direction = light.transform.forward;
                    float length = Mathf.Max(_bounds.extents.magnitude * 1.2f, 0.5f);
                    Vector3 worldStart = _bounds.center - direction * length;
                    Vector3 worldEnd = _bounds.center - direction * length * 0.15f;
                    if (!TryProject(rect, worldStart, out Vector2 start)
                        || !TryProject(rect, worldEnd, out Vector2 end))
                    {
                        continue;
                    }
                    Handles.DrawAAPolyLine(3f, start, end);
                    Vector2 arrow = (end - start).normalized;
                    Vector2 perpendicular = new Vector2(-arrow.y, arrow.x);
                    Handles.DrawAAConvexPolygon(
                        end, end - arrow * 10f + perpendicular * 5f,
                        end - arrow * 10f - perpendicular * 5f);
                    Handles.Label(start + new Vector2(4f, -4f),
                        Text("Directional Light", "Directional Light") + "  "
                        + light.intensity.ToString("0.00"), labelStyle);
                }
            }
        }

        private void DrawWireBounds(Rect rect, Bounds bounds)
        {
            Vector3 min = bounds.min;
            Vector3 max = bounds.max;
            Vector3[] corners =
            {
                new Vector3(min.x, min.y, min.z), new Vector3(max.x, min.y, min.z),
                new Vector3(max.x, max.y, min.z), new Vector3(min.x, max.y, min.z),
                new Vector3(min.x, min.y, max.z), new Vector3(max.x, min.y, max.z),
                new Vector3(max.x, max.y, max.z), new Vector3(min.x, max.y, max.z),
            };
            int[,] edges =
            {
                { 0, 1 }, { 1, 2 }, { 2, 3 }, { 3, 0 },
                { 4, 5 }, { 5, 6 }, { 6, 7 }, { 7, 4 },
                { 0, 4 }, { 1, 5 }, { 2, 6 }, { 3, 7 },
            };
            for (int index = 0; index < edges.GetLength(0); index++)
            {
                if (TryProject(rect, corners[edges[index, 0]], out Vector2 start)
                    && TryProject(rect, corners[edges[index, 1]], out Vector2 end))
                {
                    Handles.DrawAAPolyLine(2f, start, end);
                }
            }
        }

        private bool TryProject(Rect rect, Vector3 worldPosition, out Vector2 guiPosition)
        {
            Vector3 viewport = _utility.camera.WorldToViewportPoint(worldPosition);
            guiPosition = new Vector2(
                viewport.x * rect.width,
                (1f - viewport.y) * rect.height);
            return viewport.z > 0f
                && guiPosition.x >= 0f && guiPosition.x <= rect.width
                && guiPosition.y >= 0f && guiPosition.y <= rect.height;
        }

        private string Text(string japanese, string english)
        {
            return Language == AvatarMaterialStudioLanguage.Japanese ? japanese : english;
        }

        private static string QueueCategory(int queue)
        {
            if (queue <= 1499) return "Background";
            if (queue <= 2449) return "Geometry";
            if (queue <= 2499) return "AlphaTest";
            if (queue <= 2999) return "GeometryLast";
            if (queue <= 3999) return "Transparent";
            return "Overlay";
        }

        private static Color QueueColor(int queue)
        {
            if (queue <= 1499) return new Color(0.55f, 0.65f, 1f, 0.9f);
            if (queue <= 2449) return new Color(0.25f, 1f, 0.45f, 0.9f);
            if (queue <= 2499) return new Color(1f, 0.85f, 0.2f, 0.9f);
            if (queue <= 2999) return new Color(1f, 0.55f, 0.15f, 0.9f);
            if (queue <= 3999) return new Color(1f, 0.3f, 0.75f, 0.9f);
            return new Color(0.75f, 0.4f, 1f, 0.9f);
        }

        private static float NormalizeAngle(float angle)
        {
            return angle > 180f ? angle - 360f : angle;
        }

        private static string WithoutRoot(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return string.Empty;
            }
            int separator = path.IndexOf('/');
            return separator >= 0 ? path.Substring(separator + 1) : string.Empty;
        }

        private static Bounds CalculateBounds(GameObject root)
        {
            bool found = false;
            Bounds result = new Bounds(root.transform.position, Vector3.one * 0.1f);
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                if (!found)
                {
                    result = renderer.bounds;
                    found = true;
                }
                else
                {
                    result.Encapsulate(renderer.bounds);
                }
            }
            return result;
        }

        private static void SetHideFlagsRecursively(GameObject root, HideFlags flags)
        {
            foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
            {
                transform.gameObject.hideFlags = flags;
                foreach (Component component in transform.GetComponents<Component>())
                {
                    if (component != null)
                    {
                        component.hideFlags = flags;
                    }
                }
            }
        }
    }
}
