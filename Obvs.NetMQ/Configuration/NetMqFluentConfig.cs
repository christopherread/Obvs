using System;
using Obvs.Configuration;
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
        ICanSpecifySerializers OnPort(int port);
    }

    public interface ICanSpecifySerializers
    {
        ICanCreateClientOrServer SerializedWith(IMessageSerializer serializer, IMessageDeserializerFactory deserializerFactory);
    }
    
    public interface ICanCreateClientOrServer
    {
        ICanAddEndpointOrCreate AsClient();
        ICanAddEndpointOrCreate AsServer();
        ICanAddEndpointOrCreate AsClientAndServer();
    }

    internal class NetMqFluentConfig<TServiceMessage> : ICanAddNetMqAddress, ICanAddNetMqPort, ICanAddNetMqServiceName, ICanCreateClientOrServer, ICanSpecifySerializers
        where TServiceMessage : IMessage
    {
        private readonly ICanAddEndpoint _canAddEndpoint;
        private string _serviceName;
        private Uri _address;
        private int _port;
        private IMessageSerializer _serializer;
        private IMessageDeserializerFactory _deserializerFactory;

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

        ICanSpecifySerializers ICanAddNetMqPort.OnPort(int port)
        {
            _port = port;
            return this;
        }

        public ICanAddEndpointOrCreate AsClient()
        {
            return _canAddEndpoint.WithClientEndpoints(CreateProvider());
        }

        public ICanAddEndpointOrCreate AsServer()
        {
            return _canAddEndpoint.WithServerEndpoints(CreateProvider());
        }

        public ICanAddEndpointOrCreate AsClientAndServer()
        {
            return _canAddEndpoint.WithEndpoints(CreateProvider());
        }

        private NetMqServiceEndpointProvider<TServiceMessage> CreateProvider()
        {
            return new NetMqServiceEndpointProvider<TServiceMessage>(_serviceName, _address.OriginalString, _port, _serializer, _deserializerFactory);
        }

        public ICanCreateClientOrServer SerializedWith(IMessageSerializer serializer, IMessageDeserializerFactory deserializerFactory)
        {
            _serializer = serializer;
            _deserializerFactory = deserializerFactory;
            return this;
        }
    }
}