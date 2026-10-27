using System;

// v6's message bridging, kept for v6 apps: its public members are exempt from the rule that every one is documented
#pragma warning disable CS1570, CS1572, CS1591

namespace Obvs
{
    public interface IMessageConverter<in TFrom, out TTo> : IDisposable
        where TFrom : class
        where TTo : class
    {
        TTo Convert(TFrom from);
    }
}