# Robot energy

Each gameplay robot receives its own RobotEnergy component through RobotMovement.Initialize, including factory-created harvesters. Defaults are 100% and 3600 simulation seconds of continuous movement. No battery save system exists yet because the project does not save world state.

RobotMovement samples actual planar displacement and NavMeshAgent velocity in LateUpdate. It ignores pause, path preparation and stationary agents. Braking still consumes energy while the robot is moving. Drain uses Time.deltaTime, so it follows simulation time, including background execution and time scale. The planetary day clock does not multiply the drain.

RobotEnergy exposes Consume, ConsumeMovement, Recharge and Depleted for later integrations. Only movement consumes energy now; there are no charging mechanics. Depletion parks the agent, rejects new movement commands and uses the existing failure path for Lua and harvesting. Robot status becomes NoPower.

The single-selection HUD shows ENERGY and a bar. Text changes only when the selected battery or rounded percentage changes. Existing multi-selection remains unchanged.

## Verification

- Protocol > Validate Robot Energy checks initial charge, separate batteries, 30/60/120/144 FPS, half/full-hour consumption, clamping, depletion events and the configurable test duration.
- Live Unity tests passed: idle, actual movement, pause, stopped agent with active order after braking, accelerated simulation, newly produced robot, natural and external depletion, rejected movement at zero, and switching HUD between 87% and 100%.
- Lua execution, Lua movement rejection at zero, and a complete mining/delivery cycle passed.
- Planet environment validation and four-project C# solution compilation passed.
- Settings storage and focus/music handling were unchanged; focus behavior was reviewed in code, not manually retested.

## Quick test in Editor

Enter Play Mode, select a robot in Hierarchy and set RobotEnergy > Full Battery Movement Duration to 60. Issue movement commands until it reaches zero; idle time does not count. Verify the robot stops and cannot accept another movement command. Exit Play Mode to discard the temporary setting. The production default remains 3600.
