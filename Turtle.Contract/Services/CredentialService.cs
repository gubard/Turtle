using Gaia.Services;
using Turtle.Contract.Models;

namespace Turtle.Contract.Services;

public interface ICredentialService : IService<TurtleGetRequest, TurtlePostRequest, TurtleGetResponse, TurtlePostResponse>;