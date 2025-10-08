namespace DevOpsMcp.Server.Models;

public class ToolResponse<T>
{
    public bool Success { get; set; }
    public T? Data { get; set; }
    public string? Error { get; set; }

    public static ToolResponse<T> Ok(T data) => new() { Success = true, Data = data };
    public static ToolResponse<T> Fail(string error) => new() { Success = false, Error = error };
}
