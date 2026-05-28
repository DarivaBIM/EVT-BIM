namespace DarivaBIM.Domain.Mep.Classification.Connections
{
    /// <summary>
    /// Codigo discreto de cada problema detectavel durante a leitura de
    /// topologia MEP. Existe como enum (e nao string) para permitir match
    /// estavel em testes e em filtros de UI sem depender de texto livre;
    /// vide secao 8 do mep-connection-rulebook.
    /// </summary>
    public enum TopologyDiagnosticCode
    {
        // elemento nao tem MEPModel
        NoMepModel,
        NoConnectorManager,
        NonPhysicalConnectorSkipped,
        // conector Electrical num PipeFitting
        DomainMismatch,
        // Shape != Round em hidraulica
        NonRoundConnectorIgnored,
        // conector sem Radius/Width
        MissingDiameter,
        // BasisZ degenerado/zero
        BasisZIncoherent,
        OriginOutsideExpectedIntersection,
        // sem fluxo identificavel
        NoConnectedPipes,
        // exige inferencia por geometria
        PartTypeUndefined,
        // PartType=Tee mas geometria sugere Wye
        PartTypeMismatchInferred,
        // PartType=Tee mas so 2 conectores fisicos
        InsufficientConnectorsAfterFilter,
    }
}
