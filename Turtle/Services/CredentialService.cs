using Turtle.Contract.Models;
using Turtle.Contract.Services;

namespace Turtle.Services;

public class CredentialService : ICredentialService
{

    public ValueTask<TurtleGetResponse> GetAsync(TurtleGetRequest request, CancellationToken ct)
    {
        throw new NotImplementedException();
    }

    public ValueTask<TurtlePostResponse> PostAsync(TurtlePostRequest request, CancellationToken ct)
    {
        throw new NotImplementedException();
    }
}