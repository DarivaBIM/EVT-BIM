using System;
using System.IO;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using DarivaBIM.Infrastructure.Api.Clients;
using DarivaBIM.Infrastructure.Persistence.Cache;
using DarivaBIM.Revit.Adapters.V2026.Filters;
using DarivaBIM.Revit.Adapters.V2026.Mapping;
using DarivaBIM.Revit.Adapters.V2026.Parameters;
using DarivaBIM.Revit.Adapters.V2026.Transactions;
using DarivaBIM.Revit.Adapters.V2026.Writers;
using DarivaBIM.Domain.Tigre;
using DarivaBIM.Application.DTOs.Family;
using DarivaBIM.Application.DTOs.Tigre;
using DarivaBIM.Application.Contracts;

namespace DarivaBIM.Plugin.V2026.ExternalServices
{
    public class ImportFamilyHandler : IExternalEventHandler
    {
        private readonly FamilyCacheService _cacheService = new();
        private readonly FamilyDownloadService _downloadService = new();

        public ImportFamilyRequest? PendingRequest { get; set; }

        public void Execute(UIApplication app)
        {
            if (PendingRequest == null)
            {
                TaskDialog.Show(
                    "FamiliesImporterHub",
                    "Nenhuma requisição de importação foi recebida.");

                return;
            }

            ImportFamilyRequest request = PendingRequest;
            PendingRequest = null;

            try
            {
                UIDocument? uiDoc = app.ActiveUIDocument;

                if (uiDoc == null)
                {
                    TaskDialog.Show(
                        "FamiliesImporterHub",
                        "Não há nenhum documento ativo no Revit para carregar a família.");

                    return;
                }

                Document projectDoc = uiDoc.Document;

                if (projectDoc.IsFamilyDocument)
                {
                    TaskDialog.Show(
                        "FamiliesImporterHub",
                        "A importação está sendo executada em um documento de família.\n\n" +
                        "Abra um projeto do Revit (.rvt) para carregar e posicionar a família nele.");

                    return;
                }

                string cachedFilePath = _downloadService.DownloadToCache(request, _cacheService);

                if (!File.Exists(cachedFilePath))
                {
                    throw new System.IO.FileNotFoundException(
                        "O arquivo baixado não foi encontrado no cache local.",
                        cachedFilePath);
                }

                string actualFamilyName;
                Family? family = OpenAndLoadFamilyFromFile(app, projectDoc, cachedFilePath, request, out actualFamilyName);

                if (family == null)
                {
                    TaskDialog.Show(
                        "FamiliesImporterHub",
                        "O arquivo foi baixado corretamente, mas a família não pôde ser carregada nem localizada no projeto.\n\n" +
                        $"Arquivo em cache:\n{cachedFilePath}\n\n" +
                        $"Nome solicitado pela API: {request.FamilyName}\n" +
                        $"Nome interno detectado: {actualFamilyName}");

                    return;
                }

                FamilySymbol? symbol = GetFirstFamilySymbol(projectDoc, family);

                if (symbol == null)
                {
                    TaskDialog.Show(
                        "FamiliesImporterHub",
                        "A família foi carregada, mas nenhum tipo (FamilySymbol) foi encontrado para posicionamento.\n\n" +
                        $"Família: {family.Name}");

                    return;
                }

                EnsureSymbolIsActive(projectDoc, symbol);

                try
                {
                    uiDoc.PromptForFamilyInstancePlacement(symbol);
                }
                catch (Autodesk.Revit.Exceptions.OperationCanceledException)
                {
                    // Usuário cancelou com ESC.
                    // A família continua carregada no projeto.
                }
            }
            catch (Exception ex)
            {
                TaskDialog.Show(
                    "FamiliesImporterHub",
                    "Não foi possível carregar e iniciar o posicionamento da família.\n\n" +
                    $"Família: {request.FamilyName}\n" +
                    $"URL: {request.DownloadUrl}\n\n" +
                    $"Erro: {ex.Message}");
            }
        }

        public string GetName()
        {
            return "FamiliesImporterHub.ImportFamilyHandler";
        }

        private static Family? OpenAndLoadFamilyFromFile(
            UIApplication app,
            Document projectDoc,
            string cachedFilePath,
            ImportFamilyRequest request,
            out string actualFamilyName)
        {
            actualFamilyName = Path.GetFileNameWithoutExtension(cachedFilePath);
            Document? familyDoc = null;

            try
            {
                familyDoc = app.Application.OpenDocumentFile(cachedFilePath);

                if (familyDoc == null)
                {
                    return FindExistingFamily(projectDoc, request, actualFamilyName);
                }

                if (!familyDoc.IsFamilyDocument)
                {
                    throw new InvalidOperationException(
                        "O arquivo baixado foi aberto, mas não foi reconhecido como documento de família do Revit.");
                }

                if (familyDoc.OwnerFamily != null &&
                    !string.IsNullOrWhiteSpace(familyDoc.OwnerFamily.Name))
                {
                    actualFamilyName = familyDoc.OwnerFamily.Name;
                }

                Family? loadedFamily = familyDoc.LoadFamily(
                    projectDoc,
                    new RevitFamilyLoadOptions());

                if (loadedFamily != null)
                {
                    return loadedFamily;
                }

                return FindExistingFamily(projectDoc, request, actualFamilyName);
            }
            finally
            {
                if (familyDoc != null)
                {
                    try
                    {
                        familyDoc.Close(false);
                    }
                    catch
                    {
                    }
                }
            }
        }

        private static Family? FindExistingFamily(
            Document doc,
            ImportFamilyRequest request,
            string actualFamilyName)
        {
            string requestedName = request.FamilyName?.Trim() ?? string.Empty;
            string fileBaseName = Path.GetFileNameWithoutExtension(request.ResolvedFileName)?.Trim() ?? string.Empty;
            string internalName = actualFamilyName?.Trim() ?? string.Empty;

            return new FilteredElementCollector(doc)
                .OfClass(typeof(Family))
                .Cast<Family>()
                .FirstOrDefault(f =>
                    f.Name.Equals(requestedName, StringComparison.OrdinalIgnoreCase) ||
                    f.Name.Equals(fileBaseName, StringComparison.OrdinalIgnoreCase) ||
                    f.Name.Equals(internalName, StringComparison.OrdinalIgnoreCase));
        }

        private static FamilySymbol? GetFirstFamilySymbol(Document doc, Family family)
        {
            ElementId symbolId = family
                .GetFamilySymbolIds()
                .FirstOrDefault();

            if (symbolId == null || symbolId == ElementId.InvalidElementId)
            {
                return null;
            }

            return doc.GetElement(symbolId) as FamilySymbol;
        }

        private static void EnsureSymbolIsActive(Document doc, FamilySymbol symbol)
        {
            if (symbol.IsActive)
            {
                return;
            }

            using Transaction tx = new Transaction(doc, "Ativar tipo da família");
            tx.Start();

            symbol.Activate();
            doc.Regenerate();

            tx.Commit();
        }
    }
}