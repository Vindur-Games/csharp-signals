using System;

namespace Vindur.Signals
{
    /// <summary>
    /// Base exception type for all errors occurring within the reactive signals system.
    /// </summary>
    public class SignalException : InvalidOperationException
    {
        protected SignalException(string message) : base(message)
        {
        }
    }

    /// <summary>
    /// Exception thrown when a cyclic dependency is detected during signal computation or effect execution.
    /// </summary>
    public class CyclicSignalDependencyException : SignalException
    {
        public CyclicSignalDependencyException(string message) : base(message)
        {
        }
    }

    /// <summary>
    /// Exception thrown when a signal read operation is attempted in an invalid state or lifecycle phase.
    /// </summary>
    public class InvalidSignalReadException : SignalException
    {
        public InvalidSignalReadException(string message) : base(message)
        {
        }
    }

    /// <summary>
    /// Exception thrown when a signal write operation is attempted in a disallowed or pure reactive context.
    /// </summary>
    public class InvalidSignalWriteException : SignalException
    {
        public InvalidSignalWriteException(string message) : base(message)
        {
        }
    }

    /// <summary>
    /// Exception thrown when attempting to modify a read-only signal.
    /// </summary>
    public class ReadOnlySignalException : InvalidSignalWriteException
    {
        public ReadOnlySignalException(string message) : base(message)
        {
        }
    }
}
