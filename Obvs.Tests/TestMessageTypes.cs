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

        // A single-file app has no dependency context (#42), so scanning follows the app's references from its entry assembly
        [Fact]
        public void ShouldFindTheAssembliesThatUseObvsWithoutADependencyContext()
        {
            var assemblies = MessageTypes.AssembliesThatUseObvs(null, typeof(TestMessageTypes).Assembly);

            Assert.Contains(typeof(TestMessageTypes).Assembly, assemblies);
            Assert.Contains(typeof(IMessage).Assembly, assemblies);
            Assert.Contains(typeof(Obvs.Testing.ObvsTestHarness).Assembly, assemblies);
        }

        [Fact]
        public void ShouldLeaveOutAssembliesThatDontUseObvsWithoutADependencyContext()
        {
            var assemblies = MessageTypes.AssembliesThatUseObvs(null, typeof(TestMessageTypes).Assembly);

            Assert.DoesNotContain(typeof(FactAttribute).Assembly, assemblies);
            Assert.DoesNotContain(typeof(object).Assembly, assemblies);
        }

        [Fact]
        public void ShouldFindTheAssembliesThatUseObvsFromTheDependencyContext()
        {
            var assemblies = MessageTypes.AssembliesThatUseObvs(Microsoft.Extensions.DependencyModel.DependencyContext.Default, typeof(TestMessageTypes).Assembly);

            Assert.Contains(typeof(TestMessageTypes).Assembly, assemblies);
            Assert.Contains(typeof(IMessage).Assembly, assemblies);
            Assert.DoesNotContain(typeof(FactAttribute).Assembly, assemblies);
        }
    }
}