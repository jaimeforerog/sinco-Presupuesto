using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Testcontainers.PostgreSql;
using Xunit;

namespace SincoPresupuesto.Integration.Tests.Fixtures;

/// <summary>
/// Fixture compartido: levanta un contenedor PostgreSQL vía Testcontainers y
/// expone un <see cref="WebApplicationFactory{Program}"/> que usa ese contenedor
/// como <c>ConnectionStrings:Postgres</c>. El entorno se fuerza a Development
/// para que Marten haga <c>AutoCreate.CreateOrUpdate</c> del schema en la primera
/// conexión (ver <c>Program.cs</c>).
///
/// Ciclo de vida: <see cref="IAsyncLifetime"/> — el contenedor se crea una sola
/// vez al iniciar la colección de tests y se destruye al final.
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("sinco_presupuesto_tests")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
    }

    public new async Task DisposeAsync()
    {
        await _postgres.DisposeAsync();
        await base.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(Environments.Development);

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Postgres"] = _postgres.GetConnectionString(),
            });
        });
    }
}
