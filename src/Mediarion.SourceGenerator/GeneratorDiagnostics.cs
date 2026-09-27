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

        internal static readonly DiagnosticDescriptor NoHandler = new DiagnosticDescriptor(
            "MDR0003",
            "Request has no handler here",
            "'{0}' has no handler in this project, so sending it will fail at run time unless one is registered from somewhere else",
            Category,
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true);
    }
}
