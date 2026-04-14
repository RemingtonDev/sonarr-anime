using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.Configuration
{
    [TestFixture]
    public class DeploymentInfoProviderFixture : CoreTest<DeploymentInfoProvider>
    {
        [SetUp]
        public void SetUp()
        {
            var startUpFolder = "/opt/sonarr/bin";
            var releaseInfoPath = System.IO.Path.Combine(startUpFolder, "release_info");

            Mocker.GetMock<NzbDrone.Common.EnvironmentInfo.IAppFolderInfo>()
                .SetupGet(v => v.StartUpFolder)
                .Returns(startUpFolder);

            Mocker.GetMock<NzbDrone.Common.Disk.IDiskProvider>()
                .Setup(v => v.FileExists(releaseInfoPath))
                .Returns(true);

            Mocker.GetMock<NzbDrone.Common.Disk.IDiskProvider>()
                .Setup(v => v.ReadAllText(releaseInfoPath))
                .Returns("ReleaseVersion=4.0.17.2952-anime.11\nBranch=main\n");
        }

        [Test]
        public void should_read_release_version_from_release_info()
        {
            Subject.ReleaseVersion.Should().Be("4.0.17.2952-anime.11");
            Subject.ReleaseBranch.Should().Be("main");
            Subject.DefaultBranch.Should().Be("main");
        }
    }
}
