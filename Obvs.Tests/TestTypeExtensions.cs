using System.Collections.Generic;
using System.Reflection;
using Obvs.Extensions;
using Obvs.Types;
using Xunit;

namespace Obvs.Tests
{
    // The helpers extend Type, which has everything they use since .NET Standard 2.0; a TypeInfo is a Type, so calls on one still compile
    public class TestTypeExtensions
    {
        private interface IOrderMessage : IMessage { }
        private class PlaceOrder : IOrderMessage, ICommand { }
        private class OtherMessage : IMessage { }
        private class NeedsAnId
        {
            public NeedsAnId(string id) { }
        }

        [Fact]
        public void ShouldNameAGenericTypeWithoutItsArity()
        {
            Assert.Equal("List", typeof(List<int>).GetSimpleName());
        }

        [Fact]
        public void ShouldNameANonGenericTypeAsItIs()
        {
            Assert.Equal("PlaceOrder", typeof(PlaceOrder).GetSimpleName());
        }

        [Fact]
        public void ShouldStillNameATypeInfo()
        {
            Assert.Equal("List", typeof(List<int>).GetTypeInfo().GetSimpleName());
        }

        [Fact]
        public void ShouldKnowWhetherATypeCanBeCreatedWithoutArguments()
        {
            Assert.True(typeof(PlaceOrder).HasDefaultConstructor());
            Assert.False(typeof(NeedsAnId).HasDefaultConstructor());
        }

        [Fact]
        public void ShouldTakeOnlyClassesOfBothMessageTypes()
        {
            Assert.True(typeof(PlaceOrder).IsValidMessageType<IMessage, IOrderMessage>());
            Assert.False(typeof(IOrderMessage).IsValidMessageType<IMessage, IOrderMessage>());
            Assert.False(typeof(OtherMessage).IsValidMessageType<IMessage, IOrderMessage>());
        }
    }
}
