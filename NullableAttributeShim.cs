// The Il2CppInterop-generated proxy assemblies for Synth Riders 3.6.3+ (Unity 6)
// break Roslyn's lookup of the compiler-required nullable attributes (CS0656).
// Declaring them in-source restores the compiler's ability to emit nullable metadata.
namespace System.Runtime.CompilerServices
{
    [AttributeUsage(
        AttributeTargets.Class | AttributeTargets.Event | AttributeTargets.Field |
        AttributeTargets.GenericParameter | AttributeTargets.Parameter |
        AttributeTargets.Property | AttributeTargets.ReturnValue,
        Inherited = false)]
    internal sealed class NullableAttribute : Attribute
    {
        public readonly byte[] NullableFlags;

        public NullableAttribute(byte value)
        {
            NullableFlags = new[] { value };
        }

        public NullableAttribute(byte[] value)
        {
            NullableFlags = value;
        }
    }

    [AttributeUsage(
        AttributeTargets.Class | AttributeTargets.Delegate |
        AttributeTargets.Interface | AttributeTargets.Method |
        AttributeTargets.Struct,
        Inherited = false)]
    internal sealed class NullableContextAttribute : Attribute
    {
        public readonly byte Flag;

        public NullableContextAttribute(byte value)
        {
            Flag = value;
        }
    }
}
