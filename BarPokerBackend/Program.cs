using BarPokerBackend.Hubs;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
// 1. ADD THIS LINE: Enable SignalR services
builder.Services.AddSignalR();

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseHttpsRedirection();
app.UseAuthorization();

app.MapControllers();

app.UseRouting();
app.UseStaticFiles();
// Ensure CORS is allowed if running the HTML page locally outside the server's domain
app.UseCors(policy => policy
    .AllowAnyHeader()
    .AllowAnyMethod()
    .SetIsOriginAllowed(_ => true)
    .AllowCredentials());

app.MapHub<PokerHub>("/pokerhub");

app.Run();