using Autodesk.Revit.DB;

namespace DarivaBIM.Revit.Adapters.V2026.Features.FamiliesImporter
{
    /// <summary>
    /// Implementação de <see cref="IFamilyLoadOptions"/> usada pela feature
    /// <c>FamiliesImporterHub</c>: aceita sempre a família encontrada no
    /// projeto e sobrescreve valores de parâmetros, replicando o
    /// comportamento "carregar e atualizar" do script Dynamo original.
    /// </summary>
    public class RevitFamilyLoadOptions : IFamilyLoadOptions
    {
        public bool OnFamilyFound(bool familyInUse, out bool overwriteParameterValues)
        {
            overwriteParameterValues = true;
            return true;
        }

        public bool OnSharedFamilyFound(
            Family sharedFamily,
            bool familyInUse,
            out FamilySource source,
            out bool overwriteParameterValues)
        {
            source = FamilySource.Family;
            overwriteParameterValues = true;
            return true;
        }
    }
}
