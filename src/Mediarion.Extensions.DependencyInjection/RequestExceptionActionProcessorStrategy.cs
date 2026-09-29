namespace Mediarion
{
    /// <summary>
    /// When the exception actions run.
    /// </summary>
    /// <remarks>
    /// <para>
    /// An exception handler can answer in the exception's place, and an exception action only
    /// watches. This decides whether an action still watches an exception that a handler has
    /// already turned into a response.
    /// </para>
    /// <para>
    /// It is a registration order and not a check made per request: the behaviour that runs the
    /// actions goes outside the one that runs the handlers, or inside it, and the exception
    /// either reaches it or does not. Nothing is asked at run time.
    /// </para>
    /// </remarks>
    public enum RequestExceptionActionProcessorStrategy
    {
        /// <summary>
        /// Only for an exception no handler dealt with. The default, because it is the other
        /// library's.
        /// </summary>
        ApplyForUnhandledExceptions = 0,

        /// <summary>
        /// For every exception, including one a handler has already answered in the place of.
        /// </summary>
        ApplyForAllExceptions = 1,
    }
}
