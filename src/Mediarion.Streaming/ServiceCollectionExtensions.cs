using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace Mediarion
{
    /// <summary>
    /// Registers streaming with the container.
    /// </summary>
    public static class StreamingServiceCollectionExtensions
    {
        /// <summary>
        /// Registers the streaming handlers in the given assemblies, and replaces the mediator
        /// with one that can stream.
        /// </summary>
        /// <param name="services">The container.</param>
        /// <param name="assemblies">The assemblies to scan.</param>
        /// <returns>The container, to carry on configuring.</returns>
        /// <remarks>
        /// Call it after <c>AddMediarion</c>. Sending and publishing are untouched: the mediator
        /// it puts in place derives from the one that was there.
        /// </remarks>
        [RequiresUnreferencedCode("Streaming handlers are found by walking the types in an assembly.")]
        [RequiresDynamicCode("Streaming handlers are registered against generics closed at run time.")]
        public static IServiceCollection AddMediarionStreaming(
            this IServiceCollection services,
            params Assembly[] assemblies)
        {
            Guard.NotNull(services, nameof(services));
            Guard.NotNull(assemblies, nameof(assemblies));

            Replace(services, typeof(IMediator));
            Replace(services, typeof(ISender));
            Replace(services, typeof(IStreamSender));

            foreach (Assembly assembly in assemblies)
            {
                Scan(services, assembly);
            }

            AddConfiguredBehaviours(services);

            return services;
        }

        /// <summary>
        /// Registers the stream behaviours that were added to <c>AddMediarion</c>'s configuration.
        /// </summary>
        /// <remarks>
        /// <para>
        /// They are asked for over there and registered here, because the contract they go against
        /// is in this package and the configuration is in one that does not reference it. The
        /// configuration is left in the collection by <c>AddMediarion</c> for exactly this.
        /// </para>
        /// <para>
        /// They go on after the ones found by scanning, so a behaviour named by hand wraps one
        /// that was found — the same way the request pipeline puts what the application added
        /// inside what the library put there.
        /// </para>
        /// </remarks>
        [RequiresUnreferencedCode("A stream behaviour is registered against the contracts it implements.")]
        [RequiresDynamicCode("A stream behaviour is registered against generics closed at run time.")]
        private static void AddConfiguredBehaviours(IServiceCollection services)
        {
            MediarionServiceConfiguration? configuration = null;

            foreach (ServiceDescriptor descriptor in services)
            {
                if (descriptor.ServiceType == typeof(MediarionServiceConfiguration) &&
                    descriptor.ImplementationInstance is MediarionServiceConfiguration found)
                {
                    configuration = found;
                }
            }

            if (configuration is null)
            {
                return;
            }

            foreach (Registration registration in configuration.StreamBehaviourRegistrations)
            {
                AddStreamBehaviour(services, registration);
            }
        }

        [RequiresUnreferencedCode("A stream behaviour is registered against the contracts it implements.")]
        [RequiresDynamicCode("A stream behaviour is registered against generics closed at run time.")]
        private static void AddStreamBehaviour(IServiceCollection services, Registration registration)
        {
            Type behaviour = registration.ImplementationType;

            if (registration.ServiceType is Type named)
            {
                services.Add(new ServiceDescriptor(named, behaviour, registration.Lifetime));
                return;
            }

            if (behaviour.IsGenericTypeDefinition)
            {
                services.Add(new ServiceDescriptor(
                    typeof(IStreamPipelineBehavior<,>),
                    behaviour,
                    registration.Lifetime));

                return;
            }

            var registered = false;

            foreach (Type contract in behaviour.GetInterfaces())
            {
                if (contract.IsGenericType &&
                    contract.GetGenericTypeDefinition() == typeof(IStreamPipelineBehavior<,>))
                {
                    services.Add(new ServiceDescriptor(contract, behaviour, registration.Lifetime));
                    registered = true;
                }
            }

            if (!registered)
            {
                throw new MediarionException(
                    behaviour.Name + " was added as a stream behaviour but does not implement " +
                    "IStreamPipelineBehavior<TRequest, TResponse>.");
            }
        }

        /// <remarks>
        /// The mediator is registered again rather than the old registration being edited:
        /// what the container hands back is the last registration for a service type, so this is
        /// enough, and it leaves anything else the application did to that type alone.
        /// </remarks>
        [RequiresUnreferencedCode("The streaming mediator closes generics over the request type at run time.")]
        [RequiresDynamicCode("The streaming mediator closes generics over the request type at run time.")]
        private static void Replace(IServiceCollection services, Type contract)
        {
            services.Add(new ServiceDescriptor(contract, typeof(StreamingMediator), ServiceLifetime.Transient));
        }

        [RequiresUnreferencedCode("Streaming handlers are found by walking the types in an assembly.")]
        [RequiresDynamicCode("Streaming handlers are registered against generics closed at run time.")]
        private static void Scan(IServiceCollection services, Assembly assembly)
        {
            foreach (Type candidate in assembly.GetTypes())
            {
                if (candidate.IsAbstract || candidate.IsInterface || candidate.IsGenericTypeDefinition)
                {
                    continue;
                }

                foreach (Type contract in candidate.GetInterfaces())
                {
                    if (contract.IsGenericType &&
                        contract.GetGenericTypeDefinition() == typeof(IStreamRequestHandler<,>))
                    {
                        services.Add(new ServiceDescriptor(contract, candidate, ServiceLifetime.Transient));
                    }
                }
            }
        }

        /// <summary>Adds a stream pipeline behaviour, which may be an open generic.</summary>
        /// <param name="services">The container.</param>
        /// <param name="behaviourType">The behaviour type.</param>
        /// <returns>The container, to carry on configuring.</returns>
        /// <remarks>Behaviours run in the order they are added, outermost first.</remarks>
        [RequiresUnreferencedCode("A behaviour is registered against the contracts it implements.")]
        [RequiresDynamicCode("A behaviour is registered against generics closed at run time.")]
        public static IServiceCollection AddStreamBehavior(this IServiceCollection services, Type behaviourType)
        {
            Guard.NotNull(services, nameof(services));
            Guard.NotNull(behaviourType, nameof(behaviourType));

            if (behaviourType.IsGenericTypeDefinition)
            {
                services.Add(new ServiceDescriptor(
                    typeof(IStreamPipelineBehavior<,>),
                    behaviourType,
                    ServiceLifetime.Transient));

                return services;
            }

            var registered = false;

            foreach (Type contract in behaviourType.GetInterfaces())
            {
                if (contract.IsGenericType &&
                    contract.GetGenericTypeDefinition() == typeof(IStreamPipelineBehavior<,>))
                {
                    services.Add(new ServiceDescriptor(contract, behaviourType, ServiceLifetime.Transient));
                    registered = true;
                }
            }

            if (!registered)
            {
                throw new MediarionException(
                    behaviourType.Name + " was added as a stream behaviour but does not implement " +
                    "IStreamPipelineBehavior<TRequest, TResponse>.");
            }

            return services;
        }
    }
}
