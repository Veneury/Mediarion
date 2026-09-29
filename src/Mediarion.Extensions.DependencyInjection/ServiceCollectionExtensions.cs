using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Mediarion.Execution;
using Mediarion.NotificationPublishers;
using Mediarion.Pipeline;
using Microsoft.Extensions.DependencyInjection;

namespace Mediarion
{
    /// <summary>
    /// Registers Mediarion with the container.
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        private static readonly Type[] HandlerContracts =
        {
            typeof(IRequestHandler<,>),
            typeof(IRequestHandler<>),
            typeof(INotificationHandler<>),
        };

        private static readonly Type[] ProcessorContracts =
        {
            typeof(IRequestPreProcessor<>),
            typeof(IRequestPostProcessor<,>),
        };

        private static readonly Type[] ExceptionContracts =
        {
            typeof(IRequestExceptionHandler<,,>),
            typeof(IRequestExceptionAction<,>),
        };

        /// <summary>
        /// Registers the mediator and every handler in the assemblies the configuration names.
        /// </summary>
        /// <param name="services">The container.</param>
        /// <param name="configure">What to register.</param>
        /// <returns>The container, to carry on configuring.</returns>
        [RequiresUnreferencedCode("Handlers are found by walking the types in an assembly.")]
        [RequiresDynamicCode("Handlers are registered against generics closed at run time.")]
        public static IServiceCollection AddMediarion(
            this IServiceCollection services,
            Action<MediarionServiceConfiguration> configure)
        {
            Guard.NotNull(services, nameof(services));
            Guard.NotNull(configure, nameof(configure));

            var configuration = new MediarionServiceConfiguration();
            configure(configuration);

            return Add(services, configuration);
        }

        /// <summary>
        /// Registers the mediator and every handler in the given assemblies.
        /// </summary>
        /// <param name="services">The container.</param>
        /// <param name="assemblies">The assemblies to scan.</param>
        /// <returns>The container, to carry on configuring.</returns>
        [RequiresUnreferencedCode("Handlers are found by walking the types in an assembly.")]
        [RequiresDynamicCode("Handlers are registered against generics closed at run time.")]
        public static IServiceCollection AddMediarion(
            this IServiceCollection services,
            params Assembly[] assemblies)
        {
            Guard.NotNull(assemblies, nameof(assemblies));

            return AddMediarion(services, configuration => configuration.RegisterServicesFromAssemblies(assemblies));
        }

        [RequiresUnreferencedCode("Handlers are found by walking the types in an assembly.")]
        [RequiresDynamicCode("Handlers are registered against generics closed at run time.")]
        private static IServiceCollection Add(IServiceCollection services, MediarionServiceConfiguration configuration)
        {
            bool processors = configuration.AutoRegisterRequestProcessors || configuration.Processors.Count > 0;
            var exceptions = new ExceptionRegistrations();

            services.TryAddSingletonPublisher(configuration);

            // Left in the collection so AddMediarionStreaming can read the stream behaviours off
            // it. They cannot be registered here: the contract they go against lives in the
            // streaming package, which this one does not reference and must not.
            services.Add(new ServiceDescriptor(typeof(MediarionServiceConfiguration), configuration));

            Type mediator = configuration.MediatorImplementationType ?? typeof(Mediator);

            if (!typeof(IMediator).IsAssignableFrom(mediator))
            {
                throw new MediarionException(
                    mediator.Name + " was named as the mediator but does not implement IMediator.");
            }

            services.Add(new ServiceDescriptor(typeof(IMediator), mediator, ServiceLifetime.Transient));
            services.Add(new ServiceDescriptor(typeof(ISender), p => p.GetRequiredService<IMediator>(), ServiceLifetime.Transient));
            services.Add(new ServiceDescriptor(typeof(IPublisher), p => p.GetRequiredService<IMediator>(), ServiceLifetime.Transient));

            foreach (Assembly assembly in configuration.Assemblies)
            {
                Scan(
                    services,
                    assembly,
                    configuration.Lifetime,
                    configuration.AutoRegisterRequestProcessors,
                    exceptions,
                    configuration.TypeEvaluator);
            }

            if (configuration.RegisterGenericHandlers)
            {
                GenericHandlerRegistrar.Add(services, configuration);
            }

            foreach (Registration processor in configuration.ProcessorRegistrations)
            {
                AddProcessor(services, processor);
            }

            // Outermost, ahead of everything the application adds. That is not where they were
            // put first: innermost reads better, on the argument that an exception handler is
            // for what the handler threw rather than for what a behaviour decided. The other
            // library puts them outside, and the difference is visible — a behaviour that logs
            // on the way out never runs when the handler throws there, and does run here once
            // the handler has turned the exception into a response. Matching it is the point of
            // the library, so they go where it puts them.
            //
            // Only when something was found to run, for the reason the processors are: an
            // open-generic behaviour costs an enumerable from the container on every request.
            // Which of the two goes on first is the whole of RequestExceptionActionProcessorStrategy.
            // Registration order is outermost first, and both behaviours work by catching: put the
            // actions outside the handlers and an exception a handler answered never reaches them,
            // put them inside and every exception does. Nothing is asked per request.
            void AddExceptionHandlers()
            {
                if (exceptions.Handlers)
                {
                    services.Add(new ServiceDescriptor(
                        typeof(IPipelineBehavior<,>),
                        typeof(RequestExceptionProcessorBehavior<,>),
                        ServiceLifetime.Transient));
                }
            }

            void AddExceptionActions()
            {
                if (exceptions.Actions)
                {
                    services.Add(new ServiceDescriptor(
                        typeof(IPipelineBehavior<,>),
                        typeof(RequestExceptionActionProcessorBehavior<,>),
                        ServiceLifetime.Transient));
                }
            }

            if (configuration.RequestExceptionActionProcessorStrategy
                == RequestExceptionActionProcessorStrategy.ApplyForUnhandledExceptions)
            {
                AddExceptionActions();
                AddExceptionHandlers();
            }
            else
            {
                AddExceptionHandlers();
                AddExceptionActions();
            }

            // The two that run the pre- and post-processors go on first, so a pre-processor runs
            // before any behaviour the application added and a post-processor sees the response
            // the handler produced rather than what a behaviour did to it.
            //
            // Only when there is something for them to run. They used to go on always, on the
            // grounds that each is one empty loop when there is nothing — which was true and
            // cost 110 nanoseconds and 480 bytes on every request, because an open generic
            // registration is closed by the container per pair and each of these asks it for an
            // enumerable of its own. It also disagreed with the generated registration, which
            // has always left them out unless asked. A processor registered straight into the
            // container after this call is not run, which is what the other library does too.
            if (processors)
            {
                services.Add(new ServiceDescriptor(
                    typeof(IPipelineBehavior<,>),
                    typeof(RequestPreProcessorBehavior<,>),
                    ServiceLifetime.Transient));

                services.Add(new ServiceDescriptor(
                    typeof(IPipelineBehavior<,>),
                    typeof(RequestPostProcessorBehavior<,>),
                    ServiceLifetime.Transient));
            }

            // Registered in the order they were added, because the container hands them back in
            // registration order and the pipeline reads that as outermost first.
            foreach (Registration behaviour in configuration.BehaviourRegistrations)
            {
                AddBehaviour(services, behaviour);
            }

            return services;
        }

        /// <remarks>
        /// Registered twice: once against the contract it declares, and once as an adapter that
        /// takes any exception and tests the type itself. The behaviours resolve only the second
        /// shape, so choosing a handler by the exception's type never closes a generic over a
        /// type learned at run time.
        /// </remarks>
        [RequiresUnreferencedCode("An exception handler is registered against the contracts it implements.")]
        [RequiresDynamicCode("An exception handler is registered against generics closed at run time.")]
        private static void AddException(
            IServiceCollection services,
            Type contract,
            Type definition,
            Type implementation,
            ServiceLifetime lifetime,
            ExceptionRegistrations found)
        {
            services.Add(new ServiceDescriptor(contract, implementation, lifetime));

            Type[] arguments = contract.GetGenericArguments();

            // A handler already written for Exception needs no adapter, and must not get one:
            // the adapter resolves the same service type it is registered as, so it would be
            // handed itself and call itself until the stack ran out.
            if (arguments[arguments.Length - 1] == typeof(Exception))
            {
                if (definition == typeof(IRequestExceptionHandler<,,>))
                {
                    found.Handlers = true;
                }
                else
                {
                    found.Actions = true;
                }

                return;
            }

            if (definition == typeof(IRequestExceptionHandler<,,>))
            {
                services.Add(new ServiceDescriptor(
                    typeof(IRequestExceptionHandler<,,>)
                        .MakeGenericType(arguments[0], arguments[1], typeof(Exception)),
                    typeof(ExceptionHandlerAdapter<,,>)
                        .MakeGenericType(arguments[0], arguments[1], arguments[2]),
                    lifetime));

                found.Handlers = true;
            }
            else
            {
                services.Add(new ServiceDescriptor(
                    typeof(IRequestExceptionAction<,>).MakeGenericType(arguments[0], typeof(Exception)),
                    typeof(ExceptionActionAdapter<,>).MakeGenericType(arguments[0], arguments[1]),
                    lifetime));

                found.Actions = true;
            }
        }

        [RequiresUnreferencedCode("A processor is registered against the contracts it implements.")]
        [RequiresDynamicCode("A processor is registered against generics closed at run time.")]
        private static void AddProcessor(IServiceCollection services, Registration registration)
        {
            Type processor = registration.ImplementationType;

            // Named its contract, so that is the one it is registered against and nothing is
            // worked out. This is how a processor that implements several ends up in front of one
            // request rather than all of them.
            if (registration.ServiceType is Type named)
            {
                services.Add(new ServiceDescriptor(named, processor, registration.Lifetime));
                return;
            }

            // An open generic is registered against the open contract, so the container can close
            // it per request. Which contract that is comes from the interfaces the definition
            // declares and not from how many type parameters it has: counting was tried and let a
            // two-parameter pipeline behaviour through as a post-processor, which the container
            // then failed to build on the first request rather than here.
            if (processor.IsGenericTypeDefinition)
            {
                var opened = false;

                foreach (Type contract in processor.GetInterfaces())
                {
                    if (contract.IsGenericType &&
                        Array.IndexOf(ProcessorContracts, contract.GetGenericTypeDefinition()) >= 0)
                    {
                        services.Add(new ServiceDescriptor(
                            contract.GetGenericTypeDefinition(),
                            processor,
                            registration.Lifetime));

                        opened = true;
                    }
                }

                if (!opened)
                {
                    throw new MediarionException(
                        processor.Name + " was added as an open generic processor but implements " +
                        "neither IRequestPreProcessor<TRequest> nor " +
                        "IRequestPostProcessor<TRequest, TResponse>.");
                }

                return;
            }

            var registered = false;

            foreach (Type contract in processor.GetInterfaces())
            {
                if (contract.IsGenericType &&
                    Array.IndexOf(ProcessorContracts, contract.GetGenericTypeDefinition()) >= 0)
                {
                    services.Add(new ServiceDescriptor(contract, processor, registration.Lifetime));
                    registered = true;
                }
            }

            if (!registered)
            {
                throw new MediarionException(
                    processor.Name + " was added as a processor but implements neither " +
                    "IRequestPreProcessor<TRequest> nor IRequestPostProcessor<TRequest, TResponse>.");
            }
        }

        /// <remarks>
        /// An open generic is registered against the open contract and the container closes it
        /// per request. Anything else is registered against the closed contracts it actually
        /// implements, which is the only way the container can hand it back: registering a
        /// closed type against the open contract fails when the first request arrives.
        /// </remarks>
        [RequiresUnreferencedCode("A behaviour is registered against the contracts it implements.")]
        [RequiresDynamicCode("A behaviour is registered against generics closed at run time.")]
        private static void AddBehaviour(IServiceCollection services, Registration registration)
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
                    typeof(IPipelineBehavior<,>),
                    behaviour,
                    registration.Lifetime));

                return;
            }

            var registered = false;

            foreach (Type contract in behaviour.GetInterfaces())
            {
                if (contract.IsGenericType &&
                    contract.GetGenericTypeDefinition() == typeof(IPipelineBehavior<,>))
                {
                    services.Add(new ServiceDescriptor(contract, behaviour, registration.Lifetime));
                    registered = true;
                }
            }

            if (!registered)
            {
                throw new MediarionException(
                    behaviour.Name + " was added as a pipeline behaviour but does not implement " +
                    "IPipelineBehavior<TRequest, TResponse>.");
            }
        }

        /// <remarks>
        /// An instance beats a type, because an instance cannot have been given by accident: the
        /// only way to set it is to have built one. With neither, the handlers of a notification
        /// run one after another.
        /// </remarks>
        [RequiresUnreferencedCode("A publisher named by type is built by the container.")]
        [RequiresDynamicCode("A publisher named by type is built by the container.")]
        private static void TryAddSingletonPublisher(
            this IServiceCollection services,
            MediarionServiceConfiguration configuration)
        {
            foreach (ServiceDescriptor descriptor in services)
            {
                if (descriptor.ServiceType == typeof(INotificationPublisher))
                {
                    return;
                }
            }

            if (configuration.NotificationPublisher is INotificationPublisher instance)
            {
                services.Add(new ServiceDescriptor(typeof(INotificationPublisher), instance));
                return;
            }

            if (configuration.NotificationPublisherType is Type named)
            {
                if (!typeof(INotificationPublisher).IsAssignableFrom(named))
                {
                    throw new MediarionException(
                        named.Name + " was named as the notification publisher but does not " +
                        "implement INotificationPublisher.");
                }

                services.Add(new ServiceDescriptor(
                    typeof(INotificationPublisher),
                    named,
                    ServiceLifetime.Singleton));

                return;
            }

            services.Add(new ServiceDescriptor(
                typeof(INotificationPublisher),
                new ForeachAwaitPublisher()));
        }

        [RequiresUnreferencedCode("Handlers are found by walking the types in an assembly.")]
        [RequiresDynamicCode("Handlers are registered against generics closed at run time.")]
        private static void Scan(
            IServiceCollection services,
            Assembly assembly,
            ServiceLifetime lifetime,
            bool processors,
            ExceptionRegistrations exceptions,
            Func<Type, bool> evaluator)
        {
            foreach (Type candidate in assembly.GetTypes())
            {
                if (!evaluator(candidate))
                {
                    continue;
                }

                // An open generic handler is skipped. The container closes an open
                // implementation against an open service type by matching the type parameters
                // position for position, and a handler's do not line up: the request argument of
                // IRequestHandler<Wrapped<T>, T> is Wrapped<T> and not T, so registering it
                // would resolve to nothing.
                //
                // Refusing it here was tried and reverted: an application that starts today
                // would stop starting, over a request that may never be sent.
                //
                // Skipped here, not unsupported. Set RegisterGenericHandlers and the closing is
                // done at registration instead, one concrete handler per candidate type, which is
                // what the other library does under the same flag. Without it, the send says
                // which request has no handler.
                if (candidate.IsAbstract || candidate.IsInterface || candidate.IsGenericTypeDefinition)
                {
                    continue;
                }

                foreach (Type contract in candidate.GetInterfaces())
                {
                    if (!contract.IsGenericType)
                    {
                        continue;
                    }

                    Type definition = contract.GetGenericTypeDefinition();

                    // Exception handlers and actions are picked up whatever else is asked for,
                    // which is what the other library does with them: there is no flag over
                    // there and there is none here.
                    if (Array.IndexOf(ExceptionContracts, definition) >= 0)
                    {
                        AddException(services, contract, definition, candidate, lifetime, exceptions);
                        continue;
                    }

                    if (Array.IndexOf(HandlerContracts, definition) < 0 &&
                        !(processors && Array.IndexOf(ProcessorContracts, definition) >= 0))
                    {
                        continue;
                    }

                    services.Add(new ServiceDescriptor(contract, candidate, lifetime));

                    // A handler that answers with nothing is also the Unit-shaped handler the
                    // pipeline is generic over. Adapting here rather than when the request is
                    // sent keeps the send path free of the question.
                    if (definition == typeof(IRequestHandler<>))
                    {
                        Type request = contract.GetGenericArguments()[0];

                        services.Add(new ServiceDescriptor(
                            typeof(IRequestHandler<,>).MakeGenericType(request, typeof(Unit)),
                            typeof(VoidHandlerAdapter<>).MakeGenericType(request),
                            lifetime));
                    }
                }
            }
        }
        /// <summary>What the scan found, so the behaviours that run it go on only if it did.</summary>
        private sealed class ExceptionRegistrations
        {
            internal bool Handlers { get; set; }

            internal bool Actions { get; set; }
        }

    }
}
