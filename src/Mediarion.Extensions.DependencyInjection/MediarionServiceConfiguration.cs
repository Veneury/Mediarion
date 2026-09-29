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
        private readonly List<Type> streamBehaviours = new List<Type>();
        private readonly List<Registration> streamBehaviourRegistrations = new List<Registration>();

        /// <summary>What to register for the pipeline, in the order it was asked for.</summary>
        internal IReadOnlyList<Registration> BehaviourRegistrations => behaviourRegistrations;

        /// <summary>What to register for the pre- and post-processors.</summary>
        internal IReadOnlyList<Registration> ProcessorRegistrations => processorRegistrations;

        /// <summary>
        /// What to register for the streaming pipeline. Read by <c>AddMediarionStreaming</c>,
        /// because the contract these are registered against lives in the streaming package and
        /// this one does not reference it.
        /// </summary>
        internal IReadOnlyList<Registration> StreamBehaviourRegistrations => streamBehaviourRegistrations;

        /// <summary>Gets the assemblies to scan for handlers.</summary>
        public IReadOnlyList<Assembly> Assemblies => assemblies;

        /// <summary>Gets the pipeline behaviours, outermost first.</summary>
        public IReadOnlyList<Type> Behaviours => behaviours;

        /// <summary>Gets the pre- and post-processors added by name.</summary>
        public IReadOnlyList<Type> Processors => processors;

        /// <summary>Gets the streaming pipeline behaviours, outermost first.</summary>
        public IReadOnlyList<Type> StreamBehaviours => streamBehaviours;

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

        /// <summary>
        /// Gets or sets whether open generic handlers are closed over the scanned types and
        /// registered. Off by default.
        /// </summary>
        /// <remarks>
        /// <para>
        /// A container cannot register an open generic handler: it closes an open implementation
        /// against an open service type by matching type parameters position for position, and a
        /// handler's do not line up. Turning this on does the closing at registration instead,
        /// one concrete handler per candidate type, which is what the other library's flag of the
        /// same name does.
        /// </para>
        /// <para>
        /// Candidates are the concrete types of the assemblies being scanned, so
        /// <c>Wrapped&lt;Order&gt;</c> finds a handler and <c>Wrapped&lt;int&gt;</c> does not.
        /// That is the other library's behaviour as well, and it is the only way the set is
        /// finite.
        /// </para>
        /// <para>
        /// Off by default because it is a cross product: it can register a great many services,
        /// and the cost would land on every application that never wrote a generic handler.
        /// </para>
        /// </remarks>
        public bool RegisterGenericHandlers { get; set; }

        /// <summary>
        /// Gets or sets how many type parameters an open generic handler may have before it is
        /// left alone. Ten by default.
        /// </summary>
        public int MaxGenericTypeParameters { get; set; } = 10;

        /// <summary>
        /// Gets or sets how many types an open generic handler may be closed over. A hundred by
        /// default.
        /// </summary>
        public int MaxTypesClosing { get; set; } = 100;

        /// <summary>
        /// Gets or sets how many closed handlers may be registered in total. 125,000 by default.
        /// </summary>
        public int MaxGenericTypeRegistrations { get; set; } = 125_000;

        /// <summary>
        /// Gets or sets how long, in milliseconds, closing generic handlers may take before it
        /// stops. Fifteen seconds by default.
        /// </summary>
        /// <remarks>
        /// A cross product over an assembly's types can take longer than anyone wants to wait at
        /// start-up. This is the wall rather than a promise: what has been registered when the
        /// time runs out stays registered.
        /// </remarks>
        public int RegistrationTimeout { get; set; } = 15_000;

        /// <summary>
        /// Gets or sets which types the scan is allowed to register. Everything, by default.
        /// </summary>
        /// <remarks>
        /// Returning false for a type leaves it out as though it were not in the assembly. What
        /// it is for is an assembly that holds more than one application's handlers, or a test
        /// that wants one handler and not its neighbour.
        /// </remarks>
        public Func<Type, bool> TypeEvaluator { get; set; } = _ => true;

        /// <summary>
        /// Gets or sets the mediator to register. The one this library ships, when left alone.
        /// </summary>
        /// <remarks>
        /// It has to implement <see cref="IMediator"/>. Deriving from the one already here is the
        /// usual way, since that keeps the pipeline; the streaming package does exactly that.
        /// Null rather than <c>typeof(Mediator)</c> as the default, so that reading the property
        /// does not drag the mediator's constructors into a trimmed application that named its
        /// own.
        /// </remarks>
        public Type? MediatorImplementationType { get; set; }

        /// <summary>
        /// Gets or sets the notification publisher to register by type rather than by instance.
        /// </summary>
        /// <remarks>
        /// <see cref="NotificationPublisher"/> takes an instance you built. This takes a type the
        /// container builds, which is what a publisher that needs something injected has to have.
        /// When both are set, the instance wins, because an instance cannot have been given by
        /// accident.
        /// </remarks>
        public Type? NotificationPublisherType { get; set; }

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

        /// <summary>Adds a streaming pipeline behaviour, which may be an open generic.</summary>
        /// <param name="behaviourType">The behaviour type.</param>
        /// <returns>This, to carry on configuring.</returns>
        /// <remarks>
        /// Nothing happens until <c>AddMediarionStreaming</c> is called, which is where the
        /// streaming contracts live. Calling this and not that registers nothing, the same way
        /// a stream handler in the assembly is not registered until then.
        /// </remarks>
        public MediarionServiceConfiguration AddStreamBehavior(Type behaviourType) =>
            AddStreamBehavior(behaviourType, ServiceLifetime.Transient);

        /// <summary>Adds a streaming pipeline behaviour with a lifetime of its own.</summary>
        /// <param name="behaviourType">The behaviour type, which may be an open generic.</param>
        /// <param name="lifetime">How long an instance lives.</param>
        /// <returns>This, to carry on configuring.</returns>
        public MediarionServiceConfiguration AddStreamBehavior(Type behaviourType, ServiceLifetime lifetime)
        {
            Guard.NotNull(behaviourType, nameof(behaviourType));
            streamBehaviours.Add(behaviourType);
            streamBehaviourRegistrations.Add(new Registration(null, behaviourType, lifetime));
            return this;
        }

        /// <summary>Adds a streaming pipeline behaviour against the one contract named.</summary>
        /// <param name="serviceType">The contract to register it against.</param>
        /// <param name="implementationType">The behaviour type.</param>
        /// <returns>This, to carry on configuring.</returns>
        public MediarionServiceConfiguration AddStreamBehavior(Type serviceType, Type implementationType) =>
            AddStreamBehavior(serviceType, implementationType, ServiceLifetime.Transient);

        /// <summary>Adds a streaming pipeline behaviour against the one contract named, with a lifetime.</summary>
        /// <param name="serviceType">The contract to register it against.</param>
        /// <param name="implementationType">The behaviour type.</param>
        /// <param name="lifetime">How long an instance lives.</param>
        /// <returns>This, to carry on configuring.</returns>
        public MediarionServiceConfiguration AddStreamBehavior(
            Type serviceType,
            Type implementationType,
            ServiceLifetime lifetime)
        {
            Guard.NotNull(serviceType, nameof(serviceType));
            Guard.NotNull(implementationType, nameof(implementationType));
            streamBehaviours.Add(implementationType);
            streamBehaviourRegistrations.Add(new Registration(serviceType, implementationType, lifetime));
            return this;
        }

        /// <summary>Adds a streaming pipeline behaviour.</summary>
        /// <typeparam name="TBehaviour">The behaviour type.</typeparam>
        /// <returns>This, to carry on configuring.</returns>
        public MediarionServiceConfiguration AddStreamBehavior<TBehaviour>() =>
            AddStreamBehavior(typeof(TBehaviour));

        /// <summary>Adds a streaming pipeline behaviour with a lifetime of its own.</summary>
        /// <typeparam name="TBehaviour">The behaviour type.</typeparam>
        /// <param name="lifetime">How long an instance lives.</param>
        /// <returns>This, to carry on configuring.</returns>
        public MediarionServiceConfiguration AddStreamBehavior<TBehaviour>(ServiceLifetime lifetime) =>
            AddStreamBehavior(typeof(TBehaviour), lifetime);

        /// <summary>Adds a streaming pipeline behaviour against the one contract named.</summary>
        /// <typeparam name="TService">The contract to register it against.</typeparam>
        /// <typeparam name="TImplementation">The behaviour type.</typeparam>
        /// <returns>This, to carry on configuring.</returns>
        public MediarionServiceConfiguration AddStreamBehavior<TService, TImplementation>() =>
            AddStreamBehavior(typeof(TService), typeof(TImplementation));

        /// <summary>Adds a streaming pipeline behaviour against the one contract named, with a lifetime.</summary>
        /// <typeparam name="TService">The contract to register it against.</typeparam>
        /// <typeparam name="TImplementation">The behaviour type.</typeparam>
        /// <param name="lifetime">How long an instance lives.</param>
        /// <returns>This, to carry on configuring.</returns>
        public MediarionServiceConfiguration AddStreamBehavior<TService, TImplementation>(ServiceLifetime lifetime) =>
            AddStreamBehavior(typeof(TService), typeof(TImplementation), lifetime);

        /// <summary>Adds an open-generic streaming pipeline behaviour.</summary>
        /// <param name="behaviourType">The open generic behaviour type.</param>
        /// <returns>This, to carry on configuring.</returns>
        public MediarionServiceConfiguration AddOpenStreamBehavior(Type behaviourType) =>
            AddStreamBehavior(behaviourType);

        /// <summary>Adds an open-generic streaming pipeline behaviour with a lifetime of its own.</summary>
        /// <param name="behaviourType">The open generic behaviour type.</param>
        /// <param name="lifetime">How long an instance lives.</param>
        /// <returns>This, to carry on configuring.</returns>
        public MediarionServiceConfiguration AddOpenStreamBehavior(Type behaviourType, ServiceLifetime lifetime) =>
            AddStreamBehavior(behaviourType, lifetime);
    }
}
