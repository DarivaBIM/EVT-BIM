; ============================================================================
;  EVT-BIM â€” Inno Setup script
;
;  Gera um instalador unico (.exe) que distribui o plugin EVT-BIM para Revit
;  2025 e/ou 2026. Modo "Recomendado" instala automaticamente para todas as
;  versoes detectadas na maquina; modo "Personalizado" permite escolher.
;
;  Pre-requisitos:
;    1. Inno Setup 6 (https://jrsoftware.org/isinfo.php) com ISCC.exe no PATH
;       ou em "C:\Program Files (x86)\Inno Setup 6\ISCC.exe".
;    2. Os binarios publicados precisam estar em
;       artifacts/installer/v2025 e artifacts/installer/v2026 â€” use
;       build_installer.cmd para gerar o staging e compilar tudo.
; ============================================================================

#define AppName        "EVT-BIM"
#define AppVersion     "0.3.0"
#define AppPublisher   "Engenharia de Valor Tigre"
#define AppURL         "https://github.com/darivabim/evt-bim"
; AppId fixo: garante que reinstalacoes futuras desinstalem a versao anterior.
#define AppId          "{B6A8F2D4-9C3E-4F1B-8E5A-2D7C9A4F1E3B}"

[Setup]
AppId={{#AppId}}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL={#AppURL}
AppSupportURL={#AppURL}
AppUpdatesURL={#AppURL}
VersionInfoVersion={#AppVersion}
DefaultDirName={commonappdata}\Autodesk\Revit\Addins
DisableDirPage=yes
DisableProgramGroupPage=yes
CreateAppDir=no
UninstallDisplayName={#AppName} {#AppVersion}
UninstallFilesDir={commonappdata}\{#AppName}\uninstall
OutputDir=..\..\..\artifacts\installer
OutputBaseFilename=EVT-BIM-Setup-v{#AppVersion}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
ShowLanguageDialog=no

[Languages]
Name: "brazilianportuguese"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"

[Types]
Name: "recommended"; Description: "Recomendado (instalar para todas as versoes do Revit detectadas)"
Name: "custom";      Description: "Personalizado (escolher manualmente)"; Flags: iscustom

[Components]
Name: "v2025"; Description: "Plugin EVT-BIM para Revit 2025"; Types: recommended custom
Name: "v2026"; Description: "Plugin EVT-BIM para Revit 2026"; Types: recommended custom

[Files]
; --- Revit 2025 ---
Source: "..\..\..\artifacts\installer\v2025\addin\EVT-BIM.V2025.addin"; \
    DestDir: "{commonappdata}\Autodesk\Revit\Addins\2025"; \
    Components: v2025; Flags: ignoreversion
Source: "..\..\..\artifacts\installer\v2025\plugin\*"; \
    Excludes: "*.addin"; \
    DestDir: "{commonappdata}\Autodesk\Revit\Addins\2025\EVT-BIM"; \
    Components: v2025; Flags: ignoreversion recursesubdirs createallsubdirs

; --- Revit 2026 ---
Source: "..\..\..\artifacts\installer\v2026\addin\EVT-BIM.V2026.addin"; \
    DestDir: "{commonappdata}\Autodesk\Revit\Addins\2026"; \
    Components: v2026; Flags: ignoreversion
Source: "..\..\..\artifacts\installer\v2026\plugin\*"; \
    Excludes: "*.addin"; \
    DestDir: "{commonappdata}\Autodesk\Revit\Addins\2026\EVT-BIM"; \
    Components: v2026; Flags: ignoreversion recursesubdirs createallsubdirs

[UninstallDelete]
; Limpa tudo que o instalador colocou nas pastas de addin.
Type: files;          Name: "{commonappdata}\Autodesk\Revit\Addins\2025\EVT-BIM.V2025.addin"
Type: filesandordirs; Name: "{commonappdata}\Autodesk\Revit\Addins\2025\EVT-BIM"
Type: files;          Name: "{commonappdata}\Autodesk\Revit\Addins\2026\EVT-BIM.V2026.addin"
Type: filesandordirs; Name: "{commonappdata}\Autodesk\Revit\Addins\2026\EVT-BIM"

[Code]
// --------------------------------------------------------------------------
// Deteccao de versoes do Revit instaladas. Verifica diretorio padrao e
// chave de registro (HKLM) para cobrir instalacoes nao-padrao.
// --------------------------------------------------------------------------

function IsRevitInstalled(const Version: string): Boolean;
var
  DefaultPath: string;
begin
  DefaultPath := ExpandConstant('{commonpf}') + '\Autodesk\Revit ' + Version;
  Result := DirExists(DefaultPath)
    or RegKeyExists(HKLM, 'SOFTWARE\Autodesk\Revit\' + Version)
    or RegKeyExists(HKLM, 'SOFTWARE\WOW6432Node\Autodesk\Revit\' + Version);
end;

function IsRevit2025Installed(): Boolean;
begin
  Result := IsRevitInstalled('2025');
end;

function IsRevit2026Installed(): Boolean;
begin
  Result := IsRevitInstalled('2026');
end;

// --------------------------------------------------------------------------
// Aviso amigavel se o usuario rodar o instalador em uma maquina sem nenhuma
// versao suportada do Revit. Permite seguir mesmo assim (preparacao offline).
// --------------------------------------------------------------------------

function InitializeSetup(): Boolean;
begin
  Result := True;
  if not (IsRevit2025Installed or IsRevit2026Installed) then
  begin
    if MsgBox(
      'Nao foi detectada nenhuma instalacao do Revit 2025 ou 2026 nesta maquina.' + #13#10 + #13#10 +
      'Voce ainda pode prosseguir e instalar os arquivos (utilize o modo Personalizado),' + #13#10 +
      'mas o plugin so sera carregado quando o Revit correspondente for instalado.' + #13#10 + #13#10 +
      'Deseja continuar mesmo assim?',
      mbConfirmation, MB_YESNO or MB_DEFBUTTON2) = IDNO then
      Result := False;
  end;
end;

// --------------------------------------------------------------------------
// Modo "Recomendado": auto-marca apenas as versoes detectadas.
// Modo "Personalizado": preserva a escolha do usuario sem interferir.
//
// As constantes c_RecommendedTypeIndex e c_*ComponentIndex refletem a ordem
// de declaracao em [Types] e [Components] acima â€” se essa ordem mudar,
// atualizar os indices.
// --------------------------------------------------------------------------

const
  c_RecommendedTypeIndex = 0;
  c_V2025ComponentIndex  = 0;
  c_V2026ComponentIndex  = 1;

procedure CurPageChanged(CurPageID: Integer);
begin
  if CurPageID = wpSelectComponents then
  begin
    if WizardForm.TypesCombo.ItemIndex = c_RecommendedTypeIndex then
    begin
      WizardForm.ComponentsList.Checked[c_V2025ComponentIndex] := IsRevit2025Installed;
      WizardForm.ComponentsList.Checked[c_V2026ComponentIndex] := IsRevit2026Installed;
    end;
  end;
end;

// --------------------------------------------------------------------------
// Avisa se o usuario, em modo Personalizado, marcou uma versao do Revit que
// nao esta instalada â€” pode ser intencional, mas e bom confirmar.
// --------------------------------------------------------------------------

function NextButtonClick(CurPageID: Integer): Boolean;
var
  Missing: string;
begin
  Result := True;
  if CurPageID = wpSelectComponents then
  begin
    Missing := '';
    if WizardForm.ComponentsList.Checked[c_V2025ComponentIndex] and not IsRevit2025Installed then
      Missing := Missing + ' - Revit 2025' + #13#10;
    if WizardForm.ComponentsList.Checked[c_V2026ComponentIndex] and not IsRevit2026Installed then
      Missing := Missing + ' - Revit 2026' + #13#10;

    if Missing <> '' then
    begin
      if MsgBox(
        'As seguintes versoes foram marcadas mas nao foram detectadas nesta maquina:' + #13#10 + #13#10 +
        Missing + #13#10 +
        'O plugin sera instalado mesmo assim, e sera carregado quando o Revit correspondente for instalado.' + #13#10 + #13#10 +
        'Deseja continuar?',
        mbConfirmation, MB_YESNO or MB_DEFBUTTON1) = IDNO then
        Result := False;
    end;
  end;
end;
