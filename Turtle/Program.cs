using Gaia.Helpers;
using Gaia.Services;
using Microsoft.EntityFrameworkCore;
using Nestor.Db.Sqlite;
using Turtle.Contract.Models;
using Turtle.Contract.Services;
using Turtle.Services;
using Zeus.Helpers;
using Zeus.Models;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddAuthorization();
builder.Services.AddJwtAuthentication(builder.Configuration);
builder.Services.AddTransient<IStorageService, StorageService>();
builder.Services.AddTransient<ICredentialService, CredentialService>();
builder.Services.AddDbContext<DbContext, SqliteNestorDbContext>((sp, options) =>
    options.UseSqlite($"Data Source={sp.GetRequiredService<IStorageService>().GetDbDirectory().ToFile("turtle.db")}", x => x.MigrationsAssembly(typeof(SqliteNestorDbContext).Assembly)));

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

app.Services.CreateDbDirectory();
await app.Services.MigrateDbAsync("turtle.migration");
app.Run();