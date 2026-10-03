#if !NET5_0_OR_GREATER
namespace System.Runtime.CompilerServices
{
    // Lets positional records compile on net472.
    internal static class IsExternalInit { }
}
#endif
