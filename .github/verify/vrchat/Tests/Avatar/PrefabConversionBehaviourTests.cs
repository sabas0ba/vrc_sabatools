using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SabaTools.Inspect.Avatar.SdkTests
{
    /// <summary>
    /// Pins down how Unity's prefab conversion APIs treat a duplicated and
    /// edited avatar hierarchy. The documentation does not state which values
    /// become overrides, how nested prefab instances are matched or what
    /// happens to objects the duplicate removed, and the variant tooling is
    /// built on the answers. A Unity update that changes one of them fails here
    /// rather than silently changing what a conversion produces.
    ///
    /// Observed on Unity 2022.3.22f1.
    /// </summary>
    public class PrefabConversionBehaviourTests
    {
        private const string Parent = "Assets/SabaToolsPrefabConversionTests";

        private readonly List<GameObject> _roots = new List<GameObject>();
        private string _folder;

        // Each test saves into a folder of its own, and the folders are removed
        // together at the end. Recreating a prefab asset at a path a previous
        // test deleted crashed the editor inside SaveAsPrefabAssetAndConnect.
        [SetUp]
        public void CreateFolder()
        {
            if (!AssetDatabase.IsValidFolder(Parent))
            {
                AssetDatabase.CreateFolder("Assets", "SabaToolsPrefabConversionTests");
            }
            string name = TestContext.CurrentContext.Test.Name;
            _folder = Parent + "/" + name;
            AssetDatabase.CreateFolder(Parent, name);
        }

        [TearDown]
        public void DestroyObjects()
        {
            Undo.ClearAll();
            foreach (GameObject root in _roots)
            {
                if (root != null)
                {
                    Object.DestroyImmediate(root);
                }
            }
            _roots.Clear();
        }

        [OneTimeTearDown]
        public void DeleteFolder()
        {
            AssetDatabase.DeleteAsset(Parent);
        }

        [Test]
        public void ConversionKeepsObjectsAndRecordsOnlyDifferences()
        {
            GameObject derived = BuildEditedDuplicate(out GameObject asset);
            Transform hips = derived.transform.Find("Armature/Hips");

            Convert(derived, asset);

            Assert.AreEqual(PrefabInstanceStatus.Connected, PrefabUtility.GetPrefabInstanceStatus(derived));
            Assert.AreSame(asset, PrefabUtility.GetCorrespondingObjectFromSource(derived));

            // The scene objects are reused, so references into the duplicate hold.
            Assert.IsTrue(hips != null && hips.root == derived.transform);
            Assert.AreSame(
                hips, derived.transform.Find("Body").GetComponent<SkinnedMeshRenderer>().rootBone);

            // Values equal to the asset's are not recorded.
            CollectionAssert.AreEquivalent(
                new[]
                {
                    "BoxCollider(Body) m_Size.x",
                    "BoxCollider(Body) m_Size.y",
                    "BoxCollider(Body) m_Size.z",
                    "Transform(Spine) m_LocalPosition.y",
                },
                NonDefaultOverrides(derived));
            CollectionAssert.AreEqual(new[] { "Accessory" }, AddedGameObjectNames(derived));
            CollectionAssert.AreEqual(new[] { "SphereCollider" }, AddedComponentTypes(derived));
        }

        [Test]
        public void ConversionRestoresWhatTheDuplicateRemoved()
        {
            GameObject derived = BuildEditedDuplicate(out GameObject asset);

            Convert(derived, asset);

            Assert.IsNotNull(derived.transform.Find("Body").GetComponent<CapsuleCollider>());
            Assert.IsNotNull(derived.transform.Find("Armature/Hips/Tail"));
            Assert.IsEmpty(PrefabUtility.GetRemovedComponents(derived));
            Assert.IsEmpty(PrefabUtility.GetRemovedGameObjects(derived));
        }

        [Test]
        public void ConversionRenamesTheRoot()
        {
            GameObject derived = BuildEditedDuplicate(out GameObject asset);

            Convert(derived, asset);

            Assert.AreNotEqual("Derived", derived.name);
        }

        [Test]
        public void RemovingAgainAfterConversionIsKeptByASavedVariant()
        {
            GameObject derived = BuildEditedDuplicate(out GameObject asset);
            Transform hips = derived.transform.Find("Armature/Hips");
            Convert(derived, asset);

            Object.DestroyImmediate(derived.transform.Find("Body").GetComponent<CapsuleCollider>());
            Object.DestroyImmediate(derived.transform.Find("Armature/Hips/Tail").gameObject);

            Assert.AreEqual(1, PrefabUtility.GetRemovedComponents(derived).Count);
            Assert.AreEqual(1, PrefabUtility.GetRemovedGameObjects(derived).Count);

            GameObject variant = PrefabUtility.SaveAsPrefabAssetAndConnect(
                derived, _folder + "/DerivedVariant.prefab", InteractionMode.AutomatedAction);

            Assert.AreEqual(PrefabAssetType.Variant, PrefabUtility.GetPrefabAssetType(variant));
            Assert.AreSame(asset, PrefabUtility.GetCorrespondingObjectFromSource(variant));
            Assert.IsNull(variant.transform.Find("Body").GetComponent<CapsuleCollider>());
            Assert.IsNull(variant.transform.Find("Armature/Hips/Tail"));
            Assert.AreEqual(1, PrefabUtility.GetRemovedComponents(variant).Count);
            Assert.AreEqual(1, PrefabUtility.GetRemovedGameObjects(variant).Count);

            // The scene object now follows the variant and carries nothing itself.
            Assert.AreSame(variant, PrefabUtility.GetCorrespondingObjectFromSource(derived));
            Assert.IsTrue(hips != null && hips.root == derived.transform);
            Assert.IsEmpty(NonDefaultOverrides(derived));
            Assert.IsEmpty(PrefabUtility.GetAddedGameObjects(derived));
            Assert.IsEmpty(PrefabUtility.GetAddedComponents(derived));
            Assert.IsEmpty(PrefabUtility.GetRemovedComponents(derived));
            Assert.IsEmpty(PrefabUtility.GetRemovedGameObjects(derived));
        }

        [Test]
        public void RenamedAndReparentedObjectsAreDuplicated()
        {
            GameObject baseRoot = BuildAvatar("Base");
            GameObject derived = BuildAvatar("Derived");
            derived.transform.Find("Body").name = "Body_Edited";
            derived.transform.Find("Armature/Hips/Tail")
                .SetParent(derived.transform.Find("Armature/Hips/Spine"), false);

            Convert(derived, SaveAsPrefab(baseRoot, "Base"));

            // Neither is matched: the edited object becomes an addition and the
            // asset's own copy comes back beside it.
            Assert.IsNotNull(derived.transform.Find("Body"));
            Assert.IsNotNull(derived.transform.Find("Body_Edited"));
            Assert.IsNotNull(derived.transform.Find("Armature/Hips/Tail"));
            Assert.IsNotNull(derived.transform.Find("Armature/Hips/Spine/Tail"));
            CollectionAssert.AreEquivalent(
                new[] { "Tail", "Body_Edited" }, AddedGameObjectNames(derived));
        }

        [Test]
        public void EditsUnderDuplicateSiblingNamesAreLost()
        {
            GameObject baseRoot = BuildAvatar("Base");
            GameObject derived = BuildAvatar("Derived");
            foreach (GameObject root in new[] { baseRoot, derived })
            {
                Child(root.transform, "Ribbon").gameObject.AddComponent<BoxCollider>();
                Child(root.transform, "Ribbon").gameObject.AddComponent<BoxCollider>();
            }
            derived.transform.GetChild(derived.transform.childCount - 1)
                .GetComponent<BoxCollider>().size = new Vector3(3f, 3f, 3f);

            Convert(derived, SaveAsPrefab(baseRoot, "Base"));

            Assert.IsEmpty(NonDefaultOverrides(derived));
            foreach (Transform child in derived.transform)
            {
                if (child.name == "Ribbon")
                {
                    Assert.AreEqual(Vector3.one, child.GetComponent<BoxCollider>().size);
                }
            }
        }

        [Test]
        public void NestedOutfitInstancesAreMatchedAndKeepTheirOverrides()
        {
            GameObject outfit = SaveOutfitPrefab();
            GameObject baseRoot = BuildAvatar("Base");
            AddOutfit(baseRoot, outfit, "Outfit", new Vector3(2f, 2f, 2f));
            GameObject derived = BuildAvatar("Derived");
            AddOutfit(derived, outfit, "Outfit", new Vector3(4f, 4f, 4f));
            AddOutfit(derived, outfit, "SecondOutfit", Vector3.one);

            Convert(derived, SaveAsPrefab(baseRoot, "Base"));

            Assert.AreEqual(1, CountChildren(derived.transform, "Outfit"));
            Transform mesh = derived.transform.Find("Outfit/Mesh");
            Assert.AreEqual(new Vector3(4f, 4f, 4f), mesh.GetComponent<BoxCollider>().size);
            Assert.AreEqual("Outfit", PrefabUtility.GetNearestPrefabInstanceRoot(mesh.gameObject).name);
            CollectionAssert.AreEqual(new[] { "SecondOutfit" }, AddedGameObjectNames(derived));
        }

        [Test]
        public void ConvertingAPrefabInstanceIsRejected()
        {
            GameObject derived = BuildInstancePair(out GameObject variant);

            Assert.Throws<InvalidOperationException>(() => Convert(derived, variant));
        }

        [Test]
        public void ReplacingTheAssetOfAnInstanceDuplicatesSharedAdditions()
        {
            GameObject derived = BuildInstancePair(out GameObject variant);

            PrefabUtility.ReplacePrefabAssetOfPrefabInstance(
                derived,
                variant,
                new PrefabReplacingSettings { objectMatchMode = ObjectMatchMode.ByHierarchy },
                InteractionMode.AutomatedAction);

            Assert.AreEqual(2, CountChildren(derived.transform, "Outfit"));
            Assert.AreEqual(2, CountChildren(derived.transform.Find("Armature/Hips"), "Accessory"));
        }

        [Test]
        public void UnpackingAnInstanceThenConvertingMatchesSharedAdditions()
        {
            GameObject derived = BuildInstancePair(out GameObject variant);

            PrefabUtility.UnpackPrefabInstance(
                derived, PrefabUnpackMode.OutermostRoot, InteractionMode.AutomatedAction);
            Convert(derived, variant);

            Assert.AreSame(variant, PrefabUtility.GetCorrespondingObjectFromSource(derived));
            Assert.AreEqual(1, CountChildren(derived.transform, "Outfit"));
            Assert.AreEqual(1, CountChildren(derived.transform.Find("Armature/Hips"), "Accessory"));
            CollectionAssert.AreEqual(new[] { "Hat" }, AddedGameObjectNames(derived));
            CollectionAssert.AreEquivalent(
                new[]
                {
                    "BoxCollider(Mesh) m_Size.x",
                    "BoxCollider(Mesh) m_Size.y",
                    "BoxCollider(Mesh) m_Size.z",
                    "Transform(Spine) m_LocalPosition.y",
                },
                NonDefaultOverrides(derived));
        }

        /// <summary>
        /// A plain duplicate carrying one edit of each kind, and the prefab asset
        /// saved from the hierarchy it was duplicated from.
        /// </summary>
        private GameObject BuildEditedDuplicate(out GameObject asset)
        {
            GameObject baseRoot = BuildAvatar("Base");
            GameObject derived = BuildAvatar("Derived");
            Transform body = derived.transform.Find("Body");
            Transform spine = derived.transform.Find("Armature/Hips/Spine");

            body.GetComponent<BoxCollider>().size = new Vector3(2f, 2f, 2f);
            spine.localPosition = new Vector3(0f, 0.5f, 0f);
            Child(spine, "Accessory").gameObject.AddComponent<BoxCollider>();
            derived.transform.Find("Armature/Hips").gameObject.AddComponent<SphereCollider>();
            Object.DestroyImmediate(body.GetComponent<CapsuleCollider>());
            Object.DestroyImmediate(derived.transform.Find("Armature/Hips/Tail").gameObject);

            asset = SaveAsPrefab(baseRoot, "Base");
            return derived;
        }

        /// <summary>
        /// Two instances of one model prefab that share an override, an added
        /// outfit and an added accessory. The first is saved as a variant; the
        /// second, which carries further edits, is returned.
        /// </summary>
        private GameObject BuildInstancePair(out GameObject variant)
        {
            GameObject outfit = SaveOutfitPrefab();
            GameObject model = SaveAsPrefab(BuildAvatar("Model"), "Model");

            GameObject[] instances = new GameObject[2];
            for (int index = 0; index < instances.Length; index++)
            {
                GameObject instance = Track((GameObject)PrefabUtility.InstantiatePrefab(model));
                instance.transform.Find("Body").GetComponent<BoxCollider>().size =
                    new Vector3(2f, 2f, 2f);
                AddOutfit(instance, outfit, "Outfit", new Vector3(2f, 2f, 2f));
                Child(instance.transform.Find("Armature/Hips"), "Accessory");
                instances[index] = instance;
            }

            GameObject derived = instances[1];
            derived.name = "Derived";
            derived.transform.Find("Armature/Hips/Spine").localPosition = new Vector3(0f, 0.5f, 0f);
            derived.transform.Find("Outfit/Mesh").GetComponent<BoxCollider>().size =
                new Vector3(4f, 4f, 4f);
            Child(derived.transform, "Hat");

            variant = PrefabUtility.SaveAsPrefabAssetAndConnect(
                instances[0], _folder + "/BaseVariant.prefab", InteractionMode.AutomatedAction);
            Assert.AreEqual(PrefabAssetType.Variant, PrefabUtility.GetPrefabAssetType(variant));
            return derived;
        }

        private static void Convert(GameObject plain, GameObject asset)
        {
            var settings = new ConvertToPrefabInstanceSettings
            {
                objectMatchMode = ObjectMatchMode.ByHierarchy,
                recordPropertyOverridesOfMatches = true,
                gameObjectsNotMatchedBecomesOverride = true,
                componentsNotMatchedBecomesOverride = true,
            };
            PrefabUtility.ConvertToPrefabInstance(
                plain, asset, settings, InteractionMode.AutomatedAction);
        }

        private GameObject BuildAvatar(string name)
        {
            GameObject root = Track(new GameObject(name));
            Transform armature = Child(root.transform, "Armature");
            Transform hips = Child(armature, "Hips");
            Transform spine = Child(hips, "Spine");
            Child(hips, "Tail");
            Transform body = Child(root.transform, "Body");
            SkinnedMeshRenderer renderer = body.gameObject.AddComponent<SkinnedMeshRenderer>();
            renderer.rootBone = hips;
            renderer.bones = new[] { hips, spine };
            body.gameObject.AddComponent<BoxCollider>();
            body.gameObject.AddComponent<CapsuleCollider>();
            return root;
        }

        // Shares bone names with the avatar, as an outfit's armature does.
        private GameObject SaveOutfitPrefab()
        {
            GameObject root = Track(new GameObject("Outfit"));
            Child(Child(root.transform, "Armature"), "Hips");
            Child(root.transform, "Mesh").gameObject.AddComponent<BoxCollider>();
            return SaveAsPrefab(root, "Outfit");
        }

        private static void AddOutfit(GameObject avatar, GameObject outfit, string name, Vector3 size)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(outfit, avatar.transform);
            instance.name = name;
            instance.transform.Find("Mesh").GetComponent<BoxCollider>().size = size;
        }

        private GameObject SaveAsPrefab(GameObject root, string name)
        {
            return PrefabUtility.SaveAsPrefabAssetAndConnect(
                root, _folder + "/" + name + ".prefab", InteractionMode.AutomatedAction);
        }

        private GameObject Track(GameObject root)
        {
            _roots.Add(root);
            return root;
        }

        private static Transform Child(Transform parent, string name)
        {
            var child = new GameObject(name);
            child.transform.SetParent(parent, false);
            return child.transform;
        }

        private static int CountChildren(Transform parent, string name)
        {
            int count = 0;
            foreach (Transform child in parent)
            {
                if (child.name == name)
                {
                    count++;
                }
            }
            return count;
        }

        /// <summary>
        /// Property overrides other than the root's name and placement, which
        /// every prefab instance carries.
        /// </summary>
        private static List<string> NonDefaultOverrides(GameObject root)
        {
            GameObject sourceRoot = PrefabUtility.GetCorrespondingObjectFromSource(root);
            var result = new List<string>();
            foreach (PropertyModification modification in
                PrefabUtility.GetPropertyModifications(root) ?? new PropertyModification[0])
            {
                Object target = modification.target;
                if (target == sourceRoot || target == sourceRoot.transform)
                {
                    continue;
                }
                result.Add(target.GetType().Name + "(" + target.name + ") " + modification.propertyPath);
            }
            return result;
        }

        private static List<string> AddedGameObjectNames(GameObject root)
        {
            var result = new List<string>();
            foreach (AddedGameObject added in PrefabUtility.GetAddedGameObjects(root))
            {
                result.Add(added.instanceGameObject.name);
            }
            return result;
        }

        private static List<string> AddedComponentTypes(GameObject root)
        {
            var result = new List<string>();
            foreach (AddedComponent added in PrefabUtility.GetAddedComponents(root))
            {
                result.Add(added.instanceComponent.GetType().Name);
            }
            return result;
        }
    }
}
