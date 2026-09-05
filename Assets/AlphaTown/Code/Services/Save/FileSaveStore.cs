using System;
using System.IO;
using System.Text;
using AlphaTown.Core.Diagnostics;

namespace AlphaTown.Services.Save
{
    /// <summary>
    /// Local file store under persistentDataPath.
    ///
    /// Writes are staged through a temp file and the previous save is kept as a backup, because
    /// the OS can kill a mobile app mid-write at any moment. A half-written save that replaces a
    /// good one is an account lost, so reads fall back to the backup when the primary will not parse.
    /// </summary>
    public sealed class FileSaveStore : ISaveStore
    {
        const string Extension = ".json";
        const string TempExtension = ".tmp";
        const string BackupExtension = ".bak";

        readonly string _directory;

        public FileSaveStore(string directory)
        {
            _directory = Guard.NotNullOrEmpty(directory, nameof(directory));
        }

        /// <summary>Default location: {persistentDataPath}/Saves.</summary>
        public static FileSaveStore CreateDefault() =>
            new FileSaveStore(Path.Combine(UnityEngine.Application.persistentDataPath, "Saves"));

        public bool Exists(string key) => File.Exists(PathFor(key, Extension));

        public bool TryRead(string key, out string contents)
        {
            if (TryReadFile(PathFor(key, Extension), out contents)) return true;

            // Primary is missing or unreadable — fall back to the previous good write.
            if (TryReadFile(PathFor(key, BackupExtension), out contents))
            {
                Log.Warn("Save", "Primary save '" + key + "' was unreadable; recovered the backup.");
                return true;
            }

            contents = null;
            return false;
        }

        public bool TryWrite(string key, string contents)
        {
            var target = PathFor(key, Extension);
            var temp = PathFor(key, TempExtension);
            var backup = PathFor(key, BackupExtension);

            try
            {
                Directory.CreateDirectory(_directory);
                File.WriteAllText(temp, contents, Encoding.UTF8);

                // File.Replace is not dependable across every mobile filesystem, so move by hand.
                if (File.Exists(target))
                {
                    if (File.Exists(backup)) File.Delete(backup);
                    File.Move(target, backup);
                }

                File.Move(temp, target);
                return true;
            }
            catch (Exception exception)
            {
                Log.Error("Save", "Failed to write save '" + key + "': " + exception.Message);
                TryDeleteFile(temp);
                return false;
            }
        }

        public bool Delete(string key)
        {
            var deleted = TryDeleteFile(PathFor(key, Extension));
            TryDeleteFile(PathFor(key, BackupExtension));
            TryDeleteFile(PathFor(key, TempExtension));
            return deleted;
        }

        string PathFor(string key, string extension) => Path.Combine(_directory, Sanitize(key) + extension);

        /// <summary>Keys come from code today, but never let one escape the save directory.</summary>
        static string Sanitize(string key)
        {
            Guard.NotNullOrEmpty(key, nameof(key));

            var builder = new StringBuilder(key.Length);
            for (var i = 0; i < key.Length; i++)
            {
                var character = key[i];
                var safe = char.IsLetterOrDigit(character) || character == '_' || character == '-';
                builder.Append(safe ? character : '_');
            }

            return builder.ToString();
        }

        static bool TryReadFile(string path, out string contents)
        {
            try
            {
                if (File.Exists(path))
                {
                    contents = File.ReadAllText(path, Encoding.UTF8);
                    return !string.IsNullOrEmpty(contents);
                }
            }
            catch (Exception exception)
            {
                Log.Error("Save", "Failed to read '" + path + "': " + exception.Message);
            }

            contents = null;
            return false;
        }

        static bool TryDeleteFile(string path)
        {
            try
            {
                if (!File.Exists(path)) return false;
                File.Delete(path);
                return true;
            }
            catch (Exception exception)
            {
                Log.Error("Save", "Failed to delete '" + path + "': " + exception.Message);
                return false;
            }
        }
    }
}
