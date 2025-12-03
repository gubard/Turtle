using Gaia.Errors;
using Gaia.Services;

namespace Turtle.Contract.Models;

public class TurtleGetResponse : IValidationErrors
{
    public ValidationError[] ValidationErrors { get; set; } = [];
}