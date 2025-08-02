$ErrorActionPreference = 'Stop'

uv run python -m nuitka .\src\recipe_box\main.py --windows-console-mode=disable --mode=onefile --enable-plugin=pyside6

if (Test-Path -Path "main.exe" -PathType Leaf) {
    Copy-Item -Path "main.exe" -Destination "publish\RecipeBox.exe" -Force
}

if ($LASTEXITCODE -ne 0) {
    Write-Error "Nuitka build failed with exit code $LASTEXITCODE"
    exit $LASTEXITCODE
}

& "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" install.iss
