using Turtle.Contract.Models;
using Turtle.Contract.Services;
using Turtle.Services;
using Zeus.Helpers;

await WebApplication
    .CreateBuilder(args)
    .CreateAndRunZeusApp<
        ICredentialService,
        EfCredentialService,
        TurtleGetRequest,
        TurtlePostRequest,
        TurtleGetResponse,
        TurtlePostResponse,
        TurtleDbContext
    >("Turtle");
