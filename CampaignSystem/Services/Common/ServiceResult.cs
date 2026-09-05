namespace CampaignSystem.Services;

/// <summary>
/// How a service operation ended, in terms the controller can turn into a status code
/// without knowing which business rule produced it.
/// </summary>
public enum ResultStatus
{
    Success,

    /// <summary>The record the operation targets does not exist. Maps to 404.</summary>
    NotFound,

    /// <summary>Well formed, but breaks a business rule. Maps to 400.</summary>
    Invalid,

    /// <summary>Collides with a record that already exists. Maps to 409.</summary>
    Conflict
}

/// <summary>Outcome of an operation that returns nothing.</summary>
public record ServiceResult(ResultStatus Status, string? Error = null)
{
    public static ServiceResult Success() => new(ResultStatus.Success);

    public static ServiceResult NotFound(string? error = null) => new(ResultStatus.NotFound, error);

    public static ServiceResult Invalid(string error) => new(ResultStatus.Invalid, error);

    public static ServiceResult Conflict(string error) => new(ResultStatus.Conflict, error);
}

/// <summary>Outcome of an operation that returns a value on success.</summary>
public record ServiceResult<T>(ResultStatus Status, T? Value = default, string? Error = null)
{
    public static ServiceResult<T> Success(T value) => new(ResultStatus.Success, value);

    public static ServiceResult<T> NotFound(string? error = null) => new(ResultStatus.NotFound, default, error);

    public static ServiceResult<T> Invalid(string error) => new(ResultStatus.Invalid, default, error);

    public static ServiceResult<T> Conflict(string error) => new(ResultStatus.Conflict, default, error);
}
