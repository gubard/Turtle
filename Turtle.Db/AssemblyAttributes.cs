using Nestor.Db.LiteDb.Models;
using Turtle.Contract.Models;

[assembly: LiteDb(typeof(CredentialEntity), nameof(CredentialEntity.Id), false)]
[assembly: LiteDbSourceEntity(typeof(CredentialEntity), nameof(CredentialEntity.Id))]
