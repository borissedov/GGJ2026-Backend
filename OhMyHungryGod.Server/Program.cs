using OhMyHungryGod.Server.Hubs;
using OhMyHungryGod.Server.Services;
using OhMyHungryGod.Server.State;

var builder = WebApplication.CreateBuilder(args);

// SignalR
builder.Services.AddSignalR();

// State and Services
builder.Services.AddSingleton<InMemoryRoomStore>();
builder.Services.AddSingleton<RoomService>();
builder.Services.AddSingleton<OrderGeneratorService>();
builder.Services.AddSingleton<MoodCalculatorService>();
builder.Services.AddSingleton<GameEngineService>();

// Background Services
builder.Services.AddHostedService<BackgroundTimerService>();

// CORS for web display and mobile clients
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.SetIsOriginAllowed(_ => true)  // Allow any origin
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();  // Required for SignalR
    });
});

var app = builder.Build();

// Use CORS
app.UseCors("AllowAll");

// Health check endpoint
app.MapGet("/health", () => Results.Ok(new
{
    status = "healthy",
    timestamp = DateTime.UtcNow,
    version = "1.0.0"
}));

// Map SignalR hub
app.MapHub<GameHub>("/gamehub");

app.Run();
