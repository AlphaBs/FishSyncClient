Remove-Item -Recurse -Force -Path "bin\Release\net8.0-windows\win-x64\publish\*"
dotnet publish -c Release --self-contained=true
Compress-Archive -Path "bin\Release\net8.0-windows\win-x64\publish\*" -DestinationPath "bin\Release\net8.0-windows\win-x64\publish\publish.zip" -Force