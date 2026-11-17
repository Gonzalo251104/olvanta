using System.Xml.Linq;
using Xunit;

namespace Olvanta.FoundationTests;

public class ArchitectureTests
{
    [Theory]
    [InlineData("Domain")]
    [InlineData("Application", "Domain")]
    [InlineData("Infrastructure", "Application", "Domain")]
    public void CoreLayersReferenceOnlyAllowedProjects(string layer, params string[] allowed)
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root != null && !File.Exists(Path.Combine(root.FullName, "Olvanta.slnx")))
        {
            root = root.Parent;
        }
        Assert.NotNull(root);
        var name = $"Olvanta.{layer}";
        var project = XDocument.Load(Path.Combine(root.FullName, "src", name, name + ".csproj"));
        var allowedNames = allowed.Select(value => $"Olvanta.{value}");
        foreach (var reference in project.Descendants("ProjectReference"))
        {
            var include = reference.Attribute("Include")!.Value.Replace('\\', '/');
            Assert.Contains(Path.GetFileNameWithoutExtension(include), allowedNames);
        }
    }
}
