namespace Turtle.Contract.Models;

public class TurtlePostRequest
{
    public long LastLocalId { get; set; }
    public Guid[] DeleteIds { get; set; } = [];
    public Credential[] CreateCredentials { get; set; } = [];
    public EditCredential[] EditCredentials { get; set; } = [];
    public ChangeOrder[] ChangeOrders { get; set; } = [];
}