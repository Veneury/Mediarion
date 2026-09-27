namespace System.Diagnostics.CodeAnalysis
{
#if !NET5_0_OR_GREATER

    /// <summary>
    /// Polyfill of the framework attribute for target frameworks that predate it. PolySharp covers
    /// the nullability and language polyfills but not the trimming ones.
    /// </summary>
    [AttributeUsage(
        AttributeTargets.Class | AttributeTargets.Constructor | AttributeTargets.Method,
        Inherited = false)]
    internal sealed class RequiresUnreferencedCodeAttribute : Attribute
    {
        internal RequiresUnreferencedCodeAttribute(string message)
        {
            Message = message;
        }

        public string Message { get; }

        public string? Url { get; set; }
    }

    /// <summary>
    /// Polyfill of the framework attribute for target frameworks that predate it.
    /// </summary>
    [AttributeUsage(
        AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Constructor |
        AttributeTargets.Method | AttributeTargets.Property | AttributeTargets.Field |
        AttributeTargets.Event | AttributeTargets.Interface | AttributeTargets.Delegate,
        Inherited = false,
        AllowMultiple = true)]
    internal sealed class UnconditionalSuppressMessageAttribute : Attribute
    {
        internal UnconditionalSuppressMessageAttribute(string category, string checkId)
        {
            Category = category;
            CheckId = checkId;
        }

        public string Category { get; }

        public string CheckId { get; }

        public string? Scope { get; set; }

        public string? Target { get; set; }

        public string? MessageId { get; set; }

        public string? Justification { get; set; }
    }

#endif

#if !NET7_0_OR_GREATER

    /// <summary>
    /// Polyfill of the framework attribute for target frameworks that predate it.
    /// </summary>
    [AttributeUsage(
        AttributeTargets.Class | AttributeTargets.Constructor | AttributeTargets.Method,
        Inherited = false)]
    internal sealed class RequiresDynamicCodeAttribute : Attribute
    {
        internal RequiresDynamicCodeAttribute(string message)
        {
            Message = message;
        }

        public string Message { get; }

        public string? Url { get; set; }
    }

#endif
}
