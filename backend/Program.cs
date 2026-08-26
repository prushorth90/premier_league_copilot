using Backend.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSingleton<IHealthStatusService, HealthStatusService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.MapGet("/health", (IHealthStatusService healthStatusService) =>
        Results.Ok(healthStatusService.GetStatus()))
    .WithName("GetHealth")
    .WithTags("System");

app.Run();
