using System.Collections.Generic;
using Game.Core.Events;
using NUnit.Framework;

namespace Game.Tests
{
    public class EventBusTests
    {
        private struct Ping { public int Value; }
        private struct Pong { }

        [Test]
        public void Publish_DeliversToSubscribersOfThatTypeOnly()
        {
            var bus = new EventBus();
            var pings = new List<int>();
            var pongs = 0;
            bus.Subscribe<Ping>(p => pings.Add(p.Value));
            bus.Subscribe<Pong>(_ => pongs++);

            bus.Publish(new Ping { Value = 7 });

            CollectionAssert.AreEqual(new[] { 7 }, pings);
            Assert.AreEqual(0, pongs);
        }

        [Test]
        public void DisposingToken_Unsubscribes()
        {
            var bus = new EventBus();
            var count = 0;
            var token = bus.Subscribe<Ping>(_ => count++);

            token.Dispose();
            token.Dispose();
            bus.Publish(new Ping());

            Assert.AreEqual(0, count);
            Assert.AreEqual(0, bus.SubscriberCount<Ping>());
        }

        [Test]
        public void UnsubscribingDuringPublish_DoesNotSkipOtherHandlers()
        {
            var bus = new EventBus();
            var calls = new List<string>();
            System.IDisposable first = null;
            first = bus.Subscribe<Ping>(_ => { calls.Add("a"); first.Dispose(); });
            bus.Subscribe<Ping>(_ => calls.Add("b"));

            bus.Publish(new Ping());
            bus.Publish(new Ping());

            CollectionAssert.AreEqual(new[] { "a", "b", "b" }, calls);
        }

        [Test]
        public void Publish_WithNoSubscribers_DoesNothing()
        {
            Assert.DoesNotThrow(() => new EventBus().Publish(new Ping()));
        }
    }
}
