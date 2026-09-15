namespace PLS.Api.Tests;

[CollectionDefinition("Integration", DisableParallelization = true)]
public class IntegrationTestCollection : ICollectionFixture<PlsWebAppFactory>
{
}
