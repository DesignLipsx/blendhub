using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace BlendHub.Helpers
{
    public class BlenderVersionFetcher
    {
        private static readonly HttpClient _httpClient = new();
        private const string GitHubRawUrl = "https://raw.githubusercontent.com/DesignLipsx/blendhub/master/BlendHub/blender_versions_web.json";
        private const int TimeoutSeconds = 10;

        public BlenderVersionFetcher()
        {
            _httpClient.Timeout = TimeSpan.FromSeconds(TimeoutSeconds);
        }

        /// <summary>
        /// Asynchronously fetches Blender versions JSON from GitHub.
        /// </summary>
        /// <returns>JSON string with version data, or null if fetch fails</returns>
        public async Task<string?> FetchVersionsJsonAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync(GitHubRawUrl);
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadAsStringAsync();
            }
            catch (HttpRequestException ex)
            {
                System.Diagnostics.Debug.WriteLine($"HTTP Error fetching versions: {ex.Message}");
                return null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error fetching versions: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Asynchronously fetches and deserializes Blender versions from GitHub.
        /// </summary>
        /// <typeparam name="T">Type to deserialize into</typeparam>
        /// <returns>Deserialized object or null if fetch fails</returns>
        public async Task<T?> FetchVersionsAsync<T>() where T : class
        {
            try
            {
                var json = await FetchVersionsJsonAsync();
                if (string.IsNullOrEmpty(json))
                    return null;

                return JsonSerializer.Deserialize<T>(json);
            }
            catch (JsonException ex)
            {
                System.Diagnostics.Debug.WriteLine($"JSON Error: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Fetch with fallback to local data if GitHub fetch fails.
        /// </summary>
        /// <param name="fallbackJson">Local JSON fallback data</param>
        /// <returns>JSON string (from GitHub or fallback)</returns>
        public async Task<string?> FetchVersionsWithFallbackAsync(string? fallbackJson)
        {
            var result = await FetchVersionsJsonAsync();
            return result ?? fallbackJson;
        }
    }
}
