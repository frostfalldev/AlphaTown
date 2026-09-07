using System;
using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Text;

namespace UnityEngine
{
    /// <summary>
    /// A stand-in for Unity's JsonUtility that keeps its constraints rather than papering over
    /// them: public fields only, arrays not dictionaries, enums as ints, no polymorphism, and a
    /// null nested object round-tripping to a default instance rather than back to null.
    ///
    /// Matching the constraints matters more than matching the implementation. A permissive
    /// serializer here would let a save DTO through that Unity would silently mangle on device.
    /// </summary>
    public static class JsonUtility
    {
        public static string ToJson(object value) => ToJson(value, false);

        public static string ToJson(object value, bool prettyPrint)
        {
            var builder = new StringBuilder();
            WriteObject(builder, value, prettyPrint, 0);
            return builder.ToString();
        }

        public static T FromJson<T>(string json) => (T)FromJson(json, typeof(T));

        public static object FromJson(string json, Type type)
        {
            var reader = new Reader(json);
            reader.SkipWhitespace();
            var value = reader.ReadValue(type);
            return value;
        }

        // --- Writing ------------------------------------------------------------------------

        static void WriteObject(StringBuilder builder, object value, bool pretty, int depth)
        {
            if (value == null) { builder.Append("{}"); return; }

            var fields = Fields(value.GetType());
            builder.Append('{');

            var first = true;
            foreach (var field in fields)
            {
                if (!first) builder.Append(',');
                first = false;

                if (pretty) { builder.Append('\n'); builder.Append(' ', (depth + 1) * 4); }

                builder.Append('"').Append(field.Name).Append("\":");
                if (pretty) builder.Append(' ');

                WriteValue(builder, field.GetValue(value), field.FieldType, pretty, depth + 1);
            }

            if (pretty && !first) { builder.Append('\n'); builder.Append(' ', depth * 4); }
            builder.Append('}');
        }

        static void WriteValue(StringBuilder builder, object value, Type type, bool pretty, int depth)
        {
            if (type.IsEnum) { builder.Append(Convert.ToInt32(value)); return; }

            if (type == typeof(string))
            {
                WriteString(builder, (string)value ?? string.Empty);
                return;
            }

            if (type == typeof(bool)) { builder.Append((bool)value ? "true" : "false"); return; }

            if (type == typeof(int) || type == typeof(long) || type == typeof(short) || type == typeof(byte))
            {
                builder.Append(Convert.ToInt64(value).ToString(CultureInfo.InvariantCulture));
                return;
            }

            if (type == typeof(float) || type == typeof(double))
            {
                builder.Append(Convert.ToDouble(value).ToString("R", CultureInfo.InvariantCulture));
                return;
            }

            if (type.IsArray)
            {
                builder.Append('[');
                var array = (Array)value;

                if (array != null)
                {
                    for (var i = 0; i < array.Length; i++)
                    {
                        if (i > 0) builder.Append(',');
                        WriteValue(builder, array.GetValue(i), type.GetElementType(), pretty, depth);
                    }
                }

                builder.Append(']');
                return;
            }

            WriteObject(builder, value, pretty, depth);
        }

        static void WriteString(StringBuilder builder, string value)
        {
            builder.Append('"');
            foreach (var character in value)
            {
                switch (character)
                {
                    case '"': builder.Append("\\\""); break;
                    case '\\': builder.Append("\\\\"); break;
                    case '\n': builder.Append("\\n"); break;
                    case '\r': builder.Append("\\r"); break;
                    case '\t': builder.Append("\\t"); break;
                    default:
                        if (character < ' ') builder.Append("\\u").Append(((int)character).ToString("x4"));
                        else builder.Append(character);
                        break;
                }
            }

            builder.Append('"');
        }

        /// <summary>
        /// Public instance fields, in declaration order, skipping the ones Unity also skips.
        /// Private fields marked [SerializeField] are included, matching Unity.
        /// </summary>
        static FieldInfo[] Fields(Type type)
        {
            var all = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            var kept = new System.Collections.Generic.List<FieldInfo>(all.Length);

            foreach (var field in all)
            {
                if (field.IsInitOnly || field.IsLiteral || field.IsStatic) continue;
                if (!field.IsPublic && field.GetCustomAttribute<SerializeField>() == null) continue;

                kept.Add(field);
            }

            return kept.ToArray();
        }

        // --- Reading ------------------------------------------------------------------------

        sealed class Reader
        {
            readonly string _text;
            int _index;

            public Reader(string text) { _text = text; }

            public void SkipWhitespace()
            {
                while (_index < _text.Length && char.IsWhiteSpace(_text[_index])) _index++;
            }

            public object ReadValue(Type type)
            {
                SkipWhitespace();
                if (_index >= _text.Length) return Default(type);

                if (type.IsArray) return ReadArray(type);
                if (type == typeof(string)) return ReadString();
                if (type.IsEnum) return Enum.ToObject(type, (int)ReadNumber());
                if (type == typeof(bool)) return ReadBool();
                if (type.IsPrimitive) return Convert.ChangeType(ReadNumber(), type, CultureInfo.InvariantCulture);

                return ReadObject(type);
            }

            object ReadObject(Type type)
            {
                Expect('{');
                var instance = Activator.CreateInstance(type);

                SkipWhitespace();
                if (Peek() == '}') { _index++; return instance; }

                while (true)
                {
                    SkipWhitespace();
                    var name = ReadString();
                    SkipWhitespace();
                    Expect(':');

                    var field = FindField(type, name);
                    if (field == null) SkipValue();
                    else field.SetValue(instance, ReadValue(field.FieldType));

                    SkipWhitespace();
                    if (Peek() == ',') { _index++; continue; }

                    Expect('}');
                    return instance;
                }
            }

            object ReadArray(Type type)
            {
                var element = type.GetElementType();
                Expect('[');

                var items = new ArrayList();
                SkipWhitespace();

                if (Peek() == ']') { _index++; return ToArray(items, element); }

                while (true)
                {
                    items.Add(ReadValue(element));
                    SkipWhitespace();

                    if (Peek() == ',') { _index++; continue; }

                    Expect(']');
                    return ToArray(items, element);
                }
            }

            static Array ToArray(ArrayList items, Type element)
            {
                var array = Array.CreateInstance(element, items.Count);
                for (var i = 0; i < items.Count; i++) array.SetValue(items[i], i);
                return array;
            }

            string ReadString()
            {
                Expect('"');
                var builder = new StringBuilder();

                while (_index < _text.Length && _text[_index] != '"')
                {
                    if (_text[_index] == '\\')
                    {
                        _index++;
                        switch (_text[_index])
                        {
                            case 'n': builder.Append('\n'); break;
                            case 'r': builder.Append('\r'); break;
                            case 't': builder.Append('\t'); break;
                            case 'u':
                                builder.Append((char)Convert.ToInt32(_text.Substring(_index + 1, 4), 16));
                                _index += 4;
                                break;
                            default: builder.Append(_text[_index]); break;
                        }
                    }
                    else builder.Append(_text[_index]);

                    _index++;
                }

                Expect('"');
                return builder.ToString();
            }

            bool ReadBool()
            {
                if (string.CompareOrdinal(_text, _index, "true", 0, 4) == 0) { _index += 4; return true; }
                if (string.CompareOrdinal(_text, _index, "false", 0, 5) == 0) { _index += 5; return false; }

                throw new FormatException("Expected a boolean at " + _index + ".");
            }

            double ReadNumber()
            {
                var start = _index;
                while (_index < _text.Length && "+-.eE0123456789".IndexOf(_text[_index]) >= 0) _index++;

                if (start == _index) throw new FormatException("Expected a number at " + start + ".");
                return double.Parse(_text.Substring(start, _index - start), CultureInfo.InvariantCulture);
            }

            void SkipValue()
            {
                SkipWhitespace();
                var c = Peek();

                if (c == '"') { ReadString(); return; }
                if (c == '{' || c == '[')
                {
                    var open = c;
                    var close = c == '{' ? '}' : ']';
                    var depth = 0;

                    while (_index < _text.Length)
                    {
                        if (_text[_index] == '"') { ReadString(); continue; }
                        if (_text[_index] == open) depth++;
                        else if (_text[_index] == close && --depth == 0) { _index++; return; }

                        _index++;
                    }

                    return;
                }

                while (_index < _text.Length && _text[_index] != ',' && _text[_index] != '}' && _text[_index] != ']')
                    _index++;
            }

            char Peek() => _index < _text.Length ? _text[_index] : '\0';

            void Expect(char character)
            {
                SkipWhitespace();
                if (_index >= _text.Length || _text[_index] != character)
                    throw new FormatException("Expected '" + character + "' at " + _index + ".");

                _index++;
            }

            static FieldInfo FindField(Type type, string name)
            {
                foreach (var field in Fields(type))
                {
                    if (string.Equals(field.Name, name, StringComparison.Ordinal)) return field;
                }

                return null;
            }

            static object Default(Type type) => type.IsValueType ? Activator.CreateInstance(type) : null;
        }
    }
}
