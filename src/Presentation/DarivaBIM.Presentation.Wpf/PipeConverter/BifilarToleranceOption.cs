namespace DarivaBIM.Presentation.Wpf.PipeConverter
{
    /// <summary>
    /// Opção de tolerância exposta no ComboBox da UI. Carrega o
    /// <see cref="BifilarToleranceLevel"/> (chave) e o <see cref="DisplayName"/>
    /// localizado (mostrado ao usuário). Existe para a UI poder bindar
    /// diretamente em uma <c>ObservableCollection</c> com
    /// <c>DisplayMemberPath="DisplayName"</c>.
    /// </summary>
    public sealed class BifilarToleranceOption
    {
        public BifilarToleranceOption(BifilarToleranceLevel level, string displayName)
        {
            Level = level;
            DisplayName = displayName;
        }

        public BifilarToleranceLevel Level { get; }
        public string DisplayName { get; }

        public override string ToString() => DisplayName;
    }
}
