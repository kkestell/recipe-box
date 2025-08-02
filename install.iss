#define SourceDir "C:\Users\Kyle\Source\recipe-box"

[Setup]
AppId={{d8266f16-378b-44ee-8082-933aea8c3609}}
AppName=Recipe Box
AppVersion=0.1.0
AppVerName=Recipe Box
AppPublisher=Kyle Kestell
AppPublisherURL=https://github.com/kkestell/recipe-box
AppSupportURL=https://github.com/kkestell/recipe-box
AppUpdatesURL=https://github.com/kkestell/recipe-box
DefaultDirName={autopf}\RecipeBox
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
DisableProgramGroupPage=yes
LicenseFile={#SourceDir}\LICENSE
PrivilegesRequired=lowest
OutputDir={#SourceDir}\publish
OutputBaseFilename=RecipeBox_0.1.0_Setup
Compression=lzma
SolidCompression=yes
WizardStyle=modern
UninstallDisplayIcon={app}\RecipeBox.exe

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a Desktop shortcut"; GroupDescription: "Additional shortcuts:"
Name: "startmenuicon"; Description: "Create a Start Menu shortcut"; GroupDescription: "Additional shortcuts:"

[Files]
Source: "{#SourceDir}\publish\RecipeBox.exe"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\RecipeBox"; Filename: "{app}\RecipeBox.exe"; Tasks: startmenuicon
Name: "{userdesktop}\RecipeBox"; Filename: "{app}\RecipeBox.exe"; Tasks: desktopicon
