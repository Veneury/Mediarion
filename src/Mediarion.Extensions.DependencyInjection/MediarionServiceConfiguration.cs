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

        /// <summary>Gets the assemblies to scan for handlers.</summary>
        public IReadOnlyList<Assembly> Assemblies => assemblies;

        /// <summary>Gets the pipeline behaviours, outermost first.</summary>
        public IReadOnlyList<Type> Behaviours => behaviours;

        /// <summary>Gets or sets the lifetime handlers are registered with. Transient by default.</summary>
        public ServiceLifetime Lifetime { get; set; } = ServiceLifetime.Transient;

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
        public MediarionServiceConfiguration AddBehavior(Type behaviourType)
        {
            Guard.NotNull(behaviourType, nameof(behaviourType));
            behaviours.Add(behaviourType);
            return this;
        }

        /// <summary>Adds a pipeline behaviour.</summary>
        /// <typeparam name="TBehaviour">The behaviour type.</typeparam>
        /// <returns>This, to carry on configuring.</returns>
        public MediarionServiceConfiguration AddBehavior<TBehaviour>() =>
            AddBehavior(typeof(TBehaviour));

        /// <summary>Adds an open-generic pipeline behaviour that wraps every request.</summary>
        /// <param name="behaviourType">The open generic behaviour type.</param>
        /// <returns>This, to carry on configuring.</returns>
        public MediarionServiceConfiguration AddOpenBehavior(Type behaviourType) =>
            AddBehavior(behaviourType);
    }
}
