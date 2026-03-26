using Nestor.Db.LiteDb.Models;
using Nestor.Db.Models;
using Turtle.Contract.Models;

[assembly: Ado(typeof(CredentialEntity), nameof(CredentialEntity.Id), false)]
[assembly: AdoSourceEntity(typeof(CredentialEntity), nameof(CredentialEntity.Id))]
[assembly: LiteDb(typeof(CredentialEntity), nameof(CredentialEntity.Id), false)]
[assembly: LiteDbSourceEntity(typeof(CredentialEntity), nameof(CredentialEntity.Id))]
