using System;

namespace Mediarion
{
    /// <summary>
    /// Marks a partial class for the generator to implement <see cref="IMediator"/> on.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="Mediator"/> works out which handler answers a request while the program runs,
    /// which means closing a generic over a type it only learns then. That cannot be done in an
    /// application published ahead of time. A class marked with this gets the same dispatch
    /// written out as ordinary C# instead, one case per request, settled while the project
    /// compiles.
    /// </para>
    /// <para>
    /// The class needs a constructor taking an <see cref="IServiceProvider"/>, and the generator
    /// writes it. Everything else — the handlers, the behaviours, the processors — is the same
    /// code either way.
    /// </para>
    /// </remarks>
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class GeneratedMediatorAttribute : Attribute
    {
        /// <summary>
        /// Gets or sets whether the generated registration also registers the pre- and
        /// post-processors it found. Off by default.
        /// </summary>
        /// <remarks>
        /// Off, to match <c>AddMediarion</c>, whose own flag is off for the same reason: a
        /// processor sitting in the project should not start running because somebody added a
        /// mediator. The two ways of registering have to agree about this, or the same
        /// application behaves differently depending on which one it used.
        /// </remarks>
        public bool RegisterRequestProcessors { get; set; }
    }
}
