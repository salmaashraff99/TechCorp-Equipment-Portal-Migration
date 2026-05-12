namespace TechCorp.Shared.DTOs;

public class DTO_Response<T>
{
    public int code { get; set; }
    public bool error { get; set; }
    public string message { get; set; } = string.Empty;
    public List<T> data { get; set; } = new();
}
