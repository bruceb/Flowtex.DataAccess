using Flowtex.DataAccess.Application.Abstractions;
using Microsoft.EntityFrameworkCore;
using Samples.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddDbContext<SampleDbContext>(options =>
    options.UseInMemoryDatabase("SampleApiDb"));

builder.Services.AddScoped<IDataStore, SampleDataStore>();
builder.Services.AddScoped<IReadStore>(provider => provider.GetService<IDataStore>()!);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Initialize database
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<SampleDbContext>();
    await context.Database.EnsureCreatedAsync();
}

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();