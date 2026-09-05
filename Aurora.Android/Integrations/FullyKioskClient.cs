using System.Net.Http.Json;

namespace Aurora.Android.Integrations;

public sealed class FullyKioskClient(HttpClient http)
{
    public async Task<string?> GetDeviceInfoAsync(Uri baseUri, CancellationToken cancellationToken = default)
    {
        using var response = await http.GetAsync(new Uri(baseUri, "/api/getDeviceInfo"), cancellationToken);
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadAsStringAsync(cancellationToken);
    }

    public async Task<bool> IsReachableAsync(Uri baseUri, CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await http.GetAsync(new Uri(baseUri, "/api/getDeviceInfo"), cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch { return false; }
    }
}
