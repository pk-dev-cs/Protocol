# Hauler, Lua editor lock and charging

## Implemented

- Software is read-only when any selected programming target has Running Lua. Code/highlighting/scrolling, Stop and Save remain available. Typing, paste, delete, completion, indentation and undo/redo cannot modify source. Stopped, Completed and Error unlock editing. Selecting another unit updates the lock; opening/closing leaves scripts running.
- Hauler uses existing production, movement, battery, selection, portrait and Lua systems. Its red/graphite tracked placeholder has orange lights and a rear tow hook.
- Production defaults: 200 wood, 16 simulation seconds, 30 kW. Battery: 10 kWh. Movement: 4 kW. Towing adds 6 kW during actual movement. Configured in UnitProductionRecipe and retained in saves.
- Base Lua provides buildHauler(), including IntelliSense.
- Select Hauler, click Tow, then a depleted Harvester/Assembler. Right-click cancels target picking; Anuluj Tow or an ordinary movement command cancels recovery.
- Recovery reserves an available reachable station before pickup. One victim has one owner. Its own propulsion and parked obstacle are suspended; its position follows the Hauler on NavMesh without reparenting. Cancel, station loss, deactivation and Hauler depletion release references/reservations.
- Handoff starts existing charging and releases Hauler. Lua tow() yields until handoff; it does not wait for the victim to finish charging.
- Assembler and Hauler now receive RobotCharging and charging HUD/Lua controls. Charging cancels unfinished construction with the existing wood refund. Starting construction releases charging.
- Station values remain 1 kW idle, 20 kW transfer. All charging energy comes from the existing grid.
- Save/load records type, production counter/recipe, active recovery, victim ownership, reservation and Lua robot handles. Older saves without new fields remain supported.

## Lua examples

Base:

    if economy.wood >= 200 then
        buildHauler()
    end

Hauler:

    while true do
        local target = robot.findNearestDisabledRobot()
        if target ~= nil then
            robot.tow(target)
        else
            robot.wait(1)
        end
    end

All battery-equipped robots:

    local station = robot.findNearestChargingStation()
    if station ~= nil then
        robot.charge(station)
    end

## Important files

- Assets/Scripts/Robots/HaulerTowing.cs: recovery, attachment and cancellation.
- Assets/Scripts/Movement/RobotMovement.cs: ownership and actual movement energy.
- Assets/Scripts/Robots/RobotCharging.cs and Assets/Scripts/Economy/ChargingStationStructure.cs: shared charging and reserved handoff.
- Assets/Scripts/Economy/UnitProductionRecipe.cs, BaseBuilding.cs, HarvesterFactory.cs: production and configuration.
- Assets/Scripts/Lua/RobotProgrammingPanel.cs, LuaCodeEditor.cs: read-only editor.
- Assets/Scripts/Lua/BaseLuaAPI.cs, RobotLuaAPI.cs, LuaEditorLanguage.cs: async towing, production and completion.
- Assets/Scripts/UI/UnitPanel.cs, SelectionPortraits.cs, Assets/Scripts/Selection/SelectionManager.cs: commands, feedback and portrait.
- Assets/Scripts/Saving/SaveStateCodec.cs, GameSaveService.cs: persistence and backward compatibility.
- Assets/Art/Resources/Units/Hauler.prefab and six Hauler mesh assets: placeholder.
- Assets/Editor/RobotModelTools.cs: Protocol / Art / Build Hauler Model.
- Assets/Scripts/Robots/HaulerGameplayChecks.cs: Editor-only integration checks.

## Manual Unity verification

Use fresh Stage01 so test fixtures cannot disturb a game you want to preserve.

1. Run Harvester Lua and open Software. Code is visible; typing/paste/delete/undo/redo/completion are blocked; scroll and Stop work.
2. Select a stopped Assembler: editing works. Re-select running Harvester: lock returns. Stop, Completed and Error unlock it. Closing/opening must leave Lua running.
3. Run buildHauler() in base with 200 wood and power. Progress takes 16 simulation seconds, production adds 30 kW and disappears after deployment.
4. Select/move Hauler; verify portrait, 10 kWh battery, Software and movement consumption.
5. Build Charging Station; drain Harvester in Inspector or via movement/work. Hauler > Tow > click depleted victim. Verify pickup, visual transport, unchanged victim battery, added towing draw and charging handoff.
6. Repeat for Assembler. A second Hauler must not claim the same victim.
7. Send partly depleted Assembler to Ładuj. Check travel, battery growth, 20 kW charging plus station 1 kW idle, full release and construction afterward.
8. Charge Hauler. Check grid/battery conservation and capacity clamp.
9. Cancel during pickup/transport with Anuluj Tow or movement. Victim parks, station frees and NavMesh remains usable.
10. Test no free station, station/victim disable or destruction, depleted Hauler. Check feedback and cleanup.
11. Save during pickup/transport, then load that save. Hauler type, battery/source, victim ownership/position and reservation must resume.
12. Run Hauler Lua example; it must yield during recovery and continue after handoff.
13. Pause, then 1x / 4x. All simulation/energy uses existing time.
14. Check Console for new errors.

## Automated checks

In fresh Stage01 Play Mode choose Protocol / Validate Hauler Gameplay. It tests lock/context, production, Harvester/Assembler recovery, exclusivity, cancellation, pause, charging arithmetic, active tow save/load, Lua continuation, Hauler depletion and station loss. It reloads Stage01 during save/load. Reports and isolated saves go beneath ignored Artifacts, not the player save directory. Exit Play Mode afterward to discard fixtures.

## Validation status

Protocol.slnx compilation passed with 0 errors and 0 warnings. Relevant modified tracked files pass whitespace checks. Unity MCP generated Hauler prefab/meshes successfully.

Unity MCP Play Mode integration checks passed on 2026-10-11: editor lock and context, Stop/Error unlock, timed Hauler production, battery independence, Harvester/Assembler recovery, exclusive reservations, pause, cancellation, active tow save/load, charging transfer arithmetic, direct Assembler/Hauler charging, Lua continuation, depleted Hauler and station loss cleanup. Report: Artifacts/HaulerGameplayChecks.txt. Play Mode was stopped afterward to discard fixtures. Physical keyboard/mouse checks remain for manual verification. During validation, pause handling was tightened and occupied charging docks are now skipped.

## MVP limits

No rope physics, queues or alternative station rerouting. Station loss cancels recovery. Only depleted Harvesters/Assemblers can be towed. Their Lua is stopped on pickup; automatic restart is not part of this task. Hauler portrait uses the existing rendered 3D thumbnail, with a custom portrait slot available later.
