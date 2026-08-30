namespace Ymir.GeminiSync.Domain.Repositories;

public interface ILoglineRepository
{
    Task<List<LoglineLine>> GetLoglineLines(int customerId, string placeTypeDescription);
}
