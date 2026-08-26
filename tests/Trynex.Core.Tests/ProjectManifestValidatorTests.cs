using Trynex.Core.Projects;

namespace Trynex.Core.Tests;

public sealed class ProjectManifestValidatorTests
{
    private const string ValidHash = "ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad";

    [Fact]
    public void Validate_AcceptsWellFormedProject()
    {
        var result = new ProjectManifestValidator().Validate(CreateProject());

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Validate_RejectsUnsafeDestinationAndObjectPaths()
    {
        var project = CreateProject() with
        {
            Files =
            [
                new ProjectFileEntry("../outside.pbo", "https://evil.test/file.pbo", 3, ValidHash)
            ]
        };

        var result = new ProjectManifestValidator().Validate(project);

        Assert.Contains(result.Errors, error => error.Code == "project.path.unsafe");
        Assert.Contains(result.Errors, error => error.Code == "project.sourcePath.unsafe");
    }

    [Fact]
    public void Validate_RequiresEnglishFallbackForUserFacingText()
    {
        var russianOnly = new LocalizedProjectText(new Dictionary<string, string>
        {
            ["ru-RU"] = "Проект"
        });
        var project = CreateProject() with { Name = russianOnly };

        var result = new ProjectManifestValidator().Validate(project);

        Assert.Contains(result.Errors, error => error.Code == "project.name.fallback");
    }

    [Fact]
    public void LocalizedText_ResolvesLanguageIgnoringCaseAndFallsBackToEnglish()
    {
        var text = new LocalizedProjectText(new Dictionary<string, string>
        {
            ["ru-RU"] = "Проект",
            ["en-US"] = "Project"
        });

        Assert.Equal("Проект", text.Resolve("RU-ru"));
        Assert.Equal("Project", text.Resolve("fr-FR"));
    }

    internal static ProjectManifest CreateProject() => new(
        1,
        "mr-project",
        "1.0.0-preview.1",
        GamePlatform.ArmaReforger,
        Text("MR PROJECT"),
        Text("Project description"),
        Text("IN DEVELOPMENT"),
        "#68D9FA",
        "/Trynex.Launcher;component/Assets/Projects/mr-project.png",
        "projects/mr-project/1.0.0-preview.1/",
        new ProjectLaunchProfile("1874880", Arguments: []),
        [new ProjectFileEntry("addons/package.bin", "package.bin", 3, ValidHash)]);

    private static LocalizedProjectText Text(string value) => new(new Dictionary<string, string>
    {
        ["en-US"] = value
    });
}
