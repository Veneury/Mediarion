using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace Mediarion
{
    /// <summary>
    /// Closes open generic handlers over the types in the scanned assemblies and registers the
    /// results.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A container cannot do this by itself. It closes an open implementation against an open
    /// service type by matching type parameters position for position, and a handler's do not
    /// line up: the request argument of <c>IRequestHandler&lt;Wrapped&lt;T&gt;, T&gt;</c> is
    /// <c>Wrapped&lt;T&gt;</c> and not <c>T</c>. So the closing is done here instead, at
    /// registration, one concrete handler per candidate type.
    /// </para>
    /// <para>
    /// That is a cross product, which is why every limit below exists. The defaults are the other
    /// library's, so an application that turns this on gets the same registrations and the same
    /// refusals it got there.
    /// </para>
    /// <para>
    /// Off unless asked for. It can register a great many services, and the cost lands on every
    /// application that never wrote a generic handler.
    /// </para>
    /// </remarks>
    internal static class GenericHandlerRegistrar
    {
        private static readonly Type[] Contracts =
        {
            typeof(IRequestHandler<,>),
            typeof(IRequestHandler<>),
            typeof(INotificationHandler<>),
        };

        [RequiresUnreferencedCode("Handlers are found by walking the types in an assembly.")]
        [RequiresDynamicCode("Open generic handlers are closed over types found at run time.")]
        internal static void Add(
            IServiceCollection services,
            MediarionServiceConfiguration configuration)
        {
            List<Type> candidates = Candidates(configuration);
            var watch = Stopwatch.StartNew();
            var registered = 0;

            foreach (Assembly assembly in configuration.Assemblies)
            {
                foreach (Type definition in assembly.GetTypes())
                {
                    if (!definition.IsGenericTypeDefinition ||
                        definition.IsAbstract ||
                        definition.IsInterface ||
                        !configuration.TypeEvaluator(definition) ||
                        !Handles(definition))
                    {
                        continue;
                    }

                    Type[] parameters = definition.GetGenericArguments();

                    if (parameters.Length > configuration.MaxGenericTypeParameters)
                    {
                        continue;
                    }

                    registered += Close(services, configuration, definition, parameters, candidates, registered, watch);
                }
            }
        }

        [RequiresUnreferencedCode("A handler is recognised by the interfaces it declares.")]
        private static bool Handles(Type definition)
        {
            foreach (Type contract in definition.GetInterfaces())
            {
                if (contract.IsGenericType &&
                    Array.IndexOf(Contracts, contract.GetGenericTypeDefinition()) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        /// <remarks>
        /// What a type parameter may be closed over: the concrete, non-generic types of the
        /// assemblies being scanned. Types from elsewhere are not candidates, which is why
        /// <c>Wrapped&lt;int&gt;</c> finds no handler while <c>Wrapped&lt;Order&gt;</c> does.
        /// That is the other library's behaviour too, and it is the only way the set is finite.
        /// </remarks>
        [RequiresUnreferencedCode("Candidates are found by walking the types in an assembly.")]
        private static List<Type> Candidates(MediarionServiceConfiguration configuration)
        {
            var candidates = new List<Type>();

            foreach (Assembly assembly in configuration.Assemblies)
            {
                foreach (Type candidate in assembly.GetTypes())
                {
                    if (!candidate.IsAbstract &&
                        !candidate.IsInterface &&
                        !candidate.IsGenericTypeDefinition &&
                        !candidate.ContainsGenericParameters &&
                        configuration.TypeEvaluator(candidate) &&
                        candidates.Count < configuration.MaxTypesClosing)
                    {
                        candidates.Add(candidate);
                    }
                }
            }

            return candidates;
        }

        [RequiresUnreferencedCode("Handlers are found by walking the types in an assembly.")]
        [RequiresDynamicCode("Open generic handlers are closed over types found at run time.")]
        private static int Close(
            IServiceCollection services,
            MediarionServiceConfiguration configuration,
            Type definition,
            Type[] parameters,
            List<Type> candidates,
            int already,
            Stopwatch watch)
        {
            var added = 0;
            var arguments = new Type[parameters.Length];

            void Walk(int position)
            {
                if (already + added >= configuration.MaxGenericTypeRegistrations ||
                    watch.ElapsedMilliseconds > configuration.RegistrationTimeout)
                {
                    return;
                }

                if (position == parameters.Length)
                {
                    Type closed;

                    // Closing throws when the arguments break a constraint, which is the ordinary
                    // case rather than a fault: most candidates do not fit most handlers. The
                    // ones that do not are simply not registered.
                    try
                    {
                        closed = definition.MakeGenericType(arguments);
                    }
                    catch (ArgumentException)
                    {
                        return;
                    }

                    foreach (Type contract in closed.GetInterfaces())
                    {
                        if (contract.IsGenericType &&
                            Array.IndexOf(Contracts, contract.GetGenericTypeDefinition()) >= 0)
                        {
                            services.Add(new ServiceDescriptor(contract, closed, configuration.Lifetime));
                            added++;
                        }
                    }

                    return;
                }

                foreach (Type candidate in candidates)
                {
                    arguments[position] = candidate;
                    Walk(position + 1);
                }
            }

            Walk(0);

            return added;
        }
    }
}
