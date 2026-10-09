using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

public sealed class SusGame : MonoBehaviour
{
    public Vector3 panelPosition=new Vector3(0.22f,1.15f,-0.14f);
    public float tabletopHeight=.95f;
    public bool reducedMotion,muted;
    public bool CanAnswer=>state==State.Reading&&settingsMenu!=null&&!settingsMenu.activeSelf&&(helpPanel==null||!helpPanel.activeSelf);
    public TMP_FontAsset bodyFont,displayFont;
    enum State { Title, Moving, Reading, Verdict, Summary }
    State state;
    AdProvider provider;
    Camera view;
    DeskMouseLook mouseLook;
    Transform panel;
    TMP_Text hud,header,feedback,modalTitle,modalBody;
    RawImage productPicture,adPicture;
    ClaimSelection selection;
    GameObject modal;
    Button action;
    Button continueButton;
    RectTransform hudStrip,modalRect;
    RectTransform settingsRect,settingsMenuRect;
    TMP_Text scoreLabel,caseLabel,streakLabel,caseMeta,evidenceHint,feedbackTitle,stampText;
    readonly List<Button> evidenceRows=new List<Button>();
    Button clearEvidence;
    CanvasGroup stampGroup;
    RectTransform stamp;
    Button soundButton,motionButton,textButton;
    bool largeText;
    float messageUntil;
    readonly List<DeskVerdictButton> verdictButtons=new List<DeskVerdictButton>();
    Coroutine stampAnimation;
    AudioClip markSound,stampSound,paperSound;
    int lastEvidenceCount;
    int[] tellCounts=new int[5];
    int legitCorrect;
    GameObject summaryStats;
    TMP_Text summaryNumbers,summaryReward;
    GameObject helpPanel;
    Light streakLight;
    GameObject settingsMenu;
    Image progressFill,feedbackBand;
    Vector3 restingPanelPosition;
    int layoutWidth,layoutHeight;
    CaseData current;
    int completed,score,streak,best,correct,maximum;
    bool tutorial;
    float started;
    AudioSource sound;
    AudioClip good,bad;
    readonly List<Texture2D> art=new List<Texture2D>();
    readonly Dictionary<string,Texture2D> artCache=new Dictionary<string,Texture2D>();
    readonly List<bool> recent=new List<bool>();
    static readonly Color Ink=new Color(.12f,.18f,.19f),Paper=new Color(.96f,.96f,.93f);

    void Start()
    {
        view=Camera.main;mouseLook=FindFirstObjectByType<DeskMouseLook>(); provider=GetComponent<AdProvider>();
        if(provider==null) provider=gameObject.AddComponent<AdProvider>();
        if(FindFirstObjectByType<EventSystem>()==null)
        {
            var es=new GameObject("Desk Event System",typeof(EventSystem),typeof(InputSystemUIInputModule));
            es.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
        }
        var physicsInput=view.GetComponent<PhysicsRaycaster>();
        if(physicsInput==null)physicsInput=view.gameObject.AddComponent<PhysicsRaycaster>();
        physicsInput.eventMask=1<<30;
        muted=PlayerPrefs.GetInt("SoundMuted",0)==1;reducedMotion=PlayerPrefs.GetInt("ReduceMotion",0)==1;largeText=PlayerPrefs.GetInt("LargeText",0)==1;
        BuildHUD(); BuildPanel(); BuildButtons();RefreshSettings();
        sound=gameObject.AddComponent<AudioSource>(); good=Tone(640); bad=Tone(170);markSound=Noise("Marker",.055f,.08f);stampSound=Noise("Stamp",.14f,.18f);paperSound=Noise("Paper",.11f,.055f);
        state=State.Title; modalTitle.text="Sus-tainable";
        modalBody.text="Spot the trick. Ask for proof.\n\nInspect ten product claims at your desk.\nMark suspicious words, then give your verdict.";
        var lamp=GameObject.Find("Desk lamp glow");if(lamp!=null)streakLight=lamp.GetComponent<Light>();
    }
    void Update()
    {
        if(hud==null) return;
        if(mouseLook!=null)
        {
            mouseLook.lookEnabled=!reducedMotion&&state==State.Reading&&!settingsMenu.activeSelf&&(helpPanel==null||!helpPanel.activeSelf);
            mouseLook.holdLook=selection.IsSelecting;
        }
        if(Keyboard.current!=null&&Keyboard.current.escapeKey.wasPressedThisFrame){settingsMenu.SetActive(false);if(helpPanel!=null)helpPanel.SetActive(false);}
        if(layoutWidth!=Screen.width||layoutHeight!=Screen.height) Layout();
        scoreLabel.text=score+"  points";
        caseLabel.text=state==State.Title?"READY":state==State.Summary?"SHIFT COMPLETE":tutorial?"TRAINING":"CASE  "+Mathf.Min(completed+1,10)+"/10";
        streakLabel.text=streak>1?streak+"  in a row":"";
        if(progressFill!=null)progressFill.fillAmount=completed/10f;
        if(evidenceHint!=null&&state==State.Reading&&Time.unscaledTime>messageUntil)evidenceHint.text=selection.phrases.Count==0?"":selection.phrases.Count+" / 3 marked";
        if(selection!=null)selection.interactable=CanAnswer;
        if(clearEvidence!=null)clearEvidence.interactable=CanAnswer&&selection.phrases.Count>0;
        if(clearEvidence!=null)clearEvidence.gameObject.SetActive(state==State.Reading&&selection.phrases.Count>0);
        foreach(var button in verdictButtons)button.transform.parent.gameObject.SetActive(state==State.Reading||state==State.Verdict);
        if(streakLight!=null)streakLight.intensity=Mathf.Lerp(streakLight.intensity,.5f+Mathf.Min(streak,10)*.08f,1-Mathf.Exp(-5*Time.unscaledDeltaTime));
        if(CanAnswer&&Keyboard.current!=null)
        {
            if(Keyboard.current.lKey.wasPressedThisFrame) Answer(false);
            else if(Keyboard.current.sKey.wasPressedThisFrame) Answer(true);
        }
    }
    void Anchor(RectTransform rect,Vector2 min,Vector2 max,Vector2 low,Vector2 high)
    {
        rect.anchorMin=min;rect.anchorMax=max;rect.offsetMin=low;rect.offsetMax=high;
    }
    void Layout()
    {
        layoutWidth=Screen.width;layoutHeight=Screen.height;
        Canvas.ForceUpdateCanvases();
        var root=hudStrip.parent as RectTransform;
        float width=root.rect.width,height=root.rect.height;
        modalRect.localScale=Vector3.one*Mathf.Max(.2f,Mathf.Min(1f,(width-64)/1040f,(height-180)/720f));
        if(helpPanel!=null)helpPanel.transform.localScale=Vector3.one*Mathf.Min(1f,(width-64)/900f,(height-180)/530f);
        hudStrip.localScale=Vector3.one*Mathf.Max(.2f,Mathf.Min(1f,(width-240)/530f));
        settingsRect.anchoredPosition=new Vector2(-86,-34);
        settingsMenuRect.anchoredPosition=new Vector2(-24,-66);
        if(panel!=null)
        {
            // Physical desk coordinates: the case sits in front of the lamp and PC,
            // with its bottom above the tabletop. Aspect changes scale, never depth.
            float scale=.85f*Mathf.Min(1f,view.aspect/1.6f);
            panel.localScale=Vector3.one*scale;
            restingPanelPosition=new Vector3(.34f,1.30f,-.16f);
            panel.position=restingPanelPosition;
            for(int i=0;i<verdictButtons.Count;i++)
                verdictButtons[i].transform.parent.position=new Vector3(.40f,tabletopHeight+.012f,-.16f+(i==0?-.15f:.15f));
        }
    }
    RectTransform Rect(string name,Transform parent,Vector2 p,Vector2 size)
    {
        var r=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();
        r.SetParent(parent,false); r.anchorMin=r.anchorMax=new Vector2(.5f,.5f); r.anchoredPosition=p; r.sizeDelta=size; return r;
    }
    TMP_Text Label(string name,Transform parent,string value,Vector2 p,Vector2 size,float font,Color color)
    {
        var t=Rect(name,parent,p,size).gameObject.AddComponent<TextMeshProUGUI>();
        t.text=value; t.fontSize=font; t.color=color; t.alignment=TextAlignmentOptions.MidlineLeft; t.raycastTarget=false; t.richText=false;
        t.font=bodyFont!=null?bodyFont:TMP_Settings.defaultFontAsset; return t;
    }
    Image Back(string name,Transform parent,Vector2 p,Vector2 size,Color color)
    {var i=Rect(name,parent,p,size).gameObject.AddComponent<Image>(); i.color=color;i.raycastTarget=false; return i;}
    Button Button(string name,Transform parent,string value,Vector2 p,Vector2 size,System.Action callback)
    {
        var i=Back(name,parent,p,size,new Color(.19f,.24f,.22f));i.raycastTarget=true; var b=i.gameObject.AddComponent<Button>();
        var colors=b.colors;colors.highlightedColor=new Color(.86f,.94f,.92f);colors.pressedColor=new Color(.65f,.79f,.75f);colors.disabledColor=new Color(.55f,.6f,.58f,.65f);colors.fadeDuration=.08f;b.colors=colors;
        b.onClick.AddListener(()=>callback()); var t=Label("Label",i.transform,value,Vector2.zero,size,23,Paper); t.alignment=TextAlignmentOptions.Center; return b;
    }
    void BuildHUD()
    {
        var go=new GameObject("Bureau HUD",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
        go.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;
        var scaler=go.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1600,900);scaler.screenMatchMode=CanvasScaler.ScreenMatchMode.Expand;
        var strip=Back("Shift header",go.transform,Vector2.zero,new Vector2(530,52),new Color(.10f,.13f,.13f,.86f));hudStrip=strip.rectTransform;
        hudStrip.anchorMin=hudStrip.anchorMax=new Vector2(.5f,1);hudStrip.anchoredPosition=new Vector2(0,-38);
        hud=Label("HUD state",strip.transform,"",Vector2.zero,Vector2.zero,1,Paper);hud.enabled=false;
        scoreLabel=Label("Score value",strip.transform,"",new Vector2(-172,0),new Vector2(160,36),18,Paper);
        caseLabel=Label("Case value",strip.transform,"",Vector2.zero,new Vector2(180,36),18,Paper);caseLabel.alignment=TextAlignmentOptions.Center;
        streakLabel=Label("Streak value",strip.transform,"",new Vector2(172,0),new Vector2(160,36),18,Paper);streakLabel.alignment=TextAlignmentOptions.MidlineRight;
        Back("Divider",strip.transform,new Vector2(-90,0),new Vector2(1,16),new Color(.3f,.39f,.39f));
        Back("Divider",strip.transform,new Vector2(90,0),new Vector2(1,16),new Color(.3f,.39f,.39f));
        progressFill=Back("Shift progress",strip.transform,new Vector2(0,-27),new Vector2(530,2),new Color(.66f,.78f,.55f));progressFill.type=Image.Type.Filled;progressFill.fillMethod=Image.FillMethod.Horizontal;
        var settings=Button("Settings",go.transform,"Menu",Vector2.zero,new Vector2(100,40),()=>settingsMenu.SetActive(!settingsMenu.activeSelf));
        settings.GetComponent<Image>().color=new Color(.10f,.13f,.13f,.9f);
        settingsRect=settings.GetComponent<RectTransform>();settingsRect.anchorMin=settingsRect.anchorMax=new Vector2(1,1);settingsRect.anchoredPosition=new Vector2(-86,-34);
        settingsMenu=Back("Settings menu",go.transform,Vector2.zero,new Vector2(278,296),Paper).gameObject;
        settingsMenu.GetComponent<Image>().raycastTarget=true;
        var menu=settingsMenu.GetComponent<RectTransform>();settingsMenuRect=menu;menu.pivot=new Vector2(1,1);menu.anchorMin=menu.anchorMax=new Vector2(1,1);menu.anchoredPosition=new Vector2(-24,-66);
        Label("Settings heading",menu,"Desk settings",new Vector2(0,112),new Vector2(230,30),24,Ink);
        soundButton=Button("Sound",menu,"",new Vector2(0,57),new Vector2(230,44),()=>{muted=!muted;PlayerPrefs.SetInt("SoundMuted",muted?1:0);RefreshSettings();});
        motionButton=Button("Motion",menu,"",new Vector2(0,3),new Vector2(230,44),()=>{reducedMotion=!reducedMotion;PlayerPrefs.SetInt("ReduceMotion",reducedMotion?1:0);RefreshSettings();});
        textButton=Button("Text size",menu,"",new Vector2(0,-51),new Vector2(230,44),()=>{largeText=!largeText;PlayerPrefs.SetInt("LargeText",largeText?1:0);RefreshSettings();});
        Button("Help",menu,"How to play",new Vector2(0,-109),new Vector2(230,38),()=>{helpPanel.SetActive(true);settingsMenu.SetActive(false);});
        foreach(var row in menu.GetComponentsInChildren<Button>())
        {
            row.GetComponent<Image>().color=new Color(.87f,.87f,.82f);
            var text=row.GetComponentInChildren<TMP_Text>();text.color=Ink;text.fontSize=20;text.alignment=TextAlignmentOptions.MidlineLeft;text.margin=new Vector4(14,0,14,0);
        }
        settingsMenu.SetActive(false);
        modal=Back("Welcome dossier",go.transform,Vector2.zero,new Vector2(1040,720),Paper).gameObject;modal.GetComponent<Image>().raycastTarget=true;modalRect=modal.GetComponent<RectTransform>();
        Back("Dossier spine",modal.transform,new Vector2(-501,0),new Vector2(38,720),new Color(.12f,.23f,.23f));
        Label("Bureau name",modal.transform,"Bureau of Sus",new Vector2(15,298),new Vector2(840,45),22,Ink);
        modalTitle=Label("Report title",modal.transform,"",new Vector2(15,208),new Vector2(840,150),76,Ink);modalTitle.fontStyle=FontStyles.Bold;if(displayFont!=null)modalTitle.font=displayFont;
        modalBody=Label("Report body",modal.transform,"",new Vector2(15,-50),new Vector2(840,300),27,Ink);modalBody.lineSpacing=5;modalBody.enableAutoSizing=true;modalBody.fontSizeMin=21;modalBody.fontSizeMax=27;
        action=Button("Report action",modal.transform,"Start shift",new Vector2(-260,-294),new Vector2(290,66),BeginShift);
        summaryStats=Back("Shift totals",modal.transform,new Vector2(15,82),new Vector2(840,78),new Color(.88f,.92f,.88f)).gameObject;
        summaryNumbers=Label("Shift totals text",summaryStats.transform,"",Vector2.zero,new Vector2(792,68),26,Ink);summaryNumbers.enableAutoSizing=true;summaryNumbers.fontSizeMin=20;summaryNumbers.fontSizeMax=26;summaryStats.SetActive(false);
        summaryReward=Label("Shift reward",modal.transform,"",new Vector2(175,-294),new Vector2(480,68),22,Ink);summaryReward.alignment=TextAlignmentOptions.MidlineRight;
        helpPanel=Back("How to play",go.transform,Vector2.zero,new Vector2(900,530),Paper).gameObject;helpPanel.GetComponent<Image>().raycastTarget=true;
        Label("Help title",helpPanel.transform,"Read. Mark. Decide.",new Vector2(0,172),new Vector2(790,70),42,Ink).fontStyle=FontStyles.Bold;
        Label("Help copy",helpPanel.transform,"Drag across words to mark a phrase. Use up to three marks.\nClick a mark or its evidence row to remove it.\n\nLEGIT: this claim has supporting evidence.\nSUS: it contains a misleading or unsupported promise.\n\nAfter the verdict: green = found, amber = missed, red = false evidence.\nKeyboard: arrows move, Space starts/ends a phrase, L/S submits.",new Vector2(0,-5),new Vector2(790,300),24,Ink);
        Button("Close help",helpPanel.transform,"Back to desk",new Vector2(-260,-207),new Vector2(270,52),()=>helpPanel.SetActive(false));helpPanel.SetActive(false);
    }
    void BuildPanel()
    {
        restingPanelPosition=panelPosition;restingPanelPosition.y=Mathf.Max(panelPosition.y,tabletopHeight+.32f);
        panel=new GameObject("Sliding Case Panel").transform;panel.position=restingPanelPosition;panel.rotation=view.transform.rotation;
        var backing=GameObject.CreatePrimitive(PrimitiveType.Cube);backing.name="Dossier backing";backing.transform.SetParent(panel,false);backing.transform.localPosition=new Vector3(0,0,.013f);backing.transform.localScale=new Vector3(.886f,.531f,.02f);Destroy(backing.GetComponent<Collider>());
        var material=new Material(Shader.Find("Universal Render Pipeline/Lit"));material.color=new Color(.12f,.20f,.20f);backing.GetComponent<Renderer>().material=material;
        var go=new GameObject("Case world canvas",typeof(RectTransform),typeof(Canvas),typeof(GraphicRaycaster));go.transform.SetParent(panel,false);
        var canvas=go.GetComponent<Canvas>();canvas.renderMode=RenderMode.WorldSpace;canvas.worldCamera=view;
        var rect=go.GetComponent<RectTransform>();rect.sizeDelta=new Vector2(1100,650);rect.localScale=new Vector3(.87f/1100,.514f/650,1);
        Back("Paper",go.transform,Vector2.zero,rect.sizeDelta,Paper);
        Back("Picture column",go.transform,new Vector2(-359,0),new Vector2(322,600),new Color(.94f,.93f,.88f));
        caseMeta=Label("Case category",go.transform,"",new Vector2(172,279),new Vector2(634,32),18,new Color(.37f,.46f,.44f));caseMeta.characterSpacing=2;
        header=Label("Product name",go.transform,"",new Vector2(172,220),new Vector2(634,74),46,Ink);if(displayFont!=null)header.font=displayFont;header.enableAutoSizing=true;header.fontSizeMin=28;header.fontSizeMax=46;
        Label("Product label",go.transform,"Product",new Vector2(-360,270),new Vector2(270,32),19,Ink);
        productPicture=Rect("Product picture",go.transform,new Vector2(-359,128),new Vector2(268,215)).gameObject.AddComponent<RawImage>();productPicture.raycastTarget=false;
        Label("Campaign label",go.transform,"Campaign",new Vector2(-360,-17),new Vector2(270,28),19,Ink);
        adPicture=Rect("Ad artwork",go.transform,new Vector2(-359,-115),new Vector2(268,118)).gameObject.AddComponent<RawImage>();adPicture.raycastTarget=false;
        var claim=Label("Selectable claim",go.transform,"",new Vector2(172,91),new Vector2(634,180),34,Ink);claim.alignment=TextAlignmentOptions.MidlineLeft;claim.enableAutoSizing=true;claim.fontSizeMin=18;claim.fontSizeMax=34;claim.raycastTarget=true;
        selection=claim.gameObject.AddComponent<ClaimSelection>();selection.text=claim;selection.eventCamera=view;
        selection.Changed+=EvidenceChanged;selection.Notice+=message=>{evidenceHint.text=message;messageUntil=Time.unscaledTime+3;};
        for(int i=0;i<3;i++)
        {
            int index=i;var row=Button("Evidence "+i,go.transform,"",new Vector2(172,-30-i*32),new Vector2(634,29),()=>selection.Remove(index));
            row.GetComponent<Image>().color=new Color(.96f,.86f,.58f);var label=row.GetComponentInChildren<TMP_Text>();label.color=Ink;label.fontSize=18;label.alignment=TextAlignmentOptions.MidlineLeft;label.margin=new Vector4(10,0,10,0);label.enableAutoSizing=true;label.fontSizeMin=14;label.fontSizeMax=18;label.overflowMode=TextOverflowModes.Ellipsis;
            row.gameObject.SetActive(false);evidenceRows.Add(row);
        }
        feedbackBand=Back("Evidence feedback",go.transform,new Vector2(172,-198),new Vector2(634,145),Color.clear);
        feedbackTitle=Label("Verdict heading",go.transform,"",new Vector2(172,-150),new Vector2(586,30),22,Ink);feedbackTitle.fontStyle=FontStyles.Bold;
        feedback=Label("Case feedback",go.transform,"",new Vector2(172,-208),new Vector2(586,84),21,Ink);feedback.enableAutoSizing=true;feedback.fontSizeMin=16;feedback.fontSizeMax=21;
        evidenceHint=Label("Selected evidence",go.transform,"",new Vector2(90,-294),new Vector2(460,30),17,new Color(.35f,.43f,.4f));evidenceHint.enableAutoSizing=true;evidenceHint.fontSizeMin=13;evidenceHint.fontSizeMax=17;
        clearEvidence=Button("Clear evidence",go.transform,"Clear marks",new Vector2(-359,-266),new Vector2(150,34),()=>selection.Clear());
        continueButton=Button("Continue",go.transform,"Next case",new Vector2(383,-294),new Vector2(200,42),Continue);continueButton.gameObject.SetActive(false);
        var stampGo=new GameObject("Verdict stamp",typeof(RectTransform),typeof(CanvasGroup));stamp=stampGo.GetComponent<RectTransform>();stamp.SetParent(go.transform,false);stamp.anchorMin=stamp.anchorMax=new Vector2(.5f,.5f);stamp.anchoredPosition=new Vector2(-359,-224);stamp.sizeDelta=new Vector2(260,64);stamp.localRotation=Quaternion.Euler(0,0,-7);
        stampGroup=stampGo.GetComponent<CanvasGroup>();stampGroup.alpha=0;stampGroup.blocksRaycasts=false;
        stampText=Label("Stamp text",stamp,"",Vector2.zero,new Vector2(252,58),38,Ink);stampText.fontStyle=FontStyles.Bold;stampText.alignment=TextAlignmentOptions.Center;if(displayFont!=null)stampText.font=displayFont;
        Back("Stamp top",stamp,new Vector2(0,31),new Vector2(260,3),Ink);Back("Stamp bottom",stamp,new Vector2(0,-31),new Vector2(260,3),Ink);
        Back("Stamp left",stamp,new Vector2(-129,0),new Vector2(3,64),Ink);Back("Stamp right",stamp,new Vector2(129,0),new Vector2(3,64),Ink);
        Layout();panel.gameObject.SetActive(false);
    }
    void BuildButtons()
    {
        for(int i=0;i<2;i++)
        {
            var root=new GameObject(i==0?"Legit verdict control":"Sus verdict control").transform;
            root.rotation=Quaternion.Euler(90,view.transform.eulerAngles.y,0);
            var housing=GameObject.CreatePrimitive(PrimitiveType.Cylinder);housing.name="Verdict button housing";housing.transform.SetParent(root,false);
            housing.transform.localRotation=Quaternion.Euler(90,0,0);housing.transform.localScale=new Vector3(.117f,.006f,.117f);Destroy(housing.GetComponent<Collider>());
            var shell=new Material(Shader.Find("Universal Render Pipeline/Lit"));shell.color=new Color(.12f,.15f,.16f);housing.GetComponent<Renderer>().material=shell;
            var go=GameObject.CreatePrimitive(PrimitiveType.Cylinder);go.layer=30;go.name=i==0?"LEGIT desk button":"SUS desk button";
            go.transform.SetParent(root,false);go.transform.localPosition=new Vector3(0,0,-.009f);
            go.transform.localRotation=Quaternion.Euler(90,0,0);go.transform.localScale=new Vector3(.101f,.006f,.101f);
            var m=new Material(Shader.Find("Universal Render Pipeline/Lit"));m.color=i==0?new Color(.24f,.40f,.32f):new Color(.58f,.25f,.20f);go.GetComponent<Renderer>().material=m;
            var control=go.AddComponent<DeskVerdictButton>();control.game=this;control.suspicious=i==1;verdictButtons.Add(control);
            var label=new GameObject("Verdict face",typeof(RectTransform),typeof(Canvas));label.transform.SetParent(root,false);
            label.transform.localPosition=new Vector3(0,0,-.016f);label.transform.localRotation=Quaternion.identity;label.transform.localScale=Vector3.one*.0005f;
            var canvas=label.GetComponent<Canvas>();canvas.renderMode=RenderMode.WorldSpace;canvas.worldCamera=view;
            var t=Label("Text",label.transform,i==0?"LEGIT":"SUS",Vector2.zero,new Vector2(170,60),30,Paper);t.fontStyle=FontStyles.Bold;t.alignment=TextAlignmentOptions.Center;
            var caption=new GameObject("Verdict caption",typeof(RectTransform),typeof(Canvas));caption.transform.SetParent(root,false);caption.transform.localPosition=new Vector3(0,-.08f,0);caption.transform.localScale=Vector3.one*.0005f;
            caption.GetComponent<Canvas>().renderMode=RenderMode.WorldSpace;
            var description=Label("Meaning",caption.transform,i==0?"Claim checks out":"Claim is misleading",Vector2.zero,new Vector2(270,40),19,Paper);description.alignment=TextAlignmentOptions.Center;control.hoverCaption=caption;caption.SetActive(false);
        }
        Layout();
    }
    public void BeginShift()
    {
        if(state!=State.Title&&state!=State.Summary)return;
        completed=score=streak=best=correct=maximum=0; recent.Clear(); provider.difficulty=1; tutorial=true; modal.SetActive(false);settingsMenu.SetActive(false);summaryStats.SetActive(false);helpPanel.SetActive(false);legitCorrect=0;System.Array.Clear(tellCounts,0,tellCounts.Length);
        current=provider.Fallback.Find(c=>c.isSus);provider.ResetShift(current?.id); StartCoroutine(PrepareShift());
    }
    IEnumerator PrepareShift()
    {
        state=State.Moving;
        // Bound initial waiting; fallback remains available if generation is slow.
        if (!tutorial&&!provider.offlineOnly && !string.IsNullOrEmpty(provider.endpoint))
        {
            float deadline=Time.unscaledTime+8f;
            while(provider.ReadyCount<3 && Time.unscaledTime<deadline) yield return null;
        }
        yield return ShowCase();
    }
    IEnumerator ShowCase()
    {
        if(stampAnimation!=null){StopCoroutine(stampAnimation);stampAnimation=null;}
        state=State.Moving; selection.interactable=false;continueButton.gameObject.SetActive(false); panel.gameObject.SetActive(true);
        if(current==null){Summary();yield break;}
        header.text=current.product;
        caseMeta.text=(tutorial?"PRACTICE":current.category.ToUpperInvariant()+"  /  "+(completed+1).ToString("00"));
        feedbackBand.color=Color.clear;evidenceHint.text="";messageUntil=0;stampGroup.alpha=0;clearEvidence.gameObject.SetActive(true);lastEvidenceCount=0;
        selection.SetText(current.adText);
        feedbackTitle.text=tutorial?"Your first case":"";
        feedback.text=tutorial?"Mark the vague promise. Then choose SUS.":"";
        Picture(productPicture,current.productImage,current.category,false); Picture(adPicture,current.adImage,current.category,true);
        yield return Slide(CaseOffscreen(-1),restingPanelPosition);
        started=Time.unscaledTime;state=State.Reading;selection.interactable=true;EvidenceChanged();
    }
    public void Answer(bool suspicious)
    {
        if(!CanAnswer)return;
        if(tutorial&&(!suspicious||!selection.phrases.Exists(p=>CaseScoring.Normalize(p)==CaseScoring.Normalize(current.tells[0].phrase))))
        {
            feedbackTitle.text="Try marking the evidence";feedback.text="Drag over the vague promise, then press SUS. This practice case does not affect your score.";return;
        }
        state=State.Verdict;selection.interactable=false;continueButton.gameObject.SetActive(true);
        bool right=suspicious==current.isSus;int found,falseEvidence;int next=right?streak+1:0;
        int points=CaseScoring.Score(current,suspicious,selection.phrases,next,Time.unscaledTime-started,out found,out falseEvidence);
        if(!tutorial)
        {
            provider.RecordAnswer(current,right,selection.phrases);
            completed++;maximum+=CaseScoring.Maximum(current,completed);score+=points;streak=next;best=Mathf.Max(best,streak);if(right){correct++;if(!current.isSus)legitCorrect++;}
            foreach(var tell in current.tells)if(selection.phrases.Exists(p=>CaseScoring.Normalize(p)==CaseScoring.Normalize(tell.phrase))){int index=System.Array.IndexOf(new[]{"vague","fake_label","no_proof","tiny_truth","wrong_comparison"},tell.type);if(index>=0)tellCounts[index]++;}
            recent.Add(right);if(recent.Count==5){int wins=recent.FindAll(v=>v).Count;if(wins>=4)provider.difficulty=Mathf.Min(5,provider.difficulty+1);else if(wins<=2)provider.difficulty=Mathf.Max(1,provider.difficulty-1);recent.Clear();}
        }
        selection.Reveal(current);clearEvidence.gameObject.SetActive(false);
        feedbackTitle.text=(right?"Correct verdict":"Verdict corrected")+"  /  "+(current.isSus?"SUS":"LEGIT");
        ReviewEvidence();stampAnimation=StartCoroutine(StampVerdict());
        feedbackBand.color=new Color(.93f,.92f,.87f);
        evidenceHint.text=tutorial?"Practice complete":"+"+points+" points  |  "+found+" found  |  "+falseEvidence+" false marks";
        feedback.text=(right?"Good call. ":"Look closer. ")+current.verdictText;
        if(!muted)sound.PlayOneShot(right?good:bad);foreach(var button in verdictButtons)if(button.suspicious==suspicious)button.Pulse();
    }
    public void Continue(){if(state==State.Verdict)StartCoroutine(Next());}
    IEnumerator Next()
    {
        state=State.Moving;selection.interactable=false;clearEvidence.gameObject.SetActive(false);continueButton.gameObject.SetActive(false);yield return Slide(restingPanelPosition,CaseOffscreen(1));
        provider.Release(current);
        if(tutorial)tutorial=false;else if(completed>=10){Summary();yield break;}
        current=provider.Take();yield return ShowCase();
    }
    Vector3 CaseOffscreen(int direction)
    {
        float depth=Vector3.Dot(restingPanelPosition-view.transform.position,view.transform.forward);
        float halfWidth=depth*Mathf.Tan(view.fieldOfView*Mathf.Deg2Rad*.5f)*view.aspect;
        return restingPanelPosition+view.transform.right*(direction*(halfWidth+.886f*panel.localScale.x+.1f));
    }
    IEnumerator Slide(Vector3 from,Vector3 to)
    {
        bool entering=(to-restingPanelPosition).sqrMagnitude<.001f;float duration=reducedMotion?.01f:entering?.55f:.4f;
        if(!muted)sound.PlayOneShot(paperSound);
        for(float t=0;t<duration;t+=Time.unscaledDeltaTime){float x=t/duration;float ease=entering?1-Mathf.Pow(1-x,3):x*x;panel.position=Vector3.Lerp(from,to,ease)-view.transform.forward*(Mathf.Sin(x*Mathf.PI)*.18f);yield return null;}panel.position=to;
    }
    void Summary()
    {
        state=State.Summary;panel.gameObject.SetActive(false);modal.SetActive(true);
        int xp=correct*10;int total=PlayerPrefs.GetInt("CareerXP",0)+xp;PlayerPrefs.SetInt("CareerXP",total);PlayerPrefs.SetInt("BestScore",Mathf.Max(score,PlayerPrefs.GetInt("BestScore",0)));PlayerPrefs.Save();
        string rank=total>=600?"Chief of Sus":total>=300?"Senior Inspector":total>=100?"Inspector":"Rookie";
        string badge=correct==10?"Zero false alarms":legitCorrect>=4?"Certified skeptic":best>=5?"On a roll":"";
        summaryReward.text="+"+xp+" XP  /  "+rank+(string.IsNullOrEmpty(badge)?"":"\n"+badge);
        float ratio=maximum>0?(float)score/maximum:0;int stars=ratio>=.9f?3:ratio>=.75f?2:ratio>=.5f?1:0;
        modalTitle.text="Shift complete";
        summaryStats.SetActive(true);summaryNumbers.text=score+" points     "+correct+" / 10 correct     "+best+" best streak";
        modalBody.rectTransform.anchoredPosition=new Vector2(15,-103);modalBody.rectTransform.sizeDelta=new Vector2(840,246);
        string strongest="";int count=0;var names=new[]{"Vague claims","Self-awarded labels","Unsupported promises","Tiny truths","Unclear comparisons"};
        for(int i=0;i<5;i++)if(tellCounts[i]>count){count=tellCounts[i];strongest=names[i];}
        modalBody.text="Your shopping checklist\n\nLook for a number. Ask who verified it.\nCheck the scope and the comparison.\n\n"+(count>0?"Best evidence: "+strongest+".":"Next shift: mark the exact words to earn evidence points.")+"\nLegitimate claims recognised: "+legitCorrect+".";
        modalTitle.text=stars==3?"Sharp detective":stars==2?"Good instincts":"Keep investigating";
        action.GetComponentInChildren<TMP_Text>().text="Play again";
    }
    void Picture(RawImage r,Texture2D image,string category,bool ad)
    {
        if(image==null)image=LocalArt(category,ad);r.texture=image;float ratio=(float)image.width/image.height;float w=268,h=ad?118:215;
        r.rectTransform.sizeDelta=ratio>w/h?new Vector2(w,w/ratio):new Vector2(h*ratio,h);
    }
    Texture2D LocalArt(string category,bool ad)
    {
        string key=category+(ad?"_ad":"_product");
        if(artCache.TryGetValue(key,out var cached))return cached;
        int w=ad?480:320,h=ad?200:300;var t=new Texture2D(w,h);var pixels=new Color[w*h];
        for(int y=0;y<h;y++)for(int x=0;x<w;x++)
        {
            float u=(float)x/w,v=(float)y/h;bool shape;
            switch(category)
            {
                case "drinks": shape=(u>.37f&&u<.63f&&v>.12f&&v<.7f)||(u>.43f&&u<.57f&&v>=.7f&&v<.86f);break;
                case "cosmetics": shape=(u>.34f&&u<.66f&&v>.12f&&v<.71f)||(u>.44f&&u<.56f&&v>.71f&&v<.84f)||(u>.44f&&u<.7f&&v>.81f&&v<.87f);break;
                case "fashion": shape=v>.18f&&v<.82f&&((u>.36f&&u<.64f)||(v>.61f&&u>.18f&&u<.82f));break;
                case "snacks": shape=u>.26f&&u<.74f&&v>.17f&&v<.83f;break;
                default: shape=(u>.3f&&u<.7f&&v>.22f&&v<.72f)||((u>.39f&&u<.44f||u>.56f&&u<.61f)&&v>.72f&&v<.87f);break;
            }
            Color bg=ad?new Color(.12f,.28f,.26f):new Color(.82f,.86f,.78f);
            Color body=v>.38f&&v<.52f?new Color(.94f,.9f,.79f):new Color(.86f,.64f,.29f);
            pixels[y*w+x]=shape?body:bg;
        }
        t.SetPixels(pixels);t.Apply();art.Add(t);artCache[key]=t;return t;
    }
    AudioClip Tone(float f)
    {
        var c=AudioClip.Create("Bureau tone",6600,1,22050,false);var values=new float[6600];
        for(int i=0;i<values.Length;i++)values[i]=Mathf.Sin(i*f*2*Mathf.PI/22050)*.13f*(1f-(float)i/values.Length);
        c.SetData(values,0);return c;
    }
    void EvidenceChanged()
    {
        for(int i=0;i<evidenceRows.Count;i++)
        {
            bool active=i<selection.phrases.Count;var row=evidenceRows[i];row.gameObject.SetActive(active);
            if(active){row.interactable=true;row.GetComponent<Image>().color=new Color(.96f,.86f,.58f);row.GetComponentInChildren<TMP_Text>().text=(i+1)+".  "+selection.phrases[i]+"   ×";}
        }
        if(state==State.Reading&&selection.phrases.Count>lastEvidenceCount&&!muted&&sound!=null)sound.PlayOneShot(markSound);
        lastEvidenceCount=selection.phrases.Count;
    }
    void ReviewEvidence()
    {
        var rows=new List<string>();var colors=new List<Color>();
        foreach(var tell in current.tells)
        {
            bool found=selection.phrases.Exists(p=>CaseScoring.Normalize(p)==CaseScoring.Normalize(tell.phrase));
            rows.Add((found?"FOUND  ":"MISSED  ")+tell.phrase+"  /  "+TellName(tell.type));
            colors.Add(found?new Color(.83f,.92f,.84f):new Color(.96f,.88f,.65f));
        }
        foreach(var phrase in selection.phrases)
            if(!System.Array.Exists(current.tells,t=>CaseScoring.Normalize(t.phrase)==CaseScoring.Normalize(phrase)))
            {rows.Add("NOT EVIDENCE  "+phrase);colors.Add(new Color(.98f,.85f,.81f));}
        for(int i=0;i<evidenceRows.Count;i++)
        {
            var row=evidenceRows[i];row.gameObject.SetActive(i<rows.Count);row.interactable=false;
            if(i<rows.Count){row.GetComponent<Image>().color=colors[i];row.GetComponentInChildren<TMP_Text>().text=rows[i];}
        }
    }
    static string TellName(string type)
    {
        switch(type){case "vague":return "Vague words";case "fake_label":return "Self-awarded label";case "no_proof":return "No proof";case "tiny_truth":return "Tiny truth";default:return "Unclear comparison";}
    }
    void RefreshSettings()
    {
        if(soundButton!=null)soundButton.GetComponentInChildren<TMP_Text>().text=muted?"Sound: off":"Sound: on";
        if(motionButton!=null)motionButton.GetComponentInChildren<TMP_Text>().text=reducedMotion?"Motion: reduced":"Motion: subtle";
        if(textButton!=null)textButton.GetComponentInChildren<TMP_Text>().text=largeText?"Text: large":"Text: standard";
        if(selection!=null){selection.text.fontSizeMax=largeText?40:34;selection.text.fontSizeMin=18;selection.RefreshHighlight();}
    }
    IEnumerator StampVerdict()
    {
        Color ink=current.isSus?new Color(.65f,.20f,.15f):new Color(.15f,.40f,.29f);
        stampText.text=current.isSus?"SUS":"LEGIT";stampText.color=ink;
        foreach(var border in stamp.GetComponentsInChildren<Image>())border.color=ink;
        stampGroup.alpha=1;if(!muted)sound.PlayOneShot(stampSound);
        if(reducedMotion){stamp.localScale=Vector3.one;yield break;}
        for(float t=0;t<.16f;t+=Time.unscaledDeltaTime)
        {float progress=t/.16f;stamp.localScale=Vector3.one*Mathf.Lerp(1.35f,1,1-Mathf.Pow(1-progress,3));yield return null;}
        stamp.localScale=Vector3.one;stampAnimation=null;
    }
    AudioClip Noise(string name,float seconds,float volume)
    {
        int samples=Mathf.CeilToInt(seconds*22050);var clip=AudioClip.Create(name,samples,1,22050,false);var values=new float[samples];var random=new System.Random(name.GetHashCode());float previous=0;
        for(int i=0;i<samples;i++){previous=Mathf.Lerp(previous,(float)random.NextDouble()*2-1,.28f);values[i]=previous*volume*Mathf.Pow(1-(float)i/samples,2);}
        clip.SetData(values,0);return clip;
    }
    void OnDestroy(){foreach(var t in art)Destroy(t);foreach(var clip in new[]{good,bad,markSound,stampSound,paperSound})if(clip!=null)Destroy(clip);}
}
