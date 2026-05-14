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
        private static readonly HttpClient HttpClient = DarivaBimHttpClientFactory.Create(
            TimeSpan.FromSeconds(10), new Uri(BaseUrl));

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
        };

        public async Task<List<FamilyItem>> GetFamiliesAsync()
        {
            using HttpResponseMessage response = await HttpClient.GetAsync("families");
            response.EnsureSuccessStatusCode();

            string json = await response.Content.ReadAsStringAsync();

            return JsonSerializer.Deserialize<List<FamilyItem>>(json, JsonOptions) ?? new List<FamilyItem>();
        }
    }
}