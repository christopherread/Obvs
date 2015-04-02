using System;
using Obvs.Configuration;
using Obvs.Serialization;
using Obvs.Types;

namespace Obvs.NetMQ.Configuration
{
    public interface ICanAddNetMqServiceName
    {
        ICanAddNetMqAddress Named(string serviceName);
    }

    public interface ICanAddNetMqAddress
    {
        ICanAddNetMqPort BindToAddress(string address);
    }

    public interface ICanAddNetMqPort
    {
        ICanSpecifyEndpointSerializers OnPort(int port);
    }

    internal class NetMqFluentConfig<TServiceMessage> : ICanAddNetMqAddress, ICanAddNetMqPort, ICanAddNetMqServiceName, ICanCreateEndpointAsClientOrServer, ICanSpecifyEndpointSerializers
        where TServiceMessage : IMessage
    {
        private readonly ICanAddEndpoint _canAddEndpoint;
        private string _serviceName;
        private Uri _address;
        private int _port;
        private IMessageSerializer _serializer;
        private IMessageDeserializerFactory _deserializerFactory;
        private string _assemblyNameContains;

        public NetMqFluentConfig(ICanAddEndpoint canAddEndpoint)
        {
            _canAddEndpoint = canAddEndpoint;
        }

        public ICanAddNetMqAddress Named(string serviceName)
        {
            _serviceName = serviceName;
            return this;
        }

        public ICanAddNetMqPort BindToAddress(string address)
        {
            _address = new Uri(address);
            return this;
        }

        ICanSpecifyEndpointSerializers ICanAddNetMqPort.OnPort(int port)
        {
            _port = port;
            return this;
        }

        public ICanAddEndpointOrLoggingOrCreate AsClient()
        {
            return _canAddEndpoint.WithClientEndpoints(CreateProvider());
        }

        public ICanAddEndpointOrLoggingOrCreate AsServer()
        {
            return _canAddEndpoint.WithServerEndpoints(CreateProvider());
        }

        public ICanAddEndpointOrLoggingOrCreate AsClientAndServer()
        {
            return _canAddEndpoint.WithEndpoints(CreateProvider());
        }

        private NetMqServiceEndpointProvider<TServiceMessage> CreateProvider()
        {
            return new NetMqServiceEndpointProvider<TServiceMessage>(_serviceName, _address.OriginalString, _port, _serializer, _deserializerFactory, _assemblyNameContains);
        }

        public ICanCreateEndpointAsClientOrServer SerializedWith(IMessageSerializer serializer, IMessageDeserializerFactory deserializerFactory)
        {
            _serializer = serializer;
            _deserializerFactory = deserializerFactory;
            return this;
        }

        public ICanCreateEndpointAsClientOrServer FilterMessageTypeAssemblies(string assemblyNameContains)
        {
            _assemblyNameContains = assemblyNameContains;
            return this;
        }
    }
}