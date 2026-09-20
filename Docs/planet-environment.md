# Detailed planet environment

Stage01 contains a first environment study around the starting base and mine.
Terrain now uses continuous world-space mineral detail instead of hard material
boundaries. All 1680 harvestable plants across the map have curved leaf meshes.
The surrounding understory contains 288 deterministic decorative clusters, and
the mine uses weathered mineral meshes. The base/robot models retain their
prototype geometry.

Open Stage01 outside Play Mode and use **Protocol > Apply Detailed Planet
Environment** to recreate the study. This replaces the generated `Planet detail
study` subtree, updates all tree crown meshes and materials, updates the mine
visuals, and saves the scene. Manual edits inside that generated subtree will be
replaced. Assets are stored in `Assets/Art/PlanetStudy` and shared by instances.

Harvestable trees keep their original roots, colliders and resource behavior.
Understory and small rocks are decorative, have no colliders and do not affect
the NavMesh. Leaf motion is bounded and follows game time, including pause.
The fog-of-war material preserves the terrain and leaf detail properties.
Tree crowns use three LOD meshes (2560, 640 and 192 triangles per crown).
The distant mesh does not cast shadows. Density is 1680 harvestable plants;
LOD affects only rendering. Reapplying the environment command updates the LODs.

Grass uses deterministic combined meshes in 256 sectors, with near and sparse
far LODs and distance culling. Blades sway in the existing wind shader, cast no
shadows and have no colliders. Roads and the starting clearing remain open.
The environment command regenerates the Planet grass subtree and its mesh assets.

Materials and meshes are procedurally authored in this project; this pass adds
no downloaded art or package dependencies. The detailed layout is baked into
the saved scene. The emergency runtime world generator remains the simpler
prototype; after generating a new world, apply the editor command again.

This is a direction study toward more natural scenery, not a photorealistic
asset set. Larger production art, LOD tuning and measured target-hardware
performance remain separate work.
