// EditMode tests that need a real editor.
//
// The offline harness in .github/verify/offline covers the rules; what it
// cannot reach is the half that depends on Unity's object model -- the
// collectors, the SerializedObject walk, EditorUtility.CollectDependencies --
// and whether the UnityEditor signatures the hand-written stub asserts are the
// real ones. That is what runs here.
using System.Collections.Generic;
using NUnit.Framework;
using SabaTools.Inspect;
using SabaTools.Inspect.Editors;
using UnityEngine;

namespace SabaTools.Inspect.CITests
{
    public class InspectApiTests
    {
        private readonly List<GameObject> _created = new List<GameObject>();

        [TearDown]
        public void DestroyCreatedObjects()
        {
            foreach (GameObject go in _created)
            {
                if (go != null)
                {
                    Object.DestroyImmediate(go);
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
        public void FindsTexturesThroughMaterials()
        {
            // The point of going through CollectDependencies rather than
            // reading assigned fields: the texture is reachable only via the
            // material's shader property.
            GameObject root = NewRoot("Root");
            root.AddComponent<MeshFilter>().sharedMesh = BuildTriangle();

            var texture = new Texture2D(64, 64, TextureFormat.RGBA32, false);
            var material = new Material(Shader.Find("Unlit/Texture"));
            material.mainTexture = texture;
            root.AddComponent<MeshRenderer>().sharedMaterial = material;

            try
            {
                InspectionReport report = InspectApi.Inspect(root, InspectMode.Generic);

                Assert.Greater(report.Snapshot.TextureCount, 0, "the material's texture is found");
                Assert.Greater(report.Snapshot.TextureMemoryBytes, 0);
            }
            finally
            {
                Object.DestroyImmediate(material);
                Object.DestroyImmediate(texture);
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
        public void RequestedModeOverridesDetection()
        {
            GameObject root = NewRoot("Root");

            InspectionReport report = InspectApi.Inspect(root, InspectMode.Avatar);

            Assert.AreEqual(InspectMode.Avatar, report.Mode);
            Assert.Greater(report.WarningCount, 0, "avatar mode notes the missing descriptor");
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
        /// JsonUtility rather than an editor serializer: this has to run the
        /// same way whichever assembly it is compiled into, and the serialized
        /// fields are exactly what a stray write would change.
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
                        : component.GetType().FullName + "|" + JsonUtility.ToJson(component));
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
        private static Mesh BuildTriangle()
        {
            var mesh = new Mesh
            {
                vertices = new[] { Vector3.zero, Vector3.up, Vector3.right },
                triangles = new[] { 0, 1, 2 },
            };
            mesh.RecalculateNormals();
            return mesh;
        }
    }
}
