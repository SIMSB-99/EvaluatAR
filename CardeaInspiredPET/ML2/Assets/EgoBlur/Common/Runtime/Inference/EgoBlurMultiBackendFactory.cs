using UnityEngine;

namespace EgoBlur
{
    public static class EgoBlurMultiBackendFactory
    {
        public static IEgoBlurMultiBackend Create()
        {
#if ENABLE_WINMD_SUPPORT
            if (Application.platform == RuntimePlatform.WSAPlayerARM ||
                Application.platform == RuntimePlatform.WSAPlayerX86 ||
                Application.platform == RuntimePlatform.WSAPlayerX64)
                return new EgoBlurWinMLMultiBackend();
#endif
            return new EgoBlurOnnxRuntimeMultiBackend();
        }
    }
}
