#define SourceDir "C:\Users\Kyle\Source\recipe-box"
[Setup]
AppId={{a3f61d87-2c40-47e8-9bce-d15892f63a91}}
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
SetupIconFile={#SourceDir}\RecipeBox\static\images\icon.ico
Compression=lzma
SolidCompression=yes
WizardStyle=modern
UninstallDisplayIcon={app}\recipebox.exe
AllowNoIcons=yes
[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
[Files]
Source: "{#SourceDir}\RecipeBox\bin\Release\net9.0\win-x64\publish\RecipeBox.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#SourceDir}\RecipeBox\bin\Release\net9.0\win-x64\publish\static\*"; DestDir: "{app}\static"; Flags: ignoreversion recursesubdirs
[Icons]
Name: "{group}\RecipeBox"; Filename: "{app}\recipebox.exe"; IconFilename: "{app}\static\images\icon.ico"
Name: "{autodesktop}\RecipeBox"; Filename: "{app}\recipebox.exe"; IconFilename: "{app}\static\images\icon.ico"; Tasks: desktopicon
[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked