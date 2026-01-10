using Nestor.Db.Models;

namespace Turtle.Contract.Models;

public class TurtlePostRequest : IPostRequest
{
    public long LastLocalId { get; set; }
    public Guid[] DeleteIds { get; set; } = [];
    public Credential[] CreateCredentials { get; set; } = [];
    public EditCredential[] EditCredentials { get; set; } = [];
    public CredentialChangeOrder[] ChangeOrders { get; set; } = [];
    public EventEntity[] Events { get; set; } = [];
}
