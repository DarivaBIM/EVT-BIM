using System;
using System.Net.Http;
using DarivaBIM.Application.Common;

namespace DarivaBIM.Infrastructure.Api.Clients
{
    /// <summary>
    /// Cria <see cref="HttpClient"/> com o User-Agent padronizado
    /// (<see cref="FeatureNames.FamiliesImporter"/>) e timeout configurável.
    /// Usado pelos clients do diretório <c>Api/Clients/</c> para evitar
    /// repetição do boilerplate de cabeçalhos. Cada caller mantém a sua
    /// instância estática — não fazemos pooling aqui.
    /// </summary>
    internal static class DarivaBimHttpClientFactory
    {
        public static HttpClient Create(TimeSpan timeout, Uri? baseAddress = null)
        {
            var client = new HttpClient
            {
                Timeout = timeout,
            };

            if (baseAddress != null)
                client.BaseAddress = baseAddress;

            client.DefaultRequestHeaders.Add("User-Agent", FeatureNames.FamiliesImporter);

            return client;
        }
    }
}
