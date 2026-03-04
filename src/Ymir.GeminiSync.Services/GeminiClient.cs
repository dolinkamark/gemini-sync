using Microsoft.Extensions.Options;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Ymir.GeminiSync.Services.Models;
using Ymir.GeminiSync.Services.Models.Containers;
using Ymir.GeminiSync.Services.Settings;

namespace Ymir.GeminiSync.Services;

public class GeminiClient : IGeminiClient
{
    private readonly GeminiSettings _settings;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly JsonSerializerOptions _options;

    public GeminiClient(GeminiSettings settings, IHttpClientFactory httpClientFactory)
    {
        _options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
        _options.Converters.Add(new UtcDateTimeConverter());
        _options.Converters.Add(new JsonStringEnumConverter());

        _settings = settings;
        _httpClientFactory = httpClientFactory;
    }

    #region Garbage bin fractions

    public async Task<List<FractionInTime>> GetFractionsInTime(int garbageBinCollectionId)
    {
        string url = $"{_settings.BaseUrl}/garbagebins/api/GarbageBinCollections/{garbageBinCollectionId}/FractionsInTime";
        return await DoApiCall<List<FractionInTime>>(url);
    }

    public async Task<bool> UpdateFractionsInTime(int garbageBinCollectionId, List<AgreementFractionTimeline> fractions)
    {
        string url = $"{_settings.BaseUrl}/garbagebins/api/GarbageBinCollections/{garbageBinCollectionId}/FractionsInTime";

        var response = await DoApiCallInternal(url, HttpMethod.Put, fractions);
        return response.IsSuccessStatusCode;
    }

    #endregion

    #region Garbage bin collections

    public async Task<List<GarbageBinsCollectionDto>> GetGarbageBinCollection(int collectionId)
    {
        string url = $"{_settings.BaseUrl}/garbagebins/api/GarbageBinCollections/{collectionId}/StateInTime";
        return await DoApiCall<List<GarbageBinsCollectionDto>>(url);
    }

    public async Task<bool> UpdateGarbageBinCollection(GarbageBinsStateInTimeDto garbageBinsStates)
    {
        if (garbageBinsStates?.StateInTime.Count == 0) return false;

        var latestId = garbageBinsStates.StateInTime.LastOrDefault().GarbageBinCollectionId;
        string url = $"{_settings.BaseUrl}/garbagebins/api/GarbageBinCollections/{latestId}/StateInTime";
        
        var response = await DoApiCallInternal(url, HttpMethod.Put, garbageBinsStates);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> DeleteGarbageBinCollection(int collectionId)
    {
        string url = $"{_settings.BaseUrl}/garbagebins/api/GarbageBinCollections/{collectionId}/StateInTime";

        var response = await DoApiCallInternal(url, HttpMethod.Put, new GarbageBinsStateInTimeDto());
        return response.IsSuccessStatusCode;
    }

    #endregion

    #region Garbage bin pickups

    public async Task<List<GarbagePickupDto>> GetGarbageBinPickups(int garbageCollectionId)
    {
        string url = $"{_settings.BaseUrl}/garbagebins/api/GarbageBinCollections/{garbageCollectionId}/pickups";
        return await DoApiCall<List<GarbagePickupDto>>(url);
    }

    public async Task<bool> AddGarbageBinPickup(GarbagePickupDto pickupDto)
    {
        string url = $"{_settings.BaseUrl}/garbagebins/api/GarbageBinCollections/{pickupDto.GarbageBinCollectionId}/pickups";
        var response = await DoApiCallInternal(url, HttpMethod.Post, pickupDto);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> DeleteGarbageBinPickup(int garbageCollectionId, int pickupId)
    {
        string url = $"{_settings.BaseUrl}/garbagebins/api/GarbageBinCollections/{garbageCollectionId}/pickups/{pickupId}";
        var response = await DoApiCallInternal(url, HttpMethod.Delete);
        return response.IsSuccessStatusCode;
    }

    #endregion

    #region Private containers

    public async Task<List<PrivateContainerFractionsResponse>> GetPrivateContainerGroupFractions(int privateContainerGroupId)
    {
        string url = $"{_settings.BaseUrl}/garbagebins/api/GarbagePrivateContainerGroup/{privateContainerGroupId}/FractionsInTime";
        return await DoApiCall<List<PrivateContainerFractionsResponse>>(url);
    }

    public async Task<bool> UpdatePrivateContainerGroupFractions(
        int privateContainerGroupId,
        List<PrivateContainerGroupAgreementFractions> agreementFractions)
    {
        if (agreementFractions.Count == 0) return false;

        string url = $"{_settings.BaseUrl}/garbagebins/api/GarbagePrivateContainerGroup/{privateContainerGroupId}/FractionsInTime";

        var response = await DoApiCallInternal(url, HttpMethod.Put, agreementFractions);
        return response.IsSuccessStatusCode;
    }

    #endregion

    #region Public containers

    public async Task<List<ConnectionTimelineDto>> GetUtilityConnectionTimeline(int agreementId)
    {
        string url = $"{_settings.BaseUrl}/garbagebins/api/garbage/utilityUnit/{agreementId}/connection";
        return await DoApiCall<List<ConnectionTimelineDto>>(url);
    }

    public async Task<bool> UpdateUtilityConnectionTimeline(int agreementId, UtilityUnitConnectionUpdateDto updateDto)
    {
        string url = $"{_settings.BaseUrl}/garbagebins/api/garbage/utilityUnit/{agreementId}/connection";

        var response = await DoApiCallInternal(url, HttpMethod.Put, updateDto);
        return response.IsSuccessStatusCode;
    }

    #endregion

    #region Private Helpers

    private async Task<T> DoApiCall<T>(string url, HttpMethod method = null, object content = null) where T : new()
    {
        var response = await DoApiCallInternal(url, method, content);
        if (response.IsSuccessStatusCode)
        {
            string responseContent = await response.Content.ReadAsStringAsync();
            if (!string.IsNullOrWhiteSpace(responseContent))
            {
                return JsonSerializer.Deserialize<T>(responseContent, _options);
            }
        }

        return new T();
    }

    private async Task<HttpResponseMessage> DoApiCallInternal(string url, HttpMethod method = null, object content = null)
    {
        if (method == null)
        {
            method = HttpMethod.Get;
        }

        using (var client = _httpClientFactory.CreateClient())
        {
            var requestMessage = new HttpRequestMessage(method, url);

            requestMessage.Headers.Add("municipalityNo", _settings.MunicipalityNo);
            requestMessage.Headers.Add("ocp-apim-subscription-key", _settings.SubscriptionKey);

            if (content != null)
            {
                requestMessage.Content = new StringContent(JsonSerializer.Serialize(content, _options), Encoding.UTF8, "application/json");
            }

            return await client.SendAsync(requestMessage);
        }
    }

    #endregion
}
