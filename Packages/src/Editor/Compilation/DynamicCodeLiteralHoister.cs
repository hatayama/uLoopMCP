using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace io.github.hatayama.uLoopMCP
{
    internal static class DynamicCodeLiteralHoister
    {
        private const string LiteralParameterPrefix = "__uloop_literal_";

        public static HoistedLiteralRewriteResult Rewrite(string source)
        {
            StringBuilder rewrittenSource = new StringBuilder(source.Length);
            List<HoistedLiteralBinding> bindings = new List<HoistedLiteralBinding>();
            int index = 0;

            while (index < source.Length)
            {
                if (TryCopyVerbatimStringLiteral(source, rewrittenSource, ref index))
                {
                    continue;
                }

                if (TryCopyCharLiteral(source, rewrittenSource, ref index))
                {
                    continue;
                }

                if (TryCopyLineComment(source, rewrittenSource, ref index))
                {
                    continue;
                }

                if (TryCopyBlockComment(source, rewrittenSource, ref index))
                {
                    continue;
                }

                if (TryHoistRegularStringLiteral(source, rewrittenSource, bindings, ref index))
                {
                    continue;
                }

                if (TryHoistIntegerLiteral(source, rewrittenSource, bindings, ref index))
                {
                    continue;
                }

                rewrittenSource.Append(source[index]);
                index++;
            }

            List<string> declarationLines = new List<string>();
            foreach (HoistedLiteralBinding binding in bindings)
            {
                declarationLines.Add(
                    $"{binding.TypeName} {binding.ParameterName} = ({binding.TypeName})parameters[\"{binding.ParameterName}\"];");
            }

            return new HoistedLiteralRewriteResult(
                rewrittenSource.ToString(),
                bindings,
                declarationLines);
        }

        private static bool TryCopyLineComment(
            string source,
            StringBuilder rewrittenSource,
            ref int index)
        {
            if (index + 1 >= source.Length || source[index] != '/' || source[index + 1] != '/')
            {
                return false;
            }

            while (index < source.Length)
            {
                char current = source[index];
                rewrittenSource.Append(current);
                index++;
                if (current == '\n')
                {
                    break;
                }
            }

            return true;
        }

        private static bool TryCopyBlockComment(
            string source,
            StringBuilder rewrittenSource,
            ref int index)
        {
            if (index + 1 >= source.Length || source[index] != '/' || source[index + 1] != '*')
            {
                return false;
            }

            rewrittenSource.Append(source[index]);
            rewrittenSource.Append(source[index + 1]);
            index += 2;

            while (index < source.Length)
            {
                char current = source[index];
                rewrittenSource.Append(current);
                index++;
                if (current == '*' && index < source.Length && source[index] == '/')
                {
                    rewrittenSource.Append('/');
                    index++;
                    break;
                }
            }

            return true;
        }

        private static bool TryCopyVerbatimStringLiteral(
            string source,
            StringBuilder rewrittenSource,
            ref int index)
        {
            if (source[index] != '@'
                || index + 1 >= source.Length
                || source[index + 1] != '"')
            {
                return false;
            }

            int start = index;
            index += 2;

            while (index < source.Length)
            {
                if (source[index] != '"')
                {
                    index++;
                    continue;
                }

                if (index + 1 < source.Length && source[index + 1] == '"')
                {
                    index += 2;
                    continue;
                }

                index++;
                rewrittenSource.Append(source, start, index - start);
                return true;
            }

            index = start;
            return false;
        }

        private static bool TryCopyCharLiteral(
            string source,
            StringBuilder rewrittenSource,
            ref int index)
        {
            if (source[index] != '\'')
            {
                return false;
            }

            int start = index;
            index++;

            while (index < source.Length)
            {
                char current = source[index];
                if (current == '\\')
                {
                    index += Math.Min(2, source.Length - index);
                    continue;
                }

                if (current == '\'')
                {
                    index++;
                    rewrittenSource.Append(source, start, index - start);
                    return true;
                }

                index++;
            }

            index = start;
            return false;
        }

        private static bool TryHoistRegularStringLiteral(
            string source,
            StringBuilder rewrittenSource,
            List<HoistedLiteralBinding> bindings,
            ref int index)
        {
            if (source[index] != '"')
            {
                return false;
            }

            if (index > 0 && (source[index - 1] == '@' || source[index - 1] == '$'))
            {
                return false;
            }

            int start = index;
            index++;

            while (index < source.Length)
            {
                char current = source[index];
                if (current == '\\')
                {
                    index += Math.Min(2, source.Length - index);
                    continue;
                }

                if (current == '"')
                {
                    index++;
                    string literalToken = source.Substring(start, index - start);
                    string parameterName = CreateParameterName(bindings.Count);
                    string value = UnescapeRegularStringLiteral(literalToken);
                    bindings.Add(new HoistedLiteralBinding(parameterName, "string", value));
                    rewrittenSource.Append(parameterName);
                    return true;
                }

                index++;
            }

            index = start;
            return false;
        }

        private static bool TryHoistIntegerLiteral(
            string source,
            StringBuilder rewrittenSource,
            List<HoistedLiteralBinding> bindings,
            ref int index)
        {
            char current = source[index];
            if (!char.IsDigit(current))
            {
                return false;
            }

            if (index > 0)
            {
                char previous = source[index - 1];
                if (char.IsLetterOrDigit(previous) || previous == '_' || previous == '.')
                {
                    return false;
                }
            }

            int start = index;
            index++;
            while (index < source.Length && char.IsDigit(source[index]))
            {
                index++;
            }

            if (index < source.Length && (source[index] == 'L' || source[index] == 'l'))
            {
                int suffixIndex = index;
                index++;
                if (!HasIntegerLiteralBoundary(source, index))
                {
                    index = start;
                    return false;
                }

                string longToken = source.Substring(start, suffixIndex - start);
                long longValue = long.Parse(longToken, CultureInfo.InvariantCulture);
                string longParameterName = CreateParameterName(bindings.Count);
                bindings.Add(new HoistedLiteralBinding(longParameterName, "long", longValue));
                rewrittenSource.Append(longParameterName);
                return true;
            }

            if (!HasIntegerLiteralBoundary(source, index))
            {
                index = start;
                return false;
            }

            string token = source.Substring(start, index - start);
            int intValue = int.Parse(token, CultureInfo.InvariantCulture);
            string parameterName = CreateParameterName(bindings.Count);
            bindings.Add(new HoistedLiteralBinding(parameterName, "int", intValue));
            rewrittenSource.Append(parameterName);
            return true;
        }

        private static bool HasIntegerLiteralBoundary(string source, int index)
        {
            if (index >= source.Length)
            {
                return true;
            }

            char next = source[index];
            return !char.IsLetter(next) && !char.IsDigit(next) && next != '_' && next != '.';
        }

        private static string CreateParameterName(int index)
        {
            return $"{LiteralParameterPrefix}{index}";
        }

        private static string UnescapeRegularStringLiteral(string token)
        {
            string inner = token.Substring(1, token.Length - 2);
            StringBuilder unescaped = new StringBuilder(inner.Length);

            for (int index = 0; index < inner.Length; index++)
            {
                char current = inner[index];
                if (current != '\\')
                {
                    unescaped.Append(current);
                    continue;
                }

                if (index + 1 >= inner.Length)
                {
                    unescaped.Append('\\');
                    break;
                }

                index++;
                char escape = inner[index];
                switch (escape)
                {
                    case '\'':
                        unescaped.Append('\'');
                        break;
                    case '"':
                        unescaped.Append('"');
                        break;
                    case '\\':
                        unescaped.Append('\\');
                        break;
                    case '0':
                        unescaped.Append('\0');
                        break;
                    case 'a':
                        unescaped.Append('\a');
                        break;
                    case 'b':
                        unescaped.Append('\b');
                        break;
                    case 'f':
                        unescaped.Append('\f');
                        break;
                    case 'n':
                        unescaped.Append('\n');
                        break;
                    case 'r':
                        unescaped.Append('\r');
                        break;
                    case 't':
                        unescaped.Append('\t');
                        break;
                    case 'v':
                        unescaped.Append('\v');
                        break;
                    case 'u':
                        unescaped.Append((char)ParseHexDigits(inner, ref index, 4));
                        break;
                    case 'U':
                        unescaped.Append(char.ConvertFromUtf32(ParseHexDigits(inner, ref index, 8)));
                        break;
                    case 'x':
                        unescaped.Append((char)ParseVariableLengthHexDigits(inner, ref index));
                        break;
                    default:
                        unescaped.Append(escape);
                        break;
                }
            }

            return unescaped.ToString();
        }

        private static int ParseHexDigits(string value, ref int index, int digitCount)
        {
            int start = index + 1;
            string hex = value.Substring(start, digitCount);
            index += digitCount;
            return int.Parse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        }

        private static int ParseVariableLengthHexDigits(string value, ref int index)
        {
            int start = index + 1;
            int maxExclusive = Math.Min(value.Length, start + 4);
            int end = start;

            while (end < maxExclusive && IsHexDigit(value[end]))
            {
                end++;
            }

            string hex = value.Substring(start, end - start);
            index = end - 1;
            return int.Parse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        }

        private static bool IsHexDigit(char value)
        {
            return (value >= '0' && value <= '9')
                || (value >= 'a' && value <= 'f')
                || (value >= 'A' && value <= 'F');
        }
    }

    internal sealed class HoistedLiteralRewriteResult
    {
        public string RewrittenSource { get; }
        public List<HoistedLiteralBinding> Bindings { get; }
        public List<string> DeclarationLines { get; }

        public HoistedLiteralRewriteResult(
            string rewrittenSource,
            List<HoistedLiteralBinding> bindings,
            List<string> declarationLines)
        {
            RewrittenSource = rewrittenSource;
            Bindings = bindings;
            DeclarationLines = declarationLines;
        }
    }
}
