using System;
using System.Collections.Generic;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using Obvs.Extensions;
using Obvs.Types;

namespace Obvs
{
    public abstract class ServiceBusErrorHandlingBase
    {
        private readonly Subject<Exception> _exceptions;

        protected ServiceBusErrorHandlingBase()
        {
            _exceptions = new Subject<Exception>();
        }

        public IObservable<Exception> Exceptions
        {
            get { return _exceptions; }
        }

        protected IObservable<IEvent> EventsWithErroHandling(IServiceEndpointClient endpoint)
        {
            return endpoint.Events.CatchAndHandle(_exceptions, () => endpoint.Events, string.Format("Error receiving {0} from endpoint {1}", typeof(IEvent), endpoint.GetType().FullName));
        }

        protected IObservable<ICommand> CommandsWithErrorHandling(IServiceEndpoint endpoint)
        {
            return endpoint.Commands.CatchAndHandle(_exceptions, () => endpoint.Commands, string.Format("Error receiving {0} from endpoint {1}", typeof(ICommand), endpoint.GetType().FullName));
        }

        protected IObservable<IRequest> RequestsWithErrorHandling(IServiceEndpoint endpoint)
        {
            return endpoint.Requests.CatchAndHandle(_exceptions, () => endpoint.Requests, string.Format("Error receiving {0} from endpoint {1}", typeof(IRequest), endpoint.GetType().FullName));
        }

        protected static string EventErrorMessage(IEndpoint endpoint)
        {
            return string.Format("Error publishing event to endpoint {0}", endpoint.GetType().FullName);
        }

        protected static string ReplyErrorMessage(IEndpoint endpoint)
        {
            return string.Format("Error sending response to endpoint {0}", endpoint.GetType().FullName);
        }

        protected static string CommandErrorMessage(IEndpoint endpoint)
        {
            return string.Format("Error sending command to endpoint {0}", endpoint.GetType().FullName);
        }

        protected static string EventErrorMessage(IEvent ev)
        {
            return string.Format("Error publishing event {0}", ev);
        }

        protected static string ReplyErrorMessage(IRequest request, IResponse response)
        {
            return string.Format("Error replying to request {0} with response {1}", request, response);
        }

        protected static string CommandErrorMessage(ICommand command)
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
    }
}