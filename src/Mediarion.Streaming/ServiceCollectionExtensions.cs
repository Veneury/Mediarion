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

            return services;
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
