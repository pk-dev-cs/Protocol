# URP and planet fog

Protocol uses Universal Render Pipeline 17.4. The pipeline and forward renderer
are stored in Assets/Settings and assigned to every quality level. Depth is
copied after opaque geometry so Pure Volumetric Fog can intersect the landscape.
Directional shadows use four cascades and a 2048 atlas.

Protocol/FogSurface is a URP PBR shader shared by terrain, vegetation and gameplay
objects. It preserves the original material properties, procedural mineral detail,
wind, shadow casting and fog-of-war. Fine world-space detail is filtered with
screen derivatives to reduce distant shimmer. Portrait rendering uses URP render
requests and bypasses fog-of-war on its isolated materials.

Stage01 contains a Pure Volumetric Fog volume and an invisible Terrain height
source matching ScenarioLandscape.HeightAt. The existing visible mesh, colliders
and navigation remain in use. The hidden Terrain has no collider and does not
draw geometry. The fog follows the terrain with a fourteen-metre layer at density
0.04, animated noise
and the scene's directional light. Legacy linear scene fog is disabled.

To rebuild the height source and fog settings after changing landscape heights,
open Stage01 outside Play mode and choose Protocol > Configure Planet Volumetric
Fog. This saves the scene. The fallback runtime world generator does not create
this authored fog setup.

Vendor integration: PureVolumetricFogCommon.hlsl has a small Protocol visibility
mask in GroundFogFrag. It rejects sky/out-of-map pixels and attenuates fog using
_ProtocolFogMap. Preserve/reapply this patch when updating the installed package.
The edit-mode fallback assumes the current 256-unit map. Portrait cameras exclude
the fog object's layer. The renderer includes PureVolumetricFogRendererFeature
at half width and height with depth-aware compositing. Raymarching uses 8–32
steps. The standalone player defaults to 120 FPS with VSync disabled and suspends
background rendering when unfocused. Editor Scene view rendering remains
controlled by Unity.

Both settings menus offer 30, 45, 60, 90 and 120 FPS. Apply persists the selection
under Settings.FpsLimit; changing scenes or regaining focus restores that value.

The top-right clock shows the lighting cycle's local time in HH:mm format.
Stage01 starts at 08:00; one full day takes 2880 simulation seconds (120 seconds per game hour). Pausing the
game stops time. Sunlight fades into blue moonlight at night, with warmer dawn
and dusk. Units and structures switch work lights on at dusk and off at dawn; unexplored locations do not emit light. The violet procedural sky uses continuous fractal clouds, fine stars and a
shaded rocky moon. The gameplay camera deliberately retains a black background
outside the map; inspect the sky with a skybox-enabled camera or Scene view.
