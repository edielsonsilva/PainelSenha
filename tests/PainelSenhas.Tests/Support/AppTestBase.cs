namespace PainelSenhas.Tests.Support;

/// <summary>Base dos testes de integração: garante banco de senhas vazio no início de cada teste.</summary>
[Collection(AppCollection.Name)]
public abstract class AppTestBase(AppFixture app) : IAsyncLifetime
{
    protected AppFixture App { get; } = app;

    public async ValueTask InitializeAsync() => await App.ResetAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
