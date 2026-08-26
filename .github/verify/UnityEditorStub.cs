// Minimal UnityEditor surface used by io.github.sabas0ba.sabatools.core,
// so the Editor assembly can be compiled outside Unity.
//
// CAVEAT: these signatures are written by hand. They verify that the package's
// own code is internally consistent and syntactically valid, and (because the
// UnityEngine side uses real reference assemblies) that its UnityEngine usage
// is correct. They do NOT independently verify UnityEditor signatures --
// UnityEditor.dll is not redistributable, so there is nothing to check against.
// The Unity workflow is the tier that closes that gap.
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace UnityEditor
{
    public enum MessageType { None = 0, Info = 1, Warning = 2, Error = 3 }

    /// <summary>
    /// Unity's precomputed type index. The core package uses it to find
    /// InspectionModule implementations in assemblies it does not reference,
    /// so the shape of the return value matters here even though the offline
    /// build never enumerates anything.
    /// </summary>
    public static class TypeCache
    {
        public class TypeCollection : IEnumerable<Type>
        {
            public IEnumerator<Type> GetEnumerator() =>
                ((IEnumerable<Type>)new Type[0]).GetEnumerator();

            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }

        public static TypeCollection GetTypesDerivedFrom<T>() => new TypeCollection();
        public static TypeCollection GetTypesDerivedFrom(Type parentType) => new TypeCollection();
    }

    [AttributeUsage(AttributeTargets.Method)]
    public sealed class MenuItemAttribute : Attribute
    {
        public MenuItemAttribute(string itemName) { }
        public MenuItemAttribute(string itemName, bool isValidateFunction) { }
        public MenuItemAttribute(string itemName, bool isValidateFunction, int priority) { }
    }

    public enum SerializedPropertyType
    {
        Generic = -1,
        Integer = 0,
        Boolean = 1,
        Float = 2,
        String = 3,
        Color = 4,
        ObjectReference = 5,
        LayerMask = 6,
        Enum = 7,
    }

    public class SerializedProperty
    {
        public SerializedPropertyType propertyType => SerializedPropertyType.Generic;
        public string propertyPath => string.Empty;
        public UnityEngine.Object objectReferenceValue { get; set; }
        public int objectReferenceInstanceIDValue { get; set; }
        public bool Next(bool enterChildren) => false;
    }

    public class SerializedObject
    {
        public SerializedObject(UnityEngine.Object obj) { }
        public SerializedProperty GetIterator() => new SerializedProperty();
        public SerializedProperty FindProperty(string propertyPath) => null;
        public void Update() { }
        public bool ApplyModifiedProperties() => false;
    }

    public class Editor : ScriptableObject
    {
        public UnityEngine.Object target { get; set; }
        public SerializedObject serializedObject { get; set; }
        public virtual void OnInspectorGUI() { }
    }

    public class EditorWindow : ScriptableObject
    {
        public Vector2 minSize { get; set; }
        public Vector2 maxSize { get; set; }

        public static T GetWindow<T>(bool utility, string title, bool focus) where T : EditorWindow =>
            CreateInstance<T>();

        public void Show() { }
        public void Close() { }
        public void Repaint() { }
    }

    public enum PrefabInstanceStatus
    {
        NotAPrefab = 0,
        Connected = 1,
        MissingAsset = 3,
    }

    public static class PrefabUtility
    {
        public static PrefabInstanceStatus GetPrefabInstanceStatus(UnityEngine.Object componentOrGameObject) =>
            PrefabInstanceStatus.NotAPrefab;
    }

    public static class EditorStyles
    {
        public static GUIStyle boldLabel => null;
        public static GUIStyle miniLabel => null;
        public static GUIStyle label => null;
    }

    public static class EditorGUIUtility
    {
        public static void PingObject(UnityEngine.Object obj) { }
        public static string systemCopyBuffer { get; set; }
    }

    public static class EditorUtility
    {
        public static void SetDirty(UnityEngine.Object target) { }
        public static UnityEngine.Object[] CollectDependencies(UnityEngine.Object[] roots) =>
            new UnityEngine.Object[0];
        public static string SaveFilePanel(string title, string directory, string defaultName, string extension) =>
            string.Empty;
        public static bool DisplayDialog(string title, string message, string ok) => false;
    }

    /// <summary>
    /// Used only by the EditMode tests, which need the material and texture in
    /// FindsTexturesThroughMaterials to be assets: CollectDependencies follows
    /// persistent references and nothing else.
    /// </summary>
    public static class AssetDatabase
    {
        public static string CreateFolder(string parentFolder, string newFolderName) => string.Empty;
        public static bool DeleteAsset(string path) => false;
        public static void CreateAsset(UnityEngine.Object asset, string path) { }
        public static void SaveAssets() { }
        public static T LoadAssetAtPath<T>(string assetPath) where T : UnityEngine.Object => null;
    }

    /// <summary>
    /// The editor serializer. Unlike UnityEngine.JsonUtility it accepts engine
    /// types, which is what the untouched-hierarchy test compares.
    /// </summary>
    public static class EditorJsonUtility
    {
        public static string ToJson(object obj) => string.Empty;
        public static string ToJson(object obj, bool prettyPrint) => string.Empty;
    }

    public static class Selection
    {
        public static GameObject activeGameObject { get; set; }
        public static UnityEngine.Object activeObject { get; set; }
        public static UnityEngine.Object[] objects { get; set; }
    }

    public static class EditorGUI
    {
        public static void BeginChangeCheck() { }
        public static bool EndChangeCheck() => false;

        public class DisabledScope : IDisposable
        {
            public DisabledScope(bool disabled) { }
            public void Dispose() { }
        }

        public class IndentLevelScope : IDisposable
        {
            public IndentLevelScope() { }
            public IndentLevelScope(int increment) { }
            public void Dispose() { }
        }
    }

    public static class EditorGUILayout
    {
        public static void Space() { }
        public static void Space(float width) { }

        public static void LabelField(string label, params GUILayoutOption[] options) { }
        public static void LabelField(string label, GUIStyle style, params GUILayoutOption[] options) { }
        public static void LabelField(string label, string label2, params GUILayoutOption[] options) { }
        public static void LabelField(string label, string label2, GUIStyle style, params GUILayoutOption[] options) { }

        public static void HelpBox(string message, MessageType type) { }
        public static void HelpBox(string message, MessageType type, bool wide) { }

        public static bool Foldout(bool foldout, string content) => foldout;
        public static bool Foldout(bool foldout, string content, bool toggleOnLabelClick) => foldout;

        public static UnityEngine.Object ObjectField(UnityEngine.Object obj, Type objType, bool allowSceneObjects, params GUILayoutOption[] options) => obj;
        public static UnityEngine.Object ObjectField(string label, UnityEngine.Object obj, Type objType, bool allowSceneObjects, params GUILayoutOption[] options) => obj;

        public static Enum EnumPopup(string label, Enum selected, params GUILayoutOption[] options) => selected;

        public static bool Toggle(string label, bool value, params GUILayoutOption[] options) => value;

        public static Vector2 BeginScrollView(Vector2 position, params GUILayoutOption[] options) => position;
        public static void EndScrollView() { }

        public class HorizontalScope : IDisposable
        {
            public HorizontalScope(params GUILayoutOption[] options) { }
            public HorizontalScope(GUIStyle style, params GUILayoutOption[] options) { }
            public void Dispose() { }
        }

        public class VerticalScope : IDisposable
        {
            public VerticalScope(params GUILayoutOption[] options) { }
            public VerticalScope(GUIStyle style, params GUILayoutOption[] options) { }
            public void Dispose() { }
        }
    }
}

// Enough NUnit to compile-check the CI project's EditMode tests offline. The
// real assertions run inside Unity via .github/workflows/unity.yml.
namespace NUnit.Framework
{
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class TestAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Method)]
    public sealed class SetUpAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Method)]
    public sealed class TearDownAttribute : Attribute { }

    public static class Assert
    {
        public static void Fail(string message) { }

        public static void IsNull(object value) { }
        public static void IsNull(object value, string message) { }
        public static void IsNotNull(object value) { }
        public static void IsNotNull(object value, string message) { }

        public static void IsTrue(bool condition) { }
        public static void IsTrue(bool condition, string message) { }
        public static void IsFalse(bool condition) { }
        public static void IsFalse(bool condition, string message) { }

        public static void AreEqual(object expected, object actual) { }
        public static void AreEqual(object expected, object actual, string message) { }
        public static void AreEqual(double expected, double actual, double delta) { }
        public static void AreEqual(double expected, double actual, double delta, string message) { }
        public static void AreNotEqual(object expected, object actual) { }
        public static void AreNotEqual(object expected, object actual, string message) { }

        public static void Greater(double arg1, double arg2) { }
        public static void Greater(double arg1, double arg2, string message) { }
        public static void Less(double arg1, double arg2) { }
        public static void Less(double arg1, double arg2, string message) { }
    }
}
