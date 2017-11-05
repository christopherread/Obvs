using System;
using System.Collections.Generic;
using System.Linq;
using Obvs.Configuration;
using Obvs.Types;
using Xunit;

namespace Obvs.Tests
{
    
    public class TestMessageTypes
    {
        public interface ITestServiceMessage : IMessage {}
        public class TestMessage1 : ITestServiceMessage { }
        public class TestMessage2 : ITestServiceMessage { }

        [Fact]
        public void TestThatCorrectMessageTypesAreFound()
        {
            IEnumerable<Type> types = MessageTypes.Get<IMessage, ITestServiceMessage>(assembly => assembly.FullName.Contains("Obvs.Tests")).ToArray();

            Assert.True(types.Any());
            Assert.True(types.Contains(typeof(TestMessage1)));
            Assert.True(types.Contains(typeof(TestMessage2)));
        }
    }
}