using Microsoft.EntityFrameworkCore;
using Nestor.Db.Services;
using Turtle.Contract.Models;

namespace Turtle.Contract.Services;

public interface ICredentialDbContext : INestorDbContext
{
    DbSet<CredentialEntity> Credentials { get; }
}
