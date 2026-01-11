using Gaia.Models;
using Gaia.Services;
using Nestor.Db.Models;

namespace Turtle.Contract.Models;

public class TurtlePostResponse : IValidationErrors, IPostResponse
{
    public List<ValidationError> ValidationErrors { get; set; } = [];
    public EventEntity[] Events { get; set; } = [];
    public bool IsEventSaved { get; set; }
}
