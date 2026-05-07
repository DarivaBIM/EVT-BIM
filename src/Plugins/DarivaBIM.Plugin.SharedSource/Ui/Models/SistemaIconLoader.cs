using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;

namespace DarivaBIM.Plugin.Ui.Models
{
    /// <summary>
    /// Catálogo de ícones vetoriais dos 14 sistemas hidráulicos. As
    /// geometrias foram extraídas dos SVGs em <c>Resources/FilterIcons/</c>
    /// (viewBox 64×64, stroke-width 3, line-cap/join round) e materializadas
    /// como <see cref="DrawingImage"/> congelados em código — sem dependência
    /// de pack URI, ResourceDictionary externo ou IO de disco.
    ///
    /// Vantagens vs. PNG:
    ///  - Sem perda de nitidez em qualquer DPI; o WPF rasteriza sob demanda
    ///    para o tamanho real do <c>Image</c>.
    ///  - Sem cache de bitmap por tamanho lógico → menos memória.
    ///  - Brushes pré-frozen, reusados entre todas as instâncias do mesmo
    ///    sistema (uma única alocação de Pen/SolidColorBrush por sistema).
    ///
    /// O método <see cref="Load(string?)"/> aceita o nome de arquivo histórico
    /// (<c>"agua_fria.png"</c>, <c>"agua_fria.svg"</c> ou <c>"agua_fria"</c>)
    /// e devolve sempre o <see cref="DrawingImage"/> correspondente, com
    /// fallback null caso a chave não exista no catálogo.
    /// </summary>
    public static class SistemaIconLoader
    {
        // Cor stroke padrão do SVG (espelha sistema.ColorHex). Extraída do
        // próprio SVG no momento da conversão para preservar fidelidade
        // visual sem precisar refletir sobre o catálogo aqui.
        private static readonly Dictionary<string, ImageSource> Cache = BuildCatalog();

        /// <param name="iconFileName">
        /// Nome de arquivo do catálogo (com ou sem extensão). Mantida a
        /// compatibilidade com o campo <c>Sistema.IconFileName</c>.
        /// </param>
        public static ImageSource? Load(string? iconFileName)
        {
            if (string.IsNullOrEmpty(iconFileName))
            {
                return null;
            }

            string key = StripExtension(iconFileName!);

            return Cache.TryGetValue(key, out ImageSource? image) ? image : null;
        }

        private static string StripExtension(string fileName)
        {
            int dotIndex = fileName.LastIndexOf('.');
            return dotIndex > 0 ? fileName.Substring(0, dotIndex) : fileName;
        }

        private static Dictionary<string, ImageSource> BuildCatalog()
        {
            var dict = new Dictionary<string, ImageSource>(StringComparer.OrdinalIgnoreCase);

            // Água fria — gota com reflexo (mesma silhueta da água quente,
            // troca apenas a cor do stroke).
            const string DropPath =
                "M38 53C38 53 42.1903 51.0479 44 48C45.8097 44.9521 45.5 41 45.5 41" +
                "M32 2C32 2 15.4328 24.1053 12.1194 33.5789C8.80597 43.0526 12.1193 62 32 62" +
                "C51.8807 62 55.194 43.0526 51.8806 33.5789C48.5671 24.1053 32 2 32 2Z";

            dict["agua_fria"] = MakeStroked("#1565C0", DropPath);
            dict["agua_quente"] = MakeStroked("#D84343", DropPath);

            // Pluvial — nuvem + linhas de chuva.
            dict["pluvial"] = MakeStroked("#5E60CE",
                "M14.2726 42H47.4707C55.495 42 62 35.495 62 27.4707V26.9317C62 16.6414 51.384 9.7772 42 14" +
                "C40.8459 7.07534 34.756 2 27.7358 2C19.6629 2 13 8.54439 13 16.6173V17.5" +
                "C6.84571 17.7797 2 22.8507 2 29.0114V29.7274C2 36.5054 7.49462 42 14.2726 42Z",
                "M21.5 52.5L25.5 46.5M9.5 52.5L13.5 46.5M33.5 52.5L37.5 46.5M45.5 52.5L49.5 46.5" +
                "M23.5 61.5L27.5 55.5M11.5 61.5L15.5 55.5M35.5 61.5L39.5 55.5M47.5 61.5L51.5 55.5");

            // Esgoto — sifão.
            dict["esgoto"] = MakeStroked("#2E7D32",
                "M4 13V41.5C4 50.0604 10.9396 57 19.5 57C28.0604 57 35 50.0604 35 41.5V35" +
                "C35 32.7909 36.7909 31 39 31H56M4 13H14M4 13H2V7H16V13H14" +
                "M56 31V21M56 31C56 33.2091 57.7909 35 60 35H62V17H60C57.7909 17 56 18.7909 56 21" +
                "M56 21C47.5993 21 43.1893 21 38.0007 21C30.821 21 25 26.8203 25 34V41.5" +
                "C25 44.5376 22.5376 47 19.5 47C16.4624 47 14 44.5376 14 41.5V13");

            // Combate a incêndio — hidrante.
            dict["combate_a_incendio"] = MakeStroked("#B71C1C",
                "M20 57.5372H13V62H51V57.5372H44M20 57.5372V24.8099M20 57.5372H44M20 24.8099H44" +
                "M20 24.8099H18V18.8595H21M44 24.8099V57.5372M44 24.8099H46V18.8595H43" +
                "M21 18.8595H43M21 18.8595C21 12.8346 25.9751 7.95041 32 7.95041C38.0249 7.95041 43 12.8346 43 18.8595",
                "M20 44.6446H16V42.6612M20 28.7769H16V30.7603M16 42.6612H12V30.7603H16M16 42.6612V30.7603",
                "M44 44.6446H48V42.6612M44 28.7769H48V30.7603M48 42.6612H52V30.7603H48M48 42.6612V30.7603",
                "M29 7.95041V5C29 3.34315 30.3431 2 32 2C33.6569 2 35 3.34315 35 5V7.95041",
                "M26 57.5372V47.6198M32 57.5372V49.6033M38 57.5372V47.6198",
                "M34.5 32.7438H29.5L27.5 36.7107L29.5 40.6777H34.5L36.5 36.7107L34.5 32.7438Z",
                Ellipse(32, 36.7107, 8, 7.93388));

            // Piscina — escada + ondas.
            dict["piscina"] = MakeStroked("#039BE5",
                "M2 44C3.23043 43.3848 4.70014 43.5001 5.81956 44.2997L7.5 45.5C9.5937 46.9955 12.4063 46.9955 14.5 45.5" +
                "C16.5937 44.0045 19.4063 44.0045 21.5 45.5C23.5937 46.9955 26.4063 46.9955 28.5 45.5" +
                "C30.5937 44.0045 33.4063 44.0045 35.5 45.5C37.5937 46.9955 40.4063 46.9955 42.5 45.5" +
                "C44.5937 44.0045 47.4063 44.0045 49.5 45.5C51.5937 46.9955 54.4063 46.9955 56.5 45.5" +
                "L58.1804 44.2997C59.2999 43.5001 60.7696 43.3848 62 44",
                "M2 56C3.23043 55.3848 4.70014 55.5001 5.81956 56.2997L7.5 57.5C9.5937 58.9955 12.4063 58.9955 14.5 57.5" +
                "C16.5937 56.0045 19.4063 56.0045 21.5 57.5C23.5937 58.9955 26.4063 58.9955 28.5 57.5" +
                "C30.5937 56.0045 33.4063 56.0045 35.5 57.5C37.5937 58.9955 40.4063 58.9955 42.5 57.5" +
                "C44.5937 56.0045 47.4063 56.0045 49.5 57.5C51.5937 58.9955 54.4063 58.9955 56.5 57.5" +
                "L58.1804 56.2997C59.2999 55.5001 60.7696 55.3848 62 56",
                "M23 40V30.5M35.6667 11.2286V8.33333C35.6667 4.83553 32.8311 2 29.3333 2C25.8355 2 23 4.83553 23 8.33333V21" +
                "M48.3333 40V30.5M61 11.2286V8.33333C61 4.83553 58.1645 2 54.6667 2C51.1689 2 48.3333 4.83553 48.3333 8.33333V21" +
                "M23 21L48.3333 21M23 21V30.5M48.3333 21V30.5M23 30.5L48.3333 30.5");

            // Irrigação — folhas/gotas em vaso. SVG original usa <mask> para
            // pintar a "tigela" — aqui simplificamos para stroke puro,
            // preservando a silhueta sem o preenchimento mascarado.
            dict["irrigacao"] = MakeStroked("#6B8E23",
                "M1 37V33H13.9538C15.9714 33 17.9758 33.3253 19.8899 33.9633" +
                "C23.85 35.2833 27.2571 37.8857 29.5726 41.3589L32 45L34.9662 40.5507" +
                "C36.9387 37.5919 39.7915 35.3282 43.121 34.0796C45.0248 33.3657 47.0415 33 49.0748 33H63V37" +
                "C63 46.9411 54.9411 55 45 55H32H19C9.05887 55 1 46.9411 1 37Z",
                "M29.5 63V54M34.5 63V54",
                "M12.1786 18.3C12.1786 18.3 13.4756 17.6818 14.0357 16.7167C14.5959 15.7515 14.5 14.5 14.5 14.5" +
                "M11.5 3C11.5 3 6.37207 10 5.34648 13C4.32089 16 5.34646 22 11.5 22C17.6536 22 18.6791 16 17.6535 13" +
                "C16.6279 10 11.5 3 11.5 3Z",
                "M53.1786 17.3C53.1786 17.3 54.4756 16.6818 55.0357 15.7167C55.5959 14.7515 55.5 13.5 55.5 13.5" +
                "M52.5 2C52.5 2 47.3721 9 46.3465 12C45.3209 15 46.3465 21 52.5 21C58.6536 21 59.6791 15 58.6535 12" +
                "C57.6279 9 52.5 2 52.5 2Z",
                "M32.6143 27.36C32.6143 27.36 34.1707 26.6182 34.8429 25.46C35.515 24.3018 35.4 22.8 35.4 22.8" +
                "M31.8 9C31.8 9 25.6465 17.4 24.4158 21C23.1851 24.6 24.4158 31.8 31.8 31.8" +
                "C39.1843 31.8 40.4149 24.6 39.1842 21C37.9535 17.4 31.8 9 31.8 9Z");

            // Reservatório — caixa d'água.
            dict["reservatorio"] = MakeStroked("#0E7490",
                "M55.5794 47.1531L54.2938 59.212C54.1854 60.2288 53.3276 61 52.3051 61H34.9524" +
                "M55.5794 47.1531H8.42063M55.5794 47.1531C56.143 47.1531 56.6198 46.7363 56.6952 46.1778L60.5 18" +
                "M8.42063 47.1531L9.70621 59.212C9.8146 60.2288 10.6724 61 11.6949 61H29.0476" +
                "M8.42063 47.1531C7.85702 47.1531 7.38024 46.7363 7.30482 46.1778L3.5 18" +
                "M3.5 18H2C1.72386 18 1.5 17.7761 1.5 17.5V13.9388C1.5 13.6626 1.72386 13.4388 2 13.4388H2.47619" +
                "M3.5 18H29.0476M60.5 18H62C62.2761 18 62.5 17.7761 62.5 17.5V13.9388C62.5 13.6626 62.2761 13.4388 62 13.4388H61.5238" +
                "M60.5 18H34.9524" +
                "M2.47619 13.4388L6.02897 9.09194C6.27917 8.78583 6.61447 8.56073 6.99254 8.44509L27.7775 2.08747" +
                "C27.9671 2.02948 28.1643 2 28.3625 2H30M2.47619 13.4388H30" +
                "M61.5238 13.4388L57.971 9.09194C57.7208 8.78583 57.3855 8.56073 57.0075 8.44509L36.2225 2.08747" +
                "C36.0329 2.02948 35.8357 2 35.6375 2H34M61.5238 13.4388H34" +
                "M30 13.4388V2M30 13.4388H34M30 2H34M34 13.4388V2" +
                "M29.0476 61V57.5816C29.0476 56.4771 29.943 55.5816 31.0476 55.5816H32.9524" +
                "C34.0569 55.5816 34.9524 56.4771 34.9524 57.5816V61M29.0476 61H34.9524" +
                "M29.0476 18V21.4184C29.0476 22.5229 29.943 23.4184 31.0476 23.4184H32.9524" +
                "C34.0569 23.4184 34.9524 22.5229 34.9524 21.4184V18M29.0476 18H34.9524");

            // Bombas — bomba centrífuga.
            dict["bombas"] = MakeStroked("#EF6C00",
                "M23.3922 28.4971L19.0377 25.6282C18.4204 25.2215 17.9685 24.6079 17.7631 23.8977" +
                "C17.419 22.7073 17.8156 21.4263 18.7721 20.6385L19.2967 20.2065C19.6639 19.9041 20.0804 19.6673 20.528 19.5064" +
                "L21.2156 19.2593C23.4459 18.4579 25.9117 18.6424 27.9978 19.7669L28.4035 19.9856" +
                "C29.1893 20.4092 29.9107 20.9429 30.5457 21.5704L32.2438 23.2486H30.1991" +
                "C29.7939 23.2486 29.3907 23.3053 29.0012 23.4171L28.5728 23.54" +
                "C27.4782 23.8542 26.6059 24.683 26.2364 25.7602C26.1114 26.1244 26.0477 26.5067 26.0477 26.8918V27.6224",
                "M28.7031 29.4263L33.3769 27.1425C34.0535 26.8119 34.8253 26.7295 35.5549 26.91" +
                "C36.769 27.2103 37.6881 28.1906 37.9019 29.4131L38.0138 30.0532" +
                "C38.0967 30.5273 38.0957 31.0126 38.0109 31.4872L37.8882 32.1736" +
                "C37.4751 34.4851 36.0757 36.5121 34.0495 37.7339L33.6454 37.9775" +
                "C32.8885 38.4339 32.07 38.7806 31.2149 39.007L28.8768 39.6262L29.8948 37.8837" +
                "C30.1002 37.532 30.2542 37.1534 30.3523 36.7593L30.4486 36.3717" +
                "C30.7258 35.2572 30.4367 34.0816 29.6743 33.2232C29.421 32.9379 29.1223 32.6957 28.7899 32.506L28.142 32.1363",
                "M24.6246 33.4485L24.289 38.5904C24.2405 39.3348 23.9268 40.0366 23.4038 40.5707" +
                "C22.5336 41.4596 21.215 41.7561 20.0368 41.3279L19.4199 41.1036" +
                "C18.9629 40.9375 18.5382 40.694 18.1647 40.3841L17.6245 39.9359" +
                "C15.8055 38.4266 14.7289 36.2154 14.6713 33.8704L14.6599 33.4028" +
                "C14.6384 32.5268 14.7438 31.6529 14.9729 30.8078L15.5994 28.4971L16.6174 30.2396" +
                "C16.8229 30.5913 17.0776 30.9124 17.374 31.1934L17.6655 31.4697" +
                "C18.5036 32.2641 19.6783 32.6045 20.8117 32.3813C21.1884 32.3071 21.55 32.1725 21.8824 31.9829L22.5303 31.6132",
                "M56 8V26M56 8V7C56 6.44772 56.4477 6 57 6H61C61.5523 6 62 6.44772 62 7V27" +
                "C62 27.5523 61.5523 28 61 28H57C56.4477 28 56 27.5523 56 27V26M56 8H24" +
                "C11.8497 8 2 17.8497 2 30C2 42.1503 11.8497 52 24 52C36.1503 52 46 42.1503 46 30L45.9293 26H56" +
                "M12.2756 41C9.60885 38.0351 8 34.195 8 30C8 20.6112 16.0589 13 26 13",
                "M14 50L7 60H41L34 50",
                Ellipse(25.6051, 30.684, 3.09805, 3.06166));

            // Válvula — registro.
            dict["valvula"] = MakeStroked("#00796B",
                "M2 54H10V36H2V54Z",
                "M54 54H62V36H54V54Z",
                "M10 39H20L20.5275 38.367C21.8127 36.8248 23.5323 35.7051 25.4625 35.1536L26 35V26H28" +
                "M54 39H44L43.4725 38.367C42.1873 36.8248 40.4677 35.7051 38.5375 35.1536L38 35V26H28" +
                "M10 51H19.0388C19.6463 51 20.2197 51.2746 20.6586 51.6947" +
                "C22.0938 53.0684 25.8742 56 32 56C38.1258 56 41.9062 53.0684 43.3414 51.6947" +
                "C43.7803 51.2746 44.3537 51 44.9612 51H54M28 26V15.5" +
                "M28 15.5H15.25C12.9028 15.5 11 13.5972 11 11.25C11 8.90279 12.9028 7 15.25 7H48.75" +
                "C51.0972 7 53 8.90279 53 11.25C53 13.5972 51.0972 15.5 48.75 15.5H28Z" +
                "M36 26C36 25.6 36 18.5 36 15.5");

            // Caixas e ralos — vista de topo.
            dict["caixas_e_ralos"] = MakeStroked("#546E7A",
                "M3 3V61H61V3H3Z",
                "M9 32H25M16 16L27 27M32 25V9M16 48L27 37M55 32H39M48 16L37 27M48 48L37 37M32 39V55",
                Ellipse(32, 32, 7, 7),
                Ellipse(32, 32, 15, 15),
                Ellipse(32, 32, 23, 23));

            // Tratamento de esgoto — fossa com azulejos.
            dict["tratamento_de_esgoto"] = MakeStroked("#6D4C41",
                "M60 7V62H5V7M60 7H55M60 7C61.3807 7 62.5 5.88071 62.5 4.5C62.5 3.11929 61.3807 2 60 2H4" +
                "C2.61929 2 1.5 3.11929 1.5 4.5C1.5 5.88071 2.61929 7 4 7H5M5 7H10M10 7H55M10 7V18.5M55 7V18.5" +
                "M10 18.5V57H55V18.5M10 18.5C11.493 17.007 13.7738 16.6369 15.6623 17.5811L18.4042 18.9521" +
                "C20.9826 20.2413 24.0174 20.2413 26.5958 18.9521L27.5 18.5C30.6476 16.9262 34.3524 16.9262 37.5 18.5" +
                "L38.4042 18.9521C40.9826 20.2413 44.0174 20.2413 46.5958 18.9521L49.3377 17.5811" +
                "C51.2262 16.6369 53.507 17.007 55 18.5",
                "M15 26H20M25 26H30M35 26H40M45 26H50M20 31H25M30 31H35M40 31H45" +
                "M15 36H20M25 36H30M35 36H40M45 36H50M20 41H25M30 41H35M40 41H45" +
                "M15 46H20M25 46H30M35 46H40M45 46H50M20 51H25M30 51H35M40 51H45");

            // Poço — bomba manual + gota.
            dict["poco"] = MakeStroked("#C88719",
                "M27 46.3137H25C24.4477 46.3137 24 45.866 24 45.3137V23.7647" +
                "M27 46.3137H35M27 46.3137V57.098M24 23.7647H23C22.4477 23.7647 22 23.317 22 22.7647V19.8627" +
                "C22 19.3105 22.4477 18.8627 23 18.8627H27M24 23.7647H38M27 18.8627H35M27 18.8627V12M35 18.8627H39" +
                "C39.5523 18.8627 40 19.3105 40 19.8627V22.7647C40 23.317 39.5523 23.7647 39 23.7647H38" +
                "M35 18.8627V12M38 23.7647V45.3137C38 45.866 37.5523 46.3137 37 46.3137H35M35 46.3137V57.098" +
                "M35 57.098H35.5253C35.8257 57.098 36.1102 57.2331 36.3001 57.4658L38.6681 60.3678" +
                "C39.2011 61.0209 38.7363 62 37.8933 62H24.1067C23.2637 62 22.7989 61.0209 23.3319 60.3678L25.6999 57.4658" +
                "C25.8898 57.2331 26.1743 57.098 26.4747 57.098H27M35 57.098H27",
                "M36 5C36 5 40 5 44 5C48 5 50 6.40001 52 12C53.7158 16.8043 58.0077 29.5575 59.1927 33.0847" +
                "C59.3655 33.5989 59.0934 34.1479 58.5836 34.3332L54.9655 35.6489" +
                "C54.4366 35.8412 53.8553 35.5658 53.6773 35.0318C52.6868 32.0603 49.657 22.9709 48 18" +
                "C46 12 46 10.5 42 10.5C38 10.5 36.5 10.5 36.5 10.5",
                "M20 33H14C14 33 13 33 12.5 33.5C12 34 12 35 12 35V37H7V31C7 31 7 30 8 29C9 28 10 28 10 28H20M24 26H20V35H24",
                "M6.18657 46.2632C6.73881 44.6842 9.50001 41 9.50001 41C9.50001 41 12.2612 44.6842 12.8134 46.2632" +
                "C13.3657 47.8421 12.8135 51 9.50001 51C6.18656 51 5.63433 47.8421 6.18657 46.2632Z",
                Ellipse(31, 8, 5.5, 5.5));

            // Ponto de utilização — vaso sanitário.
            dict["ponto_de_utilizacao"] = MakeStroked("#616161",
                "M10 34L17.4002 49.2937C17.4659 49.4295 17.5 49.5784 17.5 49.7292V61" +
                "C17.5 61.5523 17.9477 62 18.5 62H48C48.5523 62 49 61.5523 49 61V50.231" +
                "C49 49.7956 49.28 49.4038 49.669 49.2083C50.7449 48.6675 52.9943 47.2536 55.5 44" +
                "C59 39.4554 58.5 34 58.5 34M10 34H6.5C5.94772 34 5.5 33.5523 5.5 33V7" +
                "C5.5 4.23858 7.73858 2 10.5 2H17C19.7614 2 22 4.23858 22 7V34M10 34H22" +
                "M58.5 34H28M58.5 34V32C58.5 29.7909 56.7091 28 54.5 28H32C29.7909 28 28 29.7909 28 32V34" +
                "M22 34H28");

            return dict;
        }

        /// <summary>
        /// Monta um <see cref="DrawingImage"/> em viewBox 64×64 stroke-only,
        /// com line-cap/line-join round (padrão dos SVGs do catálogo). Aceita
        /// strings de path (parseadas via <see cref="Geometry.Parse"/>) e
        /// <see cref="Geometry"/> já instanciadas (ellipses, retângulos).
        /// O resultado é congelado para reuso seguro entre threads e listas
        /// virtualizadas.
        /// </summary>
        private static DrawingImage MakeStroked(string strokeHex, params object[] geometries)
        {
            Color color = (Color)ColorConverter.ConvertFromString(strokeHex);
            SolidColorBrush brush = new SolidColorBrush(color);
            brush.Freeze();

            Pen pen = new Pen(brush, 3.0)
            {
                StartLineCap = PenLineCap.Round,
                EndLineCap = PenLineCap.Round,
                DashCap = PenLineCap.Round,
                LineJoin = PenLineJoin.Round,
            };
            pen.Freeze();

            DrawingGroup group = new DrawingGroup();

            // Âncora invisível para travar os bounds em 0,0,64,64. Sem isso,
            // ícones cujos paths não cobrem todo o viewBox são centralizados
            // de forma diferente quando renderizados com Stretch="Uniform".
            RectangleGeometry anchor = new RectangleGeometry(new Rect(0, 0, 64, 64));
            anchor.Freeze();
            GeometryDrawing anchorDrawing = new GeometryDrawing(Brushes.Transparent, null, anchor);
            anchorDrawing.Freeze();
            group.Children.Add(anchorDrawing);

            foreach (object item in geometries)
            {
                Geometry geometry = item switch
                {
                    string s => Geometry.Parse(s),
                    Geometry g => g,
                    _ => throw new ArgumentException(
                        $"Tipo de geometria não suportado: {item?.GetType().FullName ?? "null"}",
                        nameof(geometries)),
                };

                if (!geometry.IsFrozen)
                {
                    geometry.Freeze();
                }

                GeometryDrawing drawing = new GeometryDrawing(null, pen, geometry);
                drawing.Freeze();
                group.Children.Add(drawing);
            }

            group.Freeze();

            DrawingImage image = new DrawingImage(group);
            image.Freeze();
            return image;
        }

        /// <summary>
        /// Helper para os SVGs que usam <c>&lt;circle&gt;</c> ou
        /// <c>&lt;ellipse&gt;</c>. WPF não tem parser de SVG; usamos
        /// <see cref="EllipseGeometry"/> direto pra evitar conversão manual
        /// para arc commands.
        /// </summary>
        private static Geometry Ellipse(double cx, double cy, double rx, double ry)
        {
            EllipseGeometry geometry = new EllipseGeometry(new Point(cx, cy), rx, ry);
            geometry.Freeze();
            return geometry;
        }
    }
}
