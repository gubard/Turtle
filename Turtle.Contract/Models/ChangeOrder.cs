namespace Turtle.Contract.Models;

public class ChangeOrder
{
    public Guid StartId { get; set; }
    public Guid[] InsertIds { get; set; } = [];
    public  bool IsAfter{ get; set; }
}