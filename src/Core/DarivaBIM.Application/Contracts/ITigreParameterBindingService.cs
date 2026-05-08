using DarivaBIM.Application.DTOs.Tigre;

namespace DarivaBIM.Application.Contracts
{
    /// <summary>
    /// Garante que o shared parameter Tigre: Código exista no projeto e
    /// esteja vinculado às tubulações como parâmetro de instância. Operação
    /// idempotente — chamar novamente em um projeto já configurado é seguro
    /// e barato.
    /// </summary>
    public interface ITigreParameterBindingService
    {
        TigreEnsureParameterResult Ensure();
    }
}
