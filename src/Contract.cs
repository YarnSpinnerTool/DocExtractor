using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

// This class was extracted and modified from the Omnisharp-Roslyn codebase.
namespace DocExtractor
{
    internal static class Contract
    {
        // Guidance on inlining:
        // ThrowXxx methods are used heavily across the code base. 
        // Inline their implementation of condition checking but don't inline the code that is only executed on failure.
        // This approach makes the common path efficient (both execution time and code size) 
        // while keeping the rarely executed code in a separate method.

        /// <summary>
        /// Throws a non-accessible exception if the provided value is null.  This method executes in
        /// all builds
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ThrowIfNull<T>([NotNull] T value, [CallerLineNumber] int lineNumber = 0) where T : class?
        {
            if (value is null)
            {
                Fail("Unexpected null", lineNumber);
            }
        }

        [DebuggerHidden]
        [DoesNotReturn]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void Fail(string message = "Unexpected", [CallerLineNumber] int lineNumber = 0)
            => throw new InvalidOperationException($"{message} - line {lineNumber}");
    }

    // public static class Debug {

    // }

}
