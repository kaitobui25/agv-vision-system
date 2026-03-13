using AgvControl.Services;
using AgvControl.Data;

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------------------
// DI Registration
// ---------------------------------------------------------------------------

// Bind "VisionAi" section from appsettings.json → VisionAiSettings
builder.Services.Configure<VisionAiSettings>(builder.Configuration.GetSection("VisionAi"));

// Register VisionClient with managed HttpClient (IHttpClientFactory)
builder.Services.AddHttpClient<IVisionClient, VisionClient>();

//Bind "PathPlanner" section from appsettings.json
builder.Services.Configure<PathPlannerOptions>(builder.Configuration.GetSection("PathPlanner"));

//Bind "Database" section from appsettings.json
builder.Services.Configure<DatabaseSettings>(builder.Configuration.GetSection("Database"));
builder.Services.AddSingleton<IDbLogger, DbLogger>();

// Add services to the container.
builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthorization();

app.MapControllers();

app.Run();
