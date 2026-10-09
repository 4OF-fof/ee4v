using System;
using System.Collections.Generic;
using System.Linq;
using Ee4v.Core.Settings;
using UnityEditor;
using UnityEngine;

namespace Ee4v.Core.EditorIntegration
{
    public sealed class PreviewMotionSelection
    {
        public string ClipId { get; set; } = string.Empty;
        public bool Loop { get; set; } = true;
    }

    /// <summary>Project-specific motion selections for the shared body-part preview.</summary>
    [InitializeOnLoad]
    public static class AvatarPreviewMotionSettings
    {
        private static readonly BodyPartCategory[] Parts =
        {
            BodyPartCategory.Other, BodyPartCategory.Head, BodyPartCategory.Shoulders,
            BodyPartCategory.Hands, BodyPartCategory.Chest, BodyPartCategory.Waist,
            BodyPartCategory.Legs, BodyPartCategory.Feet
        };

        public static IReadOnlyList<SettingDefinition<PreviewMotionSelection>> Definitions { get; } =
            Array.AsReadOnly(Parts.Select((part, index) => new SettingDefinition<PreviewMotionSelection>(
                "core.preview.motion." + part,
                SettingScope.Project, "Core", "settings.section.previewMotion",
                "settings.previewMotion." + part, "settings.previewMotion.tooltip",
                new PreviewMotionSelection(), order: index,
                keywords: new[] { "preview", "animation", "motion", "clip", "loop", "プレビュー", "アニメーション", "ループ" }
            )).ToArray());

        public static event Action Changed;

        static AvatarPreviewMotionSettings()
        {
            foreach (var definition in Definitions) CoreSettings.Current.Register(definition);
            CoreSettings.Current.Changed += (_, e) =>
            {
                if (Definitions.Any(d => d.Key == e.Definition.Key)) Changed?.Invoke();
            };
        }

        public static PreviewMotionSelection Get(BodyPartCategory? part)
        {
            var key = part ?? BodyPartCategory.Other;
            if (key == BodyPartCategory.Arms) key = BodyPartCategory.Shoulders;
            var index = Array.IndexOf(Parts, key);
            return CoreSettings.Current.Get(Definitions[index < 0 ? 0 : index]) ?? new PreviewMotionSelection();
        }

        public static AnimationClip ResolveClip(PreviewMotionSelection selection)
        {
            return selection != null && GlobalObjectId.TryParse(selection.ClipId ?? string.Empty, out var id)
                ? GlobalObjectId.GlobalObjectIdentifierToObjectSlow(id) as AnimationClip
                : null;
        }

        public static PreviewMotionSelection Capture(AnimationClip clip, bool loop)
        {
            if (clip != null && !EditorUtility.IsPersistent(clip))
                throw new ArgumentException("Select an AnimationClip asset.", nameof(clip));
            return new PreviewMotionSelection
            {
                ClipId = clip != null ? GlobalObjectId.GetGlobalObjectIdSlow(clip).ToString() : string.Empty,
                Loop = loop
            };
        }
    }
}
