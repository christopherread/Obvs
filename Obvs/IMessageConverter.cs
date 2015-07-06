using System;

namespace Obvs
{
    public interface IMessageConverter<in TFrom, out TTo> : IDisposable
        where TFrom : class
        where TTo : class
    {
        TTo Convert(TFrom from);
    }
}