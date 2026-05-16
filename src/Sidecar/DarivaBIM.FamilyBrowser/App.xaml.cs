using System;
using System.Windows;

namespace DarivaBIM.FamilyBrowser
{
    /// <summary>
    /// Entry point WPF do sidecar. Parseia argumentos de linha de comando
    /// (<c>--url</c>, <c>--pipe</c>) e constroi a <see cref="MainWindow"/>
    /// com os valores resolvidos.
    /// </summary>
    public partial class App : Application
    {
        // URL default usada quando --url nao e fornecido. Aponta pra bing.com
        // por padrao pra que o spike funcione "sem dependencias" — qualquer
        // maquina com WebView2 runtime carrega. Em uso real, o plugin Revit
        // spawna o EXE com --url http://localhost:3000/embed/revit (dev) ou
        // https://acervobim.darivabim.com/embed/revit (prod).
        private const string DefaultUrl = "https://www.bing.com";

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            string url = ResolveArg(e.Args, "--url") ?? DefaultUrl;
            string? pipeName = ResolveArg(e.Args, "--pipe");

            MainWindow window = new(url, pipeName);
            MainWindow = window;
            window.Show();
        }

        // Procura --<name> <value> nos args. Nao tenta resolver/normalizar
        // o valor aqui — a MainWindow valida com Uri.TryCreate antes de
        // passar pro WebView2, retornando erro amigavel se invalido. Pipe
        // ausente cai num modo "browser standalone sem bridge" (degradacao
        // graciosa: util pra abrir o EXE manualmente fora do Revit).
        private static string? ResolveArg(string[] args, string name)
        {
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
                {
                    return args[i + 1];
                }
            }

            return null;
        }
    }
}
