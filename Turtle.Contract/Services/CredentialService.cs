using Gaia.Services;
using Nestor.Db.Services;
using Turtle.Contract.Models;

namespace Turtle.Contract.Services;

public interface ICredentialHttpService
    : ICredentialService,
        IHttpService<TurtleGetRequest, TurtlePostRequest, TurtleGetResponse, TurtlePostResponse>;

public interface ICredentialService
    : IService<TurtleGetRequest, TurtlePostRequest, TurtleGetResponse, TurtlePostResponse>;

public interface ICredentialDbCache : IDbCache<TurtlePostRequest, TurtleGetResponse>;

public interface ICredentialDbService
    : ICredentialService,
        IDbService<TurtleGetRequest, TurtlePostRequest, TurtleGetResponse, TurtlePostResponse>;

public sealed class EmptyCredentialDbCache
    : EmptyDbCache<TurtlePostRequest, TurtleGetResponse>,
        ICredentialDbCache;

public sealed class EmptyCredentialDbService
    : EmptyDbService<TurtleGetRequest, TurtlePostRequest, TurtleGetResponse, TurtlePostResponse>,
        ICredentialDbService;
