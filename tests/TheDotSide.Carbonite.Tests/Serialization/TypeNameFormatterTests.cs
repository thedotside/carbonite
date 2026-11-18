using TheDotSide.Carbonite.Serialization;

namespace TheDotSide.Carbonite.Tests.Serialization;

public class TypeNameFormatterTests
{
    [Test]
    public async Task Formats_type_names_like_csharp_without_namespaces()
    {
        await Assert.That(TypeNameFormatter.Format(typeof(int))).IsEqualTo("int");
        await Assert.That(TypeNameFormatter.Format(typeof(string))).IsEqualTo("string");
        await Assert.That(TypeNameFormatter.Format(typeof(object))).IsEqualTo("object");
        await Assert.That(TypeNameFormatter.Format(typeof(int?))).IsEqualTo("int?");
        await Assert.That(TypeNameFormatter.Format(typeof(DateTime))).IsEqualTo("DateTime");
        await Assert.That(TypeNameFormatter.Format(typeof(List<string>))).IsEqualTo("List<string>");
        await Assert
            .That(TypeNameFormatter.Format(typeof(Dictionary<string, int[]>)))
            .IsEqualTo("Dictionary<string, int[]>");
        await Assert.That(TypeNameFormatter.Format(typeof(int[,]))).IsEqualTo("int[,]");
        await Assert.That(TypeNameFormatter.Format(typeof(byte[][]))).IsEqualTo("byte[][]");
        await Assert
            .That(TypeNameFormatter.Format(typeof((int, string))))
            .IsEqualTo("(int, string)");
        await Assert
            .That(TypeNameFormatter.Format(typeof(Tuple<int, bool>)))
            .IsEqualTo("Tuple<int, bool>");
        await Assert.That(TypeNameFormatter.Format(typeof(List<>))).IsEqualTo("List<T>");
    }

    [Test]
    public async Task Formats_nested_types_with_their_declaring_types()
    {
        await Assert
            .That(TypeNameFormatter.Format(typeof(Outer.Inner)))
            .IsEqualTo("TypeNameFormatterTests.Outer.Inner");
        await Assert
            .That(TypeNameFormatter.Format(typeof(GenericOuter<int>.Nested<string>)))
            .IsEqualTo("TypeNameFormatterTests.GenericOuter<int>.Nested<string>");
    }

    [Test]
    public async Task Hides_compiler_generated_names()
    {
        var anonymous = new { A = 1 };

        await Assert
            .That(TypeNameFormatter.Format(anonymous.GetType()))
            .IsEqualTo("anonymous type");
    }

    public static class Outer
    {
        public sealed class Inner;
    }

    public static class GenericOuter<T>
    {
        public sealed class Nested<TValue>;
    }
}
