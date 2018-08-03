using JetBrains.Annotations;

namespace Vostok.SampleLibrary
{
    /// <summary>
    /// A sample class.
    /// </summary>
    [PublicAPI]
#if MAKE_CLASSES_PUBLIC
    public
#else
    internal
#endif
    class SampleClass
    {
        
    }
}