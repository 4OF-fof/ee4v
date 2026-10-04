using System.Globalization;
using System.Text;

namespace Ee4v.AssetManager.Application
{
    internal static class AssetSourceText
    {
        private static readonly int[] UppercaseStarts =
        {
            0x1D400,
            0x1D434,
            0x1D468,
            0x1D5A0,
            0x1D5D4,
            0x1D608,
            0x1D670
        };

        private static readonly int[] LowercaseStarts =
        {
            0x1D41A,
            0x1D44E,
            0x1D482,
            0x1D5BA,
            0x1D5EE,
            0x1D622,
            0x1D68A
        };

        private static readonly int[] DigitStarts =
        {
            0x1D7CE,
            0x1D7D8,
            0x1D7E2,
            0x1D7EC,
            0x1D7F6
        };

        internal static string Normalize(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            var normalized = value.Normalize(NormalizationForm.FormKC);
            var builder = new StringBuilder(normalized.Length);
            for (var i = 0; i < normalized.Length; i++)
            {
                var current = normalized[i];
                if (char.IsHighSurrogate(current) &&
                    i + 1 < normalized.Length &&
                    char.IsLowSurrogate(normalized[i + 1]))
                {
                    var mapped = TryMapMathematicalSymbol(
                        char.ConvertToUtf32(
                            current,
                            normalized[i + 1]));
                    if (mapped.HasValue)
                    {
                        builder.Append(mapped.Value);
                    }

                    i++;
                    continue;
                }

                if (char.IsSurrogate(current) ||
                    current == '\uFE0E' ||
                    current == '\uFE0F' ||
                    current == '\u200D')
                {
                    continue;
                }

                if (char.IsControl(current) &&
                    current != '\r' &&
                    current != '\n' &&
                    current != '\t')
                {
                    continue;
                }

                if (char.GetUnicodeCategory(current) ==
                    UnicodeCategory.OtherSymbol)
                {
                    var mapped = TryMapOtherSymbol(current);
                    if (mapped != null)
                    {
                        builder.Append(mapped);
                    }

                    continue;
                }

                builder.Append(current);
            }

            return builder.ToString();
        }

        private static char? TryMapMathematicalSymbol(int codePoint)
        {
            var mapped = TryMapRange(
                codePoint,
                UppercaseStarts,
                26,
                'A');
            if (mapped.HasValue)
            {
                return mapped;
            }

            mapped = TryMapRange(
                codePoint,
                LowercaseStarts,
                26,
                'a');
            return mapped ?? TryMapRange(
                codePoint,
                DigitStarts,
                10,
                '0');
        }

        private static char? TryMapRange(
            int codePoint,
            int[] starts,
            int length,
            char asciiStart)
        {
            for (var i = 0; i < starts.Length; i++)
            {
                if (codePoint >= starts[i] &&
                    codePoint < starts[i] + length)
                {
                    return (char)(asciiStart + codePoint - starts[i]);
                }
            }

            return null;
        }

        private static string TryMapOtherSymbol(char value)
        {
            if (value == '\u00A9')
            {
                return "(c)";
            }

            if (value == '\u00AE')
            {
                return "(r)";
            }

            if (value == '\u2122')
            {
                return "TM";
            }

            if (value == '\u2605' ||
                value == '\u2606' ||
                value == '\u2726' ||
                value == '\u2727' ||
                (value >= '\u2729' && value <= '\u2734') ||
                (value >= '\u2B50' && value <= '\u2B52'))
            {
                return "*";
            }

            return null;
        }
    }
}
