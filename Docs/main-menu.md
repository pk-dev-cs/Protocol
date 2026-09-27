# Main menu — odbiór

Scena: `Assets/Scenes/MainMenu.unity` (pierwsza aktywna scena buildu).

## Konstrukcja

- Scena zawiera zapisane obiekty 3D, kamerę, Canvas uGUI i lokalny EventSystem.
- Harvester powstaje przez istniejące `ScenarioBootstrap.BuildRobot`, bez komponentów rozgrywki i colliderów. Nie uruchamiamy `ScenarioBootstrap`.
- Krajobraz wykorzystuje istniejące siatki terenu, bazaltu i roślin. Materiały w `Assets/Materials/MainMenu` są wariantami niezależnymi od rozgrywki i ignorują mgłę wojny.
- Fioletowy skybox, oświetlenie świtu, lekka mgła Pure Volumetric Fog i lokalny profil tonemappingu należą wyłącznie do tej sceny.
- `MainMenu` steruje stronami i istniejącym przejściem do Stage01. `MainMenuView` prezentuje ustawienia, korzystając z GameSettings, FrameRatePolicy i ScreenResolutionPicker.
- `MainMenuPresentation` delikatnie moduluje światło sensora i dopasowuje kadr przy zmianie proporcji ekranu.
- MusicPlayer pozostaje globalnym singletonem. Jego dwa AudioSources obsługują istniejący crossfade. Scena nie zawiera dodatkowego odtwarzacza muzyki.
- Narzędzie `Protocol > Rebuild Main Menu Presentation` odtwarza scenę i warianty materiałów. Uruchomienie zastępuje ręczne poprawki tej sceny; nie jest potrzebne do grania ani budowania projektu.

## Procedura odbioru

1. Otwórz MainMenu i uruchom Play. Osobno sprawdź start zbudowanej aplikacji: powinna od razu pokazywać menu, bez Stage01 w tle.
2. Sprawdź kadr: niska kamera, Harvester po prawej, skały/roślinność/platforma w dalszych planach, fioletowe niebo. Kamera nie reaguje na sterowanie RTS; światło sensora i atmosfera poruszają się subtelnie.
3. Sprawdź Scenariusze, Ustawienia i Wyjdź. Najedź myszą na przyciski, kliknij je i przejdź listę strzałkami z Enterem. Zaznaczenie musi być widoczne także tuż po wejściu do menu.
4. Otwórz Ustawienia. Zmień wartości, wróć bez Zastosuj i otwórz ponownie: poprzednio zapisane wartości powinny wrócić.
5. Zmień głośność ogólną, głośność muzyki, przełącznik muzyki i czułość kamery. Kliknij Zastosuj. Sprawdź efekt oraz zachowanie wartości po ponownym uruchomieniu gry. Przy wyłączonej muzyce suwak jej głośności powinien być nieaktywny.
6. W zbudowanej aplikacji sprawdź rozdzielczość i pełny ekran. Sprawdź limit FPS oraz osobny limit po utracie focusu. Domyślny limit nadal wynosi 120 FPS, jeśli użytkownik nie zapisał innego.
7. Scenariusze > Scenariusz 1 > Rozpocznij. Obraz powinien krótko wygasnąć i załadować Stage01. Szybkie podwójne kliknięcie nie może uruchomić dwóch ładowań.
8. W rozgrywce otwórz pauzę klawiszem Escape i wróć do głównego menu. Powtórz wejście i wyjście kilka razy. Nie powinno być dekoracji menu w grze, duplikatów kamer/listenerów ani przyciemnionego przez mgłę wojny Harvestera w menu.
9. Słuchaj muzyki podczas przechodzenia między scenami. Utwór powinien kontynuować odtwarzanie, bez restartu i drugiego niezależnego odtwarzacza. Poczekaj do końca utworu: dopuszczalne jest krótkie nakładanie dźwięku w ramach dotychczasowego crossfade; po nim powinien pozostać jeden utwór.
10. W buildzie przełącz się Alt+Tab do innej aplikacji na 10–20 sekund i wróć. Muzyka ma być niesłyszalna w tle, ale czas odtwarzania powinien postępować. Powrót nie może restartować utworu. Powtórz w trakcie zmiany utworów oraz z wyłączoną muzyką.
11. Sprawdź każdą stronę w 1920×1080 (16:9), 1920×1200 (16:10) i 2560×1080 (ultrawide). W Unity użyj odpowiedniej rozdzielczości Game View, a w buildzie ustawień ekranu. Kontrolki i przyciski nie mogą wychodzić poza ekran ani zasłaniać kluczowej części Harvestera.
12. Wyjdź z gry. W edytorze przycisk kończy Play Mode, w buildzie zamyka aplikację. Sprawdź Console i Player.log pod kątem nowych błędów.

## Sprawdzone podczas implementacji

- Kompilacja skryptów w Unity i uruchomienie menu.
- Nawigacja do scenariuszy i ustawień, odrzucanie niezapisanych zmian, zapis przez istniejące usługi oraz zatrzymanie muzyki po jej wyłączeniu.
- Brak RobotController i NavMeshAgent w menu.
- Przejście MainMenu → Stage01 → MainMenu: ten sam MusicPlayer, jeden AudioListener i brak obiektów prezentacyjnych menu w Stage01.
- Układ 16:9, 16:10 i 21:9 sprawdzony w renderach do tekstury z odpowiednim skalowaniem Canvas. Rzeczywiste przełączanie trybu ekranu i Alt+Tab wymaga dodatkowo odbioru w buildzie według procedury powyżej.

## Końcowa weryfikacja kompilacji

`MSBuild Protocol.slnx /restore /t:Build /p:Configuration=Debug` zakończył się poprawnie dla wszystkich czterech projektów; pliki wyjściowe znajdują się lokalnie w `Artifacts/MainMenuCompile`.

Pełny build aplikacji Unity nie został wykonany: po końcowym przeładowaniu skryptów połączenie MCP przestało odpowiadać. Wcześniejsze testy Play Mode i przejść scen zakończyły się powodzeniem. Końcowa poprawka przywracająca zaznaczenie klawiatury po fade przeszła kompilację, ale wymaga sprawdzenia interakcji w uruchomionej grze.
