# Set the project directory
$projectDir = "C:\Users\Kyle\Source\recipe-box\RecipeBox"

# Publish the project
Write-Host "Publishing RecipeBox project..."
dotnet publish $projectDir -c Release -r win-x64

Write-Host "Process completed successfully." -ForegroundColor Green