using Gaia.Services;

namespace Turtle.Contract.Models;

public class TurtleGetRequest : IGetRequest
{
    public bool IsGetRoots { get; set; }
    public Guid[] GetChildrenIds { get; set; } = [];
    public Guid[] GetParentsIds { get; set; } = [];
    public long LastId { get; set; } = -1;
}
