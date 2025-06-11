using System.Collections.Generic;
using System.Linq;

namespace SagraFacile.NET.API.Models.Results
{
    public class ServiceResult
    {
        public bool Success { get; protected set; } = true;
        public List<string> Errors { get; protected set; } = new List<string>();

        public bool IsFailure => !Success;

        protected ServiceResult() { }

        public static ServiceResult Ok()
        {
            return new ServiceResult { Success = true };
        }

        public static ServiceResult Fail(string error)
        {
            return new ServiceResult { Success = false, Errors = new List<string> { error } };
        }

        public static ServiceResult Fail(IEnumerable<string> errors)
        {
            return new ServiceResult { Success = false, Errors = errors.ToList() };
        }
    }

    // Generic version
    public class ServiceResult<T> : ServiceResult
    {
        public T? Value { get; private set; }

        // Private constructor to force use of static factory methods
        private ServiceResult() : base() { }

        public static ServiceResult<T> Ok(T value)
        {
            return new ServiceResult<T> { Success = true, Value = value };
        }

        // Overload Fail methods to return ServiceResult<T> for consistency
        public new static ServiceResult<T> Fail(string error)
        {
            return new ServiceResult<T> { Success = false, Errors = new List<string> { error }, Value = default }; // Set Value to default on failure
        }

        public new static ServiceResult<T> Fail(IEnumerable<string> errors)
        {
            return new ServiceResult<T> { Success = false, Errors = errors.ToList(), Value = default }; // Set Value to default on failure
        }
    }
} 