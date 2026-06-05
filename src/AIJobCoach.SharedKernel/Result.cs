namespace AIJobCoach.SharedKernel;

public sealed class Result<T>
{
    private Result(bool isSuccess, T? value, Error? error)
    {
        IsSuccess = isSuccess;
        Value = value;
        Error = error;
    }
    
    public bool IsSuccess { get; }

    public bool IsFailure => !IsSuccess;
    
    public T? Value { get; }
    
    public Error? Error { get; }

    public static Result<T> Success(T value)
    {
        return new Result<T>(true, value, null);
    }

    public static Result<T> Failure(Error error)
    {
        return new Result<T>(false, default, error);
    }
}