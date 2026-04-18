import jetbrains.buildServer.configs.kotlin.*
import jetbrains.buildServer.configs.kotlin.buildSteps.dotnetBuild
import jetbrains.buildServer.configs.kotlin.buildSteps.dotnetTest
import jetbrains.buildServer.configs.kotlin.buildSteps.dotnetPublish
import jetbrains.buildServer.configs.kotlin.triggers.vcs

version = "2024.03"

project {
    buildType(BuildAndTest)
    buildType(PublishArtifacts)
}

object BuildAndTest : BuildType({
    name = "Build + Test"

    vcs { root(DslContext.settingsRoot) }

    steps {
        dotnetBuild {
            name = "Build"
            projects = "weft.sln"
            configuration = "Release"
            args = "-warnaserror"
        }
        dotnetTest {
            name = "Test"
            projects = "weft.sln"
            configuration = "Release"
            skipBuild = true
        }
    }

    triggers { vcs {} }

    artifactRules = """
        test/**/TestResults/** => test-results
        artifacts/**/* => artifacts
    """.trimIndent()
})

object PublishArtifacts : BuildType({
    name = "Publish artifacts (win-x64 + osx-arm64 + linux-x64)"

    vcs { root(DslContext.settingsRoot) }

    params {
        param("env.WEFT_VERSION", "1.0.0-local")
    }

    steps {
        dotnetPublish {
            name = "Publish win-x64"
            projects = "src/Weft.Cli/Weft.Cli.csproj"
            configuration = "Release"
            runtime = "win-x64"
            outputDir = "publish/win-x64"
            args = "--self-contained false /p:Version=%env.WEFT_VERSION%"
        }
        dotnetPublish {
            name = "Publish linux-x64"
            projects = "src/Weft.Cli/Weft.Cli.csproj"
            configuration = "Release"
            runtime = "linux-x64"
            outputDir = "publish/linux-x64"
            args = "--self-contained false /p:Version=%env.WEFT_VERSION%"
        }
        dotnetPublish {
            name = "Publish osx-arm64"
            projects = "src/Weft.Cli/Weft.Cli.csproj"
            configuration = "Release"
            runtime = "osx-arm64"
            outputDir = "publish/osx-arm64"
            args = "--self-contained false /p:Version=%env.WEFT_VERSION%"
        }
    }

    artifactRules = """
        publish/** => weft-%env.WEFT_VERSION%.zip!/
    """.trimIndent()

    dependencies {
        snapshot(BuildAndTest) {
            onDependencyFailure = FailureAction.FAIL_TO_START
        }
    }
})
