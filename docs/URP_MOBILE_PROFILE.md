# AlphaTown — mobile URP profile

Applied by **AlphaTown ▸ Setup ▸ Apply Mobile URP Profile**. The values live in
`Assets/AlphaTown/Code/Editor/Setup/UrpMobileProfile.cs`; this document explains them.

## Tiers

Every URP asset in the project is matched to a tier by its name, and all three tiers are
mobile targets — **Fidelity means a recent phone or tablet, not a desktop GPU**. There is
no PC tier, because there is no PC build.

| Asset name contains | Tier |
| --- | --- |
| `performant`, `performance`, `low`, `lite` | Performance |
| `fidelity`, `high`, `ultra`, `pc`, `desktop` | Fidelity |
| anything else (`Mobile_RPAsset`, `URP-Balanced`, …) | Balanced |

That covers the Universal 3D template on Unity 6 (`PC_RPAsset`, `Mobile_RPAsset`) and on
2022 LTS (`URP-Performant`, `URP-Balanced`, `URP-HighFidelity`) without renaming anything.
`Mobile_RPAsset` lands on Balanced and becomes `GraphicsSettings.defaultRenderPipeline`.

## Pipeline asset

| Setting | Performance | Balanced | Fidelity | Template default |
| --- | --- | --- | --- | --- |
| MSAA | Off | 2× | 4× | varies |
| Render scale | 0.8 | 1.0 | 1.0 | 1.0 |
| HDR | off | off | on | on |
| HDR precision | 32-bit | 32-bit | 32-bit | 32-bit |
| Colour grading | LDR | LDR | HDR | LDR |
| Grading LUT size | 16 | 32 | 32 | 32 |
| Fast sRGB/linear | on | off | off | off |
| **Main light shadow resolution** | **512** | **1024** | **2048** | **2048** |
| **Shadow cascades** | **2** | **2** | **2** | **4** |
| Cascade 2 split | 0.25 | 0.25 | 0.25 | 0.25 |
| Cascade border | 0.2 | 0.2 | 0.15 | 0.2 |
| Shadow distance | 30 | 40 | 55 | 50 |
| Soft shadows | off | on (Low) | on (Medium) | on |
| Conservative enclosing sphere | on | on | on | off |
| Shadow normal bias | 1.4 | 1.2 | 1.0 | 1.0 |
| Additional lights | Per-vertex | Per-pixel | Per-pixel | Per-pixel |
| Additional lights per object | 2 | 4 | 4 | 4 |
| **Additional light shadows** | **off** | **off** | **off** | on |
| Mixed lighting | on | on | on | on |
| **Depth texture** | **off** | **off** | **off** | on |
| **Opaque texture** | **off** | **off** | **off** | varies |
| SRP batcher | on | on | on | on |
| Dynamic batching | off | off | off | off |
| Terrain holes | off | off | off | on |
| Reflection probe blending | off | off | on | varies |
| Reflection probe box projection | off | off | off | varies |

## Renderer data

| Setting | All tiers | Why |
| --- | --- | --- |
| Rendering path | Forward | Deferred means a G-buffer, and a G-buffer is a bandwidth trap on tile-based GPUs |
| Depth priming | Disabled | A net loss on tilers, which already reject hidden fragments |
| Intermediate texture | Auto | `Always` forces a full-screen copy every frame |
| Copy depth mode | After opaques | Cheaper than after transparents, and unused while the depth texture is off |
| Accurate G-buffer normals | off | Deferred-only |

## Quality levels

Assigned per level, keyed off whichever pipeline asset that level uses.

| Setting | Performance | Balanced | Fidelity |
| --- | --- | --- | --- |
| Anisotropic filtering | Disable | Per-texture | Per-texture |
| Skin weights | 2 bones | 2 bones | 4 bones |
| LOD bias | 0.7 | 1.0 | 1.2 |
| Particle raycast budget | 16 | 64 | 256 |
| Realtime reflection probes | off | off | on |
| VSync count | 0 | 0 | 0 |
| Shadowmask mode | Shadowmask | Shadowmask | Shadowmask |
| Async upload time slice | 2 ms | 2 ms | 2 ms |
| Async upload buffer | 16 MB | 16 MB | 16 MB |

VSync is 0 on every tier because frame rate on mobile belongs to
`Application.targetFrameRate`, not to the vsync counter. Setting that (30 or 60, and
dropping it while the town is idle) is a battery decision for the services layer.

## The four changes that matter most

1. **4 cascades → 2.** Each cascade is another shadow-map render of the whole town.
2. **2048 → 1024 shadow map** on the default asset. Quartering the shadow map is the
   single cheapest quality-per-millisecond trade available, and a top-down camera on a
   short shadow distance barely shows it.
3. **Additional light shadows off.** Realtime shadows from every lamp and window is the
   most expensive thing you can leave enabled in a town builder at night. Bake them.
4. **Depth and opaque textures off.** Both force a resolve out of tile memory. Turn either
   back on per-feature, deliberately, when something actually needs it — and measure.

## Anti-aliasing

MSAA is resolved inside tile memory on mobile GPUs, which makes it far cheaper there than
on desktop and the right default for a game built out of hard building silhouettes.
Post-process AA (FXAA/SMAA) needs an intermediate texture and an extra full-screen pass,
so it is *not* enabled pipeline-wide.

The Performance tier disables MSAA and renders at 0.8 scale instead. If that reads too
soft on real devices, enable **FXAA per camera** (Camera ▸ Rendering ▸ Anti-aliasing) for
that tier only — a camera setting, which is why it is not in this profile.

## Re-running

The configurator is idempotent: it reports and writes only values that differ. Run the
dry-run audit any time to see whether a URP asset has drifted from the profile — that
makes it usable as a CI check.

Settings the installed URP version does not have are reported as
`? m_Something: not in this URP version, skipped` and are not an error; that is the
mechanism that keeps this working across URP upgrades.
