using Gaia.Helpers;
using Gaia.Services;
using Microsoft.EntityFrameworkCore;
using Nestor.Db.Sqlite;
using Turtle.Contract.Models;
using Turtle.Contract.Services;
using Turtle.Services;
using Zeus.Helpers;
using Zeus.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddAuthorization();
builder.Services.AddHttpContextAccessor();
builder.Services.AddJwtAuthentication(builder.Configuration);
builder.Services.AddTransient<IStorageService, StorageService>();
builder.Services.AddTransient<ICredentialService, CredentialService>();
builder.Services.AddTransient<IDbMigrator, DbMigrator>(sp => new(sp.GetRequiredService<IStorageService>().GetDbDirectory().Combine("Turtle")));
builder.Services.AddDbContext<DbContext, SqliteNestorDbContext>((sp, options) =>
{
    var httpContextAccessor = sp.GetRequiredService<IHttpContextAccessor>();
    var userId = httpContextAccessor.HttpContext.ThrowIfNull().GetUserId();
    var dataSourceFile = sp.GetRequiredService<IStorageService>().GetDbDirectory().Combine("Turtle").ToFile($"{userId}.db");
    options.UseSqlite($"Data Source={dataSourceFile}", x => x.MigrationsAssembly(typeof(SqliteNestorDbContext).Assembly));

    if (dataSourceFile.Exists)
    {
        return;
    }

    if (dataSourceFile.Directory?.Exists != true)
    {
        dataSourceFile.Directory?.Create();
    }

    using var context = new SqliteNestorDbContext(options.Options);
    context.Database.Migrate();
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

app.MapPost(RouteHelper.Get,
        (TurtleGetRequest request, ICredentialService authenticationService, CancellationToken ct) =>
            authenticationService.GetAsync(request, ct))
   .RequireAuthorization()
   .WithName(RouteHelper.GetName);

app.MapPost(RouteHelper.Post,
        (TurtlePostRequest request, ICredentialService authenticationService, CancellationToken ct) =>
            authenticationService.PostAsync(request, ct))
   .RequireAuthorization()
   .WithName(RouteHelper.PostName);

await app.Services.GetRequiredService<IDbMigrator>().MigrateAsync(CancellationToken.None);
await app.RunAsync();