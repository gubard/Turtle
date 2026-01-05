using System.Collections.Frozen;
using Nestor.Db.Sqlite.Helpers;
using Turtle.Contract.Helpers;
using Turtle.Contract.Models;
using Turtle.Contract.Services;
using Turtle.Services;
using Zeus.Helpers;

var migration = new Dictionary<int, string>();

foreach (var (key, value) in SqliteMigration.Migrations)
{
    migration.Add(key, value);
}

foreach (var (key, value) in TurtleMigration.Migrations)
{
    migration.Add(key, value);
}

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
    >(migration.ToFrozenDictionary(), "Turtle");
