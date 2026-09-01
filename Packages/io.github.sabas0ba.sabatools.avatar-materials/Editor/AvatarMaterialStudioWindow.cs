using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

namespace SabaTools.AvatarMaterials.Editors
{
    internal enum AvatarMaterialWorkspace
    {
        MaterialsAndTextures,
        LightingVisualization,
        RenderQueueAndTransparency,
        RendererBounds,
    }

    internal enum AvatarMaterialDetail
    {
        SelectedMaterial,
        TextureInventory,
    }

    public sealed class AvatarMaterialStudioWindow : EditorWindow
    {
        internal const AvatarPreviewCameraMode DefaultCameraMode =
            AvatarPreviewCameraMode.SceneViewFollow;
        internal const AvatarPreviewGizmoMode DefaultGizmoMode =
            AvatarPreviewGizmoMode.Compact;
        internal const float MinimumPreviewUiScale = 0.75f;
        internal const float MaximumPreviewUiScale = 2f;

        private readonly AvatarMaterialPreview _preview = new AvatarMaterialPreview();
        private readonly List<MaterialSlotEntry> _slots = new List<MaterialSlotEntry>();
        private readonly List<TextureUsageEntry> _textureInventory = new List<TextureUsageEntry>();
        private readonly Dictionary<int, bool> _textureFoldouts = new Dictionary<int, bool>();
        private readonly List<RenderQueueEntry> _renderQueueEntries = new List<RenderQueueEntry>();
        private readonly List<RendererBoundsEntry> _rendererBounds = new List<RendererBoundsEntry>();
        private readonly List<BoundaryProbeResult> _boundaryProbes = new List<BoundaryProbeResult>();

        private GameObject _target;
        private Material _selectedMaterial;
        private Vector2 _slotScroll;
        private Vector2 _propertyScroll;
        private Vector2 _inventoryScroll;
        private Vector2 _visualizationScroll;
        private Vector2 _renderQueueScroll;
        private Vector2 _queuePreviewScroll;
        private Vector2 _boundsScroll;
        private Vector2 _boundsPreviewScroll;
        private string _search = string.Empty;
        private string _inventorySearch = string.Empty;
        private string _renderQueueSearch = string.Empty;
        private AvatarMaterialStudioLanguage _language = AvatarMaterialStudioLanguage.Japanese;
        private AvatarMaterialWorkspace _workspace;
        private AvatarMaterialDetail _materialDetail;
        private AvatarPreviewMode _previewMode;
        [SerializeField]
        private AvatarPreviewCameraMode _cameraMode = DefaultCameraMode;
        [SerializeField]
        private AvatarPreviewGizmoMode _gizmoMode = DefaultGizmoMode;
        [SerializeField]
        private float _previewUiScale = 1f;
        private float _transparencyProbeAlpha = 0.35f;
        private float _transparencyProbeDistance = 0.45f;
        private bool _transparencyProbeZWrite;
        private bool _transparencyProbeEnabled = true;
        private Renderer _selectedBoundsRenderer;
        private int _selectedBoundaryDirection;
        private float _nearBoundaryDistance = 0.35f;
        private float _middleBoundaryDistance = 2f;
        private float _farBoundaryDistance = 10f;
        private float _boundaryFieldOfView = 60f;
        private bool _drawAllBounds = true;
        private bool _showRenderQueueSettings = true;
        private bool _showRendererBoundsList = true;
        private float _materialsLeftWidth = 410f;
        private float _renderQueueLeftWidth = 390f;
        private float _rendererBoundsLeftWidth = 390f;
        private int _activeSplitter;

        [MenuItem("Tools/SabaTools/Avatar Material Studio")]
        public static void Open()
        {
            AvatarMaterialStudioWindow window = GetWindow<AvatarMaterialStudioWindow>(
                false, "Avatar Material Studio", true);
            window.minSize = new Vector2(880f, 560f);
            window.Show();
        }

        private void OnEnable()
        {
            _preview.Language = _language;
            Undo.undoRedoPerformed += HandleExternalChange;
            SceneView.duringSceneGui += HandleSceneViewGUI;
            if (_target == null)
            {
                UseSelection();
            }
        }

        private void OnDisable()
        {
            Undo.undoRedoPerformed -= HandleExternalChange;
            SceneView.duringSceneGui -= HandleSceneViewGUI;
            _preview.Cleanup();
        }

        private void OnGUI()
        {
            DrawToolbar();
            if (_target == null)
            {
                EditorGUILayout.HelpBox(
                    T("AvatarのRootを選択してください。Material割当とTexture Propertyは編集可能で、Unity Undoに対応します。",
                      "Select an avatar root. Material assignments and texture properties are " +
                      "editable and participate in Unity Undo."),
                    MessageType.Info);
                return;
            }

            if (_workspace == AvatarMaterialWorkspace.LightingVisualization)
            {
                DrawVisualization();
            }
            else if (_workspace == AvatarMaterialWorkspace.RenderQueueAndTransparency)
            {
                DrawRenderQueueDiagnostics();
            }
            else if (_workspace == AvatarMaterialWorkspace.RendererBounds)
            {
                DrawRendererBoundsDiagnostics();
            }
            else
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    using (new EditorGUILayout.VerticalScope(GUILayout.Width(_materialsLeftWidth)))
                    {
                        DrawMaterialSlots();
                    }
                    DrawSplitter(ref _materialsLeftWidth, 1, 260f);
                    using (new EditorGUILayout.VerticalScope())
                    {
                        DrawMaterialDetails();
                    }
                }
            }
        }

        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GameObject next = (GameObject)EditorGUILayout.ObjectField(
                    _target, typeof(GameObject), true, GUILayout.MinWidth(180f));
                if (next != _target)
                {
                    _target = next;
                    Refresh();
                }
                if (GUILayout.Button(T("選択を使用", "Use Selection"),
                        EditorStyles.toolbarButton, GUILayout.Width(92f)))
                {
                    UseSelection();
                }
                if (GUILayout.Button(T("更新", "Refresh"),
                        EditorStyles.toolbarButton, GUILayout.Width(62f)))
                {
                    Refresh();
                }
                GUILayout.FlexibleSpace();
                int nextLanguage = GUILayout.Toolbar(
                    (int)_language, new[] { "日本語", "English" },
                    EditorStyles.toolbarButton, GUILayout.Width(132f));
                if (nextLanguage != (int)_language)
                {
                    _language = (AvatarMaterialStudioLanguage)nextLanguage;
                    _preview.Language = _language;
                    Refresh();
                }
            }
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                _workspace = (AvatarMaterialWorkspace)GUILayout.Toolbar(
                    (int)_workspace,
                    new[]
                    {
                        T("マテリアルとTexture", "Materials & Textures"),
                        T("ライティング", "Lighting"),
                        T("Render Queue", "Render Queue"),
                        T("描画Bounds", "Renderer Bounds"),
                    },
                    EditorStyles.toolbarButton,
                    GUILayout.ExpandWidth(true));
            }
        }

        private void DrawMaterialDetails()
        {
            _materialDetail = (AvatarMaterialDetail)GUILayout.Toolbar(
                (int)_materialDetail,
                new[] { T("選択Material", "Selected Material"),
                    T("Texture一覧", "Texture Inventory") });
            EditorGUILayout.Space(3f);
            if (_materialDetail == AvatarMaterialDetail.TextureInventory)
            {
                DrawTextureInventory();
            }
            else
            {
                DrawSelectedMaterial();
            }
        }

        private void DrawMaterialSlots()
        {
            EditorGUILayout.LabelField(
                T("Materialスロット (", "Material Slots (") + _slots.Count + ")",
                EditorStyles.boldLabel);
            _search = EditorGUILayout.TextField(T("検索", "Search"), _search);
            _slotScroll = EditorGUILayout.BeginScrollView(_slotScroll);

            string lastPath = null;
            foreach (MaterialSlotEntry slot in _slots)
            {
                string materialName = slot.Material != null ? slot.Material.name : T("未設定", "Missing");
                string shaderName = slot.Material != null && slot.Material.shader != null
                    ? slot.Material.shader.name
                    : string.Empty;
                if (!MatchesSearch(slot.RendererPath, materialName, shaderName))
                {
                    continue;
                }

                if (slot.RendererPath != lastPath)
                {
                    if (lastPath != null)
                    {
                        EditorGUILayout.Space(3f);
                    }
                    lastPath = slot.RendererPath;
                    EditorGUILayout.LabelField(slot.RendererPath, EditorStyles.boldLabel);
                    EditorGUILayout.LabelField(
                        slot.Renderer.GetType().Name, EditorStyles.miniLabel);
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.PrefixLabel(T("スロット ", "Slot ") + slot.SlotIndex);
                    Material next = (Material)EditorGUILayout.ObjectField(
                        slot.Material, typeof(Material), false);
                    if (GUILayout.Button(T("選択", "Select"), GUILayout.Width(52f)))
                    {
                        _selectedMaterial = slot.Material;
                        if (_selectedMaterial != null)
                        {
                            EditorGUIUtility.PingObject(_selectedMaterial);
                        }
                    }
                    if (next != slot.Material)
                    {
                        AvatarMaterialEditorActions.SetMaterial(slot.Renderer, slot.SlotIndex, next);
                        _selectedMaterial = next;
                        Refresh();
                        GUIUtility.ExitGUI();
                    }
                }

                if (slot.Material != null && slot.Material.shader != null)
                {
                    string fallbackTag = slot.Material.GetTag(
                        "VRCFallback", false, string.Empty);
                    FallbackShaderKind fallback = AvatarShaderRules.ResolveFallback(
                        fallbackTag,
                        slot.Material.shader.name,
                        slot.Material.HasProperty("_Ramp"),
                        slot.Material.IsKeywordEnabled("_ALPHABLEND_ON"),
                        slot.Material.IsKeywordEnabled("_ALPHATEST_ON"));
                    string quest = AvatarShaderRules.IsQuestAvatarShader(slot.Material.shader.name)
                        ? T("Quest: 対応", "Quest: supported")
                        : T("Quest: 変換が必要", "Quest: conversion required");
                    EditorGUILayout.LabelField(
                        slot.Material.shader.name + " | Fallback: " + fallback + " | " + quest,
                        EditorStyles.miniLabel);
                }
            }
            EditorGUILayout.EndScrollView();
        }

        private void DrawSelectedMaterial()
        {
            EditorGUILayout.LabelField(T("MaterialとTexture", "Material and Textures"),
                EditorStyles.boldLabel);
            Material next = (Material)EditorGUILayout.ObjectField(
                T("選択Material", "Selected Material"), _selectedMaterial, typeof(Material), false);
            if (next != _selectedMaterial)
            {
                _selectedMaterial = next;
            }

            if (_selectedMaterial == null)
            {
                EditorGUILayout.HelpBox(T("Textureを編集するMaterialスロットを選択してください。",
                    "Select a material slot to edit its textures."), MessageType.Info);
                return;
            }

            int uses = AvatarMaterialCatalog.CountUses(_slots, _selectedMaterial);
            string path = AssetDatabase.GetAssetPath(_selectedMaterial);
            EditorGUILayout.LabelField(T("Avatar内の使用数", "Uses in avatar"), uses.ToString());
            EditorGUILayout.LabelField("Asset", string.IsNullOrEmpty(path)
                ? T("Scene内 / メモリ上", "Scene/in-memory") : path);

            _propertyScroll = EditorGUILayout.BeginScrollView(
                _propertyScroll, GUILayout.MinHeight(130f), GUILayout.MaxHeight(260f));
            foreach (TexturePropertyEntry property in
                     AvatarMaterialCatalog.CollectTextures(_selectedMaterial))
            {
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        Rect thumbnailRect = GUILayoutUtility.GetRect(
                            56f, 56f, GUILayout.Width(56f), GUILayout.Height(56f));
                        if (property.Texture != null)
                        {
                            Texture thumbnail = AssetPreview.GetAssetPreview(property.Texture)
                                ?? AssetPreview.GetMiniThumbnail(property.Texture);
                            if (thumbnail != null && Event.current.type == EventType.Repaint)
                            {
                                GUI.DrawTexture(
                                    thumbnailRect, thumbnail, ScaleMode.ScaleToFit, true);
                            }
                            if (AssetPreview.IsLoadingAssetPreview(property.Texture.GetInstanceID()))
                            {
                                Repaint();
                            }
                        }
                        else if (Event.current.type == EventType.Repaint)
                        {
                            EditorGUI.DrawRect(thumbnailRect, new Color(0.16f, 0.16f, 0.16f));
                        }

                        using (new EditorGUILayout.VerticalScope())
                        {
                            string label = string.IsNullOrEmpty(property.DisplayName)
                                ? property.Name
                                : property.DisplayName + " (" + property.Name + ")";
                            Texture texture = (Texture)EditorGUILayout.ObjectField(
                                label, property.Texture, typeof(Texture), false);
                            if (texture != property.Texture)
                            {
                                AvatarMaterialEditorActions.SetTexture(
                                    _selectedMaterial, property.Name, texture);
                                Refresh();
                                GUIUtility.ExitGUI();
                            }

                            Vector2 scale = _selectedMaterial.GetTextureScale(property.Name);
                            Vector2 offset = _selectedMaterial.GetTextureOffset(property.Name);
                            Vector2 nextScale = EditorGUILayout.Vector2Field(T("タイリング", "Tiling"), scale);
                            Vector2 nextOffset = EditorGUILayout.Vector2Field(T("オフセット", "Offset"), offset);
                            if (nextScale != scale || nextOffset != offset)
                            {
                                AvatarMaterialEditorActions.SetTextureScaleOffset(
                                    _selectedMaterial, property.Name, nextScale, nextOffset);
                                _preview.Rebuild(_target, _previewMode);
                            }
                        }
                    }
                }
            }
            EditorGUILayout.EndScrollView();
        }

        private void DrawTextureInventory()
        {
            EditorGUILayout.LabelField(
                T("使用中の全Texture (", "All textures in use (")
                + _textureInventory.Count + ")", EditorStyles.boldLabel);
            int assignmentCount = 0;
            foreach (TextureUsageEntry usageEntry in _textureInventory)
            {
                assignmentCount += usageEntry.Properties.Count;
            }
            EditorGUILayout.LabelField(
                T("一意Texture: ", "Unique textures: ") + _textureInventory.Count
                + T(" / Property割当: ", " / property assignments: ") + assignmentCount
                + T(" / 同一ソース群: ", " / identical-source groups: ")
                + CountDuplicateTextureGroups(), EditorStyles.miniLabel);
            _inventorySearch = EditorGUILayout.TextField(T("検索", "Search"), _inventorySearch);
            if (_textureInventory.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    T("AvatarのMaterialに割り当てられたTextureはありません。",
                        "No assigned textures were found in the avatar's materials."),
                    MessageType.Info);
                return;
            }

            _inventoryScroll = EditorGUILayout.BeginScrollView(_inventoryScroll);
            foreach (TextureUsageEntry entry in _textureInventory)
            {
                string assetPath = entry.AssetPath;
                if (!MatchesInventorySearch(entry, assetPath))
                {
                    continue;
                }

                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        DrawTextureThumbnail(entry.Texture, 64f);
                        using (new EditorGUILayout.VerticalScope())
                        {
                            EditorGUILayout.LabelField(entry.Texture.name, EditorStyles.boldLabel);
                            EditorGUILayout.LabelField(
                                entry.Texture.GetType().Name + " | " +
                                entry.Texture.width + " x " + entry.Texture.height,
                                EditorStyles.miniLabel);
                            EditorGUILayout.LabelField(
                                entry.Properties.Count + T(" Property割当 | ", " property assignment(s) | ") +
                                entry.MaterialSlotCount + T(" Rendererスロット", " renderer slot(s)"),
                                EditorStyles.miniLabel);
                            EditorGUILayout.LabelField(
                                string.IsNullOrEmpty(assetPath)
                                    ? T("Scene内 / メモリ上", "Scene/in-memory") : assetPath,
                                EditorStyles.wordWrappedMiniLabel);
                            if (entry.DuplicateSourceCount > 1)
                            {
                                EditorGUILayout.HelpBox(
                                    T("同じファイル内容のTexture Assetが ",
                                        "Identical file content is used by ")
                                    + entry.DuplicateSourceCount
                                    + T(" 個あります。SHA-256: ", " texture assets. SHA-256: ")
                                    + entry.SourceHash.Substring(0, 12), MessageType.Warning);
                                foreach (string duplicatePath in entry.DuplicateSourcePaths)
                                {
                                    EditorGUILayout.LabelField("= " + duplicatePath,
                                        EditorStyles.wordWrappedMiniLabel);
                                }
                            }
                            if (GUILayout.Button(T("Assetを表示", "Ping Asset"), GUILayout.Width(88f)))
                            {
                                EditorGUIUtility.PingObject(entry.Texture);
                            }
                        }
                    }

                    int instanceId = entry.Texture.GetInstanceID();
                    _textureFoldouts.TryGetValue(instanceId, out bool expanded);
                    bool nextExpanded = EditorGUILayout.Foldout(
                        expanded, T("Material Property割当", "Material property assignments"), true);
                    _textureFoldouts[instanceId] = nextExpanded;
                    if (!nextExpanded)
                    {
                        continue;
                    }

                    EditorGUI.indentLevel++;
                    foreach (TexturePropertyUsage usage in entry.Properties)
                    {
                        if (usage.Material == null || !usage.Material.HasProperty(usage.PropertyName))
                        {
                            continue;
                        }

                        string propertyLabel = string.IsNullOrEmpty(usage.DisplayName)
                            ? usage.PropertyName
                            : usage.DisplayName + " (" + usage.PropertyName + ")";
                        EditorGUILayout.LabelField(
                            usage.Material.name + " | " + usage.MaterialSlotCount
                            + T(" スロット", " slot(s)"),
                            EditorStyles.miniBoldLabel);
                        Texture current = usage.Material.GetTexture(usage.PropertyName);
                        Texture next = (Texture)EditorGUILayout.ObjectField(
                            propertyLabel, current, typeof(Texture), false);
                        if (next != current)
                        {
                            AvatarMaterialEditorActions.SetTexture(
                                usage.Material, usage.PropertyName, next);
                            Refresh();
                            GUIUtility.ExitGUI();
                        }
                    }
                    EditorGUI.indentLevel--;
                }
            }
            EditorGUILayout.EndScrollView();
        }

        private void DrawVisualization()
        {
            EditorGUILayout.LabelField(T("ライティング可視化", "Lighting Visualization"),
                EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUI.BeginChangeCheck();
                _previewMode = (AvatarPreviewMode)EditorGUILayout.Popup(
                    T("表示モード", "Display Mode"), (int)_previewMode,
                    new[] { T("オリジナル", "Original"), "Fallback", "Quest" },
                    GUILayout.MaxWidth(320f));
                if (EditorGUI.EndChangeCheck())
                {
                    _preview.Rebuild(_target, _previewMode);
                }
                GUILayout.FlexibleSpace();
            }
            DrawCameraControls();

            if (_previewMode != AvatarPreviewMode.Original)
            {
                EditorGUILayout.HelpBox(
                    _previewMode == AvatarPreviewMode.Quest
                        ? T("Questモードは設定済みならAndroidプラットフォームOverrideを使用します。未設定時の非対応Materialはモバイル向け近似表示です。",
                            "Quest mode uses the Android per-platform override when configured. " +
                            "Without one, unsupported materials are only an approximate mobile conversion.")
                        : T("FallbackモードはVRChatの公開Shaderブロック規則に基づく近似です。最終結果はVRChatクライアントで確認してください。",
                            "Fallback mode approximates VRChat's documented shader blocking rules. " +
                            "Verify the final result in the VRChat client."),
                    MessageType.Warning);
            }
            if (!string.IsNullOrEmpty(_preview.Status))
            {
                EditorGUILayout.LabelField(_preview.Status, EditorStyles.wordWrappedMiniLabel);
            }

            AvatarLightingScenario[] scenarios =
                (AvatarLightingScenario[])System.Enum.GetValues(typeof(AvatarLightingScenario));
            float desiredCellWidth = ScalePreviewDimension(300f, _previewUiScale);
            int columns = Mathf.Clamp(
                Mathf.FloorToInt((position.width - 24f) / desiredCellWidth), 1, 4);
            float cellWidth = Mathf.Max(
                ScalePreviewDimension(240f, _previewUiScale),
                (position.width - 22f - 6f * (columns - 1)) / columns);

            _visualizationScroll = EditorGUILayout.BeginScrollView(_visualizationScroll);
            for (int row = 0; row * columns < scenarios.Length; row++)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    for (int column = 0; column < columns; column++)
                    {
                        int index = row * columns + column;
                        if (index >= scenarios.Length)
                        {
                            GUILayout.FlexibleSpace();
                            continue;
                        }

                        AvatarLightingScenario scenario = scenarios[index];
                        using (new EditorGUILayout.VerticalScope(
                                   EditorStyles.helpBox, GUILayout.Width(cellWidth)))
                        {
                            EditorGUILayout.LabelField(
                                LightingScenarioName(scenario),
                                EditorStyles.boldLabel);
                            Rect previewRect = GUILayoutUtility.GetRect(
                                cellWidth - 12f,
                                ScalePreviewDimension(215f, _previewUiScale),
                                GUILayout.ExpandWidth(true));
                            _preview.Draw(previewRect, scenario, CreatePreviewOptions());
                        }
                    }
                }
            }
            EditorGUILayout.EndScrollView();
        }

        private void DrawCameraControls()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    _cameraMode = (AvatarPreviewCameraMode)EditorGUILayout.Popup(
                        T("カメラ", "Camera"), (int)_cameraMode,
                        new[] { T("オービット", "Orbit"), T("自由移動", "Free Fly"),
                            T("Sceneビュー追従", "Scene View Follow") },
                        GUILayout.MaxWidth(310f));
                    if (GUILayout.Button(
                            T("カメラをリセット", "Reset Camera"), GUILayout.Width(112f)))
                    {
                        _preview.ResetCamera();
                        Repaint();
                    }
                    _previewUiScale = EditorGUILayout.Slider(
                        T("View UI倍率", "View UI Scale"), _previewUiScale,
                        MinimumPreviewUiScale, MaximumPreviewUiScale,
                        GUILayout.MaxWidth(285f));
                    if (GUILayout.Button("100%", GUILayout.Width(48f)))
                    {
                        _previewUiScale = 1f;
                    }
                }
                string help = _cameraMode == AvatarPreviewCameraMode.FreeFly
                    ? T("右ドラッグ: 視点 / 中ドラッグ: 平行移動 / Wheel: 前後",
                        "RMB: look / MMB: pan / Wheel: forward-back")
                    : _cameraMode == AvatarPreviewCameraMode.SceneViewFollow
                        ? T("最後に操作したSceneビューのカメラに追従",
                            "Follows the last active Scene view camera")
                        : T("左ドラッグ: 回転 / 中ドラッグ: 平行移動 / Wheel: Zoom",
                            "LMB: orbit / MMB: pan / Wheel: zoom");
                using (new EditorGUILayout.HorizontalScope())
                {
                    _gizmoMode = (AvatarPreviewGizmoMode)EditorGUILayout.Popup(
                        T("Gizmo表示", "Gizmos"), (int)_gizmoMode,
                        new[]
                        {
                            T("なし", "None"),
                            T("簡易", "Compact"),
                            T("詳細", "Detailed"),
                        },
                        GUILayout.MaxWidth(250f));
                    EditorGUILayout.LabelField(help, EditorStyles.miniLabel);
                }
            }
        }

        internal static float ScalePreviewDimension(float baseDimension, float scale)
        {
            return baseDimension * Mathf.Clamp(
                scale, MinimumPreviewUiScale, MaximumPreviewUiScale);
        }

        private AvatarPreviewRenderOptions CreatePreviewOptions()
        {
            return new AvatarPreviewRenderOptions
            {
                CameraMode = _cameraMode,
                QueueVisibility = RenderQueueVisibilityMode.ShowAll,
                QueueRange = new Vector2Int(0, 5000),
                DrawLightingGizmos = true,
                GizmoMode = _gizmoMode,
            };
        }

        private void DrawRenderQueueDiagnostics()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(T("Render Queueと半透明", "Render Queue and Transparency"),
                    EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button(
                        _showRenderQueueSettings
                            ? T("設定一覧を閉じる", "Hide Settings")
                            : T("設定一覧を開く", "Show Settings"),
                        GUILayout.Width(124f)))
                {
                    _showRenderQueueSettings = !_showRenderQueueSettings;
                }
            }
            DrawCameraControls();

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    _transparencyProbeEnabled = EditorGUILayout.ToggleLeft(
                        T("手前の半透明World Object Probe", "Front transparent world-object probe"),
                        _transparencyProbeEnabled, GUILayout.Width(244f));
                    _transparencyProbeAlpha = EditorGUILayout.Slider(
                        T("透明度", "Alpha"), _transparencyProbeAlpha, 0.05f, 0.95f);
                    _transparencyProbeDistance = EditorGUILayout.Slider(
                        T("カメラからの距離", "Camera distance"),
                        _transparencyProbeDistance, 0.1f, 0.9f);
                    _transparencyProbeZWrite = EditorGUILayout.ToggleLeft(
                        "ZWrite", _transparencyProbeZWrite, GUILayout.Width(65f));
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (_showRenderQueueSettings)
                {
                    using (new EditorGUILayout.VerticalScope(GUILayout.Width(_renderQueueLeftWidth)))
                    {
                        DrawRenderQueueList();
                    }
                    DrawSplitter(ref _renderQueueLeftWidth, 2, 280f);
                }
                using (new EditorGUILayout.VerticalScope())
                {
                    _queuePreviewScroll = EditorGUILayout.BeginScrollView(_queuePreviewScroll);
                    DrawQueueVisibilityMatrix();
                    EditorGUILayout.Space(8f);
                    DrawTransparencyProbeMatrix();
                    EditorGUILayout.EndScrollView();
                }
            }
        }

        private void DrawQueueVisibilityMatrix()
        {
            EditorGUILayout.LabelField(
                T("Material slot表示 / Queue範囲 比較",
                    "Material Slot Visibility / Queue Range Comparison"),
                EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                T("基準表示と、各Queueに該当するslotのみ／該当slotを除外した結果を比較します。変更はPreview Cloneだけに適用されます。",
                    "Compare the baseline with slots inside each queue range and with those slots excluded. Changes affect only the preview clone."),
                EditorStyles.wordWrappedMiniLabel);

            EditorGUILayout.LabelField(T("基準表示", "Baseline"), EditorStyles.miniBoldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                DrawQueuePreviewCard(
                    T("全slot", "All Slots"),
                    RenderQueueVisibilityMode.ShowAll,
                    new Vector2Int(0, 5000));
                GUILayout.FlexibleSpace();
            }
            EditorGUILayout.Space(6f);

            RenderQueueRangePreset[] presets =
            {
                RenderQueueRangePreset.Background,
                RenderQueueRangePreset.Geometry,
                RenderQueueRangePreset.AlphaTest,
                RenderQueueRangePreset.GeometryLast,
                RenderQueueRangePreset.Transparent,
                RenderQueueRangePreset.Overlay,
            };
            RenderQueueVisibilityMode[] modes =
            {
                RenderQueueVisibilityMode.OnlySelectedRange,
                RenderQueueVisibilityMode.ExcludeSelectedRange,
            };
            foreach (RenderQueueRangePreset preset in presets)
            {
                Vector2Int range = AvatarRenderDiagnostics.ResolveQueueRange(preset, 0, 5000);
                EditorGUILayout.LabelField(
                    QueuePresetName(preset) + "  " + range.x + ".." + range.y,
                    EditorStyles.miniBoldLabel);
                using (new EditorGUILayout.HorizontalScope())
                {
                    foreach (RenderQueueVisibilityMode mode in modes)
                    {
                        DrawQueuePreviewCard(QueueVisibilityName(mode), mode, range);
                    }
                }
            }
        }

        private void DrawQueuePreviewCard(
            string title, RenderQueueVisibilityMode mode, Vector2Int range)
        {
            using (new EditorGUILayout.VerticalScope(
                       EditorStyles.helpBox,
                       GUILayout.MinWidth(ScalePreviewDimension(
                           220f, _previewUiScale) + 12f)))
            {
                EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
                Rect previewRect = GUILayoutUtility.GetRect(
                    ScalePreviewDimension(220f, _previewUiScale),
                    ScalePreviewDimension(176f, _previewUiScale),
                    GUILayout.ExpandWidth(true));
                _preview.Draw(previewRect,
                    AvatarLightingScenario.DirectionalNeutral,
                    new AvatarPreviewRenderOptions
                    {
                        CameraMode = _cameraMode,
                        QueueVisibility = mode,
                        QueueRange = range,
                        DrawRenderQueueLabels = true,
                        DrawQueueFilterStatus = true,
                        GizmoMode = _gizmoMode,
                    });
            }
        }

        private void DrawRenderQueueList()
        {
            int issueCount = 0;
            foreach (RenderQueueEntry entry in _renderQueueEntries)
            {
                issueCount += entry.Issues.Count;
            }
            EditorGUILayout.LabelField(
                T("Mesh / Material設定 (", "Mesh / Material settings (")
                + _renderQueueEntries.Count + T(", 問題 ", ", issues ")
                + issueCount + ")", EditorStyles.boldLabel);
            _renderQueueSearch = EditorGUILayout.TextField(T("検索", "Search"), _renderQueueSearch);
            _renderQueueScroll = EditorGUILayout.BeginScrollView(_renderQueueScroll);
            foreach (RenderQueueEntry entry in _renderQueueEntries)
            {
                Material material = entry.Slot.Material;
                string materialName = material != null ? material.name : T("未設定", "Missing");
                if (!MatchesText(entry.Slot.RendererPath, _renderQueueSearch)
                    && !MatchesText(materialName, _renderQueueSearch)
                    && !MatchesText(entry.RenderType, _renderQueueSearch)
                    && !string.IsNullOrWhiteSpace(_renderQueueSearch))
                {
                    continue;
                }

                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.LabelField(entry.Slot.RendererPath, EditorStyles.boldLabel);
                    EditorGUILayout.LabelField(
                        entry.Slot.Renderer.GetType().Name + " / slot " + entry.Slot.SlotIndex
                        + " / " + materialName,
                        EditorStyles.miniLabel);
                    if (material != null)
                    {
                        int queue = EditorGUILayout.IntField(T("有効Queue", "Effective Queue"),
                            entry.EffectiveQueue);
                        if (queue != entry.EffectiveQueue)
                        {
                            AvatarMaterialEditorActions.SetRenderQueue(
                                material, Mathf.Clamp(queue, -1, 5000));
                            Refresh();
                            GUIUtility.ExitGUI();
                        }
                    }
                    EditorGUILayout.LabelField(
                        T("区分", "Category"), AvatarRenderDiagnostics.QueueName(entry.EffectiveQueue));
                    EditorGUILayout.LabelField(T("Shader既定値", "Shader default"),
                        entry.ShaderQueue.ToString());
                    EditorGUILayout.LabelField(
                        "RenderType", string.IsNullOrEmpty(entry.RenderType)
                            ? "(not declared)"
                            : entry.RenderType);
                    EditorGUILayout.LabelField(
                        "Depth / Cull / Blend",
                        "ZWrite " + NullableInt(entry.ZWrite)
                        + ", ZTest " + NullableInt(entry.ZTest)
                        + ", Cull " + NullableInt(entry.Cull)
                        + ", Src " + NullableInt(entry.SrcBlend)
                        + ", Dst " + NullableInt(entry.DstBlend));
                    EditorGUILayout.LabelField(
                        T("描画順", "Sorting"), entry.Slot.Renderer.sortingLayerName
                        + " / order " + entry.Slot.Renderer.sortingOrder
                        + " / priority " + entry.Slot.Renderer.rendererPriority);
                    foreach (string issue in entry.Issues)
                    {
                        EditorGUILayout.HelpBox(LocalizeDiagnosticIssue(issue), MessageType.Warning);
                    }
                }
            }
            EditorGUILayout.EndScrollView();
        }

        private void DrawTransparencyProbeMatrix()
        {
            EditorGUILayout.LabelField(
                T("手前の半透明Object: Queue比較", "Front transparent object: queue comparison"),
                EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                T("水色のPlaneをカメラとAvatarの間に置き、消失、誤ったBlend、深度順の変化をQueue別に比較します。",
                    "The cyan plane is placed between the camera and avatar. Compare disappearance, "
                    + "incorrect blending and depth-order changes across queues."),
                EditorStyles.wordWrappedMiniLabel);
            int[] queues = { 2501, 3000, 3100, 4000 };
            const int columns = 2;
            for (int row = 0; row < 2; row++)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    for (int column = 0; column < columns; column++)
                    {
                        int queue = queues[row * columns + column];
                        using (new EditorGUILayout.VerticalScope(
                                   EditorStyles.helpBox,
                                   GUILayout.MinWidth(ScalePreviewDimension(
                                       230f, _previewUiScale) + 12f),
                                   GUILayout.ExpandWidth(true)))
                        {
                            EditorGUILayout.LabelField(
                                T("Probe Queue ", "Probe queue ") + queue + " ("
                                + AvatarRenderDiagnostics.QueueName(queue) + ")",
                                EditorStyles.boldLabel);
                            Rect previewRect = GUILayoutUtility.GetRect(
                                ScalePreviewDimension(230f, _previewUiScale),
                                ScalePreviewDimension(210f, _previewUiScale),
                                GUILayout.ExpandWidth(true));
                            AvatarPreviewRenderOptions options = CreateQueuePreviewOptions(queue);
                            _preview.Draw(
                                previewRect, AvatarLightingScenario.DirectionalNeutral, options);
                        }
                    }
                }
            }
        }

        private AvatarPreviewRenderOptions CreateQueuePreviewOptions(int probeQueue)
        {
            return new AvatarPreviewRenderOptions
            {
                CameraMode = _cameraMode,
                QueueVisibility = RenderQueueVisibilityMode.ShowAll,
                QueueRange = new Vector2Int(0, 5000),
                TransparencyProbe = _transparencyProbeEnabled,
                TransparencyProbeQueue = probeQueue,
                TransparencyProbeAlpha = _transparencyProbeAlpha,
                TransparencyProbeZWrite = _transparencyProbeZWrite,
                TransparencyProbeDistance = _transparencyProbeDistance,
                DrawRenderQueueLabels = _gizmoMode == AvatarPreviewGizmoMode.Detailed,
                GizmoMode = _gizmoMode,
            };
        }

        private static string NullableInt(int? value)
        {
            return value.HasValue ? value.Value.ToString() : "n/a";
        }

        private void DrawRendererBoundsDiagnostics()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(T("Renderer描画Bounds", "Renderer Bounds"),
                    EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button(
                        _showRendererBoundsList
                            ? T("Renderer一覧を閉じる", "Hide Renderers")
                            : T("Renderer一覧を開く", "Show Renderers"),
                        GUILayout.Width(132f)))
                {
                    _showRendererBoundsList = !_showRendererBoundsList;
                }
            }
            EditorGUILayout.HelpBox(
                T("48点のProbeはAvatarのViewPositionを注視し、Camera FrustumとRenderer.boundsを検査します。UnityのAABB判定は保守的なため、固定Previewでも見た目を確認してください。",
                    "The 48 probes use the avatar ViewPosition and camera frustum against Renderer.bounds. "
                    + "Unity's AABB test is conservative, so use the fixed probe previews for final visual confirmation."),
                MessageType.Info);

            EditorGUI.BeginChangeCheck();
            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                _nearBoundaryDistance = EditorGUILayout.FloatField(
                    T("近距離", "Near"), _nearBoundaryDistance, GUILayout.MaxWidth(210f));
                _middleBoundaryDistance = EditorGUILayout.FloatField(
                    T("中距離", "Middle"), _middleBoundaryDistance, GUILayout.MaxWidth(210f));
                _farBoundaryDistance = EditorGUILayout.FloatField(
                    T("遠距離", "Far"), _farBoundaryDistance, GUILayout.MaxWidth(210f));
                _boundaryFieldOfView = EditorGUILayout.Slider(
                    "FOV", _boundaryFieldOfView, 20f, 120f);
                _drawAllBounds = EditorGUILayout.ToggleLeft(
                    T("全Boundsを表示", "Draw all bounds"), _drawAllBounds, GUILayout.Width(126f));
            }
            if (EditorGUI.EndChangeCheck())
            {
                _nearBoundaryDistance = Mathf.Max(0.01f, _nearBoundaryDistance);
                _middleBoundaryDistance = Mathf.Max(0.01f, _middleBoundaryDistance);
                _farBoundaryDistance = Mathf.Max(0.01f, _farBoundaryDistance);
                RefreshBoundaryProbes();
            }

            DrawBoundaryProbeMatrix();
            EditorGUILayout.LabelField(T("ユーザーカメラ操作", "User Camera Controls"),
                EditorStyles.miniBoldLabel);
            DrawCameraControls();
            using (new EditorGUILayout.HorizontalScope())
            {
                if (_showRendererBoundsList)
                {
                    using (new EditorGUILayout.VerticalScope(GUILayout.Width(_rendererBoundsLeftWidth)))
                    {
                        DrawRendererBoundsList();
                    }
                    DrawSplitter(ref _rendererBoundsLeftWidth, 3, 280f);
                }
                using (new EditorGUILayout.VerticalScope())
                {
                    _boundsPreviewScroll = EditorGUILayout.BeginScrollView(_boundsPreviewScroll);
                    DrawBoundaryPreviews();
                    DrawBoundaryPositionDiagram();
                    EditorGUILayout.EndScrollView();
                }
            }
        }

        private void DrawBoundaryProbeMatrix()
        {
            string[] distanceLabels =
                { T("近", "Near"), T("中", "Middle"), T("遠", "Far") };
            string selectedPath = SelectedBoundsPath();
            for (int distanceIndex = 0; distanceIndex < 3; distanceIndex++)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(
                        distanceLabels[distanceIndex], EditorStyles.boldLabel, GUILayout.Width(52f));
                    for (int directionIndex = 0; directionIndex < 16; directionIndex++)
                    {
                        BoundaryProbeResult probe = GetBoundaryProbe(
                            distanceIndex, directionIndex);
                        bool selectedRendererVisible = probe != null
                            && (string.IsNullOrEmpty(selectedPath)
                                ? probe.AllVisible
                                : probe.VisibleRendererPaths.Contains(selectedPath));
                        Color previous = GUI.backgroundColor;
                        if (probe == null || probe.NoneVisible)
                        {
                            GUI.backgroundColor = new Color(1f, 0.35f, 0.35f);
                        }
                        else if (!selectedRendererVisible || !probe.AllVisible)
                        {
                            GUI.backgroundColor = new Color(1f, 0.72f, 0.25f);
                        }
                        else
                        {
                            GUI.backgroundColor = new Color(0.35f, 0.9f, 0.45f);
                        }

                        bool selected = directionIndex == _selectedBoundaryDirection;
                        string label = selected ? "[" + directionIndex + "]" : directionIndex.ToString();
                        string tooltip = probe == null
                            ? T("未評価", "Not evaluated")
                            : probe.VisibleRendererCount + "/" + probe.TotalRendererCount
                              + T(" 個のRenderer BoundsがFrustum内", " renderer bounds intersect the frustum");
                        if (GUILayout.Button(
                                new GUIContent(label, tooltip), GUILayout.MinWidth(30f)))
                        {
                            _selectedBoundaryDirection = directionIndex;
                        }
                        GUI.backgroundColor = previous;
                    }
                }
            }
            EditorGUILayout.LabelField(
                T("方向0–7: 水平45度刻み、8–11: 上方斜め、12–15: 下方斜め。緑: 全て内側、橙: 一部または選択対象が外側、赤: 全て外側。方向を選ぶと近・中・遠のPreviewを同時表示します。",
                    "Directions 0-7: horizontal every 45 degrees; 8-11: upper diagonals; "
                    + "12-15: lower diagonals. Green: all; amber: partial/selected outside; red: none. Selecting a direction shows Near, Middle and Far together."),
                EditorStyles.wordWrappedMiniLabel);
        }

        private void DrawRendererBoundsList()
        {
            int issueCount = 0;
            foreach (RendererBoundsEntry entry in _rendererBounds)
            {
                issueCount += entry.Issues.Count;
            }
            EditorGUILayout.LabelField(
                T("Mesh Renderer (", "Mesh renderers (") + _rendererBounds.Count
                + T(", 問題 ", ", issues ") + issueCount + ")",
                EditorStyles.boldLabel);
            _boundsScroll = EditorGUILayout.BeginScrollView(_boundsScroll);
            foreach (RendererBoundsEntry entry in _rendererBounds)
            {
                bool selected = entry.Renderer == _selectedBoundsRenderer;
                using (new EditorGUILayout.VerticalScope(
                           selected ? EditorStyles.helpBox : GUI.skin.box))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.LabelField(entry.RendererPath, EditorStyles.boldLabel);
                        if (GUILayout.Button(selected ? T("選択中", "Selected") : T("選択", "Select"),
                                GUILayout.Width(62f)))
                        {
                            _selectedBoundsRenderer = entry.Renderer;
                            EditorGUIUtility.PingObject(entry.Renderer.gameObject);
                            RefreshBoundaryProbes();
                        }
                    }
                    EditorGUILayout.LabelField(
                        entry.Renderer.GetType().Name + " / submeshes " + entry.SubMeshCount
                        + " / materials " + entry.Renderer.sharedMaterials.Length,
                        EditorStyles.miniLabel);
                    EditorGUILayout.LabelField(
                        T("Local 中心 / サイズ", "Local center / size"),
                        FormatVector(entry.LocalBounds.center) + " / "
                        + FormatVector(entry.LocalBounds.size));
                    EditorGUILayout.LabelField(
                        T("World 中心 / サイズ", "World center / size"),
                        FormatVector(entry.WorldBounds.center) + " / "
                        + FormatVector(entry.WorldBounds.size));
                    if (entry.Renderer is SkinnedMeshRenderer)
                    {
                        EditorGUILayout.LabelField(
                            "Update When Offscreen", entry.UpdateWhenOffscreen.ToString());
                    }
                    foreach (string issue in entry.Issues)
                    {
                        EditorGUILayout.HelpBox(LocalizeDiagnosticIssue(issue), MessageType.Warning);
                    }
                }
            }
            EditorGUILayout.EndScrollView();
        }

        private void DrawBoundaryPreviews()
        {
            if (GetBoundaryProbe(0, _selectedBoundaryDirection) == null)
            {
                EditorGUILayout.HelpBox(T("Boundary Probeを利用できません。",
                    "Boundary probes are not available."), MessageType.Info);
                return;
            }

            EditorGUILayout.LabelField(
                T("選択方向の固定Probe + ユーザーカメラ", "Fixed Probes + User Camera"),
                EditorStyles.boldLabel);
            for (int row = 0; row < 2; row++)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    for (int column = 0; column < 2; column++)
                    {
                        int index = row * 2 + column;
                        using (new EditorGUILayout.VerticalScope(
                                   EditorStyles.helpBox,
                                   GUILayout.MinWidth(ScalePreviewDimension(
                                       230f, _previewUiScale) + 12f)))
                        {
                            if (index < 3)
                            {
                                DrawFixedBoundaryPreview(index);
                            }
                            else
                            {
                                DrawUserBoundaryPreview();
                            }
                        }
                    }
                }
            }
        }

        private void DrawFixedBoundaryPreview(int distanceIndex)
        {
            BoundaryProbeResult probe = GetBoundaryProbe(distanceIndex, _selectedBoundaryDirection);
            string[] labels = { T("近距離", "Near"), T("中距離", "Middle"), T("遠距離", "Far") };
            EditorGUILayout.LabelField(labels[distanceIndex] + " / "
                + T("方向 ", "Direction ") + _selectedBoundaryDirection,
                EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                T("位置 ", "Position ") + FormatVector(probe.CameraPosition)
                + T(" / 表示 ", " / visible ") + probe.VisibleRendererCount
                + "/" + probe.TotalRendererCount, EditorStyles.wordWrappedMiniLabel);
            string selectedPath = SelectedBoundsPath();
            if (!string.IsNullOrEmpty(selectedPath)
                && !probe.VisibleRendererPaths.Contains(selectedPath))
            {
                EditorGUILayout.HelpBox(T("選択RendererのBoundsはこのProbeのFrustum外です。",
                    "The selected renderer bounds is outside this probe frustum."), MessageType.Error);
            }
            Rect previewRect = GUILayoutUtility.GetRect(
                ScalePreviewDimension(230f, _previewUiScale),
                ScalePreviewDimension(220f, _previewUiScale),
                GUILayout.ExpandWidth(true));
            var options = new AvatarPreviewRenderOptions
            {
                CameraOverride = true,
                CameraPosition = probe.CameraPosition,
                CameraRotation = probe.CameraRotation,
                CameraFieldOfView = _boundaryFieldOfView,
                QueueVisibility = RenderQueueVisibilityMode.ShowAll,
                QueueRange = new Vector2Int(0, 5000),
                DrawBounds = true,
                DrawAllBounds = _drawAllBounds,
                SelectedRendererPath = selectedPath,
                DrawViewPoint = true,
                ViewPoint = GetAvatarViewPoint(),
            };
            _preview.Draw(previewRect, AvatarLightingScenario.DirectionalNeutral, options);
        }

        private void DrawUserBoundaryPreview()
        {
            EditorGUILayout.LabelField(T("ユーザーカメラ", "User Camera"), EditorStyles.boldLabel);
            EditorGUILayout.LabelField(T("固定Probe判定には影響しません。", "Does not modify fixed probe results."),
                EditorStyles.wordWrappedMiniLabel);
            Rect previewRect = GUILayoutUtility.GetRect(
                ScalePreviewDimension(230f, _previewUiScale),
                ScalePreviewDimension(220f, _previewUiScale),
                GUILayout.ExpandWidth(true));
            _preview.Draw(previewRect, AvatarLightingScenario.DirectionalNeutral,
                new AvatarPreviewRenderOptions
                {
                    CameraMode = _cameraMode,
                    QueueVisibility = RenderQueueVisibilityMode.ShowAll,
                    QueueRange = new Vector2Int(0, 5000),
                    DrawBounds = true,
                    DrawAllBounds = _drawAllBounds,
                    SelectedRendererPath = SelectedBoundsPath(),
                    DrawViewPoint = true,
                    ViewPoint = GetAvatarViewPoint(),
                });
        }

        private void DrawBoundaryPositionDiagram()
        {
            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField(
                T("ViewPositionとCameraの位置関係", "ViewPosition / Camera Layout"),
                EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                T("上面図と側面図。T: 被写体のViewPosition、N/M/F: 固定Probe、U: ユーザーカメラ。",
                    "Top and side views. T: target ViewPosition, N/M/F: fixed probes, U: user camera."),
                EditorStyles.wordWrappedMiniLabel);

            BoundaryProbeResult near = GetBoundaryProbe(0, _selectedBoundaryDirection);
            BoundaryProbeResult middle = GetBoundaryProbe(1, _selectedBoundaryDirection);
            BoundaryProbeResult far = GetBoundaryProbe(2, _selectedBoundaryDirection);
            if (near == null || middle == null || far == null)
            {
                return;
            }

            Vector3 target = GetAvatarViewPoint();
            Vector3[] points =
            {
                target,
                near.CameraPosition,
                middle.CameraPosition,
                far.CameraPosition,
                _preview.CurrentCameraPosition,
            };
            string[] labels = { "T", "N", "M", "F", "U" };
            Color[] colors =
            {
                new Color(0.2f, 0.75f, 1f),
                new Color(0.35f, 1f, 0.45f),
                new Color(1f, 0.82f, 0.2f),
                new Color(1f, 0.35f, 0.25f),
                new Color(0.85f, 0.45f, 1f),
            };
            Vector3 horizontalDirection = near.CameraPosition - target;
            horizontalDirection.y = 0f;
            if (horizontalDirection.sqrMagnitude < 0.0001f)
            {
                horizontalDirection = Vector3.forward;
            }
            horizontalDirection.Normalize();

            var top = new Vector2[points.Length];
            var side = new Vector2[points.Length];
            for (int index = 0; index < points.Length; index++)
            {
                Vector3 relative = points[index] - target;
                top[index] = new Vector2(relative.x, relative.z);
                side[index] = new Vector2(Vector3.Dot(relative, horizontalDirection), relative.y);
            }

            Rect outer = GUILayoutUtility.GetRect(
                ScalePreviewDimension(300f, _previewUiScale),
                ScalePreviewDimension(230f, _previewUiScale),
                GUILayout.ExpandWidth(true));
            if (Event.current.type == EventType.Repaint)
            {
                EditorGUI.DrawRect(outer, new Color(0.10f, 0.10f, 0.10f));
            }
            GUI.BeginGroup(outer);
            float panelWidth = (outer.width - 12f) * 0.5f;
            DrawPositionProjection(new Rect(4f, 4f, panelWidth, outer.height - 8f),
                top, labels, colors, T("上面 X/Z", "Top X/Z"));
            DrawPositionProjection(new Rect(8f + panelWidth, 4f, panelWidth, outer.height - 8f),
                side, labels, colors, T("側面 距離/Y", "Side distance/Y"));
            GUI.EndGroup();
        }

        private static void DrawPositionProjection(
            Rect rect, Vector2[] points, string[] labels, Color[] colors, string title)
        {
            if (Event.current.type == EventType.Repaint)
            {
                EditorGUI.DrawRect(rect, new Color(0.14f, 0.14f, 0.14f));
            }
            float minX = points[0].x;
            float maxX = points[0].x;
            float minY = points[0].y;
            float maxY = points[0].y;
            foreach (Vector2 point in points)
            {
                minX = Mathf.Min(minX, point.x);
                maxX = Mathf.Max(maxX, point.x);
                minY = Mathf.Min(minY, point.y);
                maxY = Mathf.Max(maxY, point.y);
            }
            float spanX = Mathf.Max(maxX - minX, 0.1f);
            float spanY = Mathf.Max(maxY - minY, 0.1f);
            minX -= spanX * 0.12f;
            maxX += spanX * 0.12f;
            minY -= spanY * 0.12f;
            maxY += spanY * 0.12f;
            Rect plot = new Rect(rect.x + 12f, rect.y + 24f,
                rect.width - 24f, rect.height - 38f);

            var screen = new Vector2[points.Length];
            for (int index = 0; index < points.Length; index++)
            {
                screen[index] = new Vector2(
                    Mathf.Lerp(plot.xMin, plot.xMax, Mathf.InverseLerp(minX, maxX, points[index].x)),
                    Mathf.Lerp(plot.yMax, plot.yMin, Mathf.InverseLerp(minY, maxY, points[index].y)));
            }

            Handles.BeginGUI();
            Handles.color = new Color(1f, 1f, 1f, 0.2f);
            for (int index = 1; index < screen.Length; index++)
            {
                Handles.DrawDottedLine(screen[0], screen[index], 4f);
            }
            for (int index = 0; index < screen.Length; index++)
            {
                Handles.color = colors[index];
                Handles.DrawSolidDisc(screen[index], Vector3.forward, index == 0 ? 5f : 4f);
                Handles.Label(screen[index] + new Vector2(6f, -8f), labels[index],
                    EditorStyles.miniBoldLabel);
            }
            Handles.EndGUI();
            GUI.Label(new Rect(rect.x + 6f, rect.y + 4f, rect.width - 12f, 18f),
                title, EditorStyles.miniBoldLabel);
        }

        private void RefreshBoundaryProbes()
        {
            _boundaryProbes.Clear();
            if (_target == null)
            {
                return;
            }
            float[] distances =
            {
                Mathf.Max(0.01f, _nearBoundaryDistance),
                Mathf.Max(0.01f, _middleBoundaryDistance),
                Mathf.Max(0.01f, _farBoundaryDistance),
            };
            _boundaryProbes.AddRange(AvatarRenderDiagnostics.EvaluateBoundaryProbes(
                _rendererBounds, GetAvatarViewPoint(), distances, _boundaryFieldOfView, 1.6f));
        }

        private BoundaryProbeResult GetBoundaryProbe(int distanceIndex, int directionIndex)
        {
            int index = distanceIndex * 16 + directionIndex;
            return index >= 0 && index < _boundaryProbes.Count
                ? _boundaryProbes[index]
                : null;
        }

        private Vector3 GetAvatarViewPoint()
        {
            if (_target == null)
            {
                return Vector3.zero;
            }
            VRCAvatarDescriptor descriptor = _target.GetComponent<VRCAvatarDescriptor>();
            return descriptor != null
                ? _target.transform.TransformPoint(descriptor.ViewPosition)
                : _target.transform.position;
        }

        private string SelectedBoundsPath()
        {
            foreach (RendererBoundsEntry entry in _rendererBounds)
            {
                if (entry.Renderer == _selectedBoundsRenderer)
                {
                    return entry.RendererPath;
                }
            }
            return string.Empty;
        }

        private void DrawSplitter(ref float leftWidth, int splitterId, float minimumWidth)
        {
            Rect splitter = GUILayoutUtility.GetRect(
                6f, 6f, GUILayout.Width(6f), GUILayout.ExpandHeight(true));
            EditorGUIUtility.AddCursorRect(splitter, MouseCursor.ResizeHorizontal);
            if (Event.current.type == EventType.Repaint)
            {
                EditorGUI.DrawRect(new Rect(splitter.center.x, splitter.y, 1f, splitter.height),
                    new Color(1f, 1f, 1f, 0.18f));
            }
            if (Event.current.type == EventType.MouseDown
                && Event.current.button == 0 && splitter.Contains(Event.current.mousePosition))
            {
                _activeSplitter = splitterId;
                Event.current.Use();
            }
            if (_activeSplitter == splitterId && Event.current.type == EventType.MouseDrag)
            {
                float maximum = Mathf.Max(minimumWidth, position.width - 320f);
                leftWidth = Mathf.Clamp(Event.current.mousePosition.x, minimumWidth, maximum);
                Repaint();
                Event.current.Use();
            }
            if (_activeSplitter == splitterId && Event.current.rawType == EventType.MouseUp)
            {
                _activeSplitter = 0;
            }
        }

        private int CountDuplicateTextureGroups()
        {
            var hashes = new HashSet<string>();
            foreach (TextureUsageEntry entry in _textureInventory)
            {
                if (entry.DuplicateSourceCount > 1 && !string.IsNullOrEmpty(entry.SourceHash))
                {
                    hashes.Add(entry.SourceHash);
                }
            }
            return hashes.Count;
        }

        private string QueuePresetName(RenderQueueRangePreset preset)
        {
            switch (preset)
            {
                case RenderQueueRangePreset.Background: return "Background";
                case RenderQueueRangePreset.Geometry: return "Geometry";
                case RenderQueueRangePreset.AlphaTest: return "AlphaTest";
                case RenderQueueRangePreset.GeometryLast: return "GeometryLast";
                case RenderQueueRangePreset.Transparent: return "Transparent";
                case RenderQueueRangePreset.Overlay: return "Overlay";
                case RenderQueueRangePreset.Custom: return T("カスタム", "Custom");
                default: return T("全範囲", "All");
            }
        }

        private string QueueVisibilityName(RenderQueueVisibilityMode mode)
        {
            switch (mode)
            {
                case RenderQueueVisibilityMode.OnlySelectedRange:
                    return T("範囲内のみ表示", "Only Range");
                case RenderQueueVisibilityMode.ExcludeSelectedRange:
                    return T("範囲内を隠す", "Hide Range");
                default:
                    return T("全Mesh表示", "Show All");
            }
        }

        private string LightingScenarioName(AvatarLightingScenario scenario)
        {
            if (_language == AvatarMaterialStudioLanguage.English)
            {
                return ObjectNames.NicifyVariableName(scenario.ToString());
            }
            switch (scenario)
            {
                case AvatarLightingScenario.NoLights: return "Lightなし";
                case AvatarLightingScenario.AmbientMinimum: return "環境光 最小";
                case AvatarLightingScenario.AmbientMaximum: return "環境光 最大";
                case AvatarLightingScenario.DirectionalMinimum: return "Directional 最小";
                case AvatarLightingScenario.DirectionalNeutral: return "Directional 標準";
                case AvatarLightingScenario.DirectionalMaximum: return "Directional 最大";
                case AvatarLightingScenario.DirectionalWarm: return "Directional 暖色";
                case AvatarLightingScenario.DirectionalCool: return "Directional 寒色";
                case AvatarLightingScenario.DualDirectional: return "Directional 2灯";
                case AvatarLightingScenario.DualColored: return "Directional 色違い2灯";
                case AvatarLightingScenario.Backlight: return "逆光";
                case AvatarLightingScenario.PointNear: return "Point 近距離";
                case AvatarLightingScenario.PointFar: return "Point 遠距離";
                case AvatarLightingScenario.DualPoint: return "Point 2灯";
                case AvatarLightingScenario.DirectionalAndPoint: return "Directional + Point";
                case AvatarLightingScenario.TopPoint: return "Point 上方";
                case AvatarLightingScenario.BottomPoint: return "Point 下方";
                default: return scenario.ToString();
            }
        }

        private string LocalizeDiagnosticIssue(string issue)
        {
            if (_language == AvatarMaterialStudioLanguage.English)
            {
                return issue;
            }
            switch (issue)
            {
                case "Material or shader is missing.":
                    return "MaterialまたはShaderがありません。";
                case "Effective render queue is outside Unity's 0..5000 range.":
                    return "有効Render QueueがUnityの0..5000範囲外です。";
                case "Transparent RenderType is in the opaque queue range.":
                    return "Transparent RenderTypeが不透明Queue範囲にあります。";
                case "Opaque RenderType is in the transparent queue range.":
                    return "Opaque RenderTypeが半透明Queue範囲にあります。";
                case "Transparent material writes depth; verify overlap and sorting intentionally.":
                    return "半透明MaterialがDepthを書き込みます。重なりとSortingが意図どおりか確認してください。";
                case "Opaque-range material does not write depth.":
                    return "不透明Queue範囲のMaterialがDepthを書き込みません。";
                case "Mesh is missing.":
                case "Renderer has no mesh.":
                    return "RendererにMeshがありません。";
                case "World bounds has a zero or near-zero axis.":
                    return "World Boundsにゼロまたは極小の軸があります。";
                case "Bounds is more than four times the avatar extent.":
                    return "BoundsがAvatar全体のExtentの4倍を超えています。";
                case "Bounds center is far outside the avatar aggregate bounds.":
                    return "Bounds中心がAvatar全体Boundsから大きく外れています。";
                case "updateWhenOffscreen is enabled; local bounds can be recomputed each frame.":
                    return "Update When Offscreenが有効です。Local Boundsが毎Frame再計算される場合があります。";
                default:
                    if (issue.StartsWith("Material slot count (",
                            System.StringComparison.Ordinal))
                    {
                        return "Materialスロット数とSubMesh数が一致しません: " + issue;
                    }
                    return issue;
            }
        }

        private string T(string japanese, string english)
        {
            return _language == AvatarMaterialStudioLanguage.Japanese ? japanese : english;
        }

        private static string FormatVector(Vector3 value)
        {
            return "(" + value.x.ToString("0.###") + ", "
                + value.y.ToString("0.###") + ", "
                + value.z.ToString("0.###") + ")";
        }

        private void UseSelection()
        {
            GameObject selected = Selection.activeGameObject;
            if (selected != null)
            {
                VRCAvatarDescriptor descriptor = selected.GetComponentInParent<VRCAvatarDescriptor>();
                if (descriptor == null)
                {
                    descriptor = selected.GetComponentInChildren<VRCAvatarDescriptor>(true);
                }
                _target = descriptor != null ? descriptor.gameObject : selected;
            }
            Refresh();
        }

        private void Refresh()
        {
            _slots.Clear();
            if (_target != null)
            {
                _slots.AddRange(AvatarMaterialCatalog.CollectSlots(_target));
            }
            _textureInventory.Clear();
            _textureInventory.AddRange(AvatarMaterialCatalog.CollectTextureInventory(_slots));
            _renderQueueEntries.Clear();
            _renderQueueEntries.AddRange(AvatarRenderDiagnostics.CollectRenderQueues(_slots));
            _rendererBounds.Clear();
            _rendererBounds.AddRange(AvatarRenderDiagnostics.CollectRendererBounds(_target));
            if (_selectedBoundsRenderer != null)
            {
                bool stillPresent = false;
                foreach (RendererBoundsEntry entry in _rendererBounds)
                {
                    if (entry.Renderer == _selectedBoundsRenderer)
                    {
                        stillPresent = true;
                        break;
                    }
                }
                if (!stillPresent)
                {
                    _selectedBoundsRenderer = null;
                }
            }
            if (_selectedBoundsRenderer == null && _rendererBounds.Count > 0)
            {
                _selectedBoundsRenderer = _rendererBounds[0].Renderer;
            }
            RefreshBoundaryProbes();
            if (_selectedMaterial != null && AvatarMaterialCatalog.CountUses(_slots, _selectedMaterial) == 0)
            {
                _selectedMaterial = null;
            }
            _preview.Rebuild(_target, _previewMode);
            Repaint();
        }

        private void HandleExternalChange()
        {
            Refresh();
        }

        private void HandleSceneViewGUI(SceneView sceneView)
        {
            if (_cameraMode == AvatarPreviewCameraMode.SceneViewFollow)
            {
                Repaint();
            }
        }

        private bool MatchesSearch(params string[] values)
        {
            if (string.IsNullOrWhiteSpace(_search))
            {
                return true;
            }
            foreach (string value in values)
            {
                if (!string.IsNullOrEmpty(value)
                    && value.IndexOf(_search, System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }
            return false;
        }

        private bool MatchesInventorySearch(TextureUsageEntry entry, string assetPath)
        {
            if (string.IsNullOrWhiteSpace(_inventorySearch))
            {
                return true;
            }

            if (MatchesText(entry.Texture != null ? entry.Texture.name : string.Empty, _inventorySearch)
                || MatchesText(assetPath, _inventorySearch))
            {
                return true;
            }

            foreach (TexturePropertyUsage usage in entry.Properties)
            {
                if (MatchesText(usage.Material != null ? usage.Material.name : string.Empty, _inventorySearch)
                    || MatchesText(usage.PropertyName, _inventorySearch)
                    || MatchesText(usage.DisplayName, _inventorySearch))
                {
                    return true;
                }
            }
            return false;
        }

        private static bool MatchesText(string value, string search)
        {
            return !string.IsNullOrEmpty(value)
                && value.IndexOf(search, System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void DrawTextureThumbnail(Texture texture, float size)
        {
            Rect thumbnailRect = GUILayoutUtility.GetRect(
                size, size, GUILayout.Width(size), GUILayout.Height(size));
            Texture thumbnail = AssetPreview.GetAssetPreview(texture)
                ?? AssetPreview.GetMiniThumbnail(texture);
            if (thumbnail != null && Event.current.type == EventType.Repaint)
            {
                GUI.DrawTexture(thumbnailRect, thumbnail, ScaleMode.ScaleToFit, true);
            }
            else if (Event.current.type == EventType.Repaint)
            {
                EditorGUI.DrawRect(thumbnailRect, new Color(0.16f, 0.16f, 0.16f));
            }

            if (AssetPreview.IsLoadingAssetPreview(texture.GetInstanceID()))
            {
                Repaint();
            }
        }
    }
}
