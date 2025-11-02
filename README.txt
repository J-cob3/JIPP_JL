Uruchomienie projektu

komendy w termianlu

dotnet build
dotnet test tests/Api.Tests/Api.Tests.csproj

dotnet watch run --project src/Api/Api.csproj <= ogólna komenda do uruchomienia projektu


Tydzień 1
http://localhost:(numer portu)port/hello/{imie}
http://localhost:(numer portu)/api/v1/health

Tydzień 2
dodanie nowego uzytkownika do bazy
Invoke-WebRequest -Uri "http://localhost:(numer portu)/users" -Method POST -ContentType "application/json" -Body '{"username":"(nazwa użytkownika)","email":"(nazwa maila)"}'
http://localhost:(nazwa portu)/users -wszyscy użytkownicy

Tydzień 3
aktualizacja danych użytkownika do bazy

Szczegóły użytkownika:
http://localhost:(port)/users/{id_usera}

# PUT: aktualizacja
Invoke-RestMethod -Method Put -ContentType 'application/json' -Body '{"username":"test","email":"test2@test.com"}' http://localhost:(port)/users/1

# DELETE: usuwanie
Invoke-RestMethod -Method Delete http://localhost:(port)/users/1

Tydzień 4
dodanie userowi testowego taska
Invoke-WebRequest -Uri "http://localhost:(port)/tasks" -Method POST -ContentType "application/json" -Body '{"Title":"Testtitle","Description":"test OK","UserID":1}'


