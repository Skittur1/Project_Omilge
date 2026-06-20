using BlynkTalk.API.Hubs;
using BlynkTalk.API.Services;
using BlynkTalk.API.Services.Interface;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);

// ── Services ──────────────────────────────────────────────────────

// Register SignalR
// MaximumReceiveMessageSize: prevents very large payloads (SDP/ICE are small — 64KB is generous)
builder.Services.AddSignalR(options =>
{
    options.MaximumReceiveMessageSize = 65536; // 64 KB
    options.ClientTimeoutInterval = TimeSpan.FromSeconds(60);
    options.KeepAliveInterval = TimeSpan.FromSeconds(30);
});

// Register our services as Singletons — they hold in-memory state
// If you register as Scoped, a new instance is created per request and state is lost
builder.Services.AddSingleton<IMatchmakingService, MatchmakingService>();
builder.Services.AddSingleton<IRoomService, RoomService>();

// CORS — allow the Angular dev server to connect
// In production, replace localhost:4200 with your actual domain
builder.Services.AddCors(options =>
{
    options.AddPolicy("AngularClient", policy =>
    {
        policy
            .WithOrigins(
                "http://localhost:4200",   // Angular dev server
                "https://yourdomain.com"   // production frontend
            )
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials(); // Required for SignalR
    });
});

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// Swagger setup 
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "BlynkTalk API",
        Version = "v1",
        Description = "Backend for BlynkTalk — SignalR hub contracts and REST endpoints"
    });
});

// ── Middleware pipeline ────────────────────────────────────────────

var app = builder.Build();

// 1. HTTPS redirect — must be first
app.UseHttpsRedirection();

// 2. Swagger — only in development
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "BlynkTalk API v1");
        // This makes Swagger load at root / instead of /swagger
        // Remove this line if you prefer it at /swagger/index.html
        options.RoutePrefix = string.Empty;
    });
}

// 3. Routing — before CORS and Authorization
app.UseRouting();

// 4. CORS — after UseRouting so endpoint metadata is available
app.UseCors("AngularClient");

// 5. Authorization (even if unused now, keep it here for future)
app.UseAuthorization();

// Map the SignalR hub to /hub/v1
// Tell Angular dev: connect to ws://localhost:5000/hub/v1
app.MapHub<VideoHub>("/hub/v1");

app.MapControllers();

app.Run();