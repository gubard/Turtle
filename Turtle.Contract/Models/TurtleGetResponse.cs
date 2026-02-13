using Gaia.Models;
using Gaia.Services;
using Nestor.Db.Models;

namespace Turtle.Contract.Models;

public sealed class TurtleGetResponse : IValidationErrors, IResponse
{
    public Credential[]? Roots { get; set; }
    public Dictionary<Guid, List<Credential>> Children { get; set; } = [];
    public Dictionary<Guid, List<Credential>> Parents { get; set; } = [];
    public List<ValidationError> ValidationErrors { get; set; } = [];
    public CredentialSelector[]? Selectors { get; set; }
    public Credential[]? Bookmarks { get; set; }
    public EventEntity[] Events { get; set; } = [];
}
