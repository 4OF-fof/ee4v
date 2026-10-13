using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Ee4v.Core.I18n;
using Ee4v.Core.Settings;
using Ee4v.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ee4v.AvatarInfo
{
    [InitializeOnLoad]
    public static class AvatarInfoSettings
    {
        private static readonly TimeSpan MatchTimeout = TimeSpan.FromMilliseconds(50);
        private static readonly SettingDefinition<string[]> PrefabExclusions =
            new SettingDefinition<string[]>("avatarInfo.attachmentWarningExcludedPrefabNames", SettingScope.User,
                "AvatarInfo", "settings.section.prefabExclusions", "settings.prefabExclusions.label",
                "settings.prefabExclusions.tooltip",
                new[] { @"^(?:ExpressionMenu|MenuItem|Gimmick)$", @"_(?:ExpressionMenu|FacialSet)$" },
                validator: ValidatePatterns, keywords: new[] { "avatar", "prefab", "attachment", "warning", "list", "exclude", "regex" });

        private static string[] _cachedPatterns;
        private static Regex[] _cachedExpressions;

        public static event Action PrefabExclusionsChanged;

        static AvatarInfoSettings()
        {
            SettingDrawerApi.Register(PrefabExclusions, CreateField);
            CoreSettings.Current.Register(PrefabExclusions);
            CoreSettings.Current.Changed += (sender, args) =>
            {
                if (!ReferenceEquals(args.Definition, PrefabExclusions)) return;
                _cachedPatterns = null;
                _cachedExpressions = null;
                PrefabExclusionsChanged?.Invoke();
            };
        }

        public static bool IsPrefabExcluded(GameObject prefabInstance)
        {
            if (prefabInstance == null) return false;
            var path = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(prefabInstance);
            return !string.IsNullOrEmpty(path) && IsPrefabNameExcluded(Path.GetFileNameWithoutExtension(path));
        }

        private static bool IsPrefabNameExcluded(string prefabName)
        {
            var settings = CoreSettings.Current;
            settings.Register(PrefabExclusions);
            var patterns = settings.Get(PrefabExclusions);
            if (!ReferenceEquals(patterns, _cachedPatterns))
            {
                _cachedExpressions = patterns.Where(pattern => !string.IsNullOrWhiteSpace(pattern))
                    .Select(CreateRegex).ToArray();
                _cachedPatterns = patterns;
            }
            foreach (var expression in _cachedExpressions)
            {
                try { if (expression.IsMatch(prefabName ?? string.Empty)) return true; }
                catch (RegexMatchTimeoutException) { }
            }
            return false;
        }

        private static Regex CreateRegex(string pattern) =>
            new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, MatchTimeout);

        private static SettingValidationResult ValidatePatterns(string[] patterns)
        {
            if (patterns == null)
                return SettingValidationResult.Error(I18N.Get("settings.prefabExclusions.invalidList"));
            foreach (var pattern in patterns)
            {
                if (string.IsNullOrWhiteSpace(pattern)) continue;
                try { CreateRegex(pattern); }
                catch (ArgumentException)
                {
                    return SettingValidationResult.Error(I18N.Get(
                        "settings.prefabExclusions.invalidPattern", pattern));
                }
            }
            return SettingValidationResult.Success;
        }

        private static VisualElement CreateField(SettingDrawerContext<string[]> context)
        {
            var field = new ListField<string>(new ListFieldState<string>(context.Value ?? Array.Empty<string>(),
                (value, notify) =>
                {
                    var input = new InputField(new InputFieldState(value,
                        placeholder: I18N.Get("settings.prefabExclusions.placeholder")));
                    input.ValueChanged += notify;
                    return input;
                }, () => string.Empty, string.IsNullOrWhiteSpace, item => ((InputField)item).FocusInput(),
                context.Tooltip, I18N.Get("settings.prefabExclusions.add"),
                I18N.Get("settings.prefabExclusions.remove")));
            field.ValuesChanged += values => context.NotifyValueChanged(values
                .Where(value => !string.IsNullOrWhiteSpace(value)).ToArray());
            return field;
        }
    }
}
