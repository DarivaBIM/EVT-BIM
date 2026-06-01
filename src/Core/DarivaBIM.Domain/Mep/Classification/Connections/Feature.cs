using System;

namespace DarivaBIM.Domain.Mep.Classification.Connections
{
    /// <summary>
    /// Caracteristicas adicionais ortogonais ao BaseKind: rosca, bucha de
    /// latao, tampa de inspecao, etc. Flags pq uma mesma peca pode acumular
    /// (joelho rosca + bucha latao = ThreadedEnd | BrassBushing).
    /// </summary>
    [Flags]
    public enum Feature
    {
        None = 0,
        ThreadedEnd = 1 << 0,
        BrassBushing = 1 << 1,
        Inspection = 1 << 2,
        VisitCap = 1 << 3,
        SlidingSleeve = 1 << 4,
        Inverted = 1 << 5,
        Reduced = 1 << 6,
        BellAndSpigot = 1 << 7,
        MaleEnd = 1 << 8,
        FemaleEnd = 1 << 9,
        FlangedEnd = 1 << 10,
        SocketEnd = 1 << 11,
    }
}
