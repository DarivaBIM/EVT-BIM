namespace DarivaBIM.Domain.Mep.Classification.Ports
{
    /// <summary>
    /// Geometria da secao do conector. Modelado como enum (e nao bool round/
    /// rect) porque o catalogo hidrossanitario admite secao oval em casos
    /// raros e perda de carga varia por formato. Vide secao 6 do rulebook.
    /// </summary>
    public enum ConnectorShape
    {
        Unknown = 0,
        Round = 1,
        Rectangular = 2,
        Oval = 3,
    }
}
