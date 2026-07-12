# PS-SZC

Aplikacja do rozliczeń i płatności szkolnych.

**English:** [README.md](README.md)

## Obsługiwane platformy

Oficjalne pakiety instalacyjne są dostępne dla:

- **macOS** — Apple Silicon (arm64)
- **Windows** — x64

Inne systemy operacyjne i architektury procesorów nie są objęte oficjalnymi wydaniami.

## Instalacja z wydania

Pobierz najnowszą wersję z:

**https://github.com/PostekSoftware/PS-SZC/releases/latest**

### macOS (Apple Silicon)

1. Pobierz plik `.dmg` z najnowszego wydania.
2. Otwórz obraz dysku i przeciągnij **PS-SZC.app** do folderu **Applications** (Aplikacje).
3. Ponieważ aplikacja nie jest notaryzowana ani podpisana certyfikatem Apple Developer, macOS może zablokować ją przy pierwszym uruchomieniu. Usuń atrybut kwarantanny, wykonując polecenie:

   ```bash
   xattr -cr /Applications/PS-SZC.app
   ```

4. Uruchom **PS-SZC** z folderu Aplikacje lub Launchpada.

Jeśli macOS nadal ostrzega o niezidentyfikowanym deweloperze, otwórz **Ustawienia systemowe → Prywatność i ochrona** i wybierz **Otwórz mimo to** dla PS-SZC.

### Windows (x64)

1. Pobierz plik `.msi` z najnowszego wydania.
2. Uruchom instalator i postępuj zgodnie z instrukcjami.
3. Uruchom **PS-SZC** z menu Start lub skrótu na pulpicie utworzonego podczas instalacji.

Instalator wymaga systemu Windows x64 oraz [.NET 10 Desktop Runtime (x64)](https://dotnet.microsoft.com/download/dotnet/10.0), jeśli nie jest jeszcze zainstalowany.

## Budowanie ze źródeł

### Wymagania

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- **macOS:** Xcode Command Line Tools (`xcode-select --install`) — wymagane do skompilowania pomocnika drukowania
- **Windows (tylko instalator):** [WiX Toolset SDK 5](https://wixtoolset.org/) — używany przy budowaniu pliku MSI

Sklonuj repozytorium:

```bash
git clone https://github.com/PostekSoftware/PS-SZC.git
cd PS-SZC
```

### Budowanie aplikacji (macOS)

Na komputerze Mac z Apple Silicon:

```bash
dotnet build PS-SZC/PS-SZC.csproj -c Release
```

Wynik:

- `PS-SZC/bin/Release/net10.0/PS-SZC.app` — pakiet aplikacji macOS
- `PS-SZC/bin/Release/net10.0/PS-SZC.dmg` — obraz dysku do dystrybucji

Uruchomienie lokalne bez instalacji:

```bash
open PS-SZC/bin/Release/net10.0/PS-SZC.app
```

### Budowanie aplikacji (Windows)

```bash
dotnet publish PS-SZC/PS-SZC.csproj -c Release -r win-x64
```

Opublikowane pliki trafiają do:

`PS-SZC/bin/Release/net10.0/win-x64/publish/`

Uruchomienie:

```powershell
.\PS-SZC\bin\Release\net10.0\win-x64\publish\PS-SZC.exe
```

### Budowanie instalatora Windows (MSI)

Plik MSI buduje się osobno i tylko w systemie Windows:

```powershell
dotnet build PS-SZC.Installer/PS-SZC.Installer.wixproj -c Release
```

Wynik:

`PS-SZC.Installer/bin/Release/PS-SZC.msi`

Projekt instalatora automatycznie publikuje aplikację Windows x64 przed pakowaniem.

### Budowanie rozwiązania (rozwój)

```bash
dotnet build PS-SZC.sln -c Debug
```

Projekt instalatora Windows jest pomijany poza systemem Windows.

## Licencja

PS-SZC jest udostępniany na licencji **MIT**. Pełny tekst znajduje się w pliku [LICENSE](LICENSE).

Copyright (c) 2026 PostekSoftware

## Biblioteki zewnętrzne

PS-SZC korzysta z poniższych komponentów open source. Obowiązują ich licencje, dodatkowo do licencji projektu.

| Biblioteka | Wersja | Zastosowanie | Licencja |
| --- | --- | --- | --- |
| [Hexa.NET.ImGui](https://www.nuget.org/packages/Hexa.NET.ImGui) | 2.2.9 | Interfejs użytkownika (immediate mode) | MIT |
| [Hexa.NET.ImGui.Backends.SDL3](https://www.nuget.org/packages/Hexa.NET.ImGui.Backends.SDL3) | 1.0.18 | Renderowanie ImGui przez SDL3 | MIT |
| [Hexa.NET.SDL3](https://www.nuget.org/packages/Hexa.NET.SDL3) | 1.2.17 | Okna, wejście, grafika | MIT |
| [SDL3](https://github.com/libsdl-org/SDL) (natywna) | (przez Hexa.NET.SDL3) | Multimedia wieloplatformowe | Zlib |
| [SixLabors.ImageSharp](https://www.nuget.org/packages/SixLabors.ImageSharp) | 3.1.11 | Wczytywanie i przetwarzanie obrazów | [Six Labors License](https://github.com/SixLabors/ImageSharp/blob/main/LICENSE) |
| [Microsoft.EntityFrameworkCore.Sqlite](https://www.nuget.org/packages/Microsoft.EntityFrameworkCore.Sqlite) | 9.0.0 | Dostęp do bazy projektu | MIT |
| [Microsoft.Data.Sqlite](https://www.nuget.org/packages/Microsoft.Data.Sqlite) | 9.0.0 | Dostawca SQLite ADO.NET | MIT |
| [SQLitePCLRaw.bundle_e_sqlite3](https://www.nuget.org/packages/SQLitePCLRaw.bundle_e_sqlite3) | 3.0.3 | Natywny silnik SQLite | Apache-2.0 |
| [SQLite](https://sqlite.org/) (natywna) | (przez SQLitePCLRaw) | Baza danych osadzona | Domena publiczna |
| [PdfSharpCore](https://www.nuget.org/packages/PdfSharpCore) | 1.3.67 | Eksport raportów PDF | MIT |
| [WixToolset SDK](https://www.nuget.org/packages/WixToolset.Sdk) | 5.0.2 | Pakowanie MSI Windows (tylko build) | MIT |

Dodatkowe biblioteki .NET przekazywane tranzytywnie (np. Microsoft.Extensions.* i HexaGen.Runtime) są na ogół udostępniane na licencji MIT, o ile w danym pakiecie nie wskazano inaczej.
