namespace Reaparr.FluentResults
{
    /// <summary>
    /// Objects from Error class cause a failed result
    /// </summary>
    public class Error : IError
    {
        /// <summary>
        /// Message of the error
        /// </summary>
        public string Message { get; protected set; } = string.Empty;

        /// <summary>
        /// Metadata of the error
        /// </summary>
        public Dictionary<string, object> Metadata { get; }

        /// <summary>
        /// Get the reasons of an error
        /// </summary>
        public List<IError> Reasons { get; }

        /// <summary>
        /// Creates a new instance of <see cref="Error"/>
        /// </summary>
        protected Error()
        {
            Metadata = new Dictionary<string, object>();
            Reasons = new List<IError>();
        }

        /// <summary>
        /// Creates a new instance of <see cref="Error"/>
        /// </summary>
        /// <param name="message">Description of the error</param>
        public Error(string message)
            : this()
        {
            Message = message;
        }

        /// <summary>
        /// Creates a new instance of <see cref="Error"/>
        /// </summary>
        /// <param name="message">Description of the error</param>
        /// <param name="causedBy">The root cause of the <see cref="Error"/></param>
        public Error(string message, IError causedBy)
            : this(message)
        {
            ArgumentNullException.ThrowIfNull(causedBy);

            Reasons.Add(causedBy);
        }

        /// <summary>
        /// Set the root cause of the error
        /// </summary>
        public Error CausedBy(IError error)
        {
            ArgumentNullException.ThrowIfNull(error);

            Reasons.Add(error);
            return this;
        }

        /// <summary>
        /// Set the root cause of the error
        /// </summary>
        public Error CausedBy(Exception exception)
        {
            ArgumentNullException.ThrowIfNull(exception);

            Reasons.Add(Result.Settings.ExceptionalErrorFactory(null, exception));
            return this;
        }

        /// <summary>
        /// Set the root cause of the error
        /// </summary>
        public Error CausedBy(string message, Exception exception)
        {
            ArgumentNullException.ThrowIfNull(exception);

            Reasons.Add(Result.Settings.ExceptionalErrorFactory(message, exception));
            return this;
        }

        /// <summary>
        /// Set the root cause of the error
        /// </summary>
        public Error CausedBy(string message)
        {
            Reasons.Add(Result.Settings.ErrorFactory(message));
            return this;
        }

        /// <summary>
        /// Set the root cause of the error
        /// </summary>
        public Error CausedBy(IEnumerable<IError> errors)
        {
            ArgumentNullException.ThrowIfNull(errors);

            Reasons.AddRange(errors);
            return this;
        }

        /// <summary>
        /// Set the root cause of the error
        /// </summary>
        public Error CausedBy(IEnumerable<string> errors)
        {
            ArgumentNullException.ThrowIfNull(errors);

            Reasons.AddRange(errors.Select(errorMessage => Result.Settings.ErrorFactory(errorMessage)));
            return this;
        }

        /// <summary>
        /// Set the metadata
        /// </summary>
        public Error WithMetadata(string metadataName, object metadataValue)
        {
            Metadata.Add(metadataName, metadataValue);
            return this;
        }

        /// <summary>
        /// Set the metadata
        /// </summary>
        public Error WithMetadata(Dictionary<string, object> metadata)
        {
            foreach (var metadataItem in metadata)
            {
                Metadata.Add(metadataItem.Key, metadataItem.Value);
            }

            return this;
        }

        /// <summary>
        /// ToString override
        /// </summary>
        public override string ToString()
        {
            return new ReasonStringBuilder()
                .WithReasonType(GetType())
                .WithInfo(nameof(Message), Message)
                .WithInfo(nameof(Metadata), string.Join("; ", Metadata))
                .WithInfo(nameof(Reasons), ReasonFormat.ErrorReasonsToString(Reasons))
                .Build();
        }
    }

    internal static class ReasonFormat
    {
        public static string ErrorReasonsToString(IEnumerable<IError> errorReasons) => string.Join("; ", errorReasons);

        public static string ReasonsToString(IEnumerable<IReason> reasons) => string.Join("; ", reasons);
    }
}
