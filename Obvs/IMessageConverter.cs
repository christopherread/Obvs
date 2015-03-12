using System;
using Obvs.Types;

namespace Obvs
{
    public interface IMessageConverter<in TFrom, out TTo> : IDisposable
        where TFrom : IMessage
        where TTo : IMessage
    {
        TTo Convert(TFrom from);
    }
}