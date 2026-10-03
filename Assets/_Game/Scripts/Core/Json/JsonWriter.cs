using System;
using System.Globalization;
using System.Text;

namespace Game.Core.Json
{
    /// <summary>Serializes <see cref="JsonNode"/> trees to text.</summary>
    public static class JsonWriter
    {
        private const string Indent = "  ";

        /// <summary>Writes <paramref name="node"/> as JSON. Pretty output uses two-space indentation.</summary>
        public static string Write(JsonNode node, bool pretty)
        {
            var sb = new StringBuilder();
            WriteNode(sb, node ?? JsonNull.Instance, pretty, 0);
            return sb.ToString();
        }

        private static void WriteNode(StringBuilder sb, JsonNode node, bool pretty, int depth)
        {
            switch (node)
            {
                case JsonNull _:
                    sb.Append("null");
                    break;
                case JsonBool b:
                    sb.Append(b.Value ? "true" : "false");
                    break;
                case JsonNumber n:
                    sb.Append(n.Raw);
                    break;
                case JsonString s:
                    WriteString(sb, s.Value);
                    break;
                case JsonArray a:
                    WriteArray(sb, a, pretty, depth);
                    break;
                case JsonObject o:
                    WriteObject(sb, o, pretty, depth);
                    break;
                default:
                    throw new InvalidOperationException($"Unknown JSON node type {node.GetType().Name}.");
            }
        }

        private static void WriteArray(StringBuilder sb, JsonArray array, bool pretty, int depth)
        {
            if (array.Count == 0)
            {
                sb.Append("[]");
                return;
            }
            sb.Append('[');
            for (var i = 0; i < array.Count; i++)
            {
                if (i > 0) sb.Append(',');
                NewLine(sb, pretty, depth + 1);
                WriteNode(sb, array[i], pretty, depth + 1);
            }
            NewLine(sb, pretty, depth);
            sb.Append(']');
        }

        private static void WriteObject(StringBuilder sb, JsonObject obj, bool pretty, int depth)
        {
            if (obj.Count == 0)
            {
                sb.Append("{}");
                return;
            }
            sb.Append('{');
            var first = true;
            foreach (var key in obj.Keys)
            {
                if (!first) sb.Append(',');
                first = false;
                NewLine(sb, pretty, depth + 1);
                WriteString(sb, key);
                sb.Append(pretty ? ": " : ":");
                WriteNode(sb, obj[key], pretty, depth + 1);
            }
            NewLine(sb, pretty, depth);
            sb.Append('}');
        }

        private static void NewLine(StringBuilder sb, bool pretty, int depth)
        {
            if (!pretty) return;
            sb.Append('\n');
            for (var i = 0; i < depth; i++) sb.Append(Indent);
        }

        private static void WriteString(StringBuilder sb, string value)
        {
            sb.Append('"');
            foreach (var c in value)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\b': sb.Append("\\b"); break;
                    case '\f': sb.Append("\\f"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < ' ')
                        {
                            sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        }
                        else
                        {
                            sb.Append(c);
                        }
                        break;
                }
            }
            sb.Append('"');
        }
    }
}
