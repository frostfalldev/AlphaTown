using System.IO;
using UnityEditor;
using UnityEngine;

namespace AlphaTown.EditorTools.Setup
{
    /// <summary>
    /// Small helpers for writing ScriptableObject content from code.
    ///
    /// Every field on a definition is private and serialized, which is right for runtime — nothing
    /// should be able to rewrite content at play time — and awkward for a generator. Going through
    /// <see cref="SerializedObject"/> keeps that encapsulation intact rather than opening the
    /// fields up just so a builder can reach them.
    ///
    /// Missing properties are reported and skipped rather than thrown, so a field renamed in Data
    /// leaves the rest of the content generated and names the one thing to fix.
    /// </summary>
    internal static class AssetAuthoring
    {
        internal static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;

            var parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            var leaf = Path.GetFileName(path);
            if (string.IsNullOrEmpty(parent) || string.IsNullOrEmpty(leaf)) return;

            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }

        /// <summary>
        /// Loads the asset at <paramref name="path"/> or creates it. Re-running the builder
        /// therefore updates content in place rather than orphaning every reference to it.
        /// </summary>
        internal static TAsset CreateOrLoad<TAsset>(string path) where TAsset : ScriptableObject
        {
            var existing = AssetDatabase.LoadAssetAtPath<TAsset>(path);
            if (existing != null) return existing;

            var created = ScriptableObject.CreateInstance<TAsset>();
            EnsureFolder(Path.GetDirectoryName(path)?.Replace('\\', '/'));
            AssetDatabase.CreateAsset(created, path);
            return created;
        }

        internal static SerializedObject Edit(Object asset) => new SerializedObject(asset);

        internal static void Set(SerializedObject serialized, string field, string value) =>
            With(serialized, field, property => property.stringValue = value);

        internal static void Set(SerializedObject serialized, string field, int value) =>
            With(serialized, field, property => property.intValue = value);

        internal static void Set(SerializedObject serialized, string field, float value) =>
            With(serialized, field, property => property.floatValue = value);

        internal static void Set(SerializedObject serialized, string field, bool value) =>
            With(serialized, field, property => property.boolValue = value);

        internal static void SetEnum(SerializedObject serialized, string field, int value) =>
            With(serialized, field, property => property.enumValueIndex = value);

        internal static void SetReference(SerializedObject serialized, string field, Object value) =>
            With(serialized, field, property => property.objectReferenceValue = value);

        internal static void SetColour(SerializedObject serialized, string field, Color value) =>
            With(serialized, field, property => property.colorValue = value);

        internal static void SetIntArray(SerializedObject serialized, string field, int[] values)
        {
            With(serialized, field, property =>
            {
                property.arraySize = values.Length;
                for (var i = 0; i < values.Length; i++)
                {
                    property.GetArrayElementAtIndex(i).intValue = values[i];
                }
            });
        }

        internal static void SetReferenceArray(SerializedObject serialized, string field, Object[] values)
        {
            With(serialized, field, property =>
            {
                property.arraySize = values.Length;
                for (var i = 0; i < values.Length; i++)
                {
                    property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
                }
            });
        }

        /// <summary>
        /// Fills an array of nested serializable structs — item amounts, currency entries and the
        /// like. The callback receives one element and the fields inside it.
        /// </summary>
        internal static void SetArray(SerializedObject serialized, string field, int count,
                                      System.Action<SerializedProperty, int> fill)
        {
            With(serialized, field, property =>
            {
                property.arraySize = count;
                for (var i = 0; i < count; i++) fill(property.GetArrayElementAtIndex(i), i);
            });
        }

        internal static void SetElement(SerializedProperty element, string field, int value) =>
            WithChild(element, field, child => child.intValue = value);

        internal static void SetElement(SerializedProperty element, string field, float value) =>
            WithChild(element, field, child => child.floatValue = value);

        internal static void SetElement(SerializedProperty element, string field, bool value) =>
            WithChild(element, field, child => child.boolValue = value);

        internal static void SetElement(SerializedProperty element, string field, Object value) =>
            WithChild(element, field, child => child.objectReferenceValue = value);

        internal static void Apply(SerializedObject serialized)
        {
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(serialized.targetObject);
        }

        static void With(SerializedObject serialized, string field, System.Action<SerializedProperty> apply)
        {
            var property = serialized.FindProperty(field);
            if (property == null)
            {
                Debug.LogWarning("[AlphaTown] " + serialized.targetObject.name +
                                 " has no serialized field '" + field + "'. Skipped.");
                return;
            }

            apply(property);
        }

        static void WithChild(SerializedProperty element, string field, System.Action<SerializedProperty> apply)
        {
            var child = element.FindPropertyRelative(field);
            if (child == null)
            {
                Debug.LogWarning("[AlphaTown] Nested field '" + field + "' not found. Skipped.");
                return;
            }

            apply(child);
        }
    }
}
