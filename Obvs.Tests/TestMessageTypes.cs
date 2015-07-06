using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Obvs.Configuration;
using Obvs.Types;

namespace Obvs.Tests
{
    [TestFixture]
    public class TestMessageTypes
    {
        public interface ITestServiceMessage : IMessage {}
        public class TestMessage1 : ITestServiceMessage { }
        public class TesTMessageWithProperties : ITestServiceMessage { }

        [Test]
        public void TestThatCorrectMessageTypesAreFound()
        {
            IEnumerable<Type> types = MessageTypes.Get<IMessage, ITestServiceMessage>(assembly => assembly.FullName.Contains("Obvs.Tests")).ToArray();

            Assert.That(types.Any());
            Assert.That(types.Contains(typeof(TestMessage1)));
            Assert.That(types.Contains(typeof(TesTMessageWithProperties)));
        }
    }
}