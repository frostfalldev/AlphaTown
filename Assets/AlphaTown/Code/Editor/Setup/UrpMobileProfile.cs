using System.Collections.Generic;
using UnityEngine;

namespace AlphaTown.EditorTools.Setup
{
    /// <summary>
    /// Quality tier a URP asset is tuned for. All three tiers target mobile hardware:
    /// "Fidelity" means a recent phone or tablet, not a desktop GPU.
    /// </summary>
    internal enum UrpTier
    {
        Performance,
        Balanced,
        Fidelity
    }

    /// <summary>A single serialized field to force on a URP asset, with the reason we force it.</summary>
    internal readonly struct SettingOverride
    {
        public readonly string PropertyPath;
        public readonly object Value;
        public readonly string Note;

        public SettingOverride(string propertyPath, object value, string note)
        {
            PropertyPath = propertyPath;
            Value = value;
            Note = note;
        }
    }

    /// <summary>Non-URP quality-level settings that pair with each tier.</summary>
    internal readonly struct QualityProfile
    {
        public readonly AnisotropicFiltering Anisotropic;
        public readonly SkinWeights SkinWeights;
        public readonly float LodBias;
        public readonly int ParticleRaycastBudget;
        public readonly bool RealtimeReflectionProbes;

        public QualityProfile(AnisotropicFiltering anisotropic, SkinWeights skinWeights, float lodBias,
                              int particleRaycastBudget, bool realtimeReflectionProbes)
        {
            Anisotropic = anisotropic;
            SkinWeights = skinWeights;
            LodBias = lodBias;
            ParticleRaycastBudget = particleRaycastBudget;
            RealtimeReflectionProbes = realtimeReflectionProbes;
        }
    }

    /// <summary>
    /// The mobile rendering profile for AlphaTown, expressed as serialized-field overrides.
    ///
    /// Values are written through <see cref="UnityEditor.SerializedProperty"/> rather than the URP
    /// C# API because most of these fields are private with read-only public properties, and the
    /// property names are stable across URP versions in a way the public API is not. Anything that
    /// no longer exists in the installed URP version is reported and skipped.
    /// </summary>
    internal static class UrpMobileProfile
    {
        // MsaaQuality: values are the sample count itself, not an index.
        const int MsaaOff = 1;
        const int Msaa2x = 2;
        const int Msaa4x = 4;

        // ShadowResolution: values are the pixel size itself, not an index.
        const int Shadow512 = 512;
        const int Shadow1024 = 1024;
        const int Shadow2048 = 2048;

        // LightRenderingMode
        const int LightsPerVertex = 1;
        const int LightsPerPixel = 2;

        // SoftShadowQuality
        const int SoftShadowLow = 1;
        const int SoftShadowMedium = 2;

        // ColorGradingMode
        const int GradingLdr = 0;
        const int GradingHdr = 1;

        // Downsampling
        const int Downsample2xBilinear = 1;

        // StoreActionsOptimization / UpscalingFilterSelection / HDRColorBufferPrecision
        const int StoreActionsAuto = 0;
        const int UpscalingAuto = 0;
        const int Hdr32Bits = 0;

        // UniversalRendererData: RenderingMode / DepthPrimingMode / IntermediateTextureMode / CopyDepthMode
        const int RenderingModeForward = 0;
        const int DepthPrimingDisabled = 0;
        const int IntermediateTextureAuto = 0;
        const int CopyDepthAfterOpaques = 0;

        /// <summary>
        /// Maps a URP asset's name onto a tier. Covers the names shipped by the Universal 3D
        /// template on both Unity 6 (PC_RPAsset / Mobile_RPAsset) and 2022 LTS
        /// (URP-Performant / URP-Balanced / URP-HighFidelity). Unknown names fall back to Balanced.
        /// </summary>
        public static UrpTier TierFor(string assetName)
        {
            var name = (assetName ?? string.Empty).ToLowerInvariant();

            if (name.Contains("performant") || name.Contains("performance") ||
                name.Contains("low") || name.Contains("lite"))
                return UrpTier.Performance;

            if (name.Contains("fidelity") || name.Contains("high") || name.Contains("ultra") ||
                name.Contains("pc") || name.Contains("desktop"))
                return UrpTier.Fidelity;

            return UrpTier.Balanced;
        }

        /// <summary>Overrides for a UniversalRenderPipelineAsset.</summary>
        public static IEnumerable<SettingOverride> Pipeline(UrpTier tier)
        {
            // --- Resolution and anti-aliasing -------------------------------------------------
            // MSAA is resolved in tile memory on mobile GPUs, so it is the cheapest AA available
            // and the right choice for a game full of hard building silhouettes. The Performance
            // tier drops it and leans on render scale plus per-camera FXAA instead.
            yield return New("m_MSAA", tier == UrpTier.Performance ? MsaaOff
                                    : tier == UrpTier.Balanced ? Msaa2x : Msaa4x,
                "tile-memory MSAA; cheapest AA on mobile");
            yield return New("m_RenderScale", tier == UrpTier.Performance ? 0.8f : 1.0f,
                "sub-native rendering on the low tier");
            yield return New("m_UpscalingFilter", UpscalingAuto, "let URP pick the upscaler");

            // --- Colour buffer ----------------------------------------------------------------
            // HDR doubles colour-buffer bandwidth. Only the top tier pays for it.
            yield return New("m_SupportsHDR", tier == UrpTier.Fidelity,
                "HDR doubles colour-buffer bandwidth");
            yield return New("m_HDRColorBufferPrecision", Hdr32Bits, "32-bit HDR buffer, never 64");
            yield return New("m_ColorGradingMode", tier == UrpTier.Fidelity ? GradingHdr : GradingLdr,
                "HDR grading requires an HDR buffer");
            yield return New("m_ColorGradingLutSize", tier == UrpTier.Performance ? 16 : 32,
                "smaller LUT costs less bandwidth per frame");
            yield return New("m_UseFastSRGBLinearConversion", tier == UrpTier.Performance,
                "approximate sRGB conversion; can band, so low tier only");

            // --- Main light shadows -----------------------------------------------------------
            // The headline mobile change: the template ships 2048 shadowmaps and 4 cascades.
            yield return New("m_MainLightRenderingMode", LightsPerPixel, "sun stays per-pixel");
            yield return New("m_MainLightShadowsSupported", true, "sun shadows on");
            yield return New("m_MainLightShadowmapResolution",
                tier == UrpTier.Performance ? Shadow512
                    : tier == UrpTier.Balanced ? Shadow1024 : Shadow2048,
                "down from the template default of 2048");
            yield return New("m_ShadowCascadeCount", 2, "2 cascades max on every tier");
            yield return New("m_Cascade2Split", 0.25f, "near cascade covers the first quarter");
            yield return New("m_CascadeBorder", tier == UrpTier.Fidelity ? 0.15f : 0.2f,
                "cascade blend band");
            yield return New("m_ShadowDistance",
                tier == UrpTier.Performance ? 30f : tier == UrpTier.Balanced ? 40f : 55f,
                "short range suits a top-down camera");
            yield return New("m_SoftShadowsSupported", tier != UrpTier.Performance,
                "soft shadows cost extra taps");
            yield return New("m_SoftShadowQuality",
                tier == UrpTier.Fidelity ? SoftShadowMedium : SoftShadowLow,
                "lowest acceptable filter quality");
            yield return New("m_ConservativeEnclosingSphere", true,
                "removes cascade popping, near-free");
            yield return New("m_ShadowDepthBias", 1.0f, "");
            yield return New("m_ShadowNormalBias",
                tier == UrpTier.Performance ? 1.4f : tier == UrpTier.Balanced ? 1.2f : 1.0f,
                "raised to hide acne at lower shadowmap resolutions");

            // --- Additional lights ------------------------------------------------------------
            // Per-light shadow maps are the single most expensive thing you can leave on for a
            // town full of lamps and windows at night. Bake them or fake them instead.
            yield return New("m_AdditionalLightsRenderingMode",
                tier == UrpTier.Performance ? LightsPerVertex : LightsPerPixel,
                "vertex lighting on the low tier");
            yield return New("m_AdditionalLightsPerObjectLimit", tier == UrpTier.Performance ? 2 : 4,
                "hard cap on lights per renderer");
            yield return New("m_AdditionalLightShadowsSupported", false,
                "no realtime shadows from point/spot lights on mobile");
            yield return New("m_AdditionalLightsShadowmapResolution",
                tier == UrpTier.Fidelity ? Shadow1024 : Shadow512, "only used if re-enabled");
            yield return New("m_MixedLightingSupported", true, "needed for baked + realtime sun");

            // --- Framebuffer reads ------------------------------------------------------------
            // Both of these force a resolve out of tile memory. Turn them on per-feature later,
            // deliberately, if something actually needs them.
            yield return New("m_RequireDepthTexture", false, "depth copy breaks tile locality");
            yield return New("m_RequireOpaqueTexture", false, "opaque copy is a full-screen blit");
            yield return New("m_OpaqueDownsampling", Downsample2xBilinear, "only used if re-enabled");
            yield return New("m_StoreActionsOptimization", StoreActionsAuto,
                "let URP discard unused attachments");

            // --- Batching and misc ------------------------------------------------------------
            yield return New("m_UseSRPBatcher", true, "");
            yield return New("m_SupportsDynamicBatching", false, "superseded by the SRP batcher");
            yield return New("m_SupportsTerrainHoles", false, "drops unused shader variants");
            yield return New("m_ReflectionProbeBlending", tier == UrpTier.Fidelity, "");
            yield return New("m_ReflectionProbeBoxProjection", false, "");
        }

        /// <summary>Overrides for a UniversalRendererData.</summary>
        public static IEnumerable<SettingOverride> Renderer(UrpTier tier)
        {
            yield return New("m_RenderingMode", RenderingModeForward,
                "forward; deferred is a bandwidth trap on tilers");
            yield return New("m_DepthPrimingMode", DepthPrimingDisabled,
                "depth priming is a net loss on tile-based GPUs");
            yield return New("m_IntermediateTextureMode", IntermediateTextureAuto,
                "avoid forcing an intermediate texture");
            yield return New("m_CopyDepthMode", CopyDepthAfterOpaques, "cheaper than after transparents");
            yield return New("m_AccurateGbufferNormals", false, "deferred only, off for safety");
        }

        /// <summary>Quality-level settings that URP does not own.</summary>
        public static QualityProfile Quality(UrpTier tier)
        {
            switch (tier)
            {
                case UrpTier.Performance:
                    return new QualityProfile(AnisotropicFiltering.Disable, SkinWeights.TwoBones,
                                              0.7f, 16, false);
                case UrpTier.Fidelity:
                    return new QualityProfile(AnisotropicFiltering.Enable, SkinWeights.FourBones,
                                              1.2f, 256, true);
                default:
                    return new QualityProfile(AnisotropicFiltering.Enable, SkinWeights.TwoBones,
                                              1.0f, 64, false);
            }
        }

        static SettingOverride New(string path, object value, string note) =>
            new SettingOverride(path, value, note);
    }
}
