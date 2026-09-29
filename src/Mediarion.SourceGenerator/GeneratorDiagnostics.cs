using Microsoft.CodeAnalysis;

namespace Mediarion.SourceGeneration
{
    /// <summary>
    /// What the generator can tell the user while the project compiles.
    /// </summary>
    internal static class GeneratorDiagnostics
    {
        private const string Category = "Mediarion";

        internal static readonly DiagnosticDescriptor MediatorMustBePartial = new DiagnosticDescriptor(
            "MDR0001",
            "Mediator class must be partial",
            "'{0}' is marked with [GeneratedMediator] but is not partial, so the dispatch has nowhere to go",
            Category,
            DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        internal static readonly DiagnosticDescriptor TwoHandlers = new DiagnosticDescriptor(
            "MDR0002",
            "Two handlers answer one request",
            "'{0}' is answered by both '{1}' and '{2}'. A request has one handler; use a notification for the other one.",
            Category,
            DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        /// <remarks>
        /// A warning and not an error, which it was until the other library was asked what it
        /// does with one. It closes them at registration under RegisterGenericHandlers, so an
        /// error here would have refused to compile code that works over there — the opposite of
        /// what this library is for. The generator still cannot write a dispatch for one, so it
        /// says so.
        /// </remarks>
        internal static readonly DiagnosticDescriptor OpenGenericHandler = new DiagnosticDescriptor(
            "MDR0004",
            "Open generic handler is not written into the generated dispatch",
            "'{0}' is an open generic handler, which the generated mediator cannot dispatch: the request type is only known once it is closed. At run time, set RegisterGenericHandlers to close it over the scanned types. Ahead of time, write one closed handler per request type, or put the shared part in an open generic IPipelineBehavior.",
            Category,
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true);

        internal static readonly DiagnosticDescriptor NoHandler = new DiagnosticDescriptor(
            "MDR0003",
            "Request has no handler here",
            "'{0}' has no handler in this project, so sending it will fail at run time unless one is registered from somewhere else",
            Category,
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true);
    }
}
