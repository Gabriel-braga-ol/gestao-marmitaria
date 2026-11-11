namespace GestaoMarmitaria.Tests.IntegrationTests;

[CollectionDefinition(Nome)]
public sealed class ApiIntegrationCollection : ICollectionFixture<ApiIntegrationFixture>
{
    public const string Nome = "api-integracao";
}
