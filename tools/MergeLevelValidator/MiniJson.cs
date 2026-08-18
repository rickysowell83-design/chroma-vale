// MiniJson.cs — minimal JSON parser for the Chroma Merge level schema.
// Zero dependencies: only supports the value types the level format needs
// (objects, arrays, strings, numbers, booleans, null).
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace ChromaMerge.Validator
{
    public sealed class JsonValue
    {
        public JsonKind Kind;
        public string Str;
        public double Num;
        public bool Bool;
        public List<JsonValue> Array;
        public Dictionary<string, JsonValue> Object;

        public static JsonValue Null() => new JsonValue { Kind = JsonKind.Null };
        public static JsonValue OfString(string s) => new JsonValue { Kind = JsonKind.String, Str = s };
        public static JsonValue OfNumber(double d) => new JsonValue { Kind = JsonKind.Number, Num = d };
        public static JsonValue OfBool(bool b) => new JsonValue { Kind = JsonKind.Bool, Bool = b };
        public static JsonValue OfArray() => new JsonValue { Kind = JsonKind.Array, Array = new List<JsonValue>() };
        public static JsonValue OfObject() => new JsonValue { Kind = JsonKind.Object, Object = new Dictionary<string, JsonValue>() };

        public JsonValue this[string key] => Object.TryGetValue(key, out var v) ? v : Null();

        public string AsString => Str ?? "";
        public int AsInt => (int)Math.Round(Num);
        public bool HasKey(string key) => Object.ContainsKey(key);
    }

    public enum JsonKind { Null, String, Number, Bool, Array, Object }

    public static class MiniJson
    {
        public static JsonValue Parse(string text)
        {
            int pos = 0;
            var v = ParseValue(text, ref pos);
            SkipWs(text, ref pos);
            return v;
        }

        private static JsonValue ParseValue(string s, ref int pos)
        {
            SkipWs(s, ref pos);
            char c = s[pos];
            switch (c)
            {
                case '{': return ParseObject(s, ref pos);
                case '[': return ParseArray(s, ref pos);
                case '"': return JsonValue.OfString(ParseString(s, ref pos));
                case 't': Expect(s, ref pos, "true"); return JsonValue.OfBool(true);
                case 'f': Expect(s, ref pos, "false"); return JsonValue.OfBool(false);
                case 'n': Expect(s, ref pos, "null"); return JsonValue.Null();
                default: return JsonValue.OfNumber(ParseNumber(s, ref pos));
            }
        }

        private static JsonValue ParseObject(string s, ref int pos)
        {
            pos++; // {
            var obj = JsonValue.OfObject();
            SkipWs(s, ref pos);
            if (s[pos] == '}') { pos++; return obj; }
            while (true)
            {
                SkipWs(s, ref pos);
                string key = ParseString(s, ref pos);
                SkipWs(s, ref pos);
                if (s[pos] != ':') throw new FormatException("Expected ':' at " + pos);
                pos++;
                obj.Object[key] = ParseValue(s, ref pos);
                SkipWs(s, ref pos);
                if (s[pos] == ',') { pos++; continue; }
                if (s[pos] == '}') { pos++; return obj; }
                throw new FormatException("Expected ',' or '}' at " + pos);
            }
        }

        private static JsonValue ParseArray(string s, ref int pos)
        {
            pos++; // [
            var arr = JsonValue.OfArray();
            SkipWs(s, ref pos);
            if (s[pos] == ']') { pos++; return arr; }
            while (true)
            {
                arr.Array.Add(ParseValue(s, ref pos));
                SkipWs(s, ref pos);
                if (s[pos] == ',') { pos++; continue; }
                if (s[pos] == ']') { pos++; return arr; }
                throw new FormatException("Expected ',' or ']' at " + pos);
            }
        }

        private static string ParseString(string s, ref int pos)
        {
            pos++; // opening quote
            var sb = new StringBuilder();
            while (true)
            {
                char c = s[pos++];
                if (c == '"') return sb.ToString();
                if (c == '\\')
                {
                    char e = s[pos++];
                    switch (e)
                    {
                        case '"': sb.Append('"'); break;
                        case '\\': sb.Append('\\'); break;
                        case '/': sb.Append('/'); break;
                        case 'n': sb.Append('\n'); break;
                        case 't': sb.Append('\t'); break;
                        case 'r': sb.Append('\r'); break;
                        case 'u':
                            sb.Append((char)int.Parse(s.Substring(pos, 4), NumberStyles.HexNumber));
                            pos += 4;
                            break;
                        default: throw new FormatException("Bad escape \\" + e);
                    }
                }
                else sb.Append(c);
            }
        }

        private static double ParseNumber(string s, ref int pos)
        {
            int start = pos;
            if (s[pos] == '-') pos++;
            while (pos < s.Length && (char.IsDigit(s[pos]) || s[pos] == '.' || s[pos] == 'e' || s[pos] == 'E' || s[pos] == '+' || s[pos] == '-')) pos++;
            return double.Parse(s.Substring(start, pos - start), CultureInfo.InvariantCulture);
        }

        private static void Expect(string s, ref int pos, string word)
        {
            if (pos + word.Length > s.Length || s.Substring(pos, word.Length) != word)
                throw new FormatException("Expected " + word + " at " + pos);
            pos += word.Length;
        }

        private static void SkipWs(string s, ref int pos)
        {
            while (pos < s.Length && char.IsWhiteSpace(s[pos])) pos++;
        }
    }
}
