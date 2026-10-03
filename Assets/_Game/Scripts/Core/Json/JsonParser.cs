using System;
using System.Globalization;
using System.Text;

namespace Game.Core.Json
{
    /// <summary>Strict recursive-descent JSON parser producing <see cref="JsonNode"/> trees.</summary>
    public static class JsonParser
    {
        private const int MaxDepth = 128;

        /// <summary>Parses <paramref name="text"/>. Throws <see cref="FormatException"/> with a position on invalid input.</summary>
        public static JsonNode Parse(string text)
        {
            if (text == null) throw new ArgumentNullException(nameof(text));
            var reader = new Reader(text);
            reader.SkipWhitespace();
            var node = reader.ReadValue(0);
            reader.SkipWhitespace();
            if (!reader.AtEnd) throw reader.Error("Unexpected text after JSON value");
            return node;
        }

        private sealed class Reader
        {
            private readonly string _text;
            private int _pos;

            public Reader(string text)
            {
                _text = text;
                // Tolerate a UTF-8 byte order mark written by some editors.
                if (_text.Length > 0 && _text[0] == '﻿') _pos = 1;
            }

            public bool AtEnd => _pos >= _text.Length;

            public FormatException Error(string message) =>
                new FormatException($"{message} at position {_pos}.");

            public void SkipWhitespace()
            {
                while (!AtEnd)
                {
                    var c = _text[_pos];
                    if (c != ' ' && c != '\t' && c != '\n' && c != '\r') break;
                    _pos++;
                }
            }

            public JsonNode ReadValue(int depth)
            {
                if (depth > MaxDepth) throw Error("JSON nested too deeply");
                if (AtEnd) throw Error("Unexpected end of JSON");

                var c = _text[_pos];
                switch (c)
                {
                    case '{': return ReadObject(depth);
                    case '[': return ReadArray(depth);
                    case '"': return new JsonString(ReadString());
                    case 't': ReadLiteral("true"); return new JsonBool(true);
                    case 'f': ReadLiteral("false"); return new JsonBool(false);
                    case 'n': ReadLiteral("null"); return JsonNull.Instance;
                    default:
                        if (c == '-' || (c >= '0' && c <= '9')) return ReadNumber();
                        throw Error($"Unexpected character '{c}'");
                }
            }

            private JsonObject ReadObject(int depth)
            {
                var obj = new JsonObject();
                _pos++; // {
                SkipWhitespace();
                if (TryConsume('}')) return obj;

                while (true)
                {
                    SkipWhitespace();
                    if (AtEnd || _text[_pos] != '"') throw Error("Expected property name");
                    var key = ReadString();
                    SkipWhitespace();
                    Expect(':');
                    SkipWhitespace();
                    obj.Set(key, ReadValue(depth + 1));
                    SkipWhitespace();
                    if (TryConsume(',')) continue;
                    Expect('}');
                    return obj;
                }
            }

            private JsonArray ReadArray(int depth)
            {
                var array = new JsonArray();
                _pos++; // [
                SkipWhitespace();
                if (TryConsume(']')) return array;

                while (true)
                {
                    SkipWhitespace();
                    array.Add(ReadValue(depth + 1));
                    SkipWhitespace();
                    if (TryConsume(',')) continue;
                    Expect(']');
                    return array;
                }
            }

            private string ReadString()
            {
                _pos++; // opening quote
                var sb = new StringBuilder();
                while (true)
                {
                    if (AtEnd) throw Error("Unterminated string");
                    var c = _text[_pos++];
                    if (c == '"') return sb.ToString();
                    if (c < ' ') throw Error("Control character in string");
                    if (c != '\\')
                    {
                        sb.Append(c);
                        continue;
                    }
                    if (AtEnd) throw Error("Unterminated escape");
                    var e = _text[_pos++];
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
                            if (_pos + 4 > _text.Length) throw Error("Bad unicode escape");
                            var hex = _text.Substring(_pos, 4);
                            if (!int.TryParse(hex, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var code))
                            {
                                throw Error("Bad unicode escape");
                            }
                            sb.Append((char)code);
                            _pos += 4;
                            break;
                        default:
                            throw Error($"Bad escape '\\{e}'");
                    }
                }
            }

            private JsonNumber ReadNumber()
            {
                var start = _pos;
                if (_text[_pos] == '-') _pos++;
                if (!ReadDigits()) throw Error("Expected digit");
                if (!AtEnd && _text[_pos] == '.')
                {
                    _pos++;
                    if (!ReadDigits()) throw Error("Expected digit after decimal point");
                }
                if (!AtEnd && (_text[_pos] == 'e' || _text[_pos] == 'E'))
                {
                    _pos++;
                    if (!AtEnd && (_text[_pos] == '+' || _text[_pos] == '-')) _pos++;
                    if (!ReadDigits()) throw Error("Expected exponent digits");
                }
                return new JsonNumber(_text.Substring(start, _pos - start));
            }

            private bool ReadDigits()
            {
                var start = _pos;
                while (!AtEnd && _text[_pos] >= '0' && _text[_pos] <= '9') _pos++;
                return _pos > start;
            }

            private void ReadLiteral(string literal)
            {
                if (string.CompareOrdinal(_text, _pos, literal, 0, literal.Length) != 0) throw Error($"Expected '{literal}'");
                _pos += literal.Length;
            }

            private bool TryConsume(char c)
            {
                if (AtEnd || _text[_pos] != c) return false;
                _pos++;
                return true;
            }

            private void Expect(char c)
            {
                if (!TryConsume(c)) throw Error($"Expected '{c}'");
            }
        }
    }
}
