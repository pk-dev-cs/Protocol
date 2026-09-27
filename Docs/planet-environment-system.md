# Planet Environment — checkpoint 1

## Architektura

- `AlienWorldLighting` jest istniejącym zegarem i cyklem dnia/nocy. Doba domyślnie trwa 2880 sekund symulacji; godzina świata trwa 120 sekund. Zegar używa `Time.deltaTime`, więc respektuje pauzę i skalę czasu. Udostępnia teraz również upływ czasu symulacji i wspólne obliczenie wysokości słońca.
- `ScenarioBootstrap` inicjalizuje świat, nawigację, ekonomię, Lua, zaznaczanie i HUD. Dodaje środowisko do `World` i panel do scenariusza. Nie ma osobnego GameManagera ani potrzeby tworzenia go.
- HUD rozgrywki to IMGUI: `UnitPanel`, `SelectionManager`, `MinimapPanel`, `PauseMenu` i zegar w `AlienWorldLighting`. Dolny panel obsługuje pojedynczego robota, grupę lub budynek. Nowy panel korzysta z tego samego sposobu wyświetlania i blokowania wejścia myszy.
- Ruch obsługuje `RobotMovement`, używając NavMeshAgent oraz przeszkody dla zaparkowanego robota. Checkpoint 1 nie zmienia ruchu, baterii ani robotów.
- Istnieje zapis ustawień w PlayerPrefs i plików programów Lua przez RobotProgramStorage. Projekt nie ma pełnego zapisu stanu świata. Nie dodajemy odrębnego zapisu pogody.

## Dane i konfiguracja

`PlanetEnvironmentSystem.State` udostępnia niezmienną migawkę sześciu wartości 0–1. System działa również wtedy, gdy panel jest zamknięty lub wyłączony. Model nie korzysta z UI ani globalnego generatora Random.

Konfiguracja: `Assets/Resources/PlanetEnvironmentProfile.asset`. Edytuj ją w Inspectorze. Profile można również tworzyć przez Create > Protocol > Planet Environment Profile. Nie zmieniaj profilu przez kod jako sposobu przechowywania bieżącego stanu — jest konfiguracją, a stan należy do systemu sceny.

| Parametr | Domyślna dynamika |
| --- | --- |
| Temperature | Cykl dobowy, baseline 0.42, amplituda 0.24, opóźnienie 2 godziny świata; szczyt około 14:00. Temperatura jest znormalizowana, nie podana w °C. |
| Solar | Dodatnia wysokość słońca z istniejącej orbity; 0 nocą, maksimum około południa. |
| Wind | Płynny deterministyczny noise, baseline 0.55, amplituda 0.25, skala zmian 300 sekund symulacji. |
| Dust | Baseline 0.35, amplituda 0.08, skala zmian 1200 sekund. |
| EM Activity | Baseline 0.18, amplituda 0.025, skala zmian 1800 sekund. |
| Radiation | Baseline 0.05, amplituda tylko 0.005, skala zmian 7200 sekund. |

Każda zmienna noise ma własny offset, zakres i skalę zmian. `PeriodSeconds` oznacza skalę czasową szumu, a nie okres powtarzanej sekwencji. Wartości są ograniczane do zakresów profilu i 0–1. `DustSolarAttenuation` domyślnie wynosi 0; pozwala w przyszłości ograniczyć Solar przez pył bez przebudowy modelu. Na tym etapie parametry nie wpływają na roboty ani wydajność gospodarki.

## Panel

Ikona planety znajduje się po lewej stronie zegara w prawym górnym rogu. Kliknięcie przełącza panel o szerokości 20% ekranu, animowany przez 0.25 sekundy czasu rzeczywistego. Panel nie wchodzi na dolny HUD. Pokazuje sześć nazw, liczby z dwoma miejscami po przecinku i cienkie paski. Teksty są próbkowane najwyżej cztery razy na sekundę, wyłącznie kiedy panel jest widoczny.

Kliknięcia i scroll nad ikoną/panelem nie trafiają do zaznaczania ani sterowania kamerą. Panel znika podczas pauzy, programowania robotów i ładowania; po zamknięciu okna modalnego wraca do poprzedniego stanu.

## Testowanie

1. Otwórz `Stage01` i uruchom Play. Kliknij ikonę planety przy zegarze; sprawdź wysunięcie, ponowne kliknięcie, hover i brak kliknięć w mapę przez panel.
2. Porównaj panel w 16:9, 16:10 i ultrawide. Jego szerokość ma pozostać równa 20% szerokości ekranu.
3. Obserwuj zmiany czasu oraz Temperature i Solar. Dla szybszego testu zmniejsz `Cycle Duration Seconds` na komponencie AlienWorldLighting w `World` w trybie Play, np. do 120. Po wyjściu z Play wartość sceny pozostanie 2880.
4. Sprawdź Solar o północy i w południe oraz szczyt temperatury po południu. Radiation powinna pozostać blisko 0.05.
5. Zamknij panel lub wyłącz sam komponent PlanetEnvironmentPanel: stan systemu nadal jest obliczany. Otwórz ponownie, aby zobaczyć bieżące dane.
6. Otwórz pauzę, poczekaj, wznow grę. Zegar i wartości powinny być zamrożone podczas pauzy. Zwiększenie Time.timeScale przyspiesza symulację wszystkich parametrów; animacja panelu pozostaje krótka.
7. Wykonaj zaznaczanie pojedyncze i grupowe, polecenie ruchu, program Lua oraz cykl zbierania i dostarczenia surowca. Sprawdź dolny panel, edytor kodu, zoom i przesuwanie kamery poza panelem środowiska.
8. Sprawdź utratę focusu i powrót: środowisko ma zachować reguły działania istniejącej symulacji w tle, bez dodatkowej pauzy ani zmiany ustawień. Panel nie zapisuje PlayerPrefs.
9. Zakończ i ponownie uruchom scenariusz. Powinna istnieć jedna instancja systemu i panelu; nowy scenariusz rozpoczyna od swojego profilu i początkowej godziny.
10. Uruchom `Protocol > Validate Planet Environment`. Test sprawdza 86 400 próbek: zakresy, płynność, dobowe zależności, opóźnienie termiczne, stabilność promieniowania i brak osobnego zegara w modelu.

## Weryfikacja implementacji

- MSBuild: wszystkie cztery projekty rozwiązania skompilowane poprawnie.
- Test modelu w Unity: 86 400 próbek bez naruszeń.
- Play Mode: dane niezależne od panelu, przyspieszenie czasu, pauza, animacja otwarcia/zamknięcia, szerokość 20% i blokada wejścia do świata pod panelem — PASS.

Energia robotów nie została zaimplementowana. Wymaga osobnej akceptacji checkpointu 2.

Dodatkowo Play Mode: przełączanie zaznaczenia, wykonanie Lua, przejazd NavMesh oraz pełny cykl wydobycia i dostarczenia 10 iron — PASS. Podgląd panelu: lokalny plik Artifacts/Environment/panel-final.png.
