
using Nuke.Common;
using Nuke.Common.CI;
using Nuke.Common.CI.GitHubActions;
using Nuke.Common.Execution;
using Nuke.Common.Git;
using Nuke.Common.IO;
using Nuke.Common.ProjectModel;
using Nuke.Common.Tools.DotNet;
using Nuke.Common.Utilities.Collections;

using static Nuke.Common.IO.FileSystemTasks;
using static Nuke.Common.IO.PathConstruction;
using static Nuke.Common.Tools.DotNet.DotNetTasks;

namespace Scriptable.Compiler {

    [CheckBuildProjectConfigurations]
    [ShutdownDotNetAfterServerBuild]
    [GitHubActions("Build", GitHubActionsImage.UbuntuLatest, GitHubActionsImage.WindowsLatest, GitHubActionsImage.MacOsLatest,
        AutoGenerate = true,
        On = new GitHubActionsTrigger[] { GitHubActionsTrigger.Push },
        OnPushBranches = new string[] { "main", "master" },
        PublishArtifacts = true,
        InvokedTargets = new string[] { "Compile", "Test", "Publish" })]
    class Build : NukeBuild {
        /// Support plugins are available for:
        ///   - JetBrains ReSharper        https://nuke.build/resharper
        ///   - JetBrains Rider            https://nuke.build/rider
        ///   - Microsoft VisualStudio     https://nuke.build/visualstudio
        ///   - Microsoft VSCode           https://nuke.build/vscode

        public static int Main() => Execute<Build>(x => x.Compile);

        [Parameter("Configuration to build - Default is 'Debug' (local) or 'Release' (server)")]
        readonly Configuration Configuration = IsLocalBuild ? Configuration.Debug : Configuration.Release;

        [Solution] readonly Solution Solution;
        [GitRepository] readonly GitRepository GitRepository;

        AbsolutePath SourceDirectory => RootDirectory / "src";
        AbsolutePath TestsDirectory => RootDirectory / "tests";
        AbsolutePath ArtifactsDirectory => RootDirectory / "artifacts";
        AbsolutePath PublishSelfContainedDirectory => ArtifactsDirectory / "publish" / "self-contained";

        Target Clean => _ => _
            .Before(Restore)
            .Before(Compile)
            .Executes(() => {
                DotNetClean(s => s
                    .EnableNoLogo()
                    .SetProject(Solution)
                    .SetConfiguration(Configuration)
                    .EnableDeterministic());
                SourceDirectory.GlobDirectories("**/bin", "**/obj").ForEach(DeleteDirectory);
                TestsDirectory.GlobDirectories("**/bin", "**/obj").ForEach(DeleteDirectory);
                EnsureCleanDirectory(ArtifactsDirectory);
            });

        Target Restore => _ => _
            .Executes(() => {
                DotNetRestore(s => s
                    .SetProjectFile(Solution)
                    .EnableDeterministic());
            });

        Target Compile => _ => _
            .DependsOn(Restore)
            .Executes(() => {
                DotNetBuild(s => s
                    .SetProjectFile(Solution)
                    .SetConfiguration(Configuration)
                    .EnableNoRestore()
                    .EnableNoLogo()
                    .EnableDeterministic());
            });

        Target PublishSelfContained => _ => _
            .DependsOn(Compile)
            .Executes(() => {
                PublishSelfContainedDirectory.GlobDirectories("*", "**/**").ForEach(DeleteDirectory);
                PublishSelfContainedDirectory.GlobFiles("*", "*.*", "**/*.*").ForEach(DeleteFile);
                DotNetPublish(s => s
                    .SetConfiguration(Configuration)
                    .EnableNoRestore()
                    .EnableNoBuild()
                    .EnableNoLogo()
                    .EnableSelfContained()
                    .EnableDeterministic()
                    .SetOutput(PublishSelfContainedDirectory));
            });

        public Target Recompile => _ => _
            .DependsOn(Clean)
            .DependsOn(Compile);
    }
}
