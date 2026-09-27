using System;

namespace Mediarion
{
    internal static class Guard
    {
        internal static void NotNull(object? value, string name)
        {
            if (value is null)
            {
                throw new ArgumentNullException(name);
            }
        }
    }
}
