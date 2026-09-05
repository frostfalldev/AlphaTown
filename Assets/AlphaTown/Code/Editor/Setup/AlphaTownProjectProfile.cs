using UnityEditor;

namespace AlphaTown.EditorTools.Setup
{
    /// <summary>
    /// Every project-identity and platform-target value in one place. Change them here, re-run
    /// AlphaTown ▸ Setup, and the change is reviewable as a one-line diff.
    /// </summary>
    internal static class AlphaTownProjectProfile
    {
        // --- Identity ---------------------------------------------------------------------
        public const string CompanyName = "Frostfall";
        public const string ProductName = "AlphaTown";

        /// <summary>
        /// PLACEHOLDER. Confirm before the first store upload — an application id cannot be
        /// changed once a listing exists without shipping a different app.
        /// </summary>
        public const string ApplicationIdentifier = "com.frostfall.alphatown";

        public const string BundleVersion = "0.1.0";
        public const int AndroidBundleVersionCode = 1;
        public const string IosBuildNumber = "1";

        // --- Orientation ------------------------------------------------------------------
        /// <summary>
        /// Landscape, matching Township and the rest of the wide-camera town-builder shelf.
        ///
        /// CONFIRM THIS. It is one constant today and a UI rebuild later.
        /// </summary>
        public const bool AllowLandscape = true;
        public const bool AllowPortrait = false;

        // --- Platform targets -------------------------------------------------------------
        /// <summary>Android 7.0. Roughly 99% of active devices, and the floor Unity 6 supports well.</summary>
        public const AndroidSdkVersions AndroidMinimumSdk = AndroidSdkVersions.AndroidApiLevel24;

        /// <summary>Auto tracks the newest installed platform, which is what Play's policy requires.</summary>
        public const AndroidSdkVersions AndroidTargetSdk = AndroidSdkVersions.AndroidApiLevelAuto;

        public const string IosMinimumVersion = "15.0";

        // --- Runtime ----------------------------------------------------------------------
        public const int TargetFrameRate = 60;

        /// <summary>Quality level index used as the default on device. 0 Low, 1 Medium, 2 High.</summary>
        public const int DefaultQualityLevelIndex = 1;
    }
}
