using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace AdvancedNPCs.Core
{
    public class JsonException : Exception
    {
        public JsonException(string message) : base(message)
        {
        }
    }

    /// <summary>
    /// Minimal JSON reader with no Unity dependency. UnityEngine.JsonUtility cannot fill nested objects of
    /// classes from DFU's runtime-compiled mod assembly, so definitions are read with this instead.
    /// Objects become Dictionary&lt;string, object&gt; (a repeated key keeps the last value), arrays List&lt;object&gt;,
    /// numbers double, plus string, bool and null.
    /// </summary>
    public static class Json
    {
        [ThreadStatic] static List<string> duplicates;

        /// <summary>As Parse; every key repeated inside one object is added to found as "\"key\" (line N)".</summary>
        public static object Parse(string text, List<string> found)
        {
            duplicates = found;
            try
            {
                return Parse(text);
            }
            finally
            {
                duplicates = null;
            }
        }

        public static object Parse(string text)
        {
            int pos = 0;
            object value = ReadValue(text, ref pos);
            SkipWhitespace(text, ref pos);
            if (pos != text.Length)
                throw Error("unexpected text after the end", text, pos);
            return value;
        }

        static object ReadValue(string text, ref int pos)
        {
            SkipWhitespace(text, ref pos);
            if (pos >= text.Length)
                throw Error("unexpected end of file", text, pos);

            char c = text[pos];
            if (c == '{')
                return ReadObject(text, ref pos);
            if (c == '[')
                return ReadArray(text, ref pos);
            if (c == '"')
                return ReadString(text, ref pos);
            if (c == '-' || (c >= '0' && c <= '9'))
                return ReadNumber(text, ref pos);
            if (Matches(text, pos, "true"))
            {
                pos += 4;
                return true;
            }
            if (Matches(text, pos, "false"))
            {
                pos += 5;
                return false;
            }
            if (Matches(text, pos, "null"))
            {
                pos += 4;
                return null;
            }
            throw Error("unexpected character '" + c + "'", text, pos);
        }

        static Dictionary<string, object> ReadObject(string text, ref int pos)
        {
            Dictionary<string, object> result = new Dictionary<string, object>(StringComparer.Ordinal);
            pos++; // {
            SkipWhitespace(text, ref pos);
            if (pos < text.Length && text[pos] == '}')
            {
                pos++;
                return result;
            }
            while (true)
            {
                SkipWhitespace(text, ref pos);
                if (pos >= text.Length || text[pos] != '"')
                    throw Error("expected a quoted key", text, pos);
                int keyAt = pos;
                string key = ReadString(text, ref pos);
                SkipWhitespace(text, ref pos);
                if (pos >= text.Length || text[pos] != ':')
                    throw Error("expected ':' after \"" + key + "\"", text, pos);
                pos++;
                if (duplicates != null && result.ContainsKey(key))
                    duplicates.Add("\"" + key + "\" (line " + LineOf(text, keyAt) + ")");
                result[key] = ReadValue(text, ref pos);
                SkipWhitespace(text, ref pos);
                if (pos < text.Length && text[pos] == ',')
                {
                    pos++;
                    continue;
                }
                if (pos < text.Length && text[pos] == '}')
                {
                    pos++;
                    return result;
                }
                throw Error("expected ',' or '}'", text, pos);
            }
        }

        static List<object> ReadArray(string text, ref int pos)
        {
            List<object> result = new List<object>();
            pos++; // [
            SkipWhitespace(text, ref pos);
            if (pos < text.Length && text[pos] == ']')
            {
                pos++;
                return result;
            }
            while (true)
            {
                result.Add(ReadValue(text, ref pos));
                SkipWhitespace(text, ref pos);
                if (pos < text.Length && text[pos] == ',')
                {
                    pos++;
                    continue;
                }
                if (pos < text.Length && text[pos] == ']')
                {
                    pos++;
                    return result;
                }
                throw Error("expected ',' or ']'", text, pos);
            }
        }

        static string ReadString(string text, ref int pos)
        {
            StringBuilder sb = new StringBuilder();
            pos++; // opening quote
            while (true)
            {
                if (pos >= text.Length)
                    throw Error("unterminated string", text, pos);
                char c = text[pos++];
                if (c == '"')
                    return sb.ToString();
                if (c < ' ')
                    throw Error("line break or control character inside a string", text, pos - 1);
                if (c != '\\')
                {
                    sb.Append(c);
                    continue;
                }
                if (pos >= text.Length)
                    throw Error("unterminated string", text, pos);
                char e = text[pos++];
                switch (e)
                {
                    case '"': sb.Append('"'); break;
                    case '\\': sb.Append('\\'); break;
                    case '/': sb.Append('/'); break;
                    case 'b': sb.Append('\b'); break;
                    case 'f': sb.Append('\f'); break;
                    case 'n': sb.Append('\n'); break;
                    case 'r': sb.Append('\r'); break;
                    case 't': sb.Append('\t'); break;
                    case 'u':
                        int code;
                        if (pos + 4 > text.Length ||
                            !int.TryParse(text.Substring(pos, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out code))
                            throw Error("bad \\u escape", text, pos);
                        sb.Append((char)code);
                        pos += 4;
                        break;
                    default:
                        throw Error("bad escape '\\" + e + "'", text, pos - 1);
                }
            }
        }

        static double ReadNumber(string text, ref int pos)
        {
            int start = pos;
            while (pos < text.Length && "+-0123456789.eE".IndexOf(text[pos]) >= 0)
                pos++;
            double value;
            if (!double.TryParse(text.Substring(start, pos - start), NumberStyles.Float, CultureInfo.InvariantCulture, out value))
                throw Error("bad number", text, start);
            return value;
        }

        static bool Matches(string text, int pos, string word)
        {
            return string.CompareOrdinal(text, pos, word, 0, word.Length) == 0;
        }

        static int LineOf(string text, int pos)
        {
            int line = 1;
            for (int i = 0; i < pos && i < text.Length; i++)
            {
                if (text[i] == '\n')
                    line++;
            }
            return line;
        }

        static void SkipWhitespace(string text, ref int pos)
        {
            while (pos < text.Length && char.IsWhiteSpace(text[pos]))
                pos++;
        }

        /// <summary>Position as line and column (1-based), which editors show, plus the character offset.</summary>
        static JsonException Error(string message, string text, int pos)
        {
            int line = 1;
            int column = 1;
            for (int i = 0; i < pos && i < text.Length; i++)
            {
                if (text[i] == '\n')
                {
                    line++;
                    column = 1;
                }
                else
                {
                    column++;
                }
            }
            return new JsonException(message + " at line " + line + ", column " + column + " (character " + (pos + 1) + ")");
        }
    }
}
