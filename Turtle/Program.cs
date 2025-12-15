using Turtle.Contract.Models;
using Turtle.Contract.Services;
using Zeus.Helpers;

await WebApplication
    .CreateBuilder(args)
    .CreateAndRunZeusApp<
        ICredentialService,
        EfCredentialService,
        TurtleGetRequest,
        TurtlePostRequest,
        TurtleGetResponse,
        TurtlePostResponse
    >("Turtle");
