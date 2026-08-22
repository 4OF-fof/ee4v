using System;
using System.IO;
using System.Linq;
using Ee4v.Core.Settings;
using UnityEditor;
using UnityEngine;

namespace Ee4v.FaceExpression
{
    internal interface IBlendShapePresetStore
    {
        event Action Changed;

        BlendShapeNamePresetState Load();

        void Save(BlendShapeFbxPreset preset);

        void OpenDirectory();
    }

    internal sealed class BlendShapePresetStorage : IBlendShapePresetStore
    {
        internal static readonly BlendShapePresetStorage Shared =
            new BlendShapePresetStorage();

        private BlendShapePresetStorage()
        {
            GlobalDataSettings.PathChanged += () => Changed?.Invoke();
        }

        public event Action Changed;

        private string DirectoryPath => Path.Combine(
            GlobalDataSettings.RootDirectory,
            "preset",
            "blendshape");

        public BlendShapeNamePresetState Load()
        {
            return new BlendShapePresetFileStore(DirectoryPath).Load();
        }

        public void Save(BlendShapeFbxPreset preset)
        {
            new BlendShapePresetFileStore(DirectoryPath).Save(preset);
            Changed?.Invoke();
        }

        public void OpenDirectory()
        {
            Directory.CreateDirectory(DirectoryPath);
            EditorUtility.RevealInFinder(DirectoryPath);
        }
    }

    internal sealed class BlendShapePresetFileStore
    {
        private readonly string _directoryPath;

        internal BlendShapePresetFileStore(string directoryPath)
        {
            if (string.IsNullOrWhiteSpace(directoryPath))
            {
                throw new ArgumentException(
                    "BlendShape preset directory is required.",
                    nameof(directoryPath));
            }

            _directoryPath = Path.GetFullPath(directoryPath);
        }

        internal BlendShapeNamePresetState Load()
        {
            var state = new BlendShapeNamePresetState();
            if (!Directory.Exists(_directoryPath))
            {
                return state;
            }

            foreach (var path in Directory
                         .EnumerateFiles(_directoryPath, "*.json")
                         .OrderBy(value => value, StringComparer.OrdinalIgnoreCase))
            {
                var preset = Read(path);
                if (preset != null)
                {
                    state.presets.Add(preset);
                }
            }

            BlendShapeNamePresetSetting.Normalize(state);
            return state;
        }

        internal void Save(BlendShapeFbxPreset preset)
        {
            if (preset == null || string.IsNullOrWhiteSpace(preset.assetGuid))
            {
                throw new ArgumentException(
                    "A BlendShape preset with an asset GUID is required.",
                    nameof(preset));
            }

            var fileName = GetFileName(preset.name);
            Directory.CreateDirectory(_directoryPath);
            var path = Path.Combine(_directoryPath, fileName);
            if (File.Exists(path))
            {
                var existing = Read(path);
                if (existing == null || !string.Equals(
                        existing.assetGuid,
                        preset.assetGuid,
                        StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        "A different BlendShape preset already uses " +
                        fileName + ".");
                }
            }

            var state = new BlendShapeNamePresetState();
            state.presets.Add(preset);
            BlendShapeNamePresetSetting.Normalize(state);
            var normalized = state.presets.Single();
            File.WriteAllText(path, JsonUtility.ToJson(normalized, true));

            foreach (var otherPath in Directory
                         .EnumerateFiles(_directoryPath, "*.json")
                         .Where(value => !string.Equals(
                             value,
                             path,
                             StringComparison.OrdinalIgnoreCase)))
            {
                var other = Read(otherPath);
                if (other != null && string.Equals(
                        other.assetGuid,
                        normalized.assetGuid,
                        StringComparison.Ordinal))
                {
                    File.Delete(otherPath);
                }
            }
        }

        private static BlendShapeFbxPreset Read(string path)
        {
            try
            {
                return JsonUtility.FromJson<BlendShapeFbxPreset>(
                    File.ReadAllText(path));
            }
            catch (ArgumentException)
            {
                return null;
            }
            catch (IOException)
            {
                return null;
            }
            catch (UnauthorizedAccessException)
            {
                return null;
            }
        }

        private static string GetFileName(string fbxName)
        {
            fbxName = (fbxName ?? string.Empty).Trim();
            if (fbxName.Length == 0 ||
                fbxName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
                fbxName.IndexOf(Path.DirectorySeparatorChar) >= 0 ||
                fbxName.IndexOf(Path.AltDirectorySeparatorChar) >= 0)
            {
                throw new InvalidOperationException(
                    "The FBX name cannot be used as a preset file name.");
            }

            return fbxName + ".json";
        }
    }
}
