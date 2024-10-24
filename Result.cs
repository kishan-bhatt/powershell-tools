using System;
using System.Collections.Generic;

namespace MyApp.Common
{
    /// <summary>
    /// A generic class to represent the result of an operation throughout the application.
    /// It provides status, messages, errors, and data payload.
    /// </summary>
    /// <typeparam name="T">The type of the data payload.</typeparam>
    public class Result<T>
    {
        // Indicates if the operation was successful.
        public bool IsSuccess { get; private set; }

        // Contains the data from the operation, if applicable.
        public T Data { get; private set; }

        // List of messages to convey additional information (e.g., warnings, notifications).
        public List<string> Messages { get; private set; } = new List<string>();

        // List of errors that occurred during the operation.
        public List<string> Errors { get; private set; } = new List<string>();

        // Optional: Track when the result was created.
        public DateTime Timestamp { get; private set; } = DateTime.UtcNow;

        // Private constructor to enforce usage of static factory methods.
        private Result(bool isSuccess, T data = default)
        {
            IsSuccess = isSuccess;
            Data = data;
        }

        /// <summary>
        /// Creates a successful result with optional data and messages.
        /// </summary>
        public static Result<T> Success(T data = default, params string[] messages)
        {
            var result = new Result<T>(true, data);
            if (messages != null) result.Messages.AddRange(messages);
            return result;
        }

        /// <summary>
        /// Creates a failed result with optional error messages.
        /// </summary>
        public static Result<T> Failure(params string[] errors)
        {
            var result = new Result<T>(false);
            if (errors != null) result.Errors.AddRange(errors);
            return result;
        }

        /// <summary>
        /// Adds a message to the result.
        /// </summary>
        public Result<T> AddMessage(string message)
        {
            Messages.Add(message);
            return this;
        }

        /// <summary>
        /// Adds an error to the result.
        /// </summary>
        public Result<T> AddError(string error)
        {
            Errors.Add(error);
            return this;
        }

        /// <summary>
        /// Converts the result to a readable string for logging or debugging.
        /// </summary>
        public override string ToString()
        {
            string status = IsSuccess ? "Success" : "Failure";
            string dataString = Data != null ? Data.ToString() : "No Data";
            string messagesString = Messages.Count > 0 ? string.Join(", ", Messages) : "No Messages";
            string errorsString = Errors.Count > 0 ? string.Join(", ", Errors) : "No Errors";

            return $"[{Timestamp}] {status}: Data = {dataString}, Messages = [{messagesString}], Errors = [{errorsString}]";
        }
    }
}
