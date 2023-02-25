using System;
using System.Runtime.Serialization;
using JetBrains.Annotations;

namespace nadena.dev.modular_avatar.editor.ErrorReporting
{
    /// <summary>
    /// These exceptions will not be logged in the error report.
    /// </summary>
    public class NominalException : Exception
    {
        public NominalException()
        {
        }

        protected NominalException([NotNull] SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }

        public NominalException(string message) : base(message)
        {
        }

        public NominalException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}