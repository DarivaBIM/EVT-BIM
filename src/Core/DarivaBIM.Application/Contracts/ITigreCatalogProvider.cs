using DarivaBIM.Domain.Tigre;

namespace DarivaBIM.Application.Contracts
{
    /// <summary>
    /// Provides the in-memory Tigre catalog. Implementations may load from a
    /// JSON file, an embedded resource or a remote API.
    /// </summary>
    public interface ITigreCatalogProvider
    {
        TigreCatalog Load();
    }
}
