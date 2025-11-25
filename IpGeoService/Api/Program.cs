using Api.Middleware;
using Application.Interfaces;
using Application.Services;
using Infrastructure.Data;
using Infrastructure.Extensions;
using Infrastructure.Services;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddScoped<IGeoApplicationService, GeoApplicationService>();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddRateLimiter(_ => _
    .AddFixedWindowLimiter("default", opt =>
    {
        opt.PermitLimit = 30;
        opt.Window = TimeSpan.FromSeconds(10);
        opt.QueueLimit = 5;
    }));

var app = builder.Build();
app.UseGlobalExceptionHandling();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<IpGeoDbContext>();
    await db.Database.MigrateAsync();

    var initializer = scope.ServiceProvider.GetRequiredService<DataInitializer>();
    await initializer.InitializeAsync();
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseRateLimiter();
app.MapControllers();
app.Run();