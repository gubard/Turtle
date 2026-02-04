using Gaia.Models;
using Nestor.Db.Models;

namespace Turtle.Contract.Models;

public sealed class TurtlePostRequest : IPostRequest, IDragChangeOrder<EditCredential>
{
    public long LastLocalId { get; set; }
    public Guid[] DeleteIds { get; set; } = [];
    public Credential[] CreateCredentials { get; set; } = [];
    public ChangeOrder[] ChangeOrders { get; set; } = [];
    public EditCredential[] Edits { get; set; } = [];
    public EventEntity[] Events { get; set; } = [];
}
