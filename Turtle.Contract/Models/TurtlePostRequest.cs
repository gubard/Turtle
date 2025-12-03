namespace Turtle.Contract.Models;

public class TurtlePostRequest
{
    public Guid[] DeleteIds { get; set; } = [];
    public CreateCredential[] CreateCredentials { get; set; } = [];
    public EditCredential[] EditCredentials { get; set; } = [];
    public ChangeOrder[] ChangeOrders { get; set; } = [];
}