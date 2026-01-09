using Nestor.Db.Models;
using Turtle.Contract.Models;

[assembly: SqliteAdo(typeof(CredentialEntity), nameof(CredentialEntity.Id))]
[assembly: SourceEntity(typeof(CredentialEntity), nameof(CredentialEntity.Id))]
