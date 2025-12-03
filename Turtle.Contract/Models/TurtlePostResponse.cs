using Gaia.Errors;
using Gaia.Services;

namespace Turtle.Contract.Models;

public class TurtlePostResponse : IValidationErrors
{
    public Guid[] CreatedIds { get; set; } = [];
    public ValidationError[] ValidationErrors { get; set; } = [];
}