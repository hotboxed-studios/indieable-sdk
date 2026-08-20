// Compile-only support for Unity's inherited Component.GetComponent<T>() API.
// This file lives under ci~/ and is never included in the released package.

global using static UnityEngine.CompileCheckComponentLookup;

namespace UnityEngine
{
    public static class CompileCheckComponentLookup
    {
        public static T GetComponent<T>() where T : Component, new()
        {
            return new T();
        }
    }
}
