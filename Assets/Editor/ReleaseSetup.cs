using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class ReleaseSetup
{
    public static void VerifyArtAndCareer()
    {
        var cases=JsonUtility.FromJson<CaseBatch>(Resources.Load<TextAsset>("fallback_ads").text).ads;
        foreach(var c in cases)
        {
            if(string.IsNullOrEmpty(c.productResource)||string.IsNullOrEmpty(c.campaignResource))throw new Exception("Missing local art reference: "+c.id);
            var product=Resources.Load<Texture2D>(c.productResource);var campaign=Resources.Load<Texture2D>(c.campaignResource);
            if(product==null||campaign==null||campaign.width<=campaign.height)throw new Exception("Missing/incorrect campaign art: "+c.id);
        }
        string[] keys={"CareerXP","BestScore","SusCareer.Caught","SusCareer.Legit","SusCareer.Fast","SusCareer.Tiny","SusCareer.Badge0","SusCareer.Badge1","SusCareer.Badge2","SusCareer.Badge3"};
        var saved=new System.Collections.Generic.Dictionary<string,int>();foreach(var key in keys){if(PlayerPrefs.HasKey(key))saved[key]=PlayerPrefs.GetInt(key);PlayerPrefs.DeleteKey(key);}
        try
        {
            var sus=new CaseData{isSus=true,tells=new[]{new CaseTell{phrase="whole product",type="tiny_truth"}}};
            var legit=new CaseData{isSus=false,tells=new CaseTell[0]};
            CareerProgress.RecordCase(sus,false,new[]{"whole product"},3);
            if(CareerProgress.ClaimsCaught!=0)throw new Exception("Wrong verdict counted as caught");
            for(int i=0;i<20;i++)CareerProgress.RecordCase(sus,true,new[]{"whole product"},8);
            for(int i=0;i<10;i++)CareerProgress.RecordCase(legit,true,Array.Empty<string>(),12);
            var earned=CareerProgress.FinishShift(10,10,2000);
            if(earned.Count!=4||CareerProgress.XP!=100||CareerProgress.ClaimsCaught!=20||CareerProgress.Rank!="Inspector")throw new Exception("Cumulative career badges/rank failed");
            if(CareerProgress.FinishShift(5,10,500).Count!=0)throw new Exception("Badges unlocked twice");
            Debug.Log("PASS: 60 bundled images, landscape campaigns, four cumulative badges, career counters and rank progress.");
        }
        finally{foreach(var key in keys){PlayerPrefs.DeleteKey(key);if(saved.TryGetValue(key,out var value))PlayerPrefs.SetInt(key,value);}PlayerPrefs.Save();}
    }
    public static void Prepare()
    {
        AssetDatabase.Refresh();
        var scene=EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");
        var game=UnityEngine.Object.FindFirstObjectByType<SusGame>();
        var presentation=game.GetComponent<DeskPresentation>();if(presentation==null)presentation=game.gameObject.AddComponent<DeskPresentation>();
        presentation.detectivePrefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Packages/SyntyStudios/PolygonCity/Prefabs/Characters/Character_BusinessMan_Suit_01.prefab");
        if(!AssetDatabase.IsValidFolder("Assets/Materials"))AssetDatabase.CreateFolder("Assets","Materials");
        foreach(string name in new[]{"OfficeSurface","WindowSurface"})
        {
            string path="Assets/Materials/"+name+".mat";
            if(AssetDatabase.LoadAssetAtPath<Material>(path)==null)
            {var shader=Shader.Find(name=="OfficeSurface"?"Universal Render Pipeline/Lit":"Universal Render Pipeline/Unlit");if(shader==null)throw new Exception("Required office shader missing");AssetDatabase.CreateAsset(new Material(shader),path);}
        }
        presentation.officeSurface=AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/OfficeSurface.mat");presentation.windowSurface=AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/WindowSurface.mat");
        foreach(string name in new[]{"bureau-jazz","marker","paper","stamp","correct","wrong"})
        {
            var path="Assets/Audio/"+name+".wav";var importer=AssetImporter.GetAtPath(path) as AudioImporter;
            if(importer!=null){var settings=importer.defaultSampleSettings;settings.compressionFormat=AudioCompressionFormat.Vorbis;settings.quality=.6f;settings.loadType=name=="bureau-jazz"?AudioClipLoadType.CompressedInMemory:AudioClipLoadType.DecompressOnLoad;importer.defaultSampleSettings=settings;importer.SaveAndReimport();}
        }
        presentation.music=AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/bureau-jazz.wav");presentation.marker=AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/marker.wav");presentation.paper=AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/paper.wav");presentation.stamp=AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/stamp.wav");presentation.correct=AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/correct.wav");presentation.wrong=AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/wrong.wav");game.presentation=presentation;
        foreach(string path in Directory.GetFiles("Assets/Resources/CaseArt","*.jpg"))
        {
            var importer=AssetImporter.GetAtPath(path) as TextureImporter;if(importer==null)continue;
            importer.mipmapEnabled=false;importer.isReadable=false;importer.maxTextureSize=1024;importer.textureCompression=TextureImporterCompression.Compressed;importer.SaveAndReimport();
        }
        var camera=Camera.main;camera.cullingMask&=~(1<<29);
        PlayerSettings.SplashScreen.show=false;PlayerSettings.SplashScreen.showUnityLogo=false;
        EditorUtility.SetDirty(presentation);EditorUtility.SetDirty(game);EditorUtility.SetDirty(camera);
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
        Debug.Log("Release presentation configured; Unity splash disabled.");
    }
}
