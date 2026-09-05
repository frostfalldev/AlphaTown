using System;
using AlphaTown.Core.Diagnostics;
using UnityEngine;

namespace AlphaTown.Services.Save
{
    /// <summary>
    /// JsonUtility-backed serializer. Chosen over a reflection-heavy library because it is
    /// AOT-safe under IL2CPP and allocation-light.
    ///
    /// The constraint it imposes on DTOs: [Serializable] classes with public fields, arrays
    /// rather than dictionaries, and no polymorphism. Every save DTO in Gameplay follows that.
    /// </summary>
    public sealed class JsonSaveSerializer : ISaveSerializer
    {
        readonly bool _prettyPrint;

        public JsonSaveSerializer(bool prettyPrint = false)
        {
            _prettyPrint = prettyPrint;
        }

        public string Serialize<TValue>(TValue value) where TValue : class =>
            JsonUtility.ToJson(value, _prettyPrint);

        public bool TryDeserialize<TValue>(string text, out TValue value) where TValue : class
        {
            value = null;
            if (string.IsNullOrEmpty(text)) return false;

            try
            {
                value = JsonUtility.FromJson<TValue>(text);
            }
            catch (Exception exception)
            {
                Log.Error("Save", "Could not parse " + typeof(TValue).Name + ": " + exception.Message);
                return false;
            }

            return value != null;
        }
    }
}
