using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using DarivaBIM.Application.Common;
using DarivaBIM.Application.DTOs.Family;
using DarivaBIM.Revit.Adapters.Features.FamiliesImporter;

namespace DarivaBIM.Plugin.Features.FamiliesImporter
{
    /// <summary>
    /// Loads a family file into the active Revit project. The handler is
    /// invoked on Revit's UI thread inside <see cref="IExternalEventHandler"/>,
    /// so it must not perform network I/O here — the caller is expected to
    /// download the .rfa to local cache asynchronously and pass the resulting
    /// path through <see cref="PendingRequest"/> + <see cref="PendingLocalFilePath"/>
    /// before raising the event.
    /// </summary>
    public class ImportFamilyHandler : IExternalEventHandler
    {
        public ImportFamilyRequest? PendingRequest { get; set; }
        public string? PendingLocalFilePath { get; set; }

        public void Execute(UIApplication app)
        {
            if (PendingRequest == null || string.IsNullOrWhiteSpace(PendingLocalFilePath))
            {
                TaskDialog.Show(
                    FeatureNames.FamiliesImporter,
                    "Nenhuma requisição de importação preparada foi recebida.");

                return;
            }

            ImportFamilyRequest request = PendingRequest;
            string cachedFilePath = PendingLocalFilePath!;
            PendingRequest = null;
            PendingLocalFilePath = null;

            try
            {
                UIDocument? uiDoc = app.ActiveUIDocument;

                if (uiDoc == null)
                {
                    TaskDialog.Show(
                        FeatureNames.FamiliesImporter,
                        "Não há nenhum documento ativo no Revit para carregar a família.");

                    return;
                }

                Document projectDoc = uiDoc.Document;

                if (projectDoc.IsFamilyDocument)
                {
                    TaskDialog.Show(
                        FeatureNames.FamiliesImporter,
                        "A importação está sendo executada em um documento de família.\n\n" +
                        "Abra um projeto do Revit (.rvt) para carregar e posicionar a família nele.");

                    return;
                }

                View? activeView = projectDoc.ActiveView;

                if (!IsViewSupportedForPlacement(activeView))
                {
                    string viewTypeLabel = activeView?.ViewType.ToString() ?? "desconhecida";
                    string viewNameLabel = string.IsNullOrWhiteSpace(activeView?.Name)
                        ? string.Empty
                        : $" \"{activeView!.Name}\"";

                    TaskDialog.Show(
                        FeatureNames.FamiliesImporter,
                        "Não é possível posicionar famílias na vista atual.\n\n" +
                        $"Vista{viewNameLabel}: {viewTypeLabel}" +
                        (activeView?.IsTemplate == true ? " (template de vista)" : string.Empty) +
                        "\n\n" +
                        "Abra uma vista de planta, corte, elevação, vista 3D ou detalhe " +
                        "e tente novamente.");

                    return;
                }

                if (!File.Exists(cachedFilePath))
                {
                    throw new FileNotFoundException(
                        "O arquivo baixado não foi encontrado no cache local.",
                        cachedFilePath);
                }

                string actualFamilyName;
                Family? family = LoadFamilyFromFile(app, projectDoc, cachedFilePath, request, out actualFamilyName);

                if (family == null)
                {
                    TaskDialog.Show(
                        FeatureNames.FamiliesImporter,
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
                        FeatureNames.FamiliesImporter,
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
                    FeatureNames.FamiliesImporter,
                    "Não foi possível carregar e iniciar o posicionamento da família.\n\n" +
                    $"Família: {request.FamilyName}\n" +
                    $"URL: {request.DownloadUrl}\n\n" +
                    $"Erro: {ex.Message}");
            }
        }

        public string GetName()
        {
            return FeatureNames.FamiliesImporter + ".ImportFamilyHandler";
        }

        // Allowlist instead of denylist: a future ViewType added to the Revit
        // API will fail closed (blocked) rather than silently slipping through
        // and producing a confusing PromptForFamilyInstancePlacement failure.
        private static bool IsViewSupportedForPlacement(View? view)
        {
            if (view == null || view.IsTemplate)
            {
                return false;
            }

            return view.ViewType switch
            {
                ViewType.FloorPlan => true,
                ViewType.CeilingPlan => true,
                ViewType.EngineeringPlan => true,
                ViewType.AreaPlan => true,
                ViewType.Elevation => true,
                ViewType.Section => true,
                ViewType.Detail => true,
                ViewType.ThreeD => true,
                ViewType.DraftingView => true,
                _ => false,
            };
        }

        // Carrega a família a partir do .rfa em cache. Estratégia em camadas:
        //
        // 1) OpenDocumentFile + familyDoc.LoadFamily(projectDoc, options) é
        //    o caminho principal. Foi o caminho original do plugin e
        //    funciona em runtime tanto pra famílias frescas quanto pra
        //    upgrade de versão (v2023 → v2025 etc.).
        //    No debugger com "break on NullReferenceException" ligado,
        //    o OpenDocumentFile dispara 3+ first-chance NREs internas da
        //    RevitAPI (resolução de path/versão) — são capturadas dentro
        //    do próprio Revit e não vazam pra cá. Pra evitar pausas no
        //    debug, desligue "Common Language Runtime Exceptions →
        //    System.NullReferenceException" em Debug → Windows →
        //    Exception Settings. Em release/uso normal NÃO acontece nada.
        //
        // 2) Caso o passo 1 não devolva a família (ex.: load no-op porque
        //    a mesma .rfa já está no projeto na mesma versão), tiramos
        //    snapshot dos Family.Id antes do load e procuramos um id
        //    novo pra cobrir o bug clássico do out Family ficar null.
        //
        // 3) Última cartada: matching tolerante de nome (normalizado, sem
        //    prefixos/sufixos comuns) — pra evitar que o usuário veja o
        //    dialog "não foi localizada" quando a família claramente
        //    existe sob algum nome próximo.
        //
        // O overload <c>Document.LoadFamily(string, options, out Family)</c>
        // (sem abrir antes como Document) NÃO é confiável: em Revit 2025
        // ele retorna sem exceção mas com <c>family == null</c> e nenhum
        // id novo para famílias frescas vindas de versões anteriores —
        // exatamente o caso de produção. Por isso voltamos pro caminho
        // mais verboso porém estável.
        private static Family? LoadFamilyFromFile(
            UIApplication app,
            Document projectDoc,
            string cachedFilePath,
            ImportFamilyRequest request,
            out string actualFamilyName)
        {
            actualFamilyName = Path.GetFileNameWithoutExtension(cachedFilePath);

            HashSet<long> existingIds = SnapshotFamilyIds(projectDoc);

            // Caminho principal: abre o .rfa como Document e carrega no
            // projeto. Esse fluxo dispara as NREs first-chance internas do
            // Revit no debugger, mas é o único que carrega corretamente
            // famílias frescas com upgrade de versão.
            Document? familyDoc = null;
            try
            {
                familyDoc = app.Application.OpenDocumentFile(cachedFilePath);

                if (familyDoc != null && familyDoc.IsFamilyDocument)
                {
                    if (familyDoc.OwnerFamily != null &&
                        !string.IsNullOrWhiteSpace(familyDoc.OwnerFamily.Name))
                    {
                        actualFamilyName = familyDoc.OwnerFamily.Name;
                    }

                    Family? loaded = familyDoc.LoadFamily(
                        projectDoc,
                        new RevitFamilyLoadOptions());

                    if (loaded != null)
                        return loaded;
                }
            }
            catch
            {
                // Fluxo cai pros fallbacks de busca abaixo.
            }
            finally
            {
                if (familyDoc != null)
                {
                    try { familyDoc.Close(false); }
                    catch { }
                }
            }

            // Caminho 2: o load foi efetivado mas familyDoc.LoadFamily
            // devolveu null (raro mas acontece). Diff dos Family.Id acha
            // a recém-carregada.
            Family? family = FindNewlyLoadedFamily(projectDoc, existingIds);
            if (family != null)
            {
                actualFamilyName = family.Name;
                return family;
            }

            // Caminho 3: load foi no-op porque a família já estava no
            // projeto na mesma versão. Busca por nome — primeiro exato,
            // depois normalizado. Devolve a família existente pra
            // continuar o fluxo de posicionamento.
            family = FindExistingFamily(projectDoc, request, actualFamilyName);
            if (family != null)
            {
                actualFamilyName = family.Name;
                return family;
            }

            return null;
        }

        private static HashSet<long> SnapshotFamilyIds(Document doc)
        {
            HashSet<long> ids = new();
            foreach (Element e in new FilteredElementCollector(doc).OfClass(typeof(Family)))
                ids.Add(e.Id.Value);
            return ids;
        }

        private static Family? FindNewlyLoadedFamily(Document doc, HashSet<long> existingIds)
        {
            return new FilteredElementCollector(doc)
                .OfClass(typeof(Family))
                .Cast<Family>()
                .FirstOrDefault(f => !existingIds.Contains(f.Id.Value));
        }

        // Busca uma família já presente no projeto que case com o que o
        // usuário pediu. Estratégia em camadas, da mais restritiva para a
        // mais tolerante:
        //   1) Match exato (case-insensitive) com algum dos candidatos
        //      (request.FamilyName, basename do .rfa, nome calculado do
        //      arquivo em cache).
        //   2) Match em forma normalizada — só letras+dígitos, lowercase.
        //      Cobre divergências de "/" vs "-", "DN" vs sem prefix,
        //      "CX_" no nome de arquivo vs ausente no nome de domínio,
        //      sufixos como " - Tigre", etc.
        //   3) Substring normalizada — última cartada para evitar mostrar
        //      ao usuário o erro "não foi localizada no projeto" quando a
        //      família claramente existe.
        private static Family? FindExistingFamily(
            Document doc,
            ImportFamilyRequest request,
            string actualFamilyName)
        {
            string[] candidates = new[]
                {
                    request.FamilyName?.Trim(),
                    Path.GetFileNameWithoutExtension(request.ResolvedFileName)?.Trim(),
                    actualFamilyName?.Trim(),
                }
                .Where(s => !string.IsNullOrEmpty(s))
                .Cast<string>()
                .ToArray();

            if (candidates.Length == 0)
                return null;

            List<Family> all = new FilteredElementCollector(doc)
                .OfClass(typeof(Family))
                .Cast<Family>()
                .ToList();

            // 1) Exato (case-insensitive).
            foreach (string c in candidates)
            {
                Family? hit = all.FirstOrDefault(f =>
                    f.Name.Equals(c, StringComparison.OrdinalIgnoreCase));
                if (hit != null) return hit;
            }

            // 2) Normalizado (alfanumérico, lower).
            string[] normalizedCandidates = candidates
                .Select(NormalizeForMatch)
                .Where(s => s.Length > 0)
                .ToArray();

            foreach (string nc in normalizedCandidates)
            {
                Family? hit = all.FirstOrDefault(f =>
                    NormalizeForMatch(f.Name) == nc);
                if (hit != null) return hit;
            }

            // 3) Substring normalizada (ambas direções).
            foreach (string nc in normalizedCandidates)
            {
                Family? hit = all.FirstOrDefault(f =>
                {
                    string nf = NormalizeForMatch(f.Name);
                    return nf.Length > 0 &&
                           (nf.Contains(nc) || (nc.Length > nf.Length && nc.Contains(nf)));
                });
                if (hit != null) return hit;
            }

            return null;
        }

        private static string NormalizeForMatch(string s)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            StringBuilder sb = new(s.Length);
            foreach (char ch in s)
            {
                if (char.IsLetterOrDigit(ch))
                    sb.Append(char.ToLowerInvariant(ch));
            }
            return sb.ToString();
        }

        private static FamilySymbol? GetFirstFamilySymbol(Document doc, Family family)
        {
            ElementId? symbolId = family
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