Uruchomienie projektu

komendy w termianlu
dotnet build
dotnet test tests/Api.Tests/Api.Tests.csproj
dotnet watch run --project src/Api/Api.csproj


Tydzień 1
http://localhost:{port}/hello/{imie}
http://localhost:{port}/api/v1/health

Tydzień 2
dodanie nowego uzytkownika do bazy danych 
Invoke-WebRequest -Uri "http://localhost:{port}/users" -Method POST -ContentType "application/json" -Body '{"username":"{imie}","email":"{mail}"}'
http://localhost:5142/users -lista wszystkich uzytkownikow
