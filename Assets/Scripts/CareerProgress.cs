using System;
using System.Collections.Generic;
using UnityEngine;

public static class CareerProgress
{
    const string Prefix="SusCareer.";
    public static readonly string[] BadgeNames={"Fine Print Hunter","Certified Skeptic","Zero False Alarms","Speed Reader"};
    public static int XP=>PlayerPrefs.GetInt("CareerXP",0);
    public static int ClaimsCaught=>PlayerPrefs.GetInt(Prefix+"Caught",0);
    public static string Rank=>XP>=600?"Chief of Sus":XP>=300?"Senior Inspector":XP>=100?"Inspector":"Rookie";
    public static string RankProgress=>XP>=600?"Top rank reached":XP+" / "+(XP>=300?600:XP>=100?300:100)+" XP to next rank";
    public static bool HasBadge(int index)=>PlayerPrefs.GetInt(Prefix+"Badge"+index,0)==1;
    public static void RecordCase(CaseData data,bool verdictCorrect,IList<string> selected,float seconds)
    {
        if(!verdictCorrect)return;
        if(data.isSus)Increment("Caught");else Increment("Legit");
        if(seconds<10)Increment("Fast");
        foreach(var tell in data.tells)
            if(tell.type=="tiny_truth"&&Contains(selected,tell.phrase))Increment("Tiny");
    }
    static bool Contains(IList<string> list,string phrase)
    {foreach(var item in list)if(CaseScoring.Normalize(item)==CaseScoring.Normalize(phrase))return true;return false;}
    static void Increment(string field)=>PlayerPrefs.SetInt(Prefix+field,PlayerPrefs.GetInt(Prefix+field,0)+1);
    public static List<string> FinishShift(int correct,int completed,int score)
    {
        PlayerPrefs.SetInt("CareerXP",XP+correct*10);
        PlayerPrefs.SetInt("BestScore",Mathf.Max(score,PlayerPrefs.GetInt("BestScore",0)));
        bool[] earned={PlayerPrefs.GetInt(Prefix+"Tiny",0)>=20,PlayerPrefs.GetInt(Prefix+"Legit",0)>=10,completed==10&&correct==10,PlayerPrefs.GetInt(Prefix+"Fast",0)>=5};
        var unlocked=new List<string>();
        for(int i=0;i<earned.Length;i++)if(earned[i]&&!HasBadge(i)){PlayerPrefs.SetInt(Prefix+"Badge"+i,1);unlocked.Add(BadgeNames[i]);}
        PlayerPrefs.Save();return unlocked;
    }
    public static string Collection()
    {
        string[] progress={PlayerPrefs.GetInt(Prefix+"Tiny",0)+" / 20 tiny truths",PlayerPrefs.GetInt(Prefix+"Legit",0)+" / 10 LEGIT verdicts","A shift with 10 correct verdicts",PlayerPrefs.GetInt(Prefix+"Fast",0)+" / 5 correct cases under 10 seconds"};
        var text=Rank+"  |  "+RankProgress+"\nMisleading claims caught: "+ClaimsCaught+"\n\n";
        for(int i=0;i<BadgeNames.Length;i++)text+=(HasBadge(i)?"Earned: ":"Locked: ")+BadgeNames[i]+"\n"+progress[i]+"\n\n";
        return text;
    }
}
