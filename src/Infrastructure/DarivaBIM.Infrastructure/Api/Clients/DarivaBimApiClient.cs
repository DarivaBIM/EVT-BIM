using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using DarivaBIM.Application.DTOs.Family;

namespace DarivaBIM.Infrastructure.Api.Clients
{
    public class ApiClient
    {
        private const string BaseUrl = "https://darivabim.link/api/";
        private static readonly HttpClient HttpClient = CreateHttpClient();

        private static HttpClient CreateHttpClient()
        {
            var client = new HttpClient
            {
                BaseAddress = new Uri(BaseUrl),
                Timeout = TimeSpan.FromSeconds(10)
            };

            client.DefaultRequestHeaders.Add("User-Agent", "FamiliesImporterHub");

            return client;
        }

        public async Task<List<FamilyItem>> GetFamiliesAsync()
        {
            using HttpResponseMessage response = await HttpClient.GetAsync("families");
            response.EnsureSuccessStatusCode();

            string json = await response.Content.ReadAsStringAsync();

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            return JsonSerializer.Deserialize<List<FamilyItem>>(json, options) ?? new List<FamilyItem>();
        }
    }
}