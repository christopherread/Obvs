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
        ICanCreateClientOrServer OnPort(int port);
    }
    
    public interface ICanCreateClientOrServer
    {
        ICanAddEndpointOrCreate AsClient();
        ICanAddEndpointOrCreate AsServer();
        ICanAddEndpointOrCreate AsClientAndServer();
    }

    internal class NetMqFluentConfig<TServiceMessage> : ICanAddNetMqAddress, ICanAddNetMqPort, ICanAddNetMqServiceName, ICanCreateClientOrServer 
        where TServiceMessage : IMessage
    {
        private readonly ICanAddEndpoint _canAddEndpoint;
        private string _serviceName;
        private Uri _address;
        private int _port;

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

        ICanCreateClientOrServer ICanAddNetMqPort.OnPort(int port)
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
            return new NetMqServiceEndpointProvider<TServiceMessage>(_serviceName, _address.OriginalString, _port);
        }
    }
}