using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Mediarion.SourceGeneration
{
    /// <summary>
    /// Writes the body of a class marked <c>[GeneratedMediator]</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The run-time mediator is handed an <c>IRequest&lt;TResponse&gt;</c> and has to find the
    /// handler for whatever the request turns out to be, which means closing a generic over a
    /// type it learns then. An application published ahead of time cannot do that.
    /// </para>
    /// <para>
    /// Every request and every handler is visible while the project compiles, so the same
    /// question is answered here instead and what comes out is a switch. Nothing is reflected
    /// over and nothing is closed at run time; the pipeline itself is the same code the run-time
    /// mediator runs, which is what keeps the two from disagreeing about the order behaviours go
    /// in.
    /// </para>
    /// </remarks>
    [Generator(LanguageNames.CSharp)]
    public sealed class MediatorGenerator : IIncrementalGenerator
    {
        private const string MediatorAttribute = "Mediarion.GeneratedMediatorAttribute";

        /// <inheritdoc />
        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            IncrementalValuesProvider<Marked> mediators = context.SyntaxProvider
                .ForAttributeWithMetadataName(
                    MediatorAttribute,
                    static (node, _) => node is ClassDeclarationSyntax,
                    static (target, _) => new Marked(
                        (INamedTypeSymbol)target.TargetSymbol,
                        Wants(target.Attributes)));

            IncrementalValuesProvider<INamedTypeSymbol> candidates = context.SyntaxProvider
                .CreateSyntaxProvider(
                    static (node, _) => node is TypeDeclarationSyntax { BaseList: not null },
                    static (target, token) => target.SemanticModel.GetDeclaredSymbol(target.Node, token) as INamedTypeSymbol)
                .Where(static symbol => symbol is not null)!;

            context.RegisterSourceOutput(
                mediators.Collect().Combine(candidates.Collect()).Combine(context.CompilationProvider),
                static (production, input) =>
                    Emit(production, input.Left.Left, input.Left.Right!, input.Right));
        }

        private static void Emit(
            SourceProductionContext context,
            ImmutableArray<Marked> mediators,
            ImmutableArray<INamedTypeSymbol> candidates,
            Compilation compilation)
        {
            if (mediators.Length == 0)
            {
                return;
            }

            Registry registry = Registry.From(context, candidates);

            foreach (Marked marked in mediators)
            {
                INamedTypeSymbol mediator = marked.Type;

                if (!IsPartial(mediator))
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        GeneratorDiagnostics.MediatorMustBePartial,
                        mediator.Locations.FirstOrDefault(),
                        mediator.Name));

                    continue;
                }

                // The registration is only written where there is a container to write it for.
                // A project that wires its handlers some other way still gets the dispatch.
                bool container = compilation.GetTypeByMetadataName(
                    "Microsoft.Extensions.DependencyInjection.IServiceCollection") is not null;

                context.AddSource(
                    mediator.ToDisplayString().Replace('<', '_').Replace('>', '_') + ".g.cs",
                    SourceText.From(
                        new MediatorEmitter(registry).Write(mediator, container, marked.RegisterProcessors),
                        Encoding.UTF8));
            }
        }

        private static bool Wants(ImmutableArray<AttributeData> attributes)
        {
            foreach (AttributeData attribute in attributes)
            {
                foreach (System.Collections.Generic.KeyValuePair<string, TypedConstant> named in attribute.NamedArguments)
                {
                    if (named.Key == "RegisterRequestProcessors" && named.Value.Value is bool wanted)
                    {
                        return wanted;
                    }
                }
            }

            return false;
        }

        private static bool IsPartial(INamedTypeSymbol mediator)
        {
            foreach (SyntaxReference reference in mediator.DeclaringSyntaxReferences)
            {
                if (reference.GetSyntax() is ClassDeclarationSyntax declaration &&
                    declaration.Modifiers.Any(modifier => modifier.ValueText == "partial"))
                {
                    return true;
                }
            }

            return false;
        }
    }

    /// <summary>One class marked for generation, and what it asked for.</summary>
    internal readonly struct Marked
    {
        internal Marked(INamedTypeSymbol type, bool registerProcessors)
        {
            Type = type;
            RegisterProcessors = registerProcessors;
        }

        internal INamedTypeSymbol Type { get; }

        internal bool RegisterProcessors { get; }
    }

    /// <summary>Every handler, processor and notification the compilation holds.</summary>
    internal sealed class Registry
    {
        private Registry(
            List<RequestPair> requests,
            List<INamedTypeSymbol> notifications,
            List<Binding> notificationHandlers,
            List<Binding> processors)
        {
            Requests = requests;
            Notifications = notifications;
            NotificationHandlers = notificationHandlers;
            Processors = processors;
        }

        internal List<RequestPair> Requests { get; }

        /// <summary>The notification types, once each, for the dispatch to switch over.</summary>
        internal List<INamedTypeSymbol> Notifications { get; }

        /// <summary>Every notification handler, for the registration to name.</summary>
        internal List<Binding> NotificationHandlers { get; }

        /// <summary>Every pre- and post-processor, for the registration to name.</summary>
        internal List<Binding> Processors { get; }

        internal static Registry From(SourceProductionContext context, ImmutableArray<INamedTypeSymbol> candidates)
        {
            var requests = new Dictionary<string, RequestPair>(System.StringComparer.Ordinal);
            var notifications = new Dictionary<string, INamedTypeSymbol>(System.StringComparer.Ordinal);
            var notificationHandlers = new List<Binding>();
            var processors = new List<Binding>();

            foreach (INamedTypeSymbol candidate in candidates)
            {
                if (candidate.IsAbstract || candidate.TypeKind == TypeKind.Interface ||
                    candidate.IsGenericType)
                {
                    continue;
                }

                foreach (INamedTypeSymbol contract in candidate.AllInterfaces)
                {
                    if (!contract.IsGenericType || contract.ContainingNamespace?.ToDisplayString() is not
                        ("Mediarion" or "Mediarion.Pipeline"))
                    {
                        continue;
                    }

                    switch (contract.Name)
                    {
                        case "IRequestHandler" when contract.TypeArguments.Length == 2:
                            Remember(context, requests, candidate, contract.TypeArguments[0], contract.TypeArguments[1]);
                            break;

                        case "IRequestHandler" when contract.TypeArguments.Length == 1:
                            Remember(context, requests, candidate, contract.TypeArguments[0], null);
                            break;

                        case "INotificationHandler" when contract.TypeArguments.Length == 1:
                            Keep(notifications, contract.TypeArguments[0]);
                            notificationHandlers.Add(new Binding(contract, candidate));
                            break;

                        case "IRequestPreProcessor":
                        case "IRequestPostProcessor":
                            processors.Add(new Binding(contract, candidate));
                            break;
                    }
                }
            }

            var ordered = new List<RequestPair>(requests.Values);
            ordered.Sort(static (left, right) => string.CompareOrdinal(left.Key, right.Key));

            var kept = new List<INamedTypeSymbol>(notifications.Values);
            kept.Sort(static (left, right) => string.CompareOrdinal(Names.Full(left), Names.Full(right)));

            notificationHandlers.Sort(Binding.ByName);
            processors.Sort(Binding.ByName);

            return new Registry(ordered, kept, notificationHandlers, processors);
        }

        private static void Remember(
            SourceProductionContext context,
            Dictionary<string, RequestPair> requests,
            INamedTypeSymbol handler,
            ITypeSymbol request,
            ITypeSymbol? response)
        {
            if (request is not INamedTypeSymbol named || named.IsUnboundGenericType)
            {
                return;
            }

            string key = Names.Full(named);

            if (requests.TryGetValue(key, out RequestPair existing))
            {
                if (!SymbolEqualityComparer.Default.Equals(existing.Handler, handler))
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        GeneratorDiagnostics.TwoHandlers,
                        handler.Locations.FirstOrDefault(),
                        named.Name,
                        existing.Handler.Name,
                        handler.Name));
                }

                return;
            }

            requests.Add(key, new RequestPair(named, response, handler, key));
        }

        private static void Keep(Dictionary<string, INamedTypeSymbol> notifications, ITypeSymbol notification)
        {
            if (notification is INamedTypeSymbol named && !named.IsUnboundGenericType)
            {
                string key = Names.Full(named);

                if (!notifications.ContainsKey(key))
                {
                    notifications.Add(key, named);
                }
            }
        }
    }

    /// <summary>A contract and the type that implements it.</summary>
    internal readonly struct Binding
    {
        internal Binding(INamedTypeSymbol contract, INamedTypeSymbol implementation)
        {
            Contract = contract;
            Implementation = implementation;
        }

        internal INamedTypeSymbol Contract { get; }

        internal INamedTypeSymbol Implementation { get; }

        internal static int ByName(Binding left, Binding right)
        {
            int byContract = string.CompareOrdinal(Names.Full(left.Contract), Names.Full(right.Contract));

            return byContract != 0
                ? byContract
                : string.CompareOrdinal(Names.Full(left.Implementation), Names.Full(right.Implementation));
        }
    }

    /// <summary>One request, what it answers with, and the handler that answers it.</summary>
    internal readonly struct RequestPair
    {
        internal RequestPair(INamedTypeSymbol request, ITypeSymbol? response, INamedTypeSymbol handler, string key)
        {
            Request = request;
            Response = response;
            Handler = handler;
            Key = key;
        }

        internal INamedTypeSymbol Request { get; }

        /// <summary>Null when the handler answers with nothing, which is <c>Unit</c> in the pipeline.</summary>
        internal ITypeSymbol? Response { get; }

        internal INamedTypeSymbol Handler { get; }

        internal string Key { get; }
    }

    internal static class Names
    {
        internal static string Full(ITypeSymbol type) =>
            type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
    }
}
