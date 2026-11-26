using System;

namespace Mongscape.Infrastructure
{
    // --mongscape-no-camera: never list or open a webcam (tests and tooling). On Windows, listing cameras can make
    // "Phone Link" pop up a connected phone's camera. Normal play is unchanged.
    public static class CameraAccess
    {
        public const string NoCameraFlag = "--mongscape-no-camera";
        private static bool? disabled;
        public static bool Disabled => disabled ??= Array.IndexOf(Environment.GetCommandLineArgs(), NoCameraFlag) >= 0;
    }
}
