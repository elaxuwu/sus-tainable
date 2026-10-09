using System;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
public static class SusBuild
{
    [MenuItem("Sus-tainable/Build WebGL")]
    public static void WebGL()
    {
        ReleaseSetup.Prepare();
        VerifyRelease();
        PlayerSettings.productName="Sus-tainable";
        PlayerSettings.SplashScreen.show=false;
        PlayerSettings.SplashScreen.showUnityLogo=false;
        PlayerSettings.runInBackground=true;
        PlayerSettings.WebGL.compressionFormat=WebGLCompressionFormat.Gzip;
        PlayerSettings.WebGL.decompressionFallback=true;
        PlayerSettings.WebGL.exceptionSupport=WebGLExceptionSupport.FullWithStacktrace;
        PlayerSettings.WebGL.template="APPLICATION:Minimal";
        var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes=new[]{"Assets/Scenes/Main.unity"}, locationPathName="public",target=BuildTarget.WebGL,options=BuildOptions.None });
        if(report.summary.result!=BuildResult.Succeeded || report.summary.totalErrors>0 || !System.IO.File.Exists("public/index.html"))throw new Exception("WebGL build failed: "+report.summary.result+" / "+report.summary.totalErrors+" errors");
        long total=0;
        foreach(string path in System.IO.Directory.GetFiles("public","*",System.IO.SearchOption.AllDirectories))
        {long bytes=new System.IO.FileInfo(path).Length;total+=bytes;if(bytes>25*1024*1024)throw new Exception("Cloudflare Pages file limit exceeded: "+path);}
        if(total>50*1024*1024)Debug.LogWarning("Build exceeds the 50 MB target: "+total);
        System.IO.File.WriteAllText("public/_routes.json","{\"version\":1,\"include\":[\"/api/*\"],\"exclude\":[]}");
        string loader=System.IO.Directory.GetFiles("public/Build","*.loader.js")[0];
        string buildName=System.IO.Path.GetFileName(loader).Replace(".loader.js","");
        System.IO.File.WriteAllText("public/index.html",System.IO.File.ReadAllText("Web/player.html").Replace("{{BUILD_NAME}}",buildName));
        Debug.Log("Sus-tainable WebGL build ready in public/");
    }
    public static void VerifyRelease()
    {
        Debug.Log(Verify());
        ReleaseSetup.VerifyArtAndCareer();
        typeof(AdaptiveAgentTests).GetMethod("RunAll",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static).Invoke(null,null);
    }
    [MenuItem("Sus-tainable/Verify Case Content and Scoring")]
    public static string Verify()
    {
        var cases=JsonUtility.FromJson<CaseBatch>(Resources.Load<TextAsset>("fallback_ads").text).ads;
        if(cases.Length!=30)throw new Exception("Expected 30 fallback cases");
        var ids=new System.Collections.Generic.HashSet<string>();int legitimate=0;
        foreach(var c in cases){if(!c.IsValid()||!ids.Add(c.id))throw new Exception("Invalid case "+c.id);if(!c.isSus)legitimate++;}
        if(legitimate<5)throw new Exception("Need legitimate cases");
        int found,wrong;var sample=cases[0];var evidence=new System.Collections.Generic.List<string>{sample.tells[0].phrase};
        if(CaseScoring.Score(sample,true,evidence,1,10,out found,out wrong)!=185||found!=1||wrong!=0)throw new Exception("Correct score failed");
        if(CaseScoring.Score(sample,false,evidence,1,10,out found,out wrong)!=0)throw new Exception("Wrong verdict score failed");
        evidence.Add("invented evidence");if(CaseScoring.Score(sample,true,evidence,1,10,out found,out wrong)!=160||wrong!=1)throw new Exception("Penalty failed");
        return "PASS: 30 valid unique cases, legitimate cases, correct verdict, wrong verdict, evidence and false-evidence penalty.";
    }
}
