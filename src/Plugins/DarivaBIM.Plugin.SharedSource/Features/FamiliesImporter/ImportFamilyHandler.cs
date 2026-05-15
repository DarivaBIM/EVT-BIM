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

        // Sinalizado em todos os caminhos de saída do Execute (sucesso,
        // erro, cancelamento, validação). Permite à UI WPF saber quando
        // o handler terminou para limpar o flag de re-entrância — sem
        // isso o usuário pode disparar imports concorrentes enfileirando
        // ExternalEvents enquanto o Revit ainda processa o anterior.
        public Action? Completed { get; set; }

        public void Execute(UIApplication app)
        {
            try
            {
                ExecuteCore(app);
            }
            finally
            {
                try { Completed?.Invoke(); }
                catch { /* nada útil a fazer se a callback explodir */ }
            }
        }

        private void ExecuteCore(UIApplication app)
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

                // PostRequestForElementTypePlacement em vez de
                // PromptForFamilyInstancePlacement.
                //
                // A diferença é arquitetural — e crítica pra UIs hospedadas
                // em DockablePane:
                //
                //   • PromptForFamilyInstancePlacement executa IMEDIATAMENTE
                //     dentro do contexto API atual e abre um nested message
                //     pump próprio do Revit. Esse pump aninhado é o que
                //     causava o freeze da DockablePane no Revit 2025/2026:
                //     o wrapper de docking interno do Revit fica em estado
                //     de composição stale e só WM_SIZE em cascata (= o
                //     usuário maximizar a janela do Revit) o destrava.
                //
                //   • PostRequestForElementTypePlacement NÃO executa
                //     imediatamente — coloca a request na fila de comandos
                //     do Revit e retorna na hora. A interação de placement
                //     acontece DEPOIS, quando o controle volta ao loop
                //     principal do Revit. Não há nested message pump
                //     dentro do nosso ExternalEvent, então a DockablePane
                //     continua totalmente responsiva durante o placement.
                //
                // Doc oficial (https://www.revitapidocs.com/2026/f9bf4ed3-0354-6bc1-6db3-e34fcbace950.htm):
                //   "This does not execute immediately, but instead when
                //    control returns to Revit from the current API context."
                //
                // Limitação: a API não notifica quando o usuário termina o
                // placement. Pra nosso caso isso não importa — só queremos
                // entregar a família ao cursor; ESC/cancelar é gestão
                // normal do Revit. Caso futuro precise saber dos elementos
                // criados, subscrever em DocumentChanged é o caminho.
                uiDoc.PostRequestForElementTypePlacement(symbol);
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

        // Carrega a família a partir do .rfa em cache. Estratégia em
        // camadas, da mais barata pra mais cara:
        //
        // 1) <c>projectDoc.LoadFamily(path, options, out family)</c> — o
        //    overload "direto" do Revit. Quando funciona é instantâneo:
        //    sem abrir o .rfa como Document, sem ciclo open/close, sem as
        //    NREs first-chance internas que o OpenDocumentFile dispara no
        //    debugger.
        //
        // 2) Snapshot dos Family.Id antes do load + diff depois — cobre o
        //    bug clássico do overload acima retornar sem exceção mas com
        //    <c>family == null</c> mesmo tendo carregado de fato (visto
        //    em Revit 2025 com .rfa de versões anteriores).
        //
        // 3) Matching tolerante de nome — DEPOIS do typed overload mas
        //    ANTES do OpenDocumentFile. Quando o usuário re-clica numa
        //    família que já está no projeto (caso comum: inserir várias
        //    instâncias do mesmo tipo), esta camada acha a família
        //    existente e PULA inteiramente o caminho pesado. Sem isso,
        //    todo re-clique pagava o custo de OpenDocumentFile só pra
        //    descobrir no fim que a família já existia.
        //
        // 4) <c>Application.OpenDocumentFile + familyDoc.LoadFamily</c> —
        //    caminho pesado mas confiável pra .rfa que precisam upgrade
        //    silencioso de versão. Síncrono no UI thread, dispara as 3+
        //    NREs first-chance no debugger (capturadas dentro do Revit).
        //    Pra evitar pausas no debug, desligue
        //    "System.NullReferenceException" em
        //    Debug → Windows → Exception Settings.
        //
        // 5) Snapshot diff pós-OpenDocumentFile — último socorro caso o
        //    overload do familyDoc também devolva null.
        private static Family? LoadFamilyFromFile(
            UIApplication app,
            Document projectDoc,
            string cachedFilePath,
            ImportFamilyRequest request,
            out string actualFamilyName)
        {
            actualFamilyName = Path.GetFileNameWithoutExtension(cachedFilePath);

            HashSet<long> existingIds = SnapshotFamilyIds(projectDoc);

            // Camada 1: overload direto. Caminho rápido.
            Family? family = null;
            try
            {
                projectDoc.LoadFamily(cachedFilePath, new RevitFamilyLoadOptions(), out family);
            }
            catch
            {
                // Erro real (path inválido, .rfa corrompido) — cai pros fallbacks.
            }

            if (family != null)
            {
                actualFamilyName = family.Name;
                return family;
            }

            // Camada 2: o load pode ter sucedido mas devolvido out null.
            family = FindNewlyLoadedFamily(projectDoc, existingIds);
            if (family != null)
            {
                actualFamilyName = family.Name;
                return family;
            }

            // Camada 3: família já no projeto. ESSE atalho economiza o
            // OpenDocumentFile inteiro quando o usuário re-clica numa
            // família já carregada — caso comum quando ele quer inserir
            // várias instâncias do mesmo tipo em sequência. Antes, esse
            // caminho só era atingido depois do open/close cycle, o que
            // significava 3 NREs no debugger + segundos de UI travada
            // por clique repetido. Agora, basta haver uma família com
            // nome compatível pra que pulemos direto pra ela.
            family = FindExistingFamily(projectDoc, request, actualFamilyName);
            if (family != null)
            {
                actualFamilyName = family.Name;
                return family;
            }

            // Camada 4: caminho pesado via OpenDocumentFile. Só roda em
            // fresh load real, quando nenhuma família compatível existe
            // ainda no projeto. existingIds segue válido (nada mudou
            // desde a camada 1) — sem segundo snapshot.
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
                // Fluxo cai pro último snapshot diff abaixo.
            }
            finally
            {
                if (familyDoc != null)
                {
                    try { familyDoc.Close(false); }
                    catch { }
                }
            }

            // Camada 5: snapshot diff pós-OpenDocumentFile pra cobrir o
            // caso de familyDoc.LoadFamily ter devolvido null mesmo tendo
            // carregado. Usa o MESMO existingIds da camada 2 — só agora
            // a comparação inclui qualquer família que o OpenDocumentFile
            // tenha adicionado.
            family = FindNewlyLoadedFamily(projectDoc, existingIds);
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