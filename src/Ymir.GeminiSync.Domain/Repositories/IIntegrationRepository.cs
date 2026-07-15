namespace Ymir.GeminiSync.Domain.Repositories;

public interface IIntegrationRepository
{
    Task<Integration> GetIntegrationAsync(int customerId, string name, string integrationType);
}
