using System;
using Microsoft.Extensions.DependencyInjection;

namespace Mediarion
{
    /// <summary>
    /// One thing the configuration was asked to register, and how.
    /// </summary>
    /// <remarks>
    /// The service type is optional because most of the time it is worked out from what the
    /// implementation implements. It is here for the overloads that name it, which is how a
    /// behaviour gets registered against one request rather than against everything it could
    /// serve.
    /// </remarks>
    internal sealed class Registration
    {
        internal Registration(Type? serviceType, Type implementationType, ServiceLifetime lifetime)
        {
            ServiceType = serviceType;
            ImplementationType = implementationType;
            Lifetime = lifetime;
        }

        internal Type? ServiceType { get; }

        internal Type ImplementationType { get; }

        internal ServiceLifetime Lifetime { get; }
    }
}
