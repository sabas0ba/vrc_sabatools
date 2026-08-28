// EditMode tests that need a real editor.
//
// The offline harness in .github/verify/offline covers the rules; what it
// cannot reach is the half that depends on Unity's object model -- the
// collectors, the SerializedObject walk, EditorUtility.CollectDependencies --
// and whether the UnityEditor signatures the hand-written stub asserts are the
// real ones. That is what runs here.
using System;
using System.Collections.Generic;
using NUnit.Framework;
using SabaTools.Inspect;
using SabaTools.Inspect.Editors;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SabaTools.Inspect.CITests
{
    public class InspectApiTests
    {
        private readonly List<UnityEngine.Object> _created = new List<UnityEngine.Object>();

        [SetUp]
        public void ResetRegressionModule()
        {
            RegressionInspectionModule.Enabled = false;
            RegressionInspectionModule.ThrowOnInspect = false;
        }

        [TearDown]
        public void DestroyCreatedObjects()
        {
            RegressionInspectionModule.Enabled = false;
            RegressionInspectionModule.ThrowOnInspect = false;

            foreach (UnityEngine.Object created in _created)
            {
                if (created != null)
                {
                    UnityEngine.Object.DestroyImmediate(created);
                }
            }
            _created.Clear();
        }

        private GameObject NewRoot(string name)
        {
            var go = new GameObject(name);
            _created.Add(go);
            return go;
        }

        [Test]
        public void RejectsNullAndInvalidTargets()
        {
            Assert.Throws<ArgumentNullException>(() => InspectApi.Inspect(null));
            Assert.Throws<ArgumentException>(() => InspectApi.InspectScene(default(Scene)));
        }

        [Test]
        public void CountsGameObjectsAndTriangles()
        {
            GameObject root = NewRoot("Root");
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.transform.SetParent(root.transform);

            InspectionReport report = InspectApi.Inspect(root, InspectMode.Generic);

            Assert.AreEqual(2, report.Snapshot.GameObjectCount, "root plus the cube");
            Assert.AreEqual(12, report.Snapshot.Triangles, "a Unity cube is 12 triangles");
            Assert.AreEqual(1, report.Snapshot.MeshRendererCount);
            Assert.AreEqual(1, report.Snapshot.MaterialSlotCount);
        }

        [Test]
        public void CountsInactiveComponents()
        {
            GameObject root = NewRoot("Root");
            GameObject particle = NewInactiveChild(root, "Particle");
            particle.AddComponent<ParticleSystem>();

            GameObject trail = NewInactiveChild(root, "Trail");
            trail.AddComponent<TrailRenderer>();

            GameObject line = NewInactiveChild(root, "Line");
            line.AddComponent<LineRenderer>();

            GameObject components = NewInactiveChild(root, "Components");
            components.AddComponent<ReflectionProbe>();
            components.AddComponent<AudioSource>();
            components.AddComponent<Animator>();
            components.AddComponent<Camera>();
            components.AddComponent<Light>();
            components.AddComponent<UnityEngine.Animations.PositionConstraint>();

            InspectionReport report = InspectApi.Inspect(root, InspectMode.Generic);

            Assert.AreEqual(5, report.Snapshot.GameObjectCount);
            Assert.AreEqual(1, report.Snapshot.ParticleSystemCount);
            Assert.AreEqual(1, report.Snapshot.TrailRendererCount);
            Assert.AreEqual(1, report.Snapshot.LineRendererCount);
            Assert.AreEqual(1, report.Snapshot.ReflectionProbeCount);
            Assert.AreEqual(1, report.Snapshot.AudioSourceCount);
            Assert.AreEqual(1, report.Snapshot.AnimatorCount);
            Assert.AreEqual(1, report.Snapshot.CameraCount);
            Assert.AreEqual(1, report.Snapshot.LightCount);
            Assert.AreEqual(1, report.Snapshot.RealtimeLightCount);
            Assert.AreEqual(1, report.Snapshot.ConstraintCount);
        }

        private static GameObject NewInactiveChild(GameObject root, string name)
        {
            var child = new GameObject(name);
            child.transform.SetParent(root.transform);
            child.SetActive(false);
            return child;
        }

        [Test]
        public void CountsSkinnedTrianglesAndUniqueBones()
        {
            GameObject root = NewRoot("Root");
            GameObject bone = new GameObject("Bone");
            bone.transform.SetParent(root.transform);
            var skinned = root.AddComponent<SkinnedMeshRenderer>();
            skinned.sharedMesh = BuildTriangle();
            skinned.bones = new[] { root.transform, bone.transform, root.transform };

            InspectionReport report = InspectApi.Inspect(root, InspectMode.Generic);

            Assert.AreEqual(1, report.Snapshot.Triangles);
            Assert.AreEqual(1, report.Snapshot.SkinnedMeshRendererCount);
            Assert.AreEqual(2, report.Snapshot.BoneCount, "duplicate bone slots count once");
        }

        [Test]
        public void CountsUniqueMaterialsAndShaders()
        {
            GameObject root = NewRoot("Root");
            Mesh mesh = BuildTriangle();
            var material = new Material(Shader.Find("Unlit/Color"));
            _created.Add(material);

            for (int i = 0; i < 2; i++)
            {
                var child = new GameObject("Mesh" + i);
                child.transform.SetParent(root.transform);
                child.AddComponent<MeshFilter>().sharedMesh = mesh;
                child.AddComponent<MeshRenderer>().sharedMaterial = material;
            }

            InspectionReport report = InspectApi.Inspect(root, InspectMode.Generic);

            Assert.AreEqual(2, report.Snapshot.MaterialSlotCount);
            Assert.AreEqual(1, report.Snapshot.UniqueMaterialCount);
            Assert.AreEqual(1, report.Snapshot.UniqueShaderCount);
        }

        [Test]
        public void ReportsAMeshFilterWithNoMesh()
        {
            GameObject root = NewRoot("Root");
            root.AddComponent<MeshFilter>();
            root.AddComponent<MeshRenderer>();

            InspectionReport report = InspectApi.Inspect(root, InspectMode.Generic);

            Assert.AreEqual(1, report.Snapshot.MissingMeshCount);
            Assert.Greater(report.ErrorCount, 0, "a missing mesh is an error");
        }

        [Test]
        public void ReportsAnEmptyMaterialSlot()
        {
            GameObject root = NewRoot("Root");
            root.AddComponent<MeshFilter>().sharedMesh = BuildTriangle();
            root.AddComponent<MeshRenderer>().sharedMaterials = new Material[] { null };

            InspectionReport report = InspectApi.Inspect(root, InspectMode.Generic);

            Assert.AreEqual(1, report.Snapshot.EmptyMaterialSlotCount);
            Assert.AreEqual(1, report.Snapshot.MaterialSlotCount);
            Assert.AreEqual(0, report.Snapshot.UniqueMaterialCount);
        }

        [Test]
        public void ReportsNullBoneSlots()
        {
            GameObject root = NewRoot("Root");
            var skinned = root.AddComponent<SkinnedMeshRenderer>();
            skinned.sharedMesh = BuildTriangle();
            skinned.bones = new Transform[] { null, root.transform, null };

            InspectionReport report = InspectApi.Inspect(root, InspectMode.Generic);

            Assert.AreEqual(2, report.Snapshot.MissingBoneCount);
            Assert.AreEqual(1, report.Snapshot.BoneCount, "only the non-null bone is counted");
        }

        [Test]
        public void ReportsADeletedSerializedAssetReference()
        {
            const string folder = "Assets/InspectApiMissingReferenceTestAssets";
            const string meshPath = folder + "/Deleted.asset";
            AssetDatabase.DeleteAsset(folder);
            AssetDatabase.CreateFolder("Assets", "InspectApiMissingReferenceTestAssets");

            GameObject root = NewRoot("Root");
            var filter = root.AddComponent<MeshFilter>();
            try
            {
                AssetDatabase.CreateAsset(BuildTriangle(), meshPath);
                filter.sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
                AssetDatabase.DeleteAsset(meshPath);

                InspectionReport report = InspectApi.Inspect(root, InspectMode.Generic);

                Assert.AreEqual(1, report.Snapshot.MissingReferenceCount);
                Assert.IsTrue(Mentions(report, "references a missing object"));
                Assert.IsTrue(MentionsPath(report, "Root"));
            }
            finally
            {
                AssetDatabase.DeleteAsset(folder);
            }
        }

        [Test]
        public void ReportsAPrefabWhoseAssetWasDeleted()
        {
            const string folder = "Assets/InspectApiPrefabTestAssets";
            const string prefabPath = folder + "/Deleted.prefab";
            AssetDatabase.DeleteAsset(folder);
            AssetDatabase.CreateFolder("Assets", "InspectApiPrefabTestAssets");

            try
            {
                var source = new GameObject("PrefabSource");
                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(source, prefabPath);
                UnityEngine.Object.DestroyImmediate(source);

                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                _created.Add(instance);
                AssetDatabase.DeleteAsset(prefabPath);

                Assert.AreEqual(
                    PrefabInstanceStatus.MissingAsset,
                    PrefabUtility.GetPrefabInstanceStatus(instance),
                    "the fixture must be a missing prefab instance");

                InspectionReport report = InspectApi.Inspect(instance, InspectMode.Generic);
                Assert.AreEqual(1, report.Snapshot.MissingPrefabCount);
                Assert.IsTrue(Mentions(report, "Prefab asset is missing"));
            }
            finally
            {
                AssetDatabase.DeleteAsset(folder);
            }
        }

        [Test]
        public void FindsTexturesThroughMaterials()
        {
            // The point of going through CollectDependencies rather than
            // reading assigned fields: the texture is reachable only via the
            // material's shader property.
            //
            // The material and the texture have to be assets on disk.
            // CollectDependencies follows persistent references only: handed a
            // hierarchy whose material was built in memory it returns the
            // GameObject and its components and stops. That is also the
            // collector's real limitation -- a scene that assembles its
            // materials at runtime reports no textures.
            const string folder = "Assets/InspectApiTestAssets";
            AssetDatabase.DeleteAsset(folder);
            AssetDatabase.CreateFolder("Assets", "InspectApiTestAssets");

            try
            {
                // Saved as a native asset rather than encoded to PNG: this
                // project does not include the image conversion module.
                var texture = new Texture2D(64, 64, TextureFormat.RGBA32, false);
                AssetDatabase.CreateAsset(texture, folder + "/Texture.asset");

                var material = new Material(Shader.Find("Unlit/Texture"))
                {
                    mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(folder + "/Texture.asset"),
                };
                AssetDatabase.CreateAsset(material, folder + "/Material.mat");
                AssetDatabase.SaveAssets();

                GameObject root = NewRoot("Root");
                root.AddComponent<MeshFilter>().sharedMesh = BuildTriangle();
                root.AddComponent<MeshRenderer>().sharedMaterial =
                    AssetDatabase.LoadAssetAtPath<Material>(folder + "/Material.mat");

                InspectionReport report = InspectApi.Inspect(root, InspectMode.Generic);

                Assert.Greater(report.Snapshot.TextureCount, 0, "the material's texture is found");
                Assert.Greater(report.Snapshot.TextureMemoryBytes, 0);
            }
            finally
            {
                AssetDatabase.DeleteAsset(folder);
            }
        }

        [Test]
        public void AutoModeFallsBackToGenericWithoutDescriptors()
        {
            // This project has no VRChat SDK, so the census finds nothing and
            // the checklist must not start asserting VRChat requirements.
            GameObject root = NewRoot("Root");

            InspectionReport report = InspectApi.Inspect(root);

            Assert.AreEqual(InspectMode.Generic, report.Mode);
            Assert.AreEqual(0, report.Items.Count, "an empty object has nothing to report");
        }

        [Test]
        public void InspectsEveryRootInAScene()
        {
            // A fresh batchmode project starts with an unsaved untitled scene.
            // Unity refuses to create an additive scene beside it, so replace
            // it with the scene this test owns before creating the roots.
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            NewRoot("First");
            NewRoot("Second");

            InspectionReport report = InspectApi.InspectScene(scene, InspectMode.Generic, null);

            Assert.AreEqual(scene.name, report.TargetName);
            Assert.AreEqual(2, report.Snapshot.GameObjectCount);
            Assert.AreEqual(string.Empty, report.GeneratedAt);
        }

        [Test]
        public void RequestedModeOverridesDetection()
        {
            GameObject root = NewRoot("Root");

            InspectionReport report = InspectApi.Inspect(root, InspectMode.Avatar);

            Assert.AreEqual(InspectMode.Avatar, report.Mode);
            Assert.Greater(report.WarningCount, 0, "avatar mode notes the missing descriptor");
        }

        [Test]
        public void InstalledModuleContributesToTheReport()
        {
            RegressionInspectionModule.Enabled = true;
            GameObject root = NewRoot("Root");

            InspectionReport report = InspectApi.Inspect(root);

            Assert.AreEqual(InspectMode.Generic, report.Mode);
            Assert.IsTrue(HasRow(report, "Regression", "Roots", "1"));
            Assert.IsTrue(Mentions(report, "test module ran"));
            Assert.IsTrue(MentionsPath(report, "Root"));
        }

        [Test]
        public void AModuleFailureDoesNotDiscardTheCoreReport()
        {
            RegressionInspectionModule.Enabled = true;
            RegressionInspectionModule.ThrowOnInspect = true;
            GameObject root = NewRoot("Root");

            InspectionReport report = InspectApi.Inspect(root);

            Assert.AreEqual(1, report.Snapshot.GameObjectCount);
            Assert.IsTrue(Mentions(report, "failed and its checks were skipped"));
            Assert.IsTrue(Mentions(report, RegressionInspectionModule.ModuleName));
        }

        [Test]
        public void ScanningLeavesTheHierarchyUntouched()
        {
            // The package's one hard promise. Comparing the serialized form of
            // every component before and after catches a collector that wrote
            // through a SerializedProperty, which is the mistake this design
            // is most exposed to.
            GameObject root = NewRoot("Root");
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.transform.SetParent(root.transform);

            List<string> before = SerializeHierarchy(root);
            InspectApi.Inspect(root, InspectMode.Generic);
            List<string> after = SerializeHierarchy(root);

            Assert.AreEqual(before.Count, after.Count, "no component was added or removed");
            for (int i = 0; i < before.Count; i++)
            {
                Assert.AreEqual(before[i], after[i], $"component {i} was modified by the scan");
            }
        }

        /// <summary>
        /// EditorJsonUtility, not JsonUtility: the latter refuses engine types
        /// ("JsonUtility.ToJson does not support engine types"), and a
        /// hierarchy of Transforms and renderers is nothing but engine types.
        /// This assembly is Editor-only, so the editor serializer is available
        /// wherever these tests run. The serialized fields are exactly what a
        /// stray write would change.
        /// </summary>
        private static List<string> SerializeHierarchy(GameObject root)
        {
            var lines = new List<string>();
            foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
            {
                lines.Add(transform.gameObject.name + "|" + transform.gameObject.activeSelf);
                foreach (Component component in transform.GetComponents<Component>())
                {
                    lines.Add(component == null
                        ? "<missing>"
                        : component.GetType().FullName + "|" + EditorJsonUtility.ToJson(component));
                }
            }
            return lines;
        }

        [Test]
        public void ReportRendersAsMarkdown()
        {
            GameObject root = NewRoot("Root");
            string markdown = InspectApi.Inspect(root, InspectMode.Generic).ToMarkdown();

            Assert.IsTrue(markdown.Contains("# SabaTools Inspect Report"));
            Assert.IsTrue(markdown.Contains("## Geometry"));
        }

        /// <summary>A one-triangle mesh, so a renderer has something to draw.</summary>
        private Mesh BuildTriangle()
        {
            var mesh = new Mesh
            {
                vertices = new[] { Vector3.zero, Vector3.up, Vector3.right },
                triangles = new[] { 0, 1, 2 },
            };
            mesh.RecalculateNormals();
            _created.Add(mesh);
            return mesh;
        }

        private static bool Mentions(InspectionReport report, string fragment)
        {
            foreach (InspectionItem item in report.Items)
            {
                if (item.Message.Contains(fragment))
                {
                    return true;
                }
            }
            return false;
        }

        private static bool MentionsPath(InspectionReport report, string path)
        {
            foreach (InspectionItem item in report.Items)
            {
                if (item.Path == path)
                {
                    return true;
                }
            }
            return false;
        }

        private static bool HasRow(
            InspectionReport report, string group, string label, string value)
        {
            foreach (StatRow row in report.Rows)
            {
                if (row.Group == group && row.Label == label && row.Value == value)
                {
                    return true;
                }
            }
            return false;
        }
    }

    /// <summary>
    /// A test-only implementation proves the public extension point is found
    /// through TypeCache and isolated from the core scan when it fails.
    /// </summary>
    public sealed class RegressionInspectionModule : InspectionModule
    {
        public const string ModuleName = "Regression inspection module";

        public static bool Enabled;
        public static bool ThrowOnInspect;

        public override string DisplayName => ModuleName;

        public override InspectMode Mode => InspectMode.Generic;

        public override bool Detect(GameObject[] roots)
        {
            return Enabled;
        }

        public override void Inspect(InspectionContext context)
        {
            if (ThrowOnInspect)
            {
                throw new InvalidOperationException("intentional regression test failure");
            }

            context.AddRow("Regression", "Roots", context.Roots.Length.ToString());
            context.Add(
                InspectionSeverity.Info, "Regression", "test module ran", context.Roots[0]);
        }
    }

}
