using System.Text;

namespace Ee4v.Core.I18n
{
    internal static class JsoncUtility
    {
        public static string Normalize(string source)
        {
            return RemoveTrailingCommas(RemoveComments(source));
        }

        private static string RemoveComments(string source)
        {
            var builder = new StringBuilder(source.Length);
            var inString = false;
            var isEscaped = false;
            var inLineComment = false;
            var inBlockComment = false;

            for (var i = 0; i < source.Length; i++)
            {
                var current = source[i];
                var next = i + 1 < source.Length ? source[i + 1] : '\0';

                if (inLineComment)
                {
                    if (current == '\r' || current == '\n')
                    {
                        inLineComment = false;
                        builder.Append(current);
                    }

                    continue;
                }

                if (inBlockComment)
                {
                    if (current == '*' && next == '/')
                    {
                        inBlockComment = false;
                        i++;
                    }

                    continue;
                }

                if (inString)
                {
                    builder.Append(current);

                    if (isEscaped)
                    {
                        isEscaped = false;
                    }
                    else if (current == '\\')
                    {
                        isEscaped = true;
                    }
                    else if (current == '"')
                    {
                        inString = false;
                    }

                    continue;
                }

                if (current == '/' && next == '/')
                {
                    inLineComment = true;
                    i++;
                    continue;
                }

                if (current == '/' && next == '*')
                {
                    inBlockComment = true;
                    i++;
                    continue;
                }

                if (current == '"')
                {
                    inString = true;
                }

                builder.Append(current);
            }

            return builder.ToString();
        }

        private static string RemoveTrailingCommas(string source)
        {
            var builder = new StringBuilder(source.Length);
            var inString = false;
            var isEscaped = false;

            for (var i = 0; i < source.Length; i++)
            {
                var current = source[i];

                if (inString)
                {
                    builder.Append(current);

                    if (isEscaped)
                    {
                        isEscaped = false;
                    }
                    else if (current == '\\')
                    {
                        isEscaped = true;
                    }
                    else if (current == '"')
                    {
                        inString = false;
                    }

                    continue;
                }

                if (current == '"')
                {
                    inString = true;
                    builder.Append(current);
                    continue;
                }

                if (current == ',')
                {
                    var nextIndex = i + 1;
                    while (nextIndex < source.Length && char.IsWhiteSpace(source[nextIndex]))
                    {
                        nextIndex++;
                    }

                    if (nextIndex < source.Length && (source[nextIndex] == '}' || source[nextIndex] == ']'))
                    {
                        continue;
                    }
                }

                builder.Append(current);
            }

            return builder.ToString();
        }
    }
}
