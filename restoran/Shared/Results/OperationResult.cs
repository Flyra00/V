namespace Restoran.Shared.Results
{
    public class OperationResult
    {
        public bool Succeeded { get; init; }
        public bool IsNotFound { get; init; }
        public string Message { get; init; } = string.Empty;

        public static OperationResult Success(string message = "")
            => new() { Succeeded = true, Message = message };

        public static OperationResult Failure(string message)
            => new() { Succeeded = false, Message = message };

        public static OperationResult NotFound(string message)
            => new() { Succeeded = false, IsNotFound = true, Message = message };
    }

    public class OperationResult<T> : OperationResult
    {
        public T? Data { get; init; }

        public static OperationResult<T> Success(T data, string message = "")
            => new() { Succeeded = true, Data = data, Message = message };

        public static new OperationResult<T> Failure(string message)
            => new() { Succeeded = false, Message = message };

        public static new OperationResult<T> NotFound(string message)
            => new() { Succeeded = false, IsNotFound = true, Message = message };
    }
}
