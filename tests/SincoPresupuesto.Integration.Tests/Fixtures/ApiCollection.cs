using Xunit;

namespace SincoPresupuesto.Integration.Tests.Fixtures;

/// <summary>
/// Colección xUnit que comparte el <see cref="ApiFactory"/> (y por tanto el
/// contenedor Postgres) entre todas las clases de test marcadas con
/// <c>[Collection(nameof(ApiCollection))]</c>. Un solo arranque por ejecución.
/// </summary>
[CollectionDefinition(nameof(ApiCollection))]
public sealed class ApiCollection : ICollectionFixture<ApiFactory> { }
