using System;
using Game.Core.Json;
using NUnit.Framework;

namespace Game.Tests
{
    public class JsonTests
    {
        [Test]
        public void RoundTrip_PreservesValuesAndKeyOrder()
        {
            var obj = new JsonObject()
                .Set("name", "Héllo \"quoted\"\n\ttab \u0001")
                .Set("big", long.MaxValue)
                .Set("neg", -42)
                .Set("ratio", 1.9)
                .Set("flag", true)
                .Set("nothing", (JsonNode)null)
                .Set("list", new JsonArray().Add(1).Add("two").Add(new JsonObject()));

            foreach (var pretty in new[] { false, true })
            {
                var parsed = (JsonObject)JsonNode.Parse(obj.ToJson(pretty));

                CollectionAssert.AreEqual(new[] { "name", "big", "neg", "ratio", "flag", "nothing", "list" }, parsed.Keys);
                Assert.AreEqual("Héllo \"quoted\"\n\ttab \u0001", parsed.GetString("name"));
                Assert.AreEqual(long.MaxValue, parsed.GetLong("big"));
                Assert.AreEqual(-42, parsed.GetInt("neg"));
                Assert.AreEqual(1.9, parsed.GetDouble("ratio"));
                Assert.IsTrue(parsed.GetBool("flag"));
                Assert.AreEqual(JsonKind.Null, parsed["nothing"].Kind);
                Assert.AreEqual(3, parsed.GetArray("list").Count);
                Assert.AreEqual(obj.ToJson(), parsed.ToJson());
            }
        }

        [Test]
        public void Parse_HandlesWhitespaceExponentsAndEscapes()
        {
            var node = (JsonObject)JsonNode.Parse("﻿ { \"a\" : [ 1e3 , -2.5E-1 ] , \"u\" : \"\\u0041\\/\" } ");

            Assert.AreEqual(1000, ((JsonNumber)node.GetArray("a")[0]).AsLong());
            Assert.AreEqual(-0.25, ((JsonNumber)node.GetArray("a")[1]).AsDouble());
            Assert.AreEqual("A/", node.GetString("u"));
        }

        [TestCase("")]
        [TestCase("{")]
        [TestCase("{\"a\":}")]
        [TestCase("{\"a\":1,}")]
        [TestCase("[1 2]")]
        [TestCase("tru")]
        [TestCase("\"unterminated")]
        [TestCase("01x")]
        [TestCase("{} extra")]
        [TestCase("{'a':1}")]
        public void Parse_InvalidInput_ThrowsFormatException(string text)
        {
            Assert.Throws<FormatException>(() => JsonNode.Parse(text));
        }

        [Test]
        public void Getters_ReturnFallbackForMissingOrWrongType()
        {
            var obj = (JsonObject)JsonNode.Parse("{\"s\":\"x\",\"n\":5}");

            Assert.AreEqual(9, obj.GetInt("s", 9));
            Assert.AreEqual("d", obj.GetString("n", "d"));
            Assert.IsNull(obj.GetObject("missing"));
            Assert.IsNull(obj["missing"]);
        }

        [Test]
        public void Rename_KeepsPosition()
        {
            var obj = new JsonObject().Set("a", 1).Set("b", 2).Set("c", 3);

            Assert.IsTrue(obj.Rename("b", "beta"));
            Assert.IsFalse(obj.Rename("zzz", "y"));

            CollectionAssert.AreEqual(new[] { "a", "beta", "c" }, obj.Keys);
            Assert.AreEqual(2, obj.GetInt("beta"));
        }
    }
}
