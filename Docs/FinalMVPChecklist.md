# Końcowy test MVP — etap 10

Otwórz projekt w Unity 6000.4.7f1, scenę `Assets/Scenes/Stage01.unity`, wyczyść Console i włącz Play. Poniższe pola wypełnij po ręcznym teście. Automatyzacja sprawdza logikę; nie potwierdza fizycznych kliknięć, czytelności panelu ani responsywności renderowanego widoku Game.

| # | Definition of Done — sprawdzenie ręczne | Pokrycie automatyczne |
| --- | --- | --- |
| 1 | [ ] Scena uruchamia się w Play. | Start i kolejne przeładowania sceny. |
| 2 | [ ] Widoczne są baza, kopalnia i dwa roboty. | Obiekty, komponenty i brak brakujących skryptów. |
| 3 | [ ] WASD/strzałki, kółko, przeciąganie środkowym przyciskiem i Home sterują kamerą. | Ruch i blokada podczas otwarcia edytora; gesty do sprawdzenia ręcznie. |
| 4 | [ ] Lewy przycisk wybiera każdego robota. | Raycast z pozycji ekranowej obu robotów. |
| 5 | [ ] Panel i zielony pierścień pokazują wybranego robota. | Zaznaczenie i przełączenie jednostki. |
| 6 | [ ] Kliknięcie Oprogramowanie działa. | Otwarcie panelu przez jego metodę. |
| 7 | [ ] Edytor Lua jest widoczny i mieści się w Game. | Stan panelu i blokada kliknięć w mapę. |
| 8 | [ ] Można wpisać, zaznaczyć, wkleić i przewinąć kod. | Robocza treść, osobne zapisy UTF-8 i ponowny odczyt. |
| 9 | [ ] Uruchom wykonuje kod wpisany do edytora. | Start, stop, restart i niezmienność działającego programu podczas edycji. |
| 10 | [ ] Robot jedzie do kopalni. | Ruch w automatycznej pętli Lua. |
| 11 | [ ] Robot wydobywa zasoby. | Stan Mining w obu programach. |
| 12 | [ ] Cargo dochodzi do 10/10. | Granice cargo i pełne dostawy. |
| 13 | [ ] Robot wraca do bazy. | Stan Returning. |
| 14 | [ ] Robot oddaje cargo. | Stan Depositing i zgodność transferu. |
| 15 | [ ] Iron rośnie o 10 na pełną dostawę. | Dokładna suma zgodna z dostawami. |
| 16 | [ ] Robot sam powtarza proces. | Po trzy dostawy obu robotów bez nowych poleceń. |
| 17 | [ ] Drugi robot wykonuje niezależny program. | Równoległe pętle, zatrzymanie pierwszego i restart. |
| 18 | [ ] Błędny Lua nie zawiesza gry ani sterowania. | 20 błędnych programów przy działającym drugim robocie. |
| 19 | [ ] Błąd jest widoczny w edytorze i statusie robota. | Error, komunikat błędu i powrót do poprawnego programu. |
| 20 | [ ] Brak krytycznych błędów kompilacji. | Kompilacja wszystkich skryptów w izolowanej kopii Unity. |

## Przebieg ręcznego testu

1. Sprawdź kamerę, wybór obu robotów i ich panele.
2. Na obu robotach wybierz „Automatyczne wydobycie (pętla)” i kliknij „Zastąp kod przykładem”, następnie Uruchom. Drugiemu możesz dopisać `robot.wait(2)` przed pętlą. Zamknij edytor i obserwuj co najmniej trzy dostawy każdego robota.
3. Zatrzymaj pierwszy program. Drugi ma nadal dostarczać iron. Uruchom pierwszy ponownie.
4. Na pierwszym zatrzymaj program i uruchom przykład „Błąd: pętla bez yield”, potem błąd składni i błąd wykonania. Sprawdź komunikat, status Error i działanie kamery po zamknięciu panelu. Drugi robot nadal pracuje.
5. Wpisz `print('Mój program')`, uruchom i sprawdź Completed oraz log. Zapisz tylko jeżeli chcesz zastąpić dotychczasowy zapis tego robota. Wyłącz i włącz Play: zapis ma wrócić, ale nie uruchomić się sam.
6. Sprawdź Console. Zgłaszając problem, podaj komunikat, kod Lua i kroki odtworzenia.

## Zakres weryfikacji

Końcowy przebieg zakończył się `STAGE10_VALIDATION_PASS` w `Artifacts/stage10-unity.log`. Przeszły wszystkie wymienione testy automatyczne; nie odnotowano błędów kompilacji ani nieoczekiwanych błędów runtime. Test automatyzacji zakończył się sumą 80 iron, a w teście błędnych skryptów drugi robot dostarczył 20 iron.

Testy działają w `Artifacts/Stage10ValidationProject`; testowe zapisy mają osobny katalog. Scena produkcyjna i zapisane programy użytkownika nie są nadpisywane. Końcowy runner ponownie wykorzystuje testy etapów 7–9. W kopii testu etapu 8 wskazuje przykład automatyzacji numer 10, ponieważ przykłady zabezpieczeń nie są automatyką wydobycia.

Ograniczenia pozostają bez zmian: brak testu buildu/IL2CPP, kooperatywny limit czasu Lua bez twardego limitu pamięci, statyczny NavMesh i brak zapisu cargo/iron między sesjami. Znany wyjątek indeksowania `UnityEditor.Search.SearchDatabase` pochodzi z edytora; test nie traktuje go jako błędu gameplayu, ale wszystkie inne błędy/wyjątki runtime powodują niepowodzenie.

MVP wymaga jeszcze ręcznego zatwierdzenia powyższej listy przez użytkownika.


