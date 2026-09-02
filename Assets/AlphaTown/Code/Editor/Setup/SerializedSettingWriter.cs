using System;
using System.Globalization;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AlphaTown.EditorTools.Setup
{
    /// <summary>
    /// Writes project settings through SerializedProperty.
    ///
    /// Most of what the setup tooling needs to change is a private serialized field with a
    /// read-only public property, and the serialized names outlive the C# API across Unity and
    /// URP upgrades. A property that no longer exists is reported and skipped, never fatal.
    /// </summary>
    internal static class SerializedSettingWriter
    {
        public static bool TryWrite(SerializedProperty property, object value)
        {
            switch (property.propertyType)
            {
                case SerializedPropertyType.Boolean:
                    property.boolValue = Convert.ToBoolean(value, CultureInfo.InvariantCulture);
                    return true;
                // URP enums such as MsaaQuality (1/2/4/8) and ShadowResolution (256..4096) serialize
                // their literal value, not an index, so intValue is the correct accessor.
                case SerializedPropertyType.Integer:
                case SerializedPropertyType.Enum:
                    property.intValue = Convert.ToInt32(value, CultureInfo.InvariantCulture);
                    return true;
                case SerializedPropertyType.Float:
                    property.floatValue = Convert.ToSingle(value, CultureInfo.InvariantCulture);
                    return true;
                case SerializedPropertyType.String:
                    property.stringValue = value as string ?? string.Empty;
                    return true;
                case SerializedPropertyType.ObjectReference:
                    property.objectReferenceValue = value as UnityEngine.Object;
                    return true;
                case SerializedPropertyType.Vector2:
                    property.vector2Value = (Vector2)value;
                    return true;
                case SerializedPropertyType.Vector3:
                    property.vector3Value = (Vector3)value;
                    return true;
                default:
                    return false;
            }
        }

        public static string Describe(SerializedProperty property)
        {
            switch (property.propertyType)
            {
                case SerializedPropertyType.Boolean:
                    return property.boolValue ? "true" : "false";
                case SerializedPropertyType.Integer:
                case SerializedPropertyType.Enum:
                    return property.intValue.ToString(CultureInfo.InvariantCulture);
                case SerializedPropertyType.Float:
                    return property.floatValue.ToString("0.###", CultureInfo.InvariantCulture);
                case SerializedPropertyType.String:
                    return property.stringValue ?? string.Empty;
                case SerializedPropertyType.ObjectReference:
                    return property.objectReferenceValue == null ? "<none>" : property.objectReferenceValue.name;
                case SerializedPropertyType.Vector2:
                    return property.vector2Value.ToString();
                case SerializedPropertyType.Vector3:
                    return property.vector3Value.ToString();
                default:
                    return "<" + property.propertyType + ">";
            }
        }

        /// <summary>Sets a relative property, reporting rather than throwing when it is absent.</summary>
        public static bool TrySetRelative(SerializedProperty parent, string relativeName, object value,
                                          StringBuilder report)
        {
            var property = parent.FindPropertyRelative(relativeName);
            if (property == null)
            {
                report.AppendLine("      ? " + relativeName + " not present in this Unity version, skipped");
                return false;
            }

            if (TryWrite(property, value)) return true;

            report.AppendLine("      ! " + relativeName + " has unsupported type " + property.propertyType);
            return false;
        }

        /// <summary>Sets a top-level property, reporting rather than throwing when it is absent.</summary>
        public static bool TrySet(SerializedObject target, string propertyPath, object value, StringBuilder report)
        {
            var property = target.FindProperty(propertyPath);
            if (property == null)
            {
                report.AppendLine("    ? " + propertyPath + " not present in this Unity version, skipped");
                return false;
            }

            if (TryWrite(property, value)) return true;

            report.AppendLine("    ! " + propertyPath + " has unsupported type " + property.propertyType);
            return false;
        }
    }
}
