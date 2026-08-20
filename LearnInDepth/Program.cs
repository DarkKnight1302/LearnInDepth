using LearnInDepth;
using LearnInDepth.Clients;
using LearnInDepth.Handlers;
using LearnInDepth.Repositories;
using LearnInDepth.Services;
using LearnInDepth.Services.Generation;
using LearnInDepth.Services.Interfaces;
using NewHorizonLib;
using NewHorizonLib.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Dev fallback so JWT registration never fails locally (Nuggets pattern); real value comes from user-secrets/env.
if (builder.Environment.IsDevelopment()
    && string.IsNullOrEmpty(builder.Configuration["TOKEN_SECRET_KEY"])
    && string.IsNullOrEmpty(Environment.GetEnvironmentVariable("TOKEN_SECRET_KEY")))
{
    builder.Configuration["TOKEN_SECRET_KEY"] = "learn-in-depth-dev-secret-key-change-in-production";
}

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddMemoryCache();
builder.Services.AddHttpContextAccessor();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy => policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
});

// Repositories (singleton, interfaces beside implementations - house pattern)
builder.Services.AddSingleton<ILearningPlanRepository, LearningPlanRepository>();
builder.Services.AddSingleton<IChapterContentRepository, ChapterContentRepository>();
builder.Services.AddSingleton<IChapterQuizRepository, ChapterQuizRepository>();
builder.Services.AddSingleton<IChapterAssignmentRepository, ChapterAssignmentRepository>();
builder.Services.AddSingleton<IAssignmentSubmissionRepository, AssignmentSubmissionRepository>();
builder.Services.AddSingleton<IUserProgressRepository, UserProgressRepository>();
builder.Services.AddSingleton<IUserRepository, UserRepository>();

// LLM client + domain services
builder.Services.AddSingleton<IOpenCodeCompletionClient, OpenCodeCompletionClient>();
builder.Services.AddSingleton<ILearningPlanService, LearningPlanService>();
builder.Services.AddSingleton<IQuizService, QuizService>();
builder.Services.AddSingleton<IAssignmentService, AssignmentService>();
builder.Services.AddSingleton<IUserProgressService, UserProgressService>();
builder.Services.AddSingleton<ISignInHandler, SignInHandler>();

// Generation pipeline
builder.Services.AddSingleton<IGenerationChannel, GenerationChannel>();
builder.Services.AddSingleton<IGenerationOrchestrator, GenerationOrchestrator>();
builder.Services.AddSingleton<IChapterGenerator, ChapterGenerator>();
builder.Services.AddHostedService<GenerationBackgroundService>();

builder.Services.AddSingleton<ICosmosCollectionBootstrapper, CosmosCollectionBootstrapper>();

// NewHorizonLib: Cosmos service, secrets, JWT bearer, OTP, email, rate limiting, swagger auth-header filter
Registration.InitializeServices(
    builder.Services,
    builder.Configuration,
    GlobalConstant.DatabaseName,
    googleApiThreshold: 0,
    issuer: GlobalConstant.Issuer,
    audience: GlobalConstant.Audience);

var app = builder.Build();

app.MapOpenApi();
app.UseSwagger();
app.UseSwaggerUI();

app.UseCors("AllowAll");
app.UseRateLimiting();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// Create database/containers before serving traffic (Nuggets pattern).
using (var scope = app.Services.CreateScope())
{
    ICosmosCollectionBootstrapper bootstrapper = scope.ServiceProvider.GetRequiredService<ICosmosCollectionBootstrapper>();
    await bootstrapper.EnsureCreatedAsync();
}

app.Run();
