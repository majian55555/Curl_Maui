# Curl_Maui


dotnet build -f net9.0-windows10.0.19041.0 -c Release -p:RuntimeIdentifierOverride=win10-x64


dotnet publish -f net9.0-windows10.0.19041.0 -c Release -p:RuntimeIdentifierOverride=win10-x64


dotnet publish -f net9.0-maccatalyst -c Release -p:CreatePackage=false