using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using SabaTools.AvatarMaterials.Editors;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3A.Editor;

namespace SabaTools.Inspect.Avatar.SdkTests
{
    public class AvatarMaterialStudioTests
    {
        private readonly List<Object> _created = new List<Object>();
        private readonly List<string> _createdAssetPaths = new List<string>();
        private GameObject _avatar;

        [SetUp]
        public void CreateAvatar()
        {
            _avatar = CreateGameObject("MaterialAvatar");
            _avatar.AddComponent<VRCAvatarDescriptor>().ViewPosition =
                new Vector3(0f, 1.6f, 0.1f);
        }

        [TearDown]
        public void DestroyObjects()
        {
            Undo.ClearAll();
            for (int index = _created.Count - 1; index >= 0; index--)
            {
                if (_created[index] != null)
                {
                    Object.DestroyImmediate(_created[index]);
                }
            }
            _created.Clear();
            foreach (string assetPath in _createdAssetPaths)
            {
                AssetDatabase.DeleteAsset(assetPath);
            }
            _createdAssetPaths.Clear();
        }

        [Test]
        public void CatalogFindsRendererSlotsAndTextureProperties()
        {
            Shader shader = Shader.Find("Standard");
            Assert.IsNotNull(shader, "Unity's Standard shader is required by the fixture");

            Material material = CreateMaterial(shader, "Body");
            var texture = new Texture2D(2, 2) { name = "Albedo" };
            _created.Add(texture);
            material.SetTexture("_MainTex", texture);

            GameObject body = CreateGameObject("Body");
            body.transform.SetParent(_avatar.transform);
            body.AddComponent<MeshRenderer>().sharedMaterial = material;

            List<MaterialSlotEntry> slots = AvatarMaterialCatalog.CollectSlots(_avatar);
            Assert.AreEqual(1, slots.Count);
            Assert.AreEqual("MaterialAvatar/Body", slots[0].RendererPath);
            Assert.AreSame(material, slots[0].Material);

            List<TexturePropertyEntry> textures = AvatarMaterialCatalog.CollectTextures(material);
            Assert.IsTrue(textures.Exists(entry =>
                entry.Name == "_MainTex" && entry.Texture == texture));
        }

        [Test]
        public void TextureInventoryGroupsAssignmentsAndCountsRendererSlots()
        {
            Shader shader = Shader.Find("Standard");
            Material material = CreateMaterial(shader, "Shared");
            var texture = new Texture2D(2, 2) { name = "SharedTexture" };
            _created.Add(texture);
            material.SetTexture("_MainTex", texture);
            material.SetTexture("_EmissionMap", texture);

            GameObject body = CreateGameObject("Body");
            body.transform.SetParent(_avatar.transform);
            body.AddComponent<MeshRenderer>().sharedMaterial = material;
            GameObject head = CreateGameObject("Head");
            head.transform.SetParent(_avatar.transform);
            head.AddComponent<MeshRenderer>().sharedMaterial = material;

            List<TextureUsageEntry> inventory = AvatarMaterialCatalog.CollectTextureInventory(
                AvatarMaterialCatalog.CollectSlots(_avatar));

            Assert.AreEqual(1, inventory.Count);
            Assert.AreSame(texture, inventory[0].Texture);
            Assert.AreEqual(2, inventory[0].Properties.Count);
            Assert.AreEqual(2, inventory[0].MaterialSlotCount);
        }

        [Test]
        public void TextureInventoryDetectsIdenticalFilesAtDifferentAssetPaths()
        {
            const string folder = "Assets/SabaToolsAvatarMaterialTests";
            if (!AssetDatabase.IsValidFolder(folder))
            {
                AssetDatabase.CreateFolder("Assets", "SabaToolsAvatarMaterialTests");
            }
            string firstPath = folder + "/DuplicateA.png";
            string secondPath = folder + "/DuplicateB.png";
            _createdAssetPaths.Add(firstPath);
            _createdAssetPaths.Add(secondPath);
            _createdAssetPaths.Add(folder);

            var source = new Texture2D(2, 2);
            source.SetPixels(new[] { Color.red, Color.green, Color.blue, Color.white });
            source.Apply();
            byte[] png = source.EncodeToPNG();
            Object.DestroyImmediate(source);
            File.WriteAllBytes(firstPath, png);
            File.WriteAllBytes(secondPath, png);
            AssetDatabase.ImportAsset(firstPath, ImportAssetOptions.ForceSynchronousImport);
            AssetDatabase.ImportAsset(secondPath, ImportAssetOptions.ForceSynchronousImport);

            Material firstMaterial = CreateMaterial(Shader.Find("Standard"), "First");
            Material secondMaterial = CreateMaterial(Shader.Find("Standard"), "Second");
            firstMaterial.SetTexture("_MainTex", AssetDatabase.LoadAssetAtPath<Texture2D>(firstPath));
            secondMaterial.SetTexture("_MainTex", AssetDatabase.LoadAssetAtPath<Texture2D>(secondPath));
            GameObject firstObject = CreateGameObject("FirstRenderer");
            firstObject.transform.SetParent(_avatar.transform);
            firstObject.AddComponent<MeshRenderer>().sharedMaterial = firstMaterial;
            GameObject secondObject = CreateGameObject("SecondRenderer");
            secondObject.transform.SetParent(_avatar.transform);
            secondObject.AddComponent<MeshRenderer>().sharedMaterial = secondMaterial;

            List<TextureUsageEntry> inventory = AvatarMaterialCatalog.CollectTextureInventory(
                AvatarMaterialCatalog.CollectSlots(_avatar));

            Assert.AreEqual(2, inventory.Count);
            Assert.IsTrue(inventory.TrueForAll(entry => entry.DuplicateSourceCount == 2));
            Assert.AreEqual(inventory[0].SourceHash, inventory[1].SourceHash);
        }

        [Test]
        public void VisualizationProvidesBroadParallelLightingMatrix()
        {
            AvatarLightingScenario[] scenarios =
                (AvatarLightingScenario[])System.Enum.GetValues(typeof(AvatarLightingScenario));

            Assert.GreaterOrEqual(scenarios.Length, 16);
            CollectionAssert.Contains(scenarios, AvatarLightingScenario.NoLights);
            CollectionAssert.Contains(scenarios, AvatarLightingScenario.AmbientMinimum);
            CollectionAssert.Contains(scenarios, AvatarLightingScenario.DirectionalMaximum);
            CollectionAssert.Contains(scenarios, AvatarLightingScenario.DualDirectional);
            CollectionAssert.Contains(scenarios, AvatarLightingScenario.PointNear);
            CollectionAssert.Contains(scenarios, AvatarLightingScenario.DirectionalAndPoint);
        }

        [Test]
        public void PreviewDefaultsToSceneViewFollowAndClampsUiScale()
        {
            Assert.AreEqual(
                AvatarPreviewCameraMode.SceneViewFollow,
                AvatarMaterialStudioWindow.DefaultCameraMode);
            Assert.AreEqual(
                AvatarPreviewGizmoMode.Compact,
                AvatarMaterialStudioWindow.DefaultGizmoMode);
            Assert.AreEqual(
                135f, AvatarMaterialStudioWindow.ScalePreviewDimension(180f, 0.5f));
            Assert.AreEqual(
                180f, AvatarMaterialStudioWindow.ScalePreviewDimension(180f, 1f));
            Assert.AreEqual(
                360f, AvatarMaterialStudioWindow.ScalePreviewDimension(180f, 3f));
        }

        [Test]
        public void RenderQueueVisibilityMaskFiltersIndividualMaterialSlots()
        {
            Shader shader = Shader.Find("Standard");
            Material background = CreateMaterial(shader, "Background");
            Material geometry = CreateMaterial(shader, "Geometry");
            Material alphaTest = CreateMaterial(shader, "AlphaTest");
            Material transparent = CreateMaterial(shader, "Transparent");
            background.renderQueue = 1000;
            geometry.renderQueue = 2000;
            alphaTest.renderQueue = 2450;
            transparent.renderQueue = 3000;
            Material[] slots = { background, geometry, alphaTest, transparent };

            CollectionAssert.AreEqual(
                new[] { false, true, false, false },
                AvatarMaterialPreview.BuildQueueVisibilityMask(
                    slots,
                    RenderQueueVisibilityMode.OnlySelectedRange,
                    AvatarRenderDiagnostics.ResolveQueueRange(
                        RenderQueueRangePreset.Geometry, 0, 5000)));
            CollectionAssert.AreEqual(
                new[] { true, true, false, true },
                AvatarMaterialPreview.BuildQueueVisibilityMask(
                    slots,
                    RenderQueueVisibilityMode.ExcludeSelectedRange,
                    AvatarRenderDiagnostics.ResolveQueueRange(
                        RenderQueueRangePreset.AlphaTest, 0, 5000)));
        }

        [Test]
        public void LightingGizmoVectorsFollowRaysFromTheirSources()
        {
            Light directional = CreateGameObject("DirectionalLight").AddComponent<Light>();
            directional.type = LightType.Directional;
            directional.transform.rotation = Quaternion.Euler(25f, 40f, 0f);
            Assert.That(
                Vector3.Dot(
                    directional.transform.forward,
                    AvatarMaterialPreview.LightVectorTowardTarget(
                        directional, _avatar.transform.position)),
                Is.EqualTo(1f).Within(0.0001f));

            Light point = CreateGameObject("PointLight").AddComponent<Light>();
            point.type = LightType.Point;
            point.transform.position = new Vector3(2f, 1f, -3f);
            Vector3 target = new Vector3(-1f, 2f, 1f);
            Assert.That(
                Vector3.Dot(
                    (target - point.transform.position).normalized,
                    AvatarMaterialPreview.LightVectorTowardTarget(point, target)),
                Is.EqualTo(1f).Within(0.0001f));
        }

        [Test]
        public void RenderQueueDiagnosticsReportsOpaqueMaterialInTransparentRange()
        {
            Shader shader = Shader.Find("Standard");
            Material material = CreateMaterial(shader, "WrongQueue");
            material.SetOverrideTag("RenderType", "Opaque");
            material.renderQueue = 3000;

            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = "QueueCube";
            cube.transform.SetParent(_avatar.transform);
            cube.GetComponent<Renderer>().sharedMaterial = material;
            _created.Add(cube);

            List<RenderQueueEntry> entries = AvatarRenderDiagnostics.CollectRenderQueues(
                AvatarMaterialCatalog.CollectSlots(_avatar));

            Assert.AreEqual(1, entries.Count);
            Assert.AreEqual(3000, entries[0].EffectiveQueue);
            Assert.IsTrue(entries[0].Issues.Exists(issue =>
                issue.Contains("Opaque RenderType")));
        }

        [Test]
        public void BoundsDiagnosticsGenerateNearMiddleFarBySixteenDirections()
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = "BoundsCube";
            cube.transform.SetParent(_avatar.transform);
            cube.transform.localPosition = new Vector3(0f, 1f, 0f);
            _created.Add(cube);

            List<RendererBoundsEntry> renderers =
                AvatarRenderDiagnostics.CollectRendererBounds(_avatar);
            List<BoundaryProbeResult> probes = AvatarRenderDiagnostics.EvaluateBoundaryProbes(
                renderers,
                _avatar.transform.TransformPoint(
                    _avatar.GetComponent<VRCAvatarDescriptor>().ViewPosition),
                new[] { 0.35f, 2f, 10f },
                60f,
                1.6f);

            Assert.AreEqual(1, renderers.Count);
            Assert.AreEqual(48, probes.Count);
            Assert.IsTrue(probes.TrueForAll(probe => probe.TotalRendererCount == 1));
        }

        [Test]
        public void RenderQueueEditParticipatesInUndo()
        {
            Material material = CreateMaterial(Shader.Find("Standard"), "QueueUndo");
            material.renderQueue = 2000;

            AvatarMaterialEditorActions.SetRenderQueue(material, 3100);
            Assert.AreEqual(3100, material.renderQueue);
            Undo.FlushUndoRecordObjects();
            Undo.PerformUndo();
            Assert.AreEqual(2000, material.renderQueue);
        }

        [Test]
        public void MaterialAndTextureEditsParticipateInUndo()
        {
            Shader shader = Shader.Find("Standard");
            Material before = CreateMaterial(shader, "Before");
            Material after = CreateMaterial(shader, "After");
            Texture texture = new Texture2D(2, 2);
            _created.Add(texture);

            MeshRenderer renderer = _avatar.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = before;
            AvatarMaterialEditorActions.SetMaterial(renderer, 0, after);
            Assert.AreSame(after, renderer.sharedMaterial);
            Undo.FlushUndoRecordObjects();
            Undo.PerformUndo();
            Assert.AreSame(before, renderer.sharedMaterial);

            AvatarMaterialEditorActions.SetTexture(before, "_MainTex", texture);
            Assert.AreSame(texture, before.GetTexture("_MainTex"));
            Undo.FlushUndoRecordObjects();
            Undo.PerformUndo();
            Assert.IsNull(before.GetTexture("_MainTex"));
        }

        [Test]
        public void QuestResolverUsesAndroidPerPlatformOverride()
        {
            GameObject android = CreateGameObject("AndroidAvatar");
            VRCAvatarDescriptor androidDescriptor = android.AddComponent<VRCAvatarDescriptor>();
            PerPlatformOverrides.SetPlatformOverrides(
                _avatar,
                new List<PerPlatformOverrides.Option>
                {
                    new PerPlatformOverrides.Option
                    {
                        platform = BuildTarget.Android,
                        avatar = androidDescriptor,
                    },
                });

            GameObject resolved = QuestAvatarResolver.Resolve(_avatar, out bool usesOverride);

            Assert.IsTrue(usesOverride);
            Assert.AreSame(android, resolved);
        }

        private GameObject CreateGameObject(string name)
        {
            var result = new GameObject(name);
            _created.Add(result);
            return result;
        }

        private Material CreateMaterial(Shader shader, string name)
        {
            var result = new Material(shader) { name = name };
            _created.Add(result);
            return result;
        }
    }
}
