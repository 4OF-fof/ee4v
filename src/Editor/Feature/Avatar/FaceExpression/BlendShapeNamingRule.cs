using System;
using System.Linq;
using System.Text.RegularExpressions;
using Ee4v.Core.Settings;

namespace Ee4v.FaceExpression
{
    internal sealed class BlendShapeName
    {
        internal BlendShapeName(
            string group,
            string role,
            string variation,
            string side)
        {
            Group = group ?? string.Empty;
            Role = role ?? string.Empty;
            Variation = variation ?? string.Empty;
            Side = side ?? string.Empty;
        }

        internal string Group { get; }
        internal string Role { get; }
        internal string Variation { get; }
        internal string Side { get; }
    }

    internal sealed class BlendShapeNamingRule
    {
        private static readonly TimeSpan MatchTimeout =
            TimeSpan.FromMilliseconds(50d);
        private readonly Regex _pattern;

        private BlendShapeNamingRule(Regex pattern)
        {
            _pattern = pattern;
        }

        internal static BlendShapeNamingRule Create(string pattern)
        {
            if (string.IsNullOrWhiteSpace(pattern))
            {
                return null;
            }

            try
            {
                return new BlendShapeNamingRule(new Regex(
                    pattern,
                    RegexOptions.Compiled | RegexOptions.CultureInvariant,
                    MatchTimeout));
            }
            catch (ArgumentException)
            {
                return null;
            }
        }

        internal static SettingValidationResult Validate(string pattern)
        {
            if (string.IsNullOrWhiteSpace(pattern))
            {
                return SettingValidationResult.Success;
            }

            try
            {
                var regex = new Regex(
                    pattern,
                    RegexOptions.CultureInvariant,
                    MatchTimeout);
                return regex.GetGroupNames().Contains("role")
                    ? SettingValidationResult.Success
                    : SettingValidationResult.Error(
                        "The regular expression requires a named 'role' group.");
            }
            catch (ArgumentException exception)
            {
                return SettingValidationResult.Error(exception.Message);
            }
        }

        internal bool TryParse(string shapeName, out BlendShapeName name)
        {
            name = null;
            try
            {
                var match = _pattern.Match(shapeName ?? string.Empty);
                var role = Read(match, "role");
                if (!match.Success || string.IsNullOrWhiteSpace(role))
                {
                    return false;
                }

                name = new BlendShapeName(
                    Read(match, "group"),
                    role,
                    Read(match, "variation"),
                    Read(match, "side").ToUpperInvariant());
                return true;
            }
            catch (RegexMatchTimeoutException)
            {
                return false;
            }
        }

        private static string Read(Match match, string groupName)
        {
            var group = match.Groups[groupName];
            return group.Success
                ? string.Join(
                    " / ",
                    group.Captures
                        .Cast<Capture>()
                        .Select(capture => capture.Value))
                : string.Empty;
        }
    }
}
