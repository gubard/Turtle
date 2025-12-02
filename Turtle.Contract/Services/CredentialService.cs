using Turtle.Contract.Models;

namespace Turtle.Contract.Services;

public interface ICredentialService
{
    ValueTask<TurtleGetResponse> GetAsync(TurtleGetRequest request, CancellationToken ct);
    ValueTask<TurtlePostResponse> PostAsync(TurtlePostRequest request, CancellationToken ct);
}