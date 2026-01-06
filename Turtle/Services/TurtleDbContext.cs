using Gaia.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Nestor.Db.Services;
using Turtle.CompiledModels;
using Turtle.Contract.Models;

namespace Turtle.Services;

public sealed class TurtleDbContext
    : NestorDbContext,
        IStaticFactory<DbContextOptions, NestorDbContext>
{
    public TurtleDbContext() { }

    public TurtleDbContext(DbContextOptions options)
        : base(options) { }

    public DbSet<CredentialEntity> Credentials { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        base.OnConfiguring(optionsBuilder);
        optionsBuilder.UseModel(TurtleDbContextModel.Instance);
    }

    public static NestorDbContext Create(DbContextOptions input)
    {
        return new TurtleDbContext(input);
    }
}

public class TurtleDbContextFactory : IDesignTimeDbContextFactory<TurtleDbContext>
{
    public TurtleDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<TurtleDbContext>();
        optionsBuilder.UseSqlite("");

        return new(optionsBuilder.Options);
    }
}
