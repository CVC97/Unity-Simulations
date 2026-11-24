using UnityEditor;
using UnityEditor.Build.Profile;
using UnityEditor.Build.Reporting;
using UnityEngine;


// Web builds of the simulations, straight into the site (one folder per simulation, next to its page):
//   Gyroscope → Build web version, Foucault → Build web version   (menu)
//   Unity -batchmode -nographics -projectPath unity-simulations -executeMethod SimBuild.BuildGyroscopeFromCommandLine -logFile build.log
//   (SimBuild.BuildFoucaultFromCommandLine likewise)
// Output ../<name>/sim/ (the folder name gives sim.loader.js, sim.data.unityweb, …, which the page loads). The
// profile sets gzip with decompression fallback: GitHub Pages sends no Content-Encoding. Exits with 0 on success.
public static class SimBuild {
    private const string GyroscopeProfile = "Assets/Settings/Build Profiles/Web - Gyroscope - Release.asset";
    private const string GyroscopeOutput = "../gyroscope/sim";
    private const string FoucaultProfile = "Assets/Settings/Build Profiles/Web - Foucault - Release.asset";
    private const string FoucaultOutput = "../foucault/sim";


    [MenuItem("Gyroscope/Build web version")]
    public static void BuildGyroscopeFromMenu() => Build(GyroscopeProfile, GyroscopeOutput);

    public static void BuildGyroscopeFromCommandLine() {
        EditorApplication.Exit(Build(GyroscopeProfile, GyroscopeOutput) ? 0 : 1);
    }

    [MenuItem("Foucault/Build web version")]
    public static void BuildFoucaultFromMenu() => Build(FoucaultProfile, FoucaultOutput);

    public static void BuildFoucaultFromCommandLine() {
        EditorApplication.Exit(Build(FoucaultProfile, FoucaultOutput) ? 0 : 1);
    }


    private static bool Build(string profilePath, string outputPath) {
        var profile = AssetDatabase.LoadAssetAtPath<BuildProfile>(profilePath);
        if (profile == null) {
            Debug.LogError($"SimBuild: build profile not found: {profilePath}");
            return false;
        }
        BuildProfile.SetActiveBuildProfile(profile);
        BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerWithProfileOptions {
            buildProfile = profile,
            locationPathName = outputPath,
            options = BuildOptions.None,
        });
        BuildSummary summary = report.summary;
        Debug.Log($"SimBuild: {summary.result}, {summary.totalErrors} errors, {summary.totalSize / (1024 * 1024)} MB, " +
                  $"{summary.totalTime.TotalMinutes:0.0} min → {outputPath}");
        return summary.result == BuildResult.Succeeded;
    }
}
