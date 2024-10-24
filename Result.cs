using System;
using System.Collections.Generic;

namespace MyApp.Common
{
    /// <summary>
    /// A class to represent the result of an operation throughout the application.
    /// It provides status, messages, errors, and optional data.
    /// </summary>
    public class Result
    {
        // Indicates if the operation was successful.
        public bool IsSuccess { get; private set; }

        // Contains the result data, if any.
        public object Data { get; private set; }

        // List of messages to convey additional information (e.g., warnings, notifications).
        public List<string> Messages { get; private set; } = new List<string>();

        // List of errors that occurred during the operation.
        public List<string> Errors { get; private set; } = new List<string>();

        // Timestamp to track when the result was created.
        public DateTime Timestamp { get; private set; } = DateTime.UtcNow;

        // Private constructor to enforce usage of factory methods.
        private Result(bool isSuccess, object data = null)
        {
            IsSuccess = isSuccess;
            Data = data;
        }

        /// <summary>
        /// Creates a successful result with optional data and messages.
        /// </summary>
        public static Result Success(object data = null, params string[] messages)
        {
            var result = new Result(true, data);
            if (messages != null) result.Messages.AddRange(messages);
            return result;
        }

        /// <summary>
        /// Creates a failed result with optional error messages.
        /// </summary>
        public static Result Failure(params string[] errors)
        {
            var result = new Result(false);
            if (errors != null) result.Errors.AddRange(errors);
            return result;
        }

        /// <summary>
        /// Adds a message to the result.
        /// </summary>
        public Result AddMessage(string message)
        {
            Messages.Add(message);
            return this;
        }

        /// <summary>
        /// Adds an error to the result.
        /// </summary>
        public Result AddError(string error)
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
