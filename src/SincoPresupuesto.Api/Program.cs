using JasperFx.CodeGeneration;
using Marten;
using Marten.Events.Projections;
using SincoPresupuesto.Api.Endpoints;
using SincoPresupuesto.Api.ExceptionHandlers;
using SincoPresupuesto.Application.Presupuestos;
using Weasel.Core;
using Wolverine;
using Wolverine.Marten;
using MartenTenancyStyle = Marten.Storage.TenancyStyle;

var builder = WebApplication.CreateBuilder(args);

var connectionString =
    builder.Configuration.GetConnectionString("Postgres")
    ?? throw new InvalidOperationException("ConnectionStrings:Postgres no configurada.");

// ─────────────── Marten (Event Store + proyecciones) ───────────────
builder.Services
    .AddMarten(opts =>
    {
        opts.Connection(connectionString);

        // Multi-tenant conjoint: una sola BD, discriminador tenant_id por documento/evento.
        opts.Events.TenancyStyle = MartenTenancyStyle.Conjoined;
        opts.Policies.AllDocumentsAreMultiTenanted();

        // En dev: crea/actualiza schema automáticamente. En prod usar migraciones (Weasel / Grate).
        opts.AutoCreateSchemaObjects = builder.Environment.IsDevelopment()
            ? AutoCreate.CreateOrUpdate
            : AutoCreate.None;

        opts.Projections.Add<PresupuestoProjection>(ProjectionLifecycle.Inline);

        // Serialización: system.text.json con polimorfismo de records.
        opts.UseSystemTextJsonForSerialization();
    })
    .IntegrateWithWolverine()        // Outbox + inbox sobre Postgres
    .UseLightweightSessions();

// ─────────────── Wolverine (mensajería + command handlers) ───────────────
builder.Host.UseWolverine(opts =>
{
    opts.Durability.Mode = DurabilityMode.MediatorOnly;
    opts.Policies.AutoApplyTransactions();
    opts.CodeGeneration.TypeLoadMode = TypeLoadMode.Auto;
});

// ─────────────── ASP.NET Core ───────────────
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddExceptionHandler<DomainExceptionHandler>();
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();
builder.Services.AddHealthChecks()
    .AddNpgSql(connectionString, name: "postgres");

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapHealthChecks("/health");
app.MapPresupuestoEndpoints();

await app.RunAsync();

// Necesario para WebApplicationFactory<Program> en integration tests a futuro.
public partial class Program { }
