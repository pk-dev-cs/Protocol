# Protocol — checkpoint 10

## Harvestery, iron, wood i produkcja bazy

Podstawowe jednostki nazywają się Harvester. Zasób metal został zastąpiony przez iron; wood jest osobnym zasobem. Zapisane wcześniej programy Builder-01/02 nadal można wczytać pod nowymi nazwami. Nowe zapisy używają nazw Harvester.

Zaznacz grupę jednostek tego samego typu i kliknij **Oprogramowanie**. Edytor pokazuje kod pierwszej jednostki; edycja, **Uruchom** i **Zapisz** stosują go do całej grupy. Każdy harvester ma własny interpreter, zmienne, cargo i plik zapisu. Otwarcie i zamknięcie edytora bez zmian nie nadpisuje pozostałych programów. Dla grup różnych typów przycisk jest wyłączony. W logu widać stany i błędy poszczególnych jednostek.

W edytorze harvesterów wybierz przykład **Automatyczna wycinka (wood)**. `robot.findNearestTree()` zwraca i rezerwuje najbliższe odkryte, osiągalne i wolne drzewo. `robot.moveTo(tree)` podjeżdża do niego, a `robot.chop(tree)` zbiera 1 wood co 0,75 s do pełnego cargo lub wyczerpania drzewa. Drzewo zawiera 30 wood; po wyczerpaniu znika wraz z kolizją i przeszkodą nawigacji. Rezerwacja zwalnia się po zatrzymaniu lub zakończeniu programu. Następnie `robot.returnToBase()` i `robot.depositResources()` dostarczają wood. Cargo mieści 10 jednostek jednego surowca; przed zmianą iron ↔ wood należy je rozładować. Gdy skończą się odkryte drzewa, trzeba zbadać kolejne obszary mapy.

Zaznacz bazę → **Oprogramowanie** → **Zastąp kod przykładem** → **Uruchom**, aby uruchomić automatyczną produkcję. `economy.iron` i `economy.wood` odczytują aktualny stan magazynu. `buildHarvester()` działa tylko w bazie, rezerwuje **100 wood** i wstrzymuje skrypt na **8 sekund** produkcji. Baza produkuje jedną jednostkę naraz. Brak wood powoduje błąd; przykład sprawdza koszt przed wywołaniem. Zatrzymanie programu podczas produkcji anuluje ją i zwraca koszt. Pauza zatrzymuje produkcję. Nowy harvester pojawia się na wolnym obszarze przy bazie i od razu obsługuje zaznaczanie, mapę, widoczność, ruch i Lua. Nie uruchamia automatycznie zapisanego programu.

Przykłady: [wycinka](Docs/Examples/auto_wood.lua), [produkcja bazy](Docs/Examples/base_production.lua). Podpowiedzi obejmują API wycinki oraz `economy`; edytor bazy podpowiada także `buildHarvester()`. Tab przy zamkniętej liście dodaje **4 spacje**, również gdy system wysyła osobno zdarzenie klawisza i znaku.

Weryfikacja w izolowanej kopii Unity: `HARVEST_VALIDATION_PASS` w `Artifacts/harvest-final-unity.log` — wspólny kod i zapis, odrzucenie różnych typów, osobne interpretery, jednoczesne dostawy wood, dostawy iron, rezerwacje i wyczerpanie drzew, produkcja i jej koszt, zwrot przy anulowaniu, tylko odczyt economy oraz wydobycie przez nowo wyprodukowaną jednostkę. `GUI_INPUT_PASS` sprawdza zdarzenia rzeczywistych kontrolek edytora, w tym podwójne zdarzenie Tab. `HARVEST_BUILD=Succeeded` w `Artifacts/harvest-build.log` oraz `HARVEST_PLAYER_PASS` w `Artifacts/harvest-player.log` potwierdzają testowy build Windows / Direct3D 12 i produkcję z Lua w EXE. Wygląd interfejsu i fizyczna klawiatura wymagają sprawdzenia w Game. Zmiany wymagają ponownego uruchomienia scenariusza i nowego buildu EXE. Testowego buildu z automatycznym zakończeniem nie używaj do gry; zwykły folder Build nie został nadpisany.

## Edytor Lua i podpowiedzi

Edytor podświetla słowa kluczowe, komentarze, napisy, liczby oraz API robota. Lista metod otwiera się po wpisaniu kropki w `robot.`. Ctrl+Space wywołuje podpowiedzi ręcznie: metody po `robot.`, a w pozostałym kodzie lokalne deklaracje `local` przed kursorem oraz `robot` i `unitName`. Pisanie początku nazwy zmiennej (np. `min`) również otwiera pasujące podpowiedzi lokalnych zmiennych oraz `robot` i `unitName`. Dalsze pisanie filtruje otwartą listę. Samo przesuwanie kursora nie otwiera podpowiedzi; komentarze i napisy są pomijane.

Strzałki góra/dół wybierają pozycję i przewijają listę. Enter lub Tab wstawia wybraną nazwę bez dopisywania nowej linii. Zmienne są wstawiane bez nawiasów; metody otrzymują nawiasy, o ile nie ma ich już w kodzie. Gdy lista jest zamknięta, Tab wstawia cztery spacje lub wcina zaznaczone wiersze, a Shift+Tab usuwa wcięcie. Escape najpierw zamyka listę. Rozpoznawanie lokalnych deklaracji jest uproszczone, bez pełnej analizy typów i zakresów Lua.

Weryfikacja: testy języka w `Artifacts/LuaLanguageTests` oraz `EDITOR_VALIDATION_PASS` w `Artifacts/editor-unity.log`: wyzwalanie listy, zmienne, obsługa zdarzeń strzałek/Enter/Tab i regresja zapisu, uruchamiania oraz przełączania kodu robotów. Unity skompilowało zmiany; znany wyjątek indeksowania Unity Search nie przerwał testów. Dodatkowy test `GUI_INPUT_PASS` w `Artifacts/editor-input-unity.log` przepuszcza zdarzenia przez rzeczywiste kontrolki TextArea i ScrollView w oknie Unity: Ctrl+Space w pustym wierszu, pisanie `min`, kropkę, strzałki, Enter (również osobne zdarzenie znaku) i Tab. Fokus i pozycję kursora ustawia programowo; nie sprawdza fizycznej klawiatury ani wyglądu interfejsu. Przed sprawdzeniem EXE wykonaj nowy build.

## Minimapa, mgła wojny i portrety

Lewą część dolnego panelu zajmuje minimapa. LPM lub przeciągnięcie LPM po minimapie przesuwa kamerę; PPM wydaje zaznaczonym robotom rozkaz ruchu do wskazanego punktu. Znaczniki pokazują własne roboty, bazę i odkryte kopalnie. Jasna ramka przybliża obszar oglądany kamerą. Kliknięcia minimapy nie przechodzą do świata ani nie zmieniają zaznaczenia.

Środkowa część panelu pokazuje portret zaznaczonego robota, bazy lub kopalni. Przy grupie wyświetlane są portrety obu robotów. Portrety są renderowane z modeli scenariusza, niezależnie od mgły wojny, i przechowywane przez czas rozgrywki.

Mgła wojny ma trzy stany: niezbadany teren jest zasłonięty, odkryty teren poza zasięgiem widzenia jest przyciemniony, a aktualnie widoczny teren ma pełne kolory. Baza odkrywa promień 30 jednostek, roboty 22. Oba roboty dzielą odkryty obszar. Mapa widoczności jest odświeżana co 0,1 sekundy czasu gry i zatrzymuje się podczas pauzy. Odkrycie nie jest zapisywane między uruchomieniami scenariusza. Zasięg jest kołowy; wzgórza i drzewa nie blokują jeszcze linii widzenia.

Nieodkrytych kopalń nie można zaznaczać ani wyszukiwać przez Lua. Obecna kopalnia znajduje się w początkowym zasięgu bazy, więc dotychczasowy program wydobycia nadal działa. Shader mgły znajduje się w Resources i jest dołączany do buildu. Wykonaj nowy build przed testem EXE.

Weryfikacja: `FOG_VALIDATION_PASS` w `Artifacts/fog-unity.log` — odkrywanie i pamięć terenu, blokada nieodkrytego obiektu, współrzędne minimapy, blokada kliknięć do świata, renderowanie czterech portretów i regresja dostaw (80 iron). `FOG_BUILD=Succeeded` i `FOG_PLAYER_PASS` w `Artifacts/fog-build.log` / `Artifacts/fog-player.log` potwierdzają shader, minimapę i portrety w Windows Mono / Direct3D 12. Zapisano obrazy mgły, minimapy i portretów w Artifacts. Testy renderowały świat i tekstury poza ekranem; rozmieszczenie dolnego panelu i gesty myszy należy jeszcze sprawdzić ręcznie w Game.

## Większy scenariusz 1: teren, las i obiekty

Mapa ma teraz 256 × 256 jednostek (wcześniej 60 × 48). Jest to przyjęty rozmiar dla rozwijanego prototypu RTS, a nie uniwersalny standard gatunku. Deterministycznie generowany teren ma wzgórza do około 20 jednostek wysokości, polanę startową, ziemny trakt i 420 sosen. Pnie mają kolizję i są uwzględnione w NavMesh; korony są wizualne. Drzewa dostarczają teraz wood przez API wycinki.

Kamera obejmuje całą mapę, śledzi wysokość terenu i pozwala mocniej oddalić widok. Home wraca do bazy. Limit czasu ruchu zależy teraz od długości trasy; pozostaje wykrywanie utknięcia. Baza, kopalnia i roboty startują na płaskiej polanie, zachowując dotychczasowy cykl wydobycia.

LPM na bazie lub kopalni zaznacza obiekt zielonym obrysem i otwiera panel informacji. Baza pokazuje także dostarczony iron. Zaznaczenie budynku zastępuje wybór robotów; ramka wybiera wyłącznie roboty. Budynki i kopalnia nie przyjmują rozkazów ruchu.

Uruchom scenariusz ponownie, aby zobaczyć zmiany. Świat zapisany wcześniejszym poleceniem Prepare Stage 1 Scene jest aktualizowany przy uruchomieniu. Do podglądu poza Play użyj ponownie tego polecenia; zapisuje teraz również siatki terenu i drzew. Wykonaj nowy build przed testowaniem EXE.

Weryfikacja: `WORLD_VALIDATION_PASS` w `Artifacts/world-unity.log` — rozmiar i wysokości mapy, 420 drzew, raycast zaznaczający bazę i kopalnię, grupa robotów, automatyczne dostawy 80 iron i przejazd do punktu około (100, 95), trwający 39 sekund czasu gry. `LANDSCAPE_BUILD=Succeeded` oraz `LANDSCAPE_PLAYER_PASS` w `Artifacts/landscape-build.log` i `Artifacts/landscape-player.log` potwierdzają również zapis przygotowanej sceny, materiały/siatki i dostawy w Windows Mono na Direct3D 12. Obrazy: `Artifacts/landscape-overview.png`, `Artifacts/landscape-start.png` i `Artifacts/LandscapeBuild/landscape-player.png`. Testy wykonano w kopii projektu; dotychczasowego folderu Build nie nadpisano.

## Sterowanie RTS i pauza

- LPM na robocie zaznacza jedną jednostkę; LPM na pustym terenie usuwa zaznaczenie.
- Przytrzymaj LPM i przeciągnij co najmniej 6 pikseli: zielona ramka zaznacza roboty znajdujące się w jej obrębie, zastępując poprzedni wybór. Działa przeciąganie w każdym kierunku.
- PPM na mapie wydaje rozkaz ruchu zaznaczonym robotom. Grupa dostaje cele oddalone od siebie o 2,8 jednostki. Ręczny rozkaz zatrzymuje Lua i ręczny cykl zbierania; już wydobyte cargo pozostaje.
- Panel grupy pokazuje liczbę robotów i sumę cargo. W celu edycji Lua zaznacz jednego robota.
- Escape otwiera menu pauzy: Wróć do gry, Ustawienia, Wyjdź do menu głównego, Wyjdź z gry. Pauza zatrzymuje czas gry, wykonywanie Lua, wydobycie i kamerę. Escape w ustawieniach wraca do menu pauzy, a w menu pauzy wznawia grę.
- Menu można otworzyć również podczas edycji Lua; po wznowieniu edytor zachowuje roboczy kod. Otwarte menu i edytor blokują zaznaczanie oraz rozkazy ruchu.
- Wyjście do menu kończy scenariusz: cargo, iron i niezapisany kod nie są zachowywane. Zapisane wcześniej programy i zastosowane ustawienia pozostają.

Weryfikacja: `CONTROLS_VALIDATION_PASS` w `Artifacts/controls-unity.log`. Test Play Mode w kopii projektu sprawdził wybór pojedynczy i prostokątny, dojście grupy do oddzielnych celów, zatrzymanie Lua przez ręczny rozkaz, zamrożenie pozycji i logów podczas pauzy, przywrócenie czasu gry, blokady interakcji pod edytorem/menu, usuwanie niedostępnych jednostek z zaznaczenia i powrót do menu głównego. Fizyczne gesty myszy, Escape i wygląd ramki/menu wymagają ręcznego checkpointu w Game. Przed testem EXE wykonaj nowy build.

## Menu główne

W obu panelach ustawień (menu główne i Escape podczas gry) dostępny jest wybór rozdzielczości strzałkami `<` / `>`. Lista zawiera rozdzielczości zgłaszane przez monitor oraz bieżący rozmiar okna, bez powtórzeń dla różnych częstotliwości odświeżania. „Zastosuj” zmienia rozdzielczość i zapisuje ją razem z pozostałymi ustawieniami; „Wróć” pomija zmiany robocze. Rozdzielczość jest przywracana przy następnym uruchomieniu. Zmianę rzeczywistego okna/pełnego ekranu sprawdź w nowym buildzie — widok Game w edytorze ma osobne ustawienia rozmiaru. Kompilacja po zmianie przeszła bez ostrzeżeń.

W edytorze otwórz `Assets/Scenes/MainMenu.unity` i włącz Play. Build uruchamia tę scenę jako pierwszą. „Scenariusze” → „Scenariusz 1” → „Rozpocznij” otwiera dotychczasową mapę `Stage01`. Można nadal otwierać `Stage01` bezpośrednio do testów gameplayu.

„Ustawienia” zawierają głośność globalną, czułość przesuwania/przybliżania kamery i pełny ekran. „Zastosuj” zapisuje wartości przez PlayerPrefs i od razu je stosuje; „Wróć” pomija niezastosowane zmiany. Scenariusz na razie nie ma muzyki ani efektów dźwiękowych. „Wyjdź z gry” zamyka aplikację w buildzie; w edytorze wyświetla informację. Zmiany wymagają nowego buildu. Menu pauzy podczas scenariusza otwiera Escape.

Weryfikacja menu: `MENU_BUILD=Succeeded` i `MENU_PLAYER_PASS` w `Artifacts/menu-build.log` oraz `Artifacts/menu-player.log`. Test Windows Mono / Direct3D 12 obejmował strony menu, zapis i zastosowanie ustawień, blokadę podwójnego rozpoczęcia, załadowanie scenariusza, dwie jednostki z działającym Lua, 20 dostarczonego iron oraz ponowne wczytanie menu przez test. Kopia testowa ma osobną nazwę produktu, aby jej PlayerPrefs nie zmieniały ustawień użytkownika. Ukryte okno testowe nie pozwoliło zweryfikować wyglądu przez zrzuty ekranu — wygląd, fizyczne kliknięcia i przycisk wyjścia wymagają ręcznego sprawdzenia.

## Poprawka uruchomienia buildu Windows

Po zgłoszeniu czarnego ekranu dodano shader Standard do Always Included Shaders w `ProjectSettings/GraphicsSettings.asset`. Materiały tworzone przez `Shader.Find("Standard")` w runtime nie zapewniały jego obecności w buildzie; brak shadera przerywał Awake przed utworzeniem kamery. Poprawiono też kolejność inicjalizacji nawigacji: agent jest wyłączany przed dodaniem przeszkody postoju.

Zbudowano wersję Windows Mono w izolowanej kopii. Test na Direct3D 12 zakończył się `SHADER_PLAYER_SMOKE_PASS` w `Artifacts/shader-fix-player-d3d12.log`; obraz `Artifacts/BuildShaderFix/shader-smoke.png` potwierdza renderowanie świata i obu robotów. Test startu/renderowania nie zastępuje pełnego testu interfejsu i gameplayu w buildzie; IL2CPP pozostaje niesprawdzone. Wykonaj nowy Build z głównego projektu — istniejący folder `Build` nie został nadpisany.

Etapy 1–9 zaakceptowane. Etap 10: końcowy przegląd i testy MVP. Odbiór projektu wymaga ręcznego testu użytkownika według [końcowej checklisty](Docs/FinalMVPChecklist.md).

Poprawki końcowe: status robota pokazuje Error po błędzie Lua; ręczny cykl zbierania bezpiecznie odrzuca lub przerywa pracę po utracie komponentów; kamera zatrzymuje sterowanie już w Update po otwarciu edytora. Nie dodano nowych funkcji.

Wynik: `STAGE10_VALIDATION_PASS` w `Artifacts/stage10-unity.log`. Przeszły testy referencji sceny, wyboru obu robotów, blokady kamery w edytorze, utraty komponentów, zapisów programów, wielokrotnych dostaw (80 iron), stop/restart, przeładowania sceny oraz 20 błędnych programów przy pracującym drugim robocie (20 iron). Brak błędów kompilacji i nieoczekiwanych wyjątków runtime. Osobno odnotowano znany wyjątek indeksowania Unity Search. Test nie renderował interfejsu.

## Checkpoint zabezpieczeń — etap 9

1. Włącz Play w `Assets/Scenes/Stage01.unity`. Na Harvester-02 uruchom „Automatyczne wydobycie (pętla)”.
2. Na Harvester-01 otwórz Oprogramowanie. Wybierz strzałkami „Błąd: pętla bez yield”, kliknij „Zastąp kod przykładem”, następnie Uruchom.
3. Po 120 klatkach obliczeń program powinien pokazać Error z komunikatem o limicie. Kamera i panel powinny nadal reagować, a Harvester-02 kontynuować dostawy.
4. Powtórz dla przykładów błędu składni, wykonania, fałszywej kopalni, argumentu wait, nieistniejącego celu, spamu logu i dostępu do pliku. Spam kończy się limitem; log zachowuje najwyżej 4096 znaków.
5. Wstaw poprawny przykład powitania i uruchom ponownie: oczekiwany stan Completed. Uruchom `robot.wait(100)` i kliknij Zatrzymaj: oczekiwany Stopped.

Celowo błędne skrypty są również w `Docs/Examples/invalid_*.lua`. Nie trzeba ich zapisywać, aby przeprowadzić test. Zapisz nadpisuje zapisany program wybranego robota.

Runtime sprawdza czas między małymi porcjami instrukcji VM: do 512 wznowień i docelowo 2 ms na klatkę na robota. Po 120 kolejnych klatkach obliczeń bez jawnego yield lub oczekującej akcji zatrzymuje program. Kontrola źródła przed kompilacją ogranicza zagnieżdżenia do 48, operatory do 128 i tokeny do 2048 (konserwatywny skaner, nie pełny parser Lua). Pozostaje limit 16000 znaków kodu. Niedostępny robot, komponent, kopalnia, baza albo cel ruchu powoduje kontrolowany błąd i anulowanie akcji.

Limit czasu jest kooperatywny: pojedyncza operacja VM/CLR może przekroczyć 2 ms. Nie ma twardego limitu pamięci ani izolacji procesu, więc to zabezpieczenia MVP, a nie gwarancja bezpieczeństwa dowolnego wrogiego kodu. Test bez renderowania nie zastępuje sprawdzenia responsywności interfejsu w Unity.

Weryfikacja: `STAGE9_VALIDATION_PASS` w `Artifacts/stage9-unity.log`. Test w izolowanej kopii projektu objął 20 błędnych programów, limit logu i liczby kroków, poprawny restart, anulowanie oczekiwania, wyłączenie celu/komponentu/kopalni/bazy oraz dwie dostawy drugiego robota (20 iron). Końcowy przebieg skompilował się w Unity i zakończył powodzeniem. Nadal występuje znany wyjątek indeksowania `UnityEditor.Search.SearchDatabase` w trybie headless; nie przerwał testów. Nie testowano jeszcze buildu ani IL2CPP.

## Checkpoint automatyzacji — etap 8

1. Uruchom scenę w Play i wybierz Harvester-01 → Oprogramowanie.
2. Strzałkami wybierz „Automatyczne wydobycie (pętla)” i kliknij „Zastąp kod przykładem”.
3. Kliknij Zapisz (jeśli chcesz zachować program), następnie Uruchom i Zamknij.
4. Robot sam jedzie do kopalni, wydobywa do 10 / 10, wraca, rozładowuje i zaczyna kolejny przejazd. Nie klikaj przycisku cyklu testowego C#.
5. Uruchom ten sam przykład na Harvester-02. Możesz dopisać `robot.wait(2)` przed pętlą, żeby drugi program zaczął później.
6. Obserwuj przynajmniej trzy dostawy każdego robota: licznik iron powinien rosnąć o 10 na dostawę, a programy pozostawać Running.
7. Otwórz edytor pierwszego robota i kliknij Zatrzymaj: drugi nadal pracuje. Uruchom ponownie, aby wznowić dostawy pierwszego.

Program jest także w [Docs/Examples/auto_mining.lua](Docs/Examples/auto_mining.lua). Gdy brak aktywnej kopalni, czeka sekundę i wyszukuje ponownie. Zachowano istniejący zapis programów: nowy przykład nie zastępuje automatycznie Twojego kodu i nie uruchamia się sam przy wejściu do Play.

Weryfikacja etapu 8: kompilacja bez ostrzeżeń oraz `STAGE8_VALIDATION_PASS` w `Artifacts/stage8-unity.log`. Oba roboty wykonały po trzy dostawy bez nowych poleceń (60 iron), drugi dostarczył kolejne 10 po zatrzymaniu pierwszego, a pierwszy następne 10 po restarcie programu (razem 80). Sprawdzono też oczekiwanie przy braku kopalni i zgodność licznika z liczbą dostaw. Test w izolowanej kopii używał czasu gry x2 i nie renderował obrazu.

Podczas testu poprawiono blokowanie tras przez nieruchome roboty. RobotMovement przełącza robota między NavMeshAgent podczas ruchu i przeszkodą z carvingiem podczas postoju; nie włącza ich jednocześnie. Przed rozpoczęciem kolejnej trasy czeka dwie klatki na aktualizację NavMesh. Pozwala to planować objazd robota stojącego przy kopalni lub zatrzymanego przyciskiem. Statyczne budynki i bariera nadal nie są przebudowywane po przesunięciu w Play.

## Checkpoint edytora — etap 7

1. Wyłącz Play, poczekaj na kompilację i uruchom scenę ponownie.
2. Wybierz Harvester-01 → Oprogramowanie. Kliknij kod, Ctrl+A i wpisz `print('Program pierwszego robota')`. Kliknij Zapisz, potem Uruchom.
3. Zamknij edytor, wybierz Harvester-02 i wpisz `print('Program drugiego robota')`. Zapisz i uruchom. Wróć do Harvester-01: jego kod i log powinny być niezależne.
4. Dopisz komentarz bez zapisu, zamknij panel i otwórz ponownie: zmiana pozostaje i jest oznaczona jako niezapisana.
5. Zapisz, wyłącz Play i włącz ponownie: zapisane programy wracają, lecz nie uruchamiają się samoczynnie.
6. Wpisz błędny kod, uruchom, sprawdź ERROR, popraw i uruchom ponownie.
7. Uruchom `robot.wait(10)` i kliknij Zatrzymaj. Stan zmieni się na Stopped.

Przykłady z wcześniejszych etapów wybiera się teraz strzałkami, a następnie przyciskiem „Zastąp kod przykładem”. Same strzałki nie zmieniają kodu. Wstawiony przykład zastępuje treść roboczą; plik zostaje nadpisany dopiero po Zapisz. Przyciski „Uruchom Lua” i „Zatrzymaj Lua” noszą teraz nazwy „Uruchom” i „Zatrzymaj”.

Uruchom wykonuje treść roboczą bez automatycznego zapisu. Edycja i zapis w czasie działania nie zmieniają skompilowanego programu — zmiany wejdą przy następnym uruchomieniu. Zamknięcie panelu zachowuje roboczy kod w bieżącej sesji i nie zatrzymuje programu. Niezapisane zmiany znikają po zakończeniu Play.

Programy są zapisywane jako UTF-8 w osobnych plikach `Application.persistentDataPath/RobotPrograms/stage01-Harvester-01.lua` i `stage01-Harvester-02.lua`. Przy obecnych ustawieniach Windows folder to `%USERPROFILE%/AppData/LocalLow/DefaultCompany/Protocol/RobotPrograms`. Błąd zapisu jest pokazywany w panelu i nie oznacza kodu jako zapisanego. Edytor ma limit 16000 znaków i przewijanie; bez kolorowania składni i autouzupełniania.

Weryfikacja etapu 7: kompilacja C# bez ostrzeżeń oraz `STAGE7_VALIDATION_PASS` w `Artifacts/stage7-unity.log`. Test obejmował osobne pliki, ponowny odczyt UTF-8, podmianę zapisu, zachowanie roboczych zmian po zamknięciu/przełączeniu, niezależność wykonywanego programu od edycji, stop/restart, poprawianie błędów Lua, kontrolowany błąd zapisu i blokadę kliknięć pod edytorem. Testowe zapisy powstały tylko w kopii projektu w Artifacts. Wygląd i fizyczne wpisywanie tekstu wymagają ręcznego checkpointu.

## Uruchomienie

1. Uruchom Unity Hub (licencja Unity Personal została potwierdzona).
2. Unity Hub → Add → Add project from disk → wybierz ten katalog.
3. Otwórz w Unity **6000.4.7f1**, zaczekaj na import i kompilację.
4. Otwórz `Assets/Scenes/Stage01.unity` i naciśnij Play.
5. Kliknij widok Game. Kamera: WASD/strzałki, Shift przyspiesza; kółko myszy przybliża/oddala; przeciąganie środkowym przyciskiem przesuwa; Home przywraca widok.

Scena startowa buduje świat w Awake. Aby oglądać i edytować wszystkie obiekty również poza Play, wybierz **Protocol → Prepare Stage 1 Scene** przed uruchomieniem. Polecenie zapisze obiekty i materiały w projekcie. Ponowne wywołanie nie duplikuje świata.

Interakcje, bariera testowa i NavMesh dołączają się automatycznie po Play, także do sceny zapisanej w etapie 1. Nie trzeba odtwarzać sceny ani wykonywać bake'u. Jeśli projekt jest otwarty, wyłącz Play i poczekaj na import modułu AI oraz kompilację skryptów przed ponownym Play.

## Checklista

- Teren ze wzgórzami, polaną i lasem; kamera patrzy ukośnie z góry.
- Po lewej baza z turkusowym rdzeniem i placem dostaw.
- Po prawej kopalnia z bursztynowymi kryształami.
- Przed bazą dwa roboty: Harvester-01 (turkus), Harvester-02 (bursztyn).
- Kamera przesuwa się, zoom ma ograniczenia, Home przywraca początek.
- Lewy przycisk na pierwszym robocie: zielony pierścień, nazwa Harvester-01, Status: Idle, Cargo: 0 / 10.
- Lewy przycisk na drugim robocie: pierścień przechodzi na Harvester-02, panel pokazuje jego nazwę.
- Lewy przycisk na „Oprogramowanie”: edytor kodu wybranego robota.
- Lewy przycisk na pustym terenie usuwa zaznaczenie; baza i kopalnia otwierają własny panel informacji. Kliknięcia panelu nie zmieniają zaznaczenia.
- Kółko i środkowy przycisk nad panelem nie sterują kamerą; nad mapą działają nadal.
- Wybierz robota i kliknij „Jedź do kopalni (test)”: status zmienia się na Moving, robot jedzie i omija grafitową barierę.
- Po dotarciu do placu przed kopalnią robot zatrzymuje się: Idle oraz „Dotarto do celu.”.
- Wyślij także drugiego robota, najlepiej podczas jazdy pierwszego. Każdy ma własny punkt docelowy przy kopalni.
- Włącz Play ponownie, aby powtórzyć przejazd od pozycji startowych.
- U góry ekranu widać Metal: 0.
- Wybierz robota i kliknij „Zbierz i dostarcz (test)”. Robot jedzie do kopalni (Moving), wydobywa (Mining), wraca z pełnym cargo (Returning) i rozładowuje się (Depositing).
- W kopalni cargo rośnie co 0,75 s: 1 / 10, 2 / 10, ... 10 / 10. Robot pozostaje przy kopalni podczas wydobywania.
- Rozładunek przy bazie trwa 1 s; potem cargo spada do 0, Metal rośnie o 10, status wraca do Idle.
- Uruchom cykl dla obu robotów równocześnie: po ich zakończeniu Metal powinien wynosić 20. Powtórzenie obu cykli zwiększa licznik do 40.
- Kliknięcia licznika iron nie zmieniają zaznaczenia ani nie sterują kamerą.
- Harvester-01 → Oprogramowanie → Uruchom Lua: wynik 42 oraz stan Completed.
- Zamknij panel, wybierz Harvester-02 → Oprogramowanie → Uruchom Lua: stan Running i licznik 60, 120, ... (liczony w klatkach, nie sekundach).
- Zamknij panel i wróć do niego: program nadal działa. „Zatrzymaj Lua” zatrzymuje licznik, „Uruchom Lua” uruchamia świeży interpreter od początku.
- Przy zatrzymanym programie przełączaj przykłady przyciskami Poprzedni/Następny. „Błąd składni” i „Błąd wykonania” mają dać stan Error oraz komunikat ze wskazaniem miejsca w skrypcie.
- Uruchom poprawny przykład po błędzie. Drugi robot zachowuje własny kod i log.
- W panelu Lua wybierz kolejno nowe przykłady API 1–6. Każdy uruchamiaj osobno i zaczekaj na Completed: wyszukanie kopalni, ruch, wydobycie, powrót, rozładunek, odczyt cargo/pozycji i wait.
- Przykład API 3 wymaga wcześniejszego dojazdu do kopalni (API 2); API 5 wymaga powrotu do bazy (API 4). Komunikat po wywołaniu pojawia się dopiero po zakończeniu czynności.
- Podczas jazdy lub wydobywania użyj „Zatrzymaj Lua”: ruch/wydobycie ustaje, dotychczasowe cargo pozostaje. Ponowne uruchomienie przykładu może dokończyć czynność.
- Zamknij panel, aby obserwować robota lub uruchomić program drugiego. Jeden robot po testach API 2–5 oddaje 10 iron; dwa oddają 20.
- Console bez błędów.

Przycisk zbierania uruchamia jeden cykl, bez Lua i bez automatycznego powtarzania. Kopalnia ma niewyczerpany zasób na potrzeby MVP. Zasoby i cargo nie są zapisywane między uruchomieniami Play. NavMesh powstaje raz przy starcie: przesunięcie statycznych przeszkód podczas Play nie przebudowuje siatki tras.

## Decyzje techniczne

Built-in Render Pipeline, bez zewnętrznych assetów. Moduły Unity: physics, audio, imgui i ai. Docelowa wersja: lokalne Unity 6000.4.7f1. Etap 3 korzysta z wbudowanego NavMeshBuilder i NavMeshAgent; dla jednej statycznej mapy nie potrzebuje dodatkowego pakietu wysokopoziomowych komponentów AI Navigation. NavMesh wyklucza roboty i dekoracje, uwzględnia teren, bazę, kopalnię i barierę. RobotMovement udostępnia wynik operacji i zdarzenie Completed; odrzuca niepełne trasy, zatrzymuje ruch po utracie celu i ma limit czasu przejazdu.

Etap 5: dołączono MoonSharp 2.0.0.0 wraz z licencją w Assets/Plugins/MoonSharp. Każdy RobotLuaRuntime ma osobny kod, interpreter i log. Każde uruchomienie zaczyna się od świeżych zmiennych. Skrypt dostaje unitName (tekst), podstawowe funkcje Lua, math i coroutine.yield() oddające sterowanie do następnej klatki. Nie otrzymuje obiektów C#/Unity ani dostępu do plików, OS, dynamicznego ładowania kodu czy tworzenia zagnieżdżonych coroutine.

Runtime korzysta z małych porcji VM (AutoYieldCounter=1) z budżetem do 512 wznowień i docelowo 2 ms na klatkę. 120 kolejnych klatek obliczeń bez jawnego yield kończy program błędem. Aktualne limity i ich ograniczenia opisuje checkpoint 9 powyżej. IL2CPP i buildy poza edytorem nie zostały sprawdzone.

RobotProgrammingPanel edytuje SourceCode konkretnego robota, a RobotProgramStorage zapisuje go i przywraca przy starcie sceny. Dostęp do plików pozostaje wyłącznie po stronie C# — nie udostępniono go Lua. Zatrzymanie programu przerywa jego bieżącą akcję, zachowując cargo.

Etap 6: RobotLuaAPI rejestruje ograniczoną tabelę robot. Operacje trwające w czasie zawieszają coroutine; host w kolejnych klatkach sprawdza zakończenie i dopiero wtedy wznawia kod. Wykorzystują istniejące RobotMovement, Mine, BaseBuilding i RobotInventory. Przy uruchomionym Lua przyciski ręcznego ruchu/zbierania są zablokowane; aktywny cykl C# musi zakończyć się przed uruchomieniem Lua. Opis funkcji i warunków: [Docs/LuaAPI.md](Docs/LuaAPI.md).

Etap 4: RobotInventory przechowuje cargo i pilnuje pojemności, Mine określa tempo wydobywania i sprawdza odległość, BaseBuilding sprawdza punkt rozładunku, ResourceManager przechowuje wspólny iron. RobotHarvestCycle wykonuje pojedynczy cykl testowy jako maszynę stanów aktualizowaną w kolejnych klatkach. Przerwanie cyklu nie usuwa cargo. Dwa roboty mają osobne stanowiska przy kopalni i bazie.

Źródła: https://unity.com/releases/unity-6 oraz https://www.moonsharp.org/coroutines.html i https://www.moonsharp.org/sandbox.html

## Weryfikacja

Etapy 1–5 zostały zaakceptowane przez użytkownika. Etap 6: kompilacja C# bez ostrzeżeń i pomyślny test Unity Play Mode na izolowanej kopii projektu. Sprawdzono osobno wszystkie przykłady API dla dwóch robotów, oczekiwanie na akcje, 20 dostarczonego iron, zatrzymanie ruchu/wydobycia/wait, zachowanie cargo, oczekiwanie podczas pauzy czasu gry, odrzucenie fałszywego uchwytu i błędnych argumentów oraz blokadę ręcznego cyklu podczas Lua. Wynik: `STAGE6_VALIDATION_PASS` w `Artifacts/stage6-unity.log`.

Test bez renderowania nie zastępuje ręcznego checkpointu. W kopii testowej Unity nadal zgłasza znany z etapu 3 wyjątek edytora `UnityEditor.Search.SearchDatabase` podczas indeksowania assetów; nie przerwał on testu. Jeśli wystąpi w Twojej sesji, przekaż dokładny log Console.






