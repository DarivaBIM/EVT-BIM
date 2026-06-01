namespace DarivaBIM.Domain.Mep.Classification.Connections
{
    /// <summary>
    /// Subkind geometrico que refina o <see cref="BaseKind"/>. Default
    /// <see cref="Unspecified"/> permite construir uma identity parcial quando
    /// a geometria nao acrescenta informacao (Cap, MultiPort, etc).
    /// </summary>
    public enum GeometryKind
    {
        Unspecified = 0,
        ShortRadius = 1,
        LongRadius = 2,
        Offset = 3,
        SShape = 4,
        Straight = 5,
        Branch = 6,
        Multi = 7,
    }
}
