using System.Collections.Frozen;
using Nestor.Db.Helpers;
using Turtle.Contract.Helpers;
using Turtle.Contract.Models;
using Turtle.Contract.Services;
using Turtle.Db.Services;
using Zeus.Helpers;

InsertHelper.AddDefaultInsert(
    nameof(CredentialEntity),
    i => new CredentialEntity[] { new() { Id = i } }.CreateInsertQuery()
);

var migration = new Dictionary<int, string>();

foreach (var (key, value) in SqliteMigration.Migrations)
{
    migration.Add(key, value);
}

foreach (var (key, value) in TurtleMigration.Migrations)
{
    migration.Add(key, value);
}

foreach (var (key, value) in IdempotenceMigration.Migrations)
{
    migration.Add(key, value);
}

await WebApplication
    .CreateBuilder(args)
    .CreateAndRunZeusApp<
        ICredentialService,
        CredentialLiteDbService,
        TurtleGetRequest,
        TurtlePostRequest,
        TurtleGetResponse,
        TurtlePostResponse
    >(
        migration.ToFrozenDictionary(),
        "Turtle",
        builder => builder.Services.AddSingleton(TurtleJsonContext.Default.Options)
    );
