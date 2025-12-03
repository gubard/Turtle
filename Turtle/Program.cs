using Microsoft.EntityFrameworkCore;
using Nestor.Db.Sqlite;
using Turtle.Contract.Models;
using Turtle.Contract.Services;
using Turtle.Services;
using Zeus.Helpers;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Services.AddTransient<ICredentialService, CredentialService>();
builder.Services.AddDbContext<DbContext, SqliteNestorDbContext>(options =>
    options.UseSqlite("Data Source=manis.db", x => x.MigrationsAssembly(typeof(SqliteNestorDbContext).Assembly)));

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.MapPost(RouteHelper.Get,
        (TurtleGetRequest request, ICredentialService authenticationService, CancellationToken ct) =>
            authenticationService.GetAsync(request, ct))
   .WithName(RouteHelper.GetName);

app.MapPost(RouteHelper.Post,
        (TurtlePostRequest request, ICredentialService authenticationService, CancellationToken ct) =>
            authenticationService.PostAsync(request, ct))
   .WithName(RouteHelper.PostName);

app.Run();