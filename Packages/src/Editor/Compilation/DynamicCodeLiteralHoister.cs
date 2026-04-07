using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace io.github.hatayama.uLoopMCP
{
    internal static class DynamicCodeLiteralHoister
    {
        private const string LiteralParameterPrefix = "__uloop_literal_";
        private static readonly Regex UnicodeEscapePattern = new Regex(@"\\u([0-9A-Fa-f]{4})", RegexOptions.Compiled);

        public static HoistedLiteralRewriteResult Rewrite(string source)
        {
            StringBuilder rewrittenSource = new StringBuilder(source.Length);
            List<HoistedLiteralBinding> bindings = new List<HoistedLiteralBinding>();
            int index = 0;

            while (index < source.Length)
            {
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
                index++;
                string longToken = source.Substring(start, index - start - 1);
                long longValue = long.Parse(longToken, CultureInfo.InvariantCulture);
                string longParameterName = CreateParameterName(bindings.Count);
                bindings.Add(new HoistedLiteralBinding(longParameterName, "long", longValue));
                rewrittenSource.Append(longParameterName);
                return true;
            }

            if (index < source.Length)
            {
                char next = source[index];
                if (char.IsLetter(next) || next == '_')
                {
                    index = start;
                    return false;
                }
            }

            string token = source.Substring(start, index - start);
            int intValue = int.Parse(token, CultureInfo.InvariantCulture);
            string parameterName = CreateParameterName(bindings.Count);
            bindings.Add(new HoistedLiteralBinding(parameterName, "int", intValue));
            rewrittenSource.Append(parameterName);
            return true;
        }

        private static string CreateParameterName(int index)
        {
            return $"{LiteralParameterPrefix}{index}";
        }

        private static string UnescapeRegularStringLiteral(string token)
        {
            string inner = token.Substring(1, token.Length - 2);
            inner = inner.Replace("\\\\", "\\u005C");
            inner = inner.Replace("\\\"", "\"");
            inner = inner.Replace("\\n", "\n");
            inner = inner.Replace("\\r", "\r");
            inner = inner.Replace("\\t", "\t");
            inner = inner.Replace("\\0", "\0");
            inner = UnicodeEscapePattern.Replace(
                inner,
                match => ((char)int.Parse(match.Groups[1].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture)).ToString());
            inner = inner.Replace("\\u005C", "\\");
            return inner;
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
