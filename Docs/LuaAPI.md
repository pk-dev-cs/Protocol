# API robota — etap 9

Wywołuj funkcje kropką (`robot.mine(mine)`), nie dwukropkiem. Każdy program ma własną tabelę API i uchwyty obiektów. Lua nie otrzymuje żadnych obiektów Unity.

| Funkcja | Działanie |
| --- | --- |
| `robot.findNearestMine()` | Zwraca uchwyt najbliższej aktywnej kopalni w scenariuszu albo `nil`. Pole `name` jest tekstem do wyświetlania. |
| `robot.moveTo(target)` | Przyjmuje uchwyt kopalni lub drzewa z API. Czeka na dotarcie. |
| `robot.mine(mine)` | Robot musi już stać na swoim stanowisku. Wydobywa 1 iron co 0,75 s do pełnego cargo; dopiero wtedy wznawia kod. Pełne cargo nie jest powiększane. |
| `robot.returnToBase()` | Jedzie do przypisanego stanowiska przy bazie i czeka na dotarcie. Nie rozładowuje cargo. |
| `robot.depositResources()` | Wymaga obecności przy bazie. Po 1 s przekazuje całe cargo graczowi, zeruje je i wznawia kod. |
| `robot.getCargo()` | Aktualna ilość iron w cargo. |
| `robot.getCargoCapacity()` | Maksymalna pojemność cargo (10). |
| `robot.getPosition()` | Kopia pozycji w tabeli `{x, y, z}`. Zmiana tej tabeli nie przesuwa robota. |
| `robot.wait(seconds)` | Czeka podaną liczbę sekund czasu gry (0–3600). Pauza czasu gry zatrzymuje oczekiwanie. Nawet 0 oddaje sterowanie co najmniej do następnej klatki. |

Operacje asynchroniczne nie zwracają wartości. Niepowodzenie zatrzymuje program i pokazuje błąd; kod po wywołaniu nie jest wtedy wykonywany. Uchwyt kopalni pochodzi z `findNearestMine()` w tym samym uruchomieniu programu; samodzielnie utworzona tabela nie jest uchwytem.

Runtime odrzuca nieprawidłową liczbę argumentów, błędne typy, NaN i nieskończoność w `wait`. Dostępność komponentów i celu jest sprawdzana także podczas oczekiwania na akcję. Błąd anuluje akcję tego robota; jego cargo pozostaje. Komunikaty Lua zawierają lokalizację, gdy udostępnia ją interpreter; błędy akcji zawierają nazwę programu i funkcji.

Skrypt otrzymuje wybrane funkcje podstawowe, math, print i wyłącznie `coroutine.yield`. Nie udostępniamy IO, OS, require/load, debug, collectgarbage, obiektów CLR ani tworzenia dodatkowych coroutine. Limity obliczeń i źródła oraz ograniczenia ochrony opisuje checkpoint 9 w README. Pętle długotrwałe muszą oddawać sterowanie przez `coroutine.yield()` lub akcję robota, np. `robot.wait(1)`.

## Wycinka i baza

`robot.findNearestTree()` zwraca uchwyt odkrytego, osiągalnego i niezarezerwowanego drzewa (lub drzewa tego harvestera), albo `nil`. Rezerwuje je dla bieżącego programu. `robot.chop(tree)` wymaga dotarcia i zbiera wood do pełnego cargo lub wyczerpania drzewa. Drzewo ma 30 wood, cargo 10, tempo wynosi 1 wood / 0,75 s. Nie można mieszać wood i iron w cargo; rozładunek w bazie trafia do odpowiedniego licznika. Uchwyt jest ważny tylko w programie, który go otrzymał.

Każdy skrypt otrzymuje globalne `economy` z bieżącymi polami tylko do odczytu: `iron` i `wood`. Baza otrzymuje globalną funkcję `buildHarvester()` zamiast API `robot`. Koszt: 100 wood, czas: 8 s. Wywołanie oddaje sterowanie do ukończenia produkcji; zatrzymanie programu anuluje ją i zwraca koszt. Kolejne wywołanie wymaga ponownego sprawdzenia zasobów.

```lua
if economy.wood > 100 then
    buildHarvester()
end
```

Warunek `>= 100` pozwala produkować również przy dokładnie 100 wood. Do stałego monitorowania użyj pętli z `coroutine.yield()` jak w [przykładzie produkcji](Examples/base_production.lua). Przykład [wycinki](Examples/auto_wood.lua) można uruchomić wspólnie na grupie harvesterów; wszystkie mają niezależne interpretery.

## Testy w Unity

Wybierz robota → Oprogramowanie → strzałkami wybierz „API 1: znajdź kopalnię”, następnie kliknij „Zastąp kod przykładem”. Przykłady API 1–6 zawierają osobne minimalne programy. Wstawiaj i uruchamiaj je po kolei, za każdym razem czekając na Completed. „API 6” sprawdza cargo, pozycję i dwusekundowe oczekiwanie. W edytorze etapu 7 można też wpisywać własny kod.

Przykład ruchu:

```lua
local mine = robot.findNearestMine()
assert(mine ~= nil, 'Brak kopalni')
robot.moveTo(mine)
print('Robot dotarł do kopalni')
```

Następnie osobno uruchom przykład wydobycia, powrotu i rozładunku. Od etapu 8 dostępny jest również przykład „Automatyczne wydobycie (pętla)” i plik [Examples/auto_mining.lua](Examples/auto_mining.lua). Ten program powtarza wszystkie czynności bez dodatkowych poleceń; każdy robot uruchamia własną kopię.

„Zatrzymaj” anuluje bieżący ruch, wydobycie, rozładunek lub wait. Już wydobyte cargo pozostaje; iron trafia do gracza dopiero przy ukończonym rozładunku. Drugi robot działa niezależnie. Mapa nadal ma jedną kopalnię i jedną bazę z osobnymi stanowiskami dla obu robotów.


