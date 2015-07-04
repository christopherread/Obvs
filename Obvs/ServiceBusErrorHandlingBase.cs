using System;
using System.Collections.Generic;
using System.Reactive.Subjects;
using System.Threading.Tasks;
using Obvs.Extensions;

namespace Obvs
{
    public abstract class ServiceBusErrorHandlingBase<TMessage, TCommand, TEvent, TRequest, TResponse> : IDisposable
        where TMessage : class
        where TCommand : TMessage
        where TEvent : TMessage
        where TRequest : TMessage
        where TResponse : TMessage
    {
        protected readonly Subject<Exception> _exceptions;

        protected ServiceBusErrorHandlingBase()
        {
            _exceptions = new Subject<Exception>();
        }

        public IObservable<Exception> Exceptions
        {
            get { return _exceptions; }
        }

        protected static string EventErrorMessage(IEndpoint<TMessage> endpoint)
        {
            return string.Format("Error publishing event to endpoint {0}", endpoint.GetType().FullName);
        }

        protected static string ReplyErrorMessage(IEndpoint<TMessage> endpoint)
        {
            return string.Format("Error sending response to endpoint {0}", endpoint.GetType().FullName);
        }

        protected static string CommandErrorMessage(IEndpoint<TMessage> endpoint)
        {
            return string.Format("Error sending command to endpoint {0}", endpoint.GetType().FullName);
        }

        protected static string EventErrorMessage(TEvent ev)
        {
            return string.Format("Error publishing event {0}", ev);
        }

        protected static string ReplyErrorMessage(TRequest request, TResponse response)
        {
            return string.Format("Error replying to request {0} with response {1}", request, response);
        }

        protected static string CommandErrorMessage(TCommand command)
        {
            return string.Format("Error sending command {0}", command);
        }

        protected static string CommandErrorMessage()
        {
            return "Error sending commands";
        }

        protected void Catch(Action action, List<Exception> exceptions, string message = null)
        {
            try
            {
                action();
            }
            catch (Exception exception)
            {
                exceptions.Add(message == null ? exception : new Exception(message, exception));
            }
        }

        protected Task Catch(Func<Task> func, List<Exception> exceptions, string message = null)
        {
            try
            {
                return func();
            }
            catch (Exception exception)
            {
                exceptions.Add(message == null ? exception : new Exception(message, exception));
            }
            return Task.FromResult(false);
        }

        public virtual void Dispose()
        {
            _exceptions.Dispose();
        }
    }
}