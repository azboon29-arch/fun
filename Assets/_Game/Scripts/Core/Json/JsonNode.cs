using System;
using System.Collections.Generic;
using System.Globalization;

namespace Game.Core.Json
{
    /// <summary>Kinds of JSON value.</summary>
    public enum JsonKind
    {
        /// <summary>null</summary>
        Null,

        /// <summary>true or false</summary>
        Bool,

        /// <summary>A number</summary>
        Number,

        /// <summary>A string</summary>
        String,

        /// <summary>An ordered list</summary>
        Array,

        /// <summary>Key/value pairs</summary>
        Object,
    }

    /// <summary>
    /// A minimal JSON document model. Used by the save system so that migrations can reshape raw data
    /// before it is mapped to typed classes, without depending on UnityEngine or third-party packages.
    /// </summary>
    public abstract class JsonNode
    {
        /// <summary>The kind of this value.</summary>
        public abstract JsonKind Kind { get; }

        /// <summary>Parses JSON text. Throws <see cref="FormatException"/> on invalid input.</summary>
        public static JsonNode Parse(string text) => JsonParser.Parse(text);

        /// <summary>Serializes this value to JSON text.</summary>
        public string ToJson(bool pretty = false) => JsonWriter.Write(this, pretty);

        /// <inheritdoc />
        public override string ToString() => ToJson();

        /// <summary>Creates a string node.</summary>
        public static implicit operator JsonNode(string value) => value == null ? (JsonNode)JsonNull.Instance : new JsonString(value);

        /// <summary>Creates a number node.</summary>
        public static implicit operator JsonNode(long value) => new JsonNumber(value);

        /// <summary>Creates a number node.</summary>
        public static implicit operator JsonNode(int value) => new JsonNumber(value);

        /// <summary>Creates a number node.</summary>
        public static implicit operator JsonNode(double value) => new JsonNumber(value);

        /// <summary>Creates a bool node.</summary>
        public static implicit operator JsonNode(bool value) => new JsonBool(value);
    }

    /// <summary>JSON null.</summary>
    public sealed class JsonNull : JsonNode
    {
        /// <summary>Shared instance.</summary>
        public static readonly JsonNull Instance = new JsonNull();

        private JsonNull() { }

        /// <inheritdoc />
        public override JsonKind Kind => JsonKind.Null;
    }

    /// <summary>JSON true/false.</summary>
    public sealed class JsonBool : JsonNode
    {
        /// <summary>Creates the node.</summary>
        public JsonBool(bool value) { Value = value; }

        /// <summary>The value.</summary>
        public bool Value { get; }

        /// <inheritdoc />
        public override JsonKind Kind => JsonKind.Bool;
    }

    /// <summary>JSON string.</summary>
    public sealed class JsonString : JsonNode
    {
        /// <summary>Creates the node.</summary>
        public JsonString(string value) { Value = value ?? throw new ArgumentNullException(nameof(value)); }

        /// <summary>The value.</summary>
        public string Value { get; }

        /// <inheritdoc />
        public override JsonKind Kind => JsonKind.String;
    }

    /// <summary>
    /// JSON number. Keeps the original text so large integers (e.g. gold totals) survive a round trip exactly.
    /// </summary>
    public sealed class JsonNumber : JsonNode
    {
        /// <summary>Creates a number from its JSON text (invariant culture).</summary>
        public JsonNumber(string raw) { Raw = raw ?? throw new ArgumentNullException(nameof(raw)); }

        /// <summary>Creates an integer number.</summary>
        public JsonNumber(long value) : this(value.ToString(CultureInfo.InvariantCulture)) { }

        /// <summary>Creates a floating point number. NaN and infinity are not valid JSON and throw.</summary>
        public JsonNumber(double value) : this(FormatDouble(value)) { }

        /// <summary>The number exactly as written in JSON.</summary>
        public string Raw { get; }

        /// <inheritdoc />
        public override JsonKind Kind => JsonKind.Number;

        /// <summary>The value as a 64-bit integer. Throws if it is not an integer in range.</summary>
        public long AsLong()
        {
            if (long.TryParse(Raw, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var l)) return l;
            var d = AsDouble();
            if (d >= long.MinValue && d <= long.MaxValue && Math.Floor(d) == d) return (long)d;
            throw new FormatException($"JSON number {Raw} is not a 64-bit integer.");
        }

        /// <summary>The value as a double.</summary>
        public double AsDouble() => double.Parse(Raw, NumberStyles.Float, CultureInfo.InvariantCulture);

        private static string FormatDouble(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(nameof(value), "JSON cannot represent NaN or infinity.");
            }
            return value.ToString("R", CultureInfo.InvariantCulture);
        }
    }

    /// <summary>JSON array.</summary>
    public sealed class JsonArray : JsonNode
    {
        private readonly List<JsonNode> _items = new List<JsonNode>();

        /// <inheritdoc />
        public override JsonKind Kind => JsonKind.Array;

        /// <summary>Number of items.</summary>
        public int Count => _items.Count;

        /// <summary>Item at <paramref name="index"/>.</summary>
        public JsonNode this[int index]
        {
            get => _items[index];
            set => _items[index] = value ?? JsonNull.Instance;
        }

        /// <summary>The items in order.</summary>
        public IReadOnlyList<JsonNode> Items => _items;

        /// <summary>Appends an item (null becomes JSON null).</summary>
        public JsonArray Add(JsonNode item)
        {
            _items.Add(item ?? JsonNull.Instance);
            return this;
        }
    }

    /// <summary>JSON object. Keys keep insertion order so written files are stable and diff-friendly.</summary>
    public sealed class JsonObject : JsonNode
    {
        private readonly Dictionary<string, JsonNode> _values = new Dictionary<string, JsonNode>();
        private readonly List<string> _order = new List<string>();

        /// <inheritdoc />
        public override JsonKind Kind => JsonKind.Object;

        /// <summary>Number of properties.</summary>
        public int Count => _order.Count;

        /// <summary>Property names in insertion order.</summary>
        public IReadOnlyList<string> Keys => _order;

        /// <summary>Gets a property (null if missing) or sets one (null becomes JSON null).</summary>
        public JsonNode this[string key]
        {
            get => _values.TryGetValue(key, out var v) ? v : null;
            set => Set(key, value);
        }

        /// <summary>Sets a property, keeping its position if it already exists.</summary>
        public JsonObject Set(string key, JsonNode value)
        {
            if (key == null) throw new ArgumentNullException(nameof(key));
            if (!_values.ContainsKey(key)) _order.Add(key);
            _values[key] = value ?? JsonNull.Instance;
            return this;
        }

        /// <summary>True if the property exists.</summary>
        public bool Has(string key) => _values.ContainsKey(key);

        /// <summary>Removes a property. Returns false if it did not exist.</summary>
        public bool Remove(string key)
        {
            if (!_values.Remove(key)) return false;
            _order.Remove(key);
            return true;
        }

        /// <summary>Renames a property in place. Returns false if <paramref name="from"/> did not exist.</summary>
        public bool Rename(string from, string to)
        {
            if (!_values.TryGetValue(from, out var value)) return false;
            if (from == to) return true;
            Remove(to);
            var index = _order.IndexOf(from);
            _order[index] = to;
            _values.Remove(from);
            _values[to] = value;
            return true;
        }

        /// <summary>Reads a string property, or <paramref name="fallback"/> if missing or not a string.</summary>
        public string GetString(string key, string fallback = null) =>
            this[key] is JsonString s ? s.Value : fallback;

        /// <summary>Reads an integer property, or <paramref name="fallback"/> if missing or not a number.</summary>
        public long GetLong(string key, long fallback = 0) =>
            this[key] is JsonNumber n ? n.AsLong() : fallback;

        /// <summary>Reads an integer property, or <paramref name="fallback"/> if missing or not a number.</summary>
        public int GetInt(string key, int fallback = 0) =>
            this[key] is JsonNumber n ? checked((int)n.AsLong()) : fallback;

        /// <summary>Reads a floating point property, or <paramref name="fallback"/> if missing or not a number.</summary>
        public double GetDouble(string key, double fallback = 0) =>
            this[key] is JsonNumber n ? n.AsDouble() : fallback;

        /// <summary>Reads a bool property, or <paramref name="fallback"/> if missing or not a bool.</summary>
        public bool GetBool(string key, bool fallback = false) =>
            this[key] is JsonBool b ? b.Value : fallback;

        /// <summary>Reads an object property, or null if missing or not an object.</summary>
        public JsonObject GetObject(string key) => this[key] as JsonObject;

        /// <summary>Reads an array property, or null if missing or not an array.</summary>
        public JsonArray GetArray(string key) => this[key] as JsonArray;
    }
}
