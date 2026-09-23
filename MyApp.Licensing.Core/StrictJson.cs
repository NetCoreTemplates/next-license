using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace MyApp.Licensing.Core;

// Deliberately small, bounded JSON reader for the signed wire schema. No runtime JSON dependencies.
internal sealed class StrictJson
{
    private readonly string text;
    private int pos;
    private StrictJson(string text) { this.text = text; }
    public static Dictionary<string, object?> Object(string text)
    {
        if (text.Length > 32768) throw new FormatException("Payload too large.");
        var parser = new StrictJson(text);
        var result = parser.Value(0) as Dictionary<string, object?> ?? throw new FormatException("Object required.");
        parser.Space();
        if (parser.pos != text.Length) throw new FormatException("Trailing JSON.");
        return result;
    }
    private object? Value(int depth)
    {
        if (depth > 8) throw new FormatException("JSON nesting too deep.");
        Space();
        if (pos >= text.Length) throw new FormatException("Incomplete JSON.");
        if (text[pos] == '"') return String();
        if (text[pos] == '{')
        {
            pos++;
            var result = new Dictionary<string, object?>(StringComparer.Ordinal);
            if (Take('}')) return result;
            do
            {
                Space();
                var key = String();
                if (!Take(':') || result.ContainsKey(key)) throw new FormatException("Duplicate field or invalid object.");
                result.Add(key, Value(depth + 1));
                if (result.Count > 32) throw new FormatException("Too many fields.");
                if (Take('}')) return result;
            } while (Take(','));
            throw new FormatException("Invalid object.");
        }
        if (text.Substring(pos).StartsWith("null", StringComparison.Ordinal)) { pos += 4; return null; }
        int start = pos;
        if (text[pos] == '-') pos++;
        while (pos < text.Length && text[pos] >= '0' && text[pos] <= '9') pos++;
        var number = text.Substring(start, pos - start);
        var digits = number.StartsWith("-", StringComparison.Ordinal) ? number.Substring(1) : number;
        if (digits.Length == 0 || (digits.Length > 1 && digits[0] == '0')
            || !long.TryParse(number, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var value))
            throw new FormatException("Integer required.");
        return value;
    }
    private string String()
    {
        if (pos >= text.Length || text[pos++] != '"') throw new FormatException("String required.");
        var value = new StringBuilder();
        while (pos < text.Length)
        {
            char c = text[pos++];
            if (c == '"')
            {
                var result = value.ToString();
                // Reject unpaired surrogates, including escaped ones.
                new UTF8Encoding(false, true).GetBytes(result);
                return result;
            }
            if (c < 32) throw new FormatException("Invalid string.");
            if (c == '\\')
            {
                if (pos == text.Length) throw new FormatException("Incomplete escape.");
                c = text[pos++];
                switch (c)
                {
                    case '"': case '\\': case '/': break;
                    case 'b': c = '\b'; break;
                    case 'f': c = '\f'; break;
                    case 'n': c = '\n'; break;
                    case 'r': c = '\r'; break;
                    case 't': c = '\t'; break;
                    case 'u':
                        if (pos + 4 > text.Length || !ushort.TryParse(text.Substring(pos, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var code))
                            throw new FormatException("Invalid unicode escape.");
                        pos += 4;
                        c = (char)code;
                        break;
                    default: throw new FormatException("Invalid escape.");
                }
            }
            value.Append(c);
            if (value.Length > 8192) throw new FormatException("String too large.");
        }
        throw new FormatException("Unterminated string.");
    }
    private bool Take(char c) { Space(); if (pos < text.Length && text[pos] == c) { pos++; return true; } return false; }
    private void Space() { while (pos < text.Length && " \t\r\n".IndexOf(text[pos]) >= 0) pos++; }
    public static string Text(Dictionary<string, object?> obj, string key, int max = 512) =>
        obj.TryGetValue(key, out var value) && value is string s && s.Length > 0 && s.Length <= max ? s : throw new FormatException("Invalid " + key);
    public static long Number(Dictionary<string, object?> obj, string key) =>
        obj.TryGetValue(key, out var value) && value is long n ? n : throw new FormatException("Invalid " + key);
    public static DateTime Date(Dictionary<string, object?> obj, string key)
    {
        var s = Text(obj, key, 40);
        if (!s.EndsWith("Z", StringComparison.Ordinal) || !DateTime.TryParseExact(s,
            new[] { "yyyy-MM-dd'T'HH:mm:ss'Z'", "yyyy-MM-dd'T'HH:mm:ss.FFFFFFF'Z'" }, CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var date)) throw new FormatException("Invalid UTC date.");
        return date;
    }
    public static void Fields(Dictionary<string, object?> obj, params string[] allowed)
    {
        foreach (var key in obj.Keys) if (Array.IndexOf(allowed, key) < 0) throw new FormatException("Unknown field: " + key);
    }
}
