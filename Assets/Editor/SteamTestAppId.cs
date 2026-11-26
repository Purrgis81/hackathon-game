using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;

public static class SteamTestAppId
{
    [PostProcessBuild]
    public static void CopyForWindowsPlaytest(BuildTarget target,string executable)
    {
        if(target!=BuildTarget.StandaloneWindows64)return;
        const string appId="steam_appid.txt";
        if(File.Exists(appId) && File.ReadAllText(appId).Trim()=="480")
            File.Copy(appId,Path.Combine(Path.GetDirectoryName(executable),appId),true);
    }
}
