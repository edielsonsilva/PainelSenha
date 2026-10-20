namespace PainelSenhas.Tests.Support;

/// <summary>Reúne as classes de teste que compartilham o mesmo banco e a mesma aplicação (executam em sequência).</summary>
[CollectionDefinition(Name)]
public sealed class AppCollection : ICollectionFixture<AppFixture>
{
    public const string Name = "app";
}
