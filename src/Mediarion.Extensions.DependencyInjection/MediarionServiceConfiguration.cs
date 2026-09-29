using System;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace Mediarion
{
    /// <summary>
    /// What to register, and how.
    /// </summary>
    public sealed class MediarionServiceConfiguration
    {
        private readonly List<Assembly> assemblies = new List<Assembly>();
        private readonly List<Type> behaviours = new List<Type>();
        private readonly List<Type> processors = new List<Type>();
        private readonly List<Registration> behaviourRegistrations = new List<Registration>();
        private readonly List<Registration> processorRegistrations = new List<Registration>();

        /// <summary>What to register for the pipeline, in the order it was asked for.</summary>
        internal IReadOnlyList<Registration> BehaviourRegistrations => behaviourRegistrations;

        /// <summary>What to register for the pre- and post-processors.</summary>
        internal IReadOnlyList<Registration> ProcessorRegistrations => processorRegistrations;

        /// <summary>Gets the assemblies to scan for handlers.</summary>
        public IReadOnlyList<Assembly> Assemblies => assemblies;

        /// <summary>Gets the pipeline behaviours, outermost first.</summary>
        public IReadOnlyList<Type> Behaviours => behaviours;

        /// <summary>Gets the pre- and post-processors added by name.</summary>
        public IReadOnlyList<Type> Processors => processors;

        /// <summary>
        /// Gets or sets whether scanning an assembly also picks up its pre- and post-processors.
        /// Off by default.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Off, because that is what the library this one is a drop-in for does. Turning it on
        /// by default would mean a codebase moved over starts running processors that were
        /// sitting in the assembly doing nothing, which is a behaviour change nobody asked for.
        /// </para>
        /// <para>
        /// Turning it on does run them, which is the one place this deliberately parts company
        /// with the other library: there, the flag registers the processors and nothing ever
        /// calls them, because the behaviours that do are only added when a processor was named
        /// one at a time. Nobody can be relying on a flag that does nothing, and doing what it
        /// says cannot break a migration that worked.
        /// </para>
        /// </remarks>
        public bool AutoRegisterRequestProcessors { get; set; }

        /// <summary>Gets or sets the lifetime handlers are registered with. Transient by default.</summary>
        public ServiceLifetime Lifetime { get; set; } = ServiceLifetime.Transient;

        /// <summary>Gets or sets when the exception actions run. Only for an exception no
        /// handler dealt with, by default.</summary>
        /// <remarks>
        /// The default is the other library's default, and the two disagreed until this existed:
        /// an action here ran even when a handler had already answered in the exception's place,
        /// which is that library's <see cref="Mediarion.RequestExceptionActionProcessorStrategy.ApplyForAllExceptions"/>
        /// and not its default. A migration would have started recording failures it used to pass
        /// over, silently and with nothing failing to compile.
        /// </remarks>
        public RequestExceptionActionProcessorStrategy RequestExceptionActionProcessorStrategy { get; set; }
            = RequestExceptionActionProcessorStrategy.ApplyForUnhandledExceptions;

        /// <summary>Gets or sets how the handlers of one notification are run.</summary>
        /// <remarks>
        /// One after another unless this says otherwise. Set it to a
        /// <c>TaskWhenAllPublisher</c> to run them together.
        /// </remarks>
        public INotificationPublisher? NotificationPublisher { get; set; }

        /// <summary>Scans an assembly for handlers.</summary>
        /// <param name="assembly">The assembly to scan.</param>
        /// <returns>This, to carry on configuring.</returns>
        public MediarionServiceConfiguration RegisterServicesFromAssembly(Assembly assembly)
        {
            Guard.NotNull(assembly, nameof(assembly));
            assemblies.Add(assembly);
            return this;
        }

        /// <summary>Scans the assembly a type lives in.</summary>
        /// <param name="type">Any type from the assembly to scan.</param>
        /// <returns>This, to carry on configuring.</returns>
        public MediarionServiceConfiguration RegisterServicesFromAssemblyContaining(Type type)
        {
            Guard.NotNull(type, nameof(type));
            return RegisterServicesFromAssembly(type.Assembly);
        }

        /// <summary>Scans the assembly a type lives in.</summary>
        /// <typeparam name="T">Any type from the assembly to scan.</typeparam>
        /// <returns>This, to carry on configuring.</returns>
        public MediarionServiceConfiguration RegisterServicesFromAssemblyContaining<T>() =>
            RegisterServicesFromAssembly(typeof(T).Assembly);

        /// <summary>Scans several assemblies for handlers.</summary>
        /// <param name="assemblies">The assemblies to scan.</param>
        /// <returns>This, to carry on configuring.</returns>
        public MediarionServiceConfiguration RegisterServicesFromAssemblies(params Assembly[] assemblies)
        {
            Guard.NotNull(assemblies, nameof(assemblies));

            foreach (Assembly assembly in assemblies)
            {
                RegisterServicesFromAssembly(assembly);
            }

            return this;
        }

        /// <summary>Adds a pipeline behaviour, which may be an open generic.</summary>
        /// <param name="behaviourType">The behaviour type.</param>
        /// <returns>This, to carry on configuring.</returns>
        /// <remarks>
        /// Behaviours run in the order they are added, outermost first.
        /// </remarks>
        public MediarionServiceConfiguration AddBehavior(Type behaviourType) =>
            AddBehavior(behaviourType, ServiceLifetime.Transient);

        /// <summary>Adds a pipeline behaviour.</summary>
        /// <typeparam name="TBehaviour">The behaviour type.</typeparam>
        /// <returns>This, to carry on configuring.</returns>
        public MediarionServiceConfiguration AddBehavior<TBehaviour>() =>
            AddBehavior(typeof(TBehaviour));

        /// <summary>Adds a pre-processor, which may be an open generic.</summary>
        /// <param name="processorType">The processor type.</param>
        /// <returns>This, to carry on configuring.</returns>
        public MediarionServiceConfiguration AddRequestPreProcessor(Type processorType) =>
            AddRequestPreProcessor(processorType, ServiceLifetime.Transient);

        /// <summary>Adds a pre-processor.</summary>
        /// <typeparam name="TProcessor">The processor type.</typeparam>
        /// <returns>This, to carry on configuring.</returns>
        public MediarionServiceConfiguration AddRequestPreProcessor<TProcessor>() =>
            AddRequestPreProcessor(typeof(TProcessor));

        /// <summary>Adds a post-processor, which may be an open generic.</summary>
        /// <param name="processorType">The processor type.</param>
        /// <returns>This, to carry on configuring.</returns>
        public MediarionServiceConfiguration AddRequestPostProcessor(Type processorType) =>
            AddRequestPostProcessor(processorType, ServiceLifetime.Transient);

        /// <summary>Adds a post-processor.</summary>
        /// <typeparam name="TProcessor">The processor type.</typeparam>
        /// <returns>This, to carry on configuring.</returns>
        public MediarionServiceConfiguration AddRequestPostProcessor<TProcessor>() =>
            AddRequestPostProcessor(typeof(TProcessor));

        /// <summary>Adds an open-generic pipeline behaviour that wraps every request.</summary>
        /// <param name="behaviourType">The open generic behaviour type.</param>
        /// <returns>This, to carry on configuring.</returns>
        public MediarionServiceConfiguration AddOpenBehavior(Type behaviourType) =>
            AddBehavior(behaviourType);

        /// <summary>Adds a pipeline behaviour with a lifetime of its own.</summary>
        /// <param name="behaviourType">The behaviour type, which may be an open generic.</param>
        /// <param name="lifetime">How long an instance lives.</param>
        /// <returns>This, to carry on configuring.</returns>
        public MediarionServiceConfiguration AddBehavior(Type behaviourType, ServiceLifetime lifetime)
        {
            Guard.NotNull(behaviourType, nameof(behaviourType));
            behaviours.Add(behaviourType);
            behaviourRegistrations.Add(new Registration(null, behaviourType, lifetime));
            return this;
        }

        /// <summary>Adds a pipeline behaviour against the one contract named.</summary>
        /// <param name="serviceType">The contract to register it against.</param>
        /// <param name="implementationType">The behaviour type.</param>
        /// <returns>This, to carry on configuring.</returns>
        /// <remarks>
        /// Without this, a behaviour is registered against every <c>IPipelineBehavior</c> it
        /// implements. Naming the contract is how one that implements several is put in front of
        /// one request and not the rest.
        /// </remarks>
        public MediarionServiceConfiguration AddBehavior(Type serviceType, Type implementationType) =>
            AddBehavior(serviceType, implementationType, ServiceLifetime.Transient);

        /// <summary>Adds a pipeline behaviour against the one contract named, with a lifetime.</summary>
        /// <param name="serviceType">The contract to register it against.</param>
        /// <param name="implementationType">The behaviour type.</param>
        /// <param name="lifetime">How long an instance lives.</param>
        /// <returns>This, to carry on configuring.</returns>
        public MediarionServiceConfiguration AddBehavior(
            Type serviceType,
            Type implementationType,
            ServiceLifetime lifetime)
        {
            Guard.NotNull(serviceType, nameof(serviceType));
            Guard.NotNull(implementationType, nameof(implementationType));
            behaviours.Add(implementationType);
            behaviourRegistrations.Add(new Registration(serviceType, implementationType, lifetime));
            return this;
        }

        /// <summary>Adds a pipeline behaviour with a lifetime of its own.</summary>
        /// <typeparam name="TBehaviour">The behaviour type.</typeparam>
        /// <param name="lifetime">How long an instance lives.</param>
        /// <returns>This, to carry on configuring.</returns>
        public MediarionServiceConfiguration AddBehavior<TBehaviour>(ServiceLifetime lifetime) =>
            AddBehavior(typeof(TBehaviour), lifetime);

        /// <summary>Adds a pipeline behaviour against the one contract named.</summary>
        /// <typeparam name="TService">The contract to register it against.</typeparam>
        /// <typeparam name="TImplementation">The behaviour type.</typeparam>
        /// <returns>This, to carry on configuring.</returns>
        public MediarionServiceConfiguration AddBehavior<TService, TImplementation>() =>
            AddBehavior(typeof(TService), typeof(TImplementation));

        /// <summary>Adds a pipeline behaviour against the one contract named, with a lifetime.</summary>
        /// <typeparam name="TService">The contract to register it against.</typeparam>
        /// <typeparam name="TImplementation">The behaviour type.</typeparam>
        /// <param name="lifetime">How long an instance lives.</param>
        /// <returns>This, to carry on configuring.</returns>
        public MediarionServiceConfiguration AddBehavior<TService, TImplementation>(ServiceLifetime lifetime) =>
            AddBehavior(typeof(TService), typeof(TImplementation), lifetime);

        /// <summary>Adds an open-generic pipeline behaviour with a lifetime of its own.</summary>
        /// <param name="behaviourType">The open generic behaviour type.</param>
        /// <param name="lifetime">How long an instance lives.</param>
        /// <returns>This, to carry on configuring.</returns>
        public MediarionServiceConfiguration AddOpenBehavior(Type behaviourType, ServiceLifetime lifetime) =>
            AddBehavior(behaviourType, lifetime);

        /// <summary>Adds several open-generic pipeline behaviours, in the order given.</summary>
        /// <param name="behaviourTypes">The open generic behaviour types.</param>
        /// <returns>This, to carry on configuring.</returns>
        public MediarionServiceConfiguration AddOpenBehaviors(IEnumerable<Type> behaviourTypes) =>
            AddOpenBehaviors(behaviourTypes, ServiceLifetime.Transient);

        /// <summary>Adds several open-generic pipeline behaviours, in the order given.</summary>
        /// <param name="behaviourTypes">The open generic behaviour types.</param>
        /// <param name="lifetime">How long an instance lives.</param>
        /// <returns>This, to carry on configuring.</returns>
        public MediarionServiceConfiguration AddOpenBehaviors(
            IEnumerable<Type> behaviourTypes,
            ServiceLifetime lifetime)
        {
            Guard.NotNull(behaviourTypes, nameof(behaviourTypes));

            foreach (Type behaviourType in behaviourTypes)
            {
                AddBehavior(behaviourType, lifetime);
            }

            return this;
        }

        /// <summary>Adds a pre-processor with a lifetime of its own.</summary>
        /// <param name="processorType">The processor type, which may be an open generic.</param>
        /// <param name="lifetime">How long an instance lives.</param>
        /// <returns>This, to carry on configuring.</returns>
        public MediarionServiceConfiguration AddRequestPreProcessor(Type processorType, ServiceLifetime lifetime)
        {
            Guard.NotNull(processorType, nameof(processorType));
            processors.Add(processorType);
            processorRegistrations.Add(new Registration(null, processorType, lifetime));
            return this;
        }

        /// <summary>Adds a pre-processor against the one contract named.</summary>
        /// <param name="serviceType">The contract to register it against.</param>
        /// <param name="implementationType">The processor type.</param>
        /// <returns>This, to carry on configuring.</returns>
        public MediarionServiceConfiguration AddRequestPreProcessor(Type serviceType, Type implementationType) =>
            AddRequestPreProcessor(serviceType, implementationType, ServiceLifetime.Transient);

        /// <summary>Adds a pre-processor against the one contract named, with a lifetime.</summary>
        /// <param name="serviceType">The contract to register it against.</param>
        /// <param name="implementationType">The processor type.</param>
        /// <param name="lifetime">How long an instance lives.</param>
        /// <returns>This, to carry on configuring.</returns>
        public MediarionServiceConfiguration AddRequestPreProcessor(
            Type serviceType,
            Type implementationType,
            ServiceLifetime lifetime)
        {
            Guard.NotNull(serviceType, nameof(serviceType));
            Guard.NotNull(implementationType, nameof(implementationType));
            processors.Add(implementationType);
            processorRegistrations.Add(new Registration(serviceType, implementationType, lifetime));
            return this;
        }

        /// <summary>Adds a pre-processor with a lifetime of its own.</summary>
        /// <typeparam name="TProcessor">The processor type.</typeparam>
        /// <param name="lifetime">How long an instance lives.</param>
        /// <returns>This, to carry on configuring.</returns>
        public MediarionServiceConfiguration AddRequestPreProcessor<TProcessor>(ServiceLifetime lifetime) =>
            AddRequestPreProcessor(typeof(TProcessor), lifetime);

        /// <summary>Adds a pre-processor against the one contract named.</summary>
        /// <typeparam name="TService">The contract to register it against.</typeparam>
        /// <typeparam name="TImplementation">The processor type.</typeparam>
        /// <returns>This, to carry on configuring.</returns>
        public MediarionServiceConfiguration AddRequestPreProcessor<TService, TImplementation>() =>
            AddRequestPreProcessor(typeof(TService), typeof(TImplementation));

        /// <summary>Adds a pre-processor against the one contract named, with a lifetime.</summary>
        /// <typeparam name="TService">The contract to register it against.</typeparam>
        /// <typeparam name="TImplementation">The processor type.</typeparam>
        /// <param name="lifetime">How long an instance lives.</param>
        /// <returns>This, to carry on configuring.</returns>
        public MediarionServiceConfiguration AddRequestPreProcessor<TService, TImplementation>(ServiceLifetime lifetime) =>
            AddRequestPreProcessor(typeof(TService), typeof(TImplementation), lifetime);

        /// <summary>Adds an open-generic pre-processor.</summary>
        /// <param name="processorType">The open generic processor type.</param>
        /// <returns>This, to carry on configuring.</returns>
        public MediarionServiceConfiguration AddOpenRequestPreProcessor(Type processorType) =>
            AddRequestPreProcessor(processorType);

        /// <summary>Adds an open-generic pre-processor with a lifetime of its own.</summary>
        /// <param name="processorType">The open generic processor type.</param>
        /// <param name="lifetime">How long an instance lives.</param>
        /// <returns>This, to carry on configuring.</returns>
        public MediarionServiceConfiguration AddOpenRequestPreProcessor(Type processorType, ServiceLifetime lifetime) =>
            AddRequestPreProcessor(processorType, lifetime);

        /// <summary>Adds a post-processor with a lifetime of its own.</summary>
        /// <param name="processorType">The processor type, which may be an open generic.</param>
        /// <param name="lifetime">How long an instance lives.</param>
        /// <returns>This, to carry on configuring.</returns>
        public MediarionServiceConfiguration AddRequestPostProcessor(Type processorType, ServiceLifetime lifetime)
        {
            Guard.NotNull(processorType, nameof(processorType));
            processors.Add(processorType);
            processorRegistrations.Add(new Registration(null, processorType, lifetime));
            return this;
        }

        /// <summary>Adds a post-processor against the one contract named.</summary>
        /// <param name="serviceType">The contract to register it against.</param>
        /// <param name="implementationType">The processor type.</param>
        /// <returns>This, to carry on configuring.</returns>
        public MediarionServiceConfiguration AddRequestPostProcessor(Type serviceType, Type implementationType) =>
            AddRequestPostProcessor(serviceType, implementationType, ServiceLifetime.Transient);

        /// <summary>Adds a post-processor against the one contract named, with a lifetime.</summary>
        /// <param name="serviceType">The contract to register it against.</param>
        /// <param name="implementationType">The processor type.</param>
        /// <param name="lifetime">How long an instance lives.</param>
        /// <returns>This, to carry on configuring.</returns>
        public MediarionServiceConfiguration AddRequestPostProcessor(
            Type serviceType,
            Type implementationType,
            ServiceLifetime lifetime)
        {
            Guard.NotNull(serviceType, nameof(serviceType));
            Guard.NotNull(implementationType, nameof(implementationType));
            processors.Add(implementationType);
            processorRegistrations.Add(new Registration(serviceType, implementationType, lifetime));
            return this;
        }

        /// <summary>Adds a post-processor with a lifetime of its own.</summary>
        /// <typeparam name="TProcessor">The processor type.</typeparam>
        /// <param name="lifetime">How long an instance lives.</param>
        /// <returns>This, to carry on configuring.</returns>
        public MediarionServiceConfiguration AddRequestPostProcessor<TProcessor>(ServiceLifetime lifetime) =>
            AddRequestPostProcessor(typeof(TProcessor), lifetime);

        /// <summary>Adds a post-processor against the one contract named.</summary>
        /// <typeparam name="TService">The contract to register it against.</typeparam>
        /// <typeparam name="TImplementation">The processor type.</typeparam>
        /// <returns>This, to carry on configuring.</returns>
        public MediarionServiceConfiguration AddRequestPostProcessor<TService, TImplementation>() =>
            AddRequestPostProcessor(typeof(TService), typeof(TImplementation));

        /// <summary>Adds a post-processor against the one contract named, with a lifetime.</summary>
        /// <typeparam name="TService">The contract to register it against.</typeparam>
        /// <typeparam name="TImplementation">The processor type.</typeparam>
        /// <param name="lifetime">How long an instance lives.</param>
        /// <returns>This, to carry on configuring.</returns>
        public MediarionServiceConfiguration AddRequestPostProcessor<TService, TImplementation>(ServiceLifetime lifetime) =>
            AddRequestPostProcessor(typeof(TService), typeof(TImplementation), lifetime);

        /// <summary>Adds an open-generic post-processor.</summary>
        /// <param name="processorType">The open generic processor type.</param>
        /// <returns>This, to carry on configuring.</returns>
        public MediarionServiceConfiguration AddOpenRequestPostProcessor(Type processorType) =>
            AddRequestPostProcessor(processorType);

        /// <summary>Adds an open-generic post-processor with a lifetime of its own.</summary>
        /// <param name="processorType">The open generic processor type.</param>
        /// <param name="lifetime">How long an instance lives.</param>
        /// <returns>This, to carry on configuring.</returns>
        public MediarionServiceConfiguration AddOpenRequestPostProcessor(Type processorType, ServiceLifetime lifetime) =>
            AddRequestPostProcessor(processorType, lifetime);
    }
}
