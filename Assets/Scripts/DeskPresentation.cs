using System.Collections;
using UnityEngine;

/// <summary>Pose the bundled low-poly detective in a small radio-call portrait.</summary>
public sealed class DeskPresentation : MonoBehaviour
{
    public GameObject detectivePrefab;
    public AudioClip music,marker,paper,stamp,correct,wrong;
    public Material officeSurface,windowSurface;
    Transform actor,head,leftArm,rightArm;
    Quaternion neutralHead,neutralLeft,neutralRight;
    float reactionUntil;
    bool happy;
    public Texture Portrait {get;private set;}
    public void Prepare()
    {
        PrepareOffice();
        if(detectivePrefab==null)return;
        var studio=new GameObject("Detective portrait studio");studio.transform.position=new Vector3(30,30,30);
        actor=Instantiate(detectivePrefab,studio.transform).transform;actor.localPosition=Vector3.zero;actor.localRotation=Quaternion.identity;
        var animator=actor.GetComponentInChildren<Animator>();if(animator!=null)animator.enabled=false;
        Bounds bounds=new Bounds(actor.position,Vector3.zero);foreach(var renderer in actor.GetComponentsInChildren<Renderer>())bounds.Encapsulate(renderer.bounds);
        float scale=1.8f/Mathf.Max(.1f,bounds.size.y);actor.localScale*=scale;
        foreach(var child in actor.GetComponentsInChildren<Transform>())
        {child.gameObject.layer=29;if(child.name=="Head")head=child;if(child.name=="Shoulder_L"||child.name=="UpperArm_L")leftArm=child;if(child.name=="Shoulder_R"||child.name=="UpperArm_R")rightArm=child;}
        if(leftArm==null||rightArm==null)foreach(var child in actor.GetComponentsInChildren<Transform>())
        {if(child.name.ToLowerInvariant().Contains("upperarm")&&child.name.ToLowerInvariant().Contains("left"))leftArm=child;if(child.name.ToLowerInvariant().Contains("upperarm")&&child.name.ToLowerInvariant().Contains("right"))rightArm=child;}
        if(leftArm!=null)leftArm.localRotation*=Quaternion.Euler(0,0,65);
        if(rightArm!=null)rightArm.localRotation*=Quaternion.Euler(0,0,-65);
        if(head!=null)neutralHead=head.localRotation;if(leftArm!=null)neutralLeft=leftArm.localRotation;if(rightArm!=null)neutralRight=rightArm.localRotation;
        var cameraGo=new GameObject("Detective portrait camera");cameraGo.transform.SetParent(studio.transform,false);
        cameraGo.transform.localPosition=new Vector3(0,1.45f,2.2f);cameraGo.transform.LookAt(studio.transform.position+new Vector3(0,1.35f,0));
        var camera=cameraGo.AddComponent<Camera>();camera.orthographic=true;camera.orthographicSize=.44f;camera.cullingMask=1<<29;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.1f,.17f,.17f);camera.depth=-1;
        var texture=new RenderTexture(320,320,16);texture.Create();camera.targetTexture=texture;Portrait=texture;
        var lamp=new GameObject("Portrait light");lamp.transform.SetParent(studio.transform,false);lamp.transform.localPosition=new Vector3(.6f,1.8f,1);var light=lamp.AddComponent<Light>();light.type=LightType.Point;light.range=4;light.intensity=2;light.cullingMask=1<<29;light.color=new Color(1,.9f,.75f);
        studio.transform.SetParent(transform,true);
    }
    public void React(bool good){happy=good;reactionUntil=Time.unscaledTime+1.4f;}
    public void Pose(bool reading,bool reduceMotion)
    {
        if(actor==null||reduceMotion)return;float t=Time.unscaledTime;
        if(head!=null){float nod=Time.unscaledTime<reactionUntil?(happy?Mathf.Sin(t*12)*7:0):reading?4+Mathf.Sin(t*2)*2:Mathf.Sin(t)*1.5f;float shake=Time.unscaledTime<reactionUntil&&!happy?Mathf.Sin(t*14)*12:0;head.localRotation=neutralHead*Quaternion.Euler(nod,shake,reading?-4:0);}
        if(rightArm!=null)rightArm.localRotation=neutralRight*Quaternion.Euler(Time.unscaledTime<reactionUntil?(happy?-12:15):reading?-8:0,0,0);
        if(rain!=null&&!reduceMotion)foreach(var drop in rain){var p=drop.localPosition;p.y-=Time.unscaledDeltaTime*.22f;if(p.y<-.33f)p.y=.33f;drop.localPosition=p;}
    }
    Transform[] rain;
    void PrepareOffice()
    {
        if(GameObject.Find("Bureau office backdrop")!=null)return;
        var room=new GameObject("Bureau office backdrop");room.transform.SetParent(transform,false);
        if(officeSurface==null||windowSurface==null){Destroy(room);return;}
        var wall=new Material(officeSurface);wall.color=new Color(.14f,.19f,.20f);
        var frame=new Material(officeSurface);frame.color=new Color(.075f,.10f,.11f);
        var glass=new Material(windowSurface);glass.color=new Color(.13f,.21f,.25f);
        Cube("Rear office wall",room.transform,new Vector3(-1.3f,1.35f,0),new Vector3(.08f,2.7f,3.2f),wall);
        Cube("Left office wall",room.transform,new Vector3(-.05f,1.35f,-1.65f),new Vector3(2.5f,2.7f,.08f),wall);
        Cube("Right office wall",room.transform,new Vector3(-.05f,1.35f,1.65f),new Vector3(2.5f,2.7f,.08f),wall);
        Cube("Rain window frame",room.transform,new Vector3(-1.248f,1.8f,-.66f),new Vector3(.03f,.82f,.66f),frame);
        var window=Cube("Rain window",room.transform,new Vector3(-1.225f,1.8f,-.66f),new Vector3(.018f,.7f,.55f),glass);
        var rainMaterial=new Material(windowSurface);rainMaterial.color=new Color(.3f,.4f,.44f);
        rain=new Transform[18];
        for(int i=0;i<rain.Length;i++){var drop=Cube("Rain streak",room.transform,new Vector3(-1.21f,1.8f+Random.Range(-.32f,.32f),-.66f+Random.Range(-.25f,.25f)),new Vector3(.006f,Random.Range(.025f,.075f),.003f),rainMaterial);drop.transform.SetParent(window.transform.parent,true);rain[i]=drop.transform;}
        // Rain coordinates are kept relative to a local pivot for bounded looping.
        var pivot=new GameObject("Rain pivot").transform;pivot.SetParent(room.transform,false);pivot.position=new Vector3(-1.21f,1.8f,-.66f);foreach(var drop in rain)drop.SetParent(pivot,true);
    }
    static GameObject Cube(string name,Transform parent,Vector3 position,Vector3 size,Material material)
    {var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(parent,false);go.transform.position=position;go.transform.localScale=size;go.GetComponent<Renderer>().sharedMaterial=material;Destroy(go.GetComponent<Collider>());return go;}
    void OnDestroy(){if(Portrait is RenderTexture texture){texture.Release();Destroy(texture);}}
}
