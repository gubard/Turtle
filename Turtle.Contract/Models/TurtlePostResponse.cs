using Gaia.Models;
using Gaia.Services;
using Nestor.Db.Models;

namespace Turtle.Contract.Models;

public class TurtlePostResponse : IValidationErrors, IResponse
{
    public List<ValidationError> ValidationErrors { get; set; } = [];
    public EventEntity[] Events { get; set; } = [];
}