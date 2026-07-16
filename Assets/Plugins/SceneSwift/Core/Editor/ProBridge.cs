using System;

namespace SceneSwift
{
    /// <summary>
    /// Bridge between Core (free) and Pro packages.
    /// Pro registers its callbacks at editor startup via ProRegistrar.
    /// Core checks IsProAvailable before showing pro-only UI.
    /// </summary>
    public static class ProBridge
    {
        public static bool IsProAvailable;
        public static Action OpenSceneManagerWindow;
    }
}
