using System;
using System.Windows;
using System.Windows.Interop;
using DarivaBIM.Infrastructure.Persistence.Preferences;

namespace DarivaBIM.Plugin.Ui
{
    /// <summary>
    /// Janela modeless que hospeda <see cref="FamiliesPage"/> fora do sistema
    /// de DockablePane do Revit. O DockablePane do Revit 2025+ tem uma
    /// regressão (AdWindows.dll na migração pra .NET 8) que deixa a pane
    /// congelada visualmente após placement de família — sintoma só
    /// "destravado" minimizando/maximizando o Revit. Hospedando o mesmo
    /// UserControl numa Window WPF normal, o HwndSource é gerenciado pelo
    /// framework WPF diretamente, sem wrapper de docking — eliminando a
    /// causa raiz do freeze.
    ///
    /// Owner é setado para a janela principal do Revit via
    /// <see cref="WindowInteropHelper"/>: a janela se comporta como
    /// filha (minimiza junto, fica acima do Revit, fecha junto).
    /// </summary>
    public partial class FamiliesWindow : Window
    {
        private readonly FamilyPreferencesService _preferences;

        public FamiliesWindow(IntPtr revitMainWindowHandle, FamilyPreferencesService preferences)
        {
            _preferences = preferences;

            InitializeComponent();

            // Owner via interop helper — precisa ser setado antes do Show()
            // e antes da HWND da janela ser criada. WPF usa esse handle como
            // parent nativo na criação do HwndSource interno, garantindo o
            // comportamento de "janela filha" (ativação, Z-order, ciclo de
            // vida acoplados ao Revit).
            new WindowInteropHelper(this).Owner = revitMainWindowHandle;

            // SourceInitialized roda depois da HWND ser criada e ANTES do
            // Show() devolver — momento certo pra restaurar placement sem
            // flicker (a janela já tem HWND, mas ainda não foi pintada).
            SourceInitialized += OnSourceInitialized;
            Closing += OnClosing;
        }

        private void OnSourceInitialized(object? sender, EventArgs e)
        {
            WindowPlacement? saved = _preferences.GetWindowPlacement();
            if (saved == null)
            {
                // Primeira execução: usa o default (CenterOwner do XAML).
                return;
            }

            // Validação básica: descarta placement com dimensões absurdas
            // ou negativas (JSON adulterado ou esquema antigo). MinWidth/
            // MinHeight do XAML são 380x500 — não respeitamos placement
            // menor que isso pra não terminar com janela invisível.
            if (saved.Width < MinWidth || saved.Height < MinHeight)
            {
                return;
            }

            // Validação de área visível: se o ponto (Left, Top) ficou fora
            // de todas as telas (usuário tinha 2 monitores, agora só 1), o
            // WPF vai abrir a janela invisível. Trata fallback pro default.
            if (!IsOnAnyScreen(saved.Left, saved.Top, saved.Width, saved.Height))
            {
                return;
            }

            WindowStartupLocation = WindowStartupLocation.Manual;
            Left = saved.Left;
            Top = saved.Top;
            Width = saved.Width;
            Height = saved.Height;

            if (saved.IsMaximized)
            {
                WindowState = WindowState.Maximized;
            }
        }

        private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            // Quando maximizado, Left/Top/Width/Height refletem a área
            // restaurada (não a tela inteira), então salvamos sempre essas
            // 4 + o flag IsMaximized. Próxima abertura restaura o estado
            // que o usuário deixou.
            WindowPlacement placement = new()
            {
                Left = RestoreBounds.Width > 0 ? RestoreBounds.Left : Left,
                Top = RestoreBounds.Height > 0 ? RestoreBounds.Top : Top,
                Width = RestoreBounds.Width > 0 ? RestoreBounds.Width : Width,
                Height = RestoreBounds.Height > 0 ? RestoreBounds.Height : Height,
                IsMaximized = WindowState == WindowState.Maximized,
            };

            _preferences.SaveWindowPlacement(placement);
        }

        // Checa se o retângulo da janela intersecta a área de trabalho de
        // alguma tela conectada. Usa SystemParameters.WorkArea como
        // primary screen (suficiente pro caso comum); multi-monitor edge
        // case extremo (ponto inteiramente fora de todas as telas) cai no
        // fallback do CenterOwner.
        private static bool IsOnAnyScreen(double left, double top, double width, double height)
        {
            // VirtualScreenLeft/Top/Width/Height cobre TODAS as telas
            // conectadas como um retângulo único. Suficiente pra validar
            // "ponto está em alguma tela".
            double vLeft = SystemParameters.VirtualScreenLeft;
            double vTop = SystemParameters.VirtualScreenTop;
            double vRight = vLeft + SystemParameters.VirtualScreenWidth;
            double vBottom = vTop + SystemParameters.VirtualScreenHeight;

            double right = left + width;
            double bottom = top + height;

            // Bastam 80 pixels visíveis em cada eixo pra considerar
            // "alcançável" — se só uma faixa estreita estiver visível,
            // o usuário consegue arrastar pra dentro.
            const double MinVisible = 80;
            return right - MinVisible > vLeft
                && left + MinVisible < vRight
                && bottom - MinVisible > vTop
                && top + MinVisible < vBottom;
        }
    }
}
