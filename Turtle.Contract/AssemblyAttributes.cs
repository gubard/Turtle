using Nestor.Db.Models;
using Turtle.Contract.Models;

[assembly: Ado(typeof(CredentialEntity), nameof(CredentialEntity.Id), false)]
[assembly: SourceEntity(typeof(CredentialEntity), nameof(CredentialEntity.Id))]
