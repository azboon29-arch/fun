using System;
using Game.Core.Services;
using NUnit.Framework;

namespace Game.Tests
{
    public class GameServicesTests
    {
        private interface IThing { }
        private sealed class Thing : IThing { }

        [Test]
        public void Get_ReturnsRegisteredService()
        {
            var services = new GameServices();
            var thing = new Thing();
            services.Register<IThing>(thing);

            Assert.AreSame(thing, services.Get<IThing>());
            Assert.IsTrue(services.TryGet<IThing>(out var found));
            Assert.AreSame(thing, found);
        }

        [Test]
        public void Get_Missing_Throws_TryGet_ReturnsFalse()
        {
            var services = new GameServices();

            Assert.Throws<InvalidOperationException>(() => services.Get<IThing>());
            Assert.IsFalse(services.TryGet<IThing>(out _));
        }

        [Test]
        public void Register_Twice_Throws()
        {
            var services = new GameServices();
            services.Register<IThing>(new Thing());

            Assert.Throws<InvalidOperationException>(() => services.Register<IThing>(new Thing()));
        }
    }
}
