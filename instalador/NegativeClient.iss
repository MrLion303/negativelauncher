; Instalador gráfico de Negative Client — compilación de prueba
#define AppName "Negative Client"
#define AppPublisher "NegativeStudios"
#define AppExeName "Negative Client.exe"
#ifndef AppVersion
  #define AppVersion "0.1.1"
#endif

[Setup]
AppId={{8D1A6D90-7A22-4B68-9E41-7D2F0C110001}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
DefaultDirName={autopf}\Negative Client
DefaultGroupName=Negative Client
DisableProgramGroupPage=yes
LicenseFile=instalador\terminos-prueba.txt
OutputDir=instalador-prueba
OutputBaseFilename=Negative-Client-Instalador-Windows-x64-prueba
SetupIconFile=Negative Client\Negative Client\Assets\negativeclient.ico
UninstallDisplayIcon={app}\{#AppExeName}
WizardStyle=modern
Compression=lzma2/ultra64
SolidCompression=yes
ArchitecturesInstallIn64BitMode=x64
PrivilegesRequired=admin
CloseApplications=yes
RestartApplications=no
Uninstallable=yes
VersionInfoDescription=Instalador de prueba de Negative Client
VersionInfoProductName=Negative Client
VersionInfoCompany=NegativeStudios

[Languages]
Name: "spanish"; MessagesFile: "compiler:Languages\Spanish.isl"

[Tasks]
Name: "desktopicon"; Description: "Crear un acceso directo en el escritorio"; GroupDescription: "Accesos directos:"; Flags: unchecked
Name: "startmenuicon"; Description: "Crear un acceso directo en el menú Inicio"; GroupDescription: "Accesos directos:"; Flags: checkedonce

[Files]
Source: "publicacion\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\Negative Client"; Filename: "{app}\{#AppExeName}"; Tasks: startmenuicon
Name: "{autodesktop}\Negative Client"; Filename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExeName}"; Description: "Iniciar Negative Client"; Flags: nowait postinstall skipifsilent
