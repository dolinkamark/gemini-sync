namespace Ymir.GeminiSync.Domain.Repositories;

public interface IPlaceRepository
{
    Task<List<Place>> GetPlaces(int customerId);
}
