using Gaia.Models;
using Gaia.Services;
using Nestor.Db.Models;

namespace Turtle.Contract.Models;

public class TurtleGetResponse : IValidationErrors
{
    public List<Credential> Roots { get; set; } = [];
    public Dictionary<Guid, List<Credential>> Children { get; set; } = [];
    public Dictionary<Guid, List<Credential>> Parents { get; set; } = [];
    public List<ValidationError> ValidationErrors { get; set; } = [];
    public EventEntity[] Events { get; set; } = [];
}