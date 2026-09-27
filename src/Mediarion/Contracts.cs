using System;
using System.Threading.Tasks;

namespace Mediarion
{
    /// <summary>
    /// The thing every request has in common, whatever it answers with.
    /// </summary>
    /// <remarks>
    /// Nothing implements this directly. It exists so that a request can be recognised without
    /// knowing its response type, which is what <see cref="ISender.Send(object, System.Threading.CancellationToken)"/>
    /// has to do when all it is handed is an object.
    /// </remarks>
    public interface IBaseRequest
    {
    }

    /// <summary>
    /// A message with one handler that answers with <typeparamref name="TResponse"/>.
    /// </summary>
    /// <typeparam name="TResponse">What the handler gives back.</typeparam>
    public interface IRequest<out TResponse> : IBaseRequest
    {
    }

    /// <summary>
    /// A message with one handler and nothing to give back.
    /// </summary>
    /// <remarks>
    /// It answers with <see cref="Unit"/> rather than with nothing at all, so that one pipeline
    /// serves both shapes instead of two that have to be kept in step.
    /// </remarks>
    public interface IRequest : IRequest<Unit>
    {
    }

    /// <summary>
    /// A message with any number of handlers, and no answer.
    /// </summary>
    public interface INotification
    {
    }

    /// <summary>
    /// The absence of a response, as a value.
    /// </summary>
    /// <remarks>
    /// A generic pipeline cannot be written over "no type at all", so a request that answers
    /// with nothing answers with this instead. There is one of it and it carries no information.
    /// </remarks>
    public readonly struct Unit : IEquatable<Unit>, IComparable<Unit>, IComparable
    {
        private static readonly Task<Unit> CompletedTask =
            System.Threading.Tasks.Task.FromResult(default(Unit));

        /// <summary>Gets the one value there is.</summary>
        public static Unit Value => default;

        /// <summary>Gets a task that has already completed with <see cref="Value"/>.</summary>
        public static Task<Unit> Task => CompletedTask;

        /// <summary>Returns true, since every instance is the same one.</summary>
        /// <param name="left">The left value.</param>
        /// <param name="right">The right value.</param>
        /// <returns><see langword="true"/>, always.</returns>
        public static bool operator ==(Unit left, Unit right) => true;

        /// <summary>Returns false, since every instance is the same one.</summary>
        /// <param name="left">The left value.</param>
        /// <param name="right">The right value.</param>
        /// <returns><see langword="false"/>, always.</returns>
        public static bool operator !=(Unit left, Unit right) => false;

        /// <summary>Compares two values, which are always equal.</summary>
        /// <param name="left">The left value.</param>
        /// <param name="right">The right value.</param>
        /// <returns><see langword="false"/>, always.</returns>
        public static bool operator <(Unit left, Unit right) => false;

        /// <summary>Compares two values, which are always equal.</summary>
        /// <param name="left">The left value.</param>
        /// <param name="right">The right value.</param>
        /// <returns><see langword="true"/>, always.</returns>
        public static bool operator <=(Unit left, Unit right) => true;

        /// <summary>Compares two values, which are always equal.</summary>
        /// <param name="left">The left value.</param>
        /// <param name="right">The right value.</param>
        /// <returns><see langword="false"/>, always.</returns>
        public static bool operator >(Unit left, Unit right) => false;

        /// <summary>Compares two values, which are always equal.</summary>
        /// <param name="left">The left value.</param>
        /// <param name="right">The right value.</param>
        /// <returns><see langword="true"/>, always.</returns>
        public static bool operator >=(Unit left, Unit right) => true;

        /// <inheritdoc/>
        public bool Equals(Unit other) => true;

        /// <inheritdoc/>
        public override bool Equals(object? obj) => obj is Unit;

        /// <inheritdoc/>
        public override int GetHashCode() => 0;

        /// <inheritdoc/>
        public int CompareTo(Unit other) => 0;

        /// <inheritdoc/>
        public int CompareTo(object? obj) => 0;

        /// <inheritdoc/>
        public override string ToString() => "()";
    }
}
