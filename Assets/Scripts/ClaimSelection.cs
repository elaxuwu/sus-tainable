using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public sealed class ClaimSelection : MonoBehaviour, IPointerDownHandler, IPointerUpHandler,
    IInitializePotentialDragHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public TMP_Text text;
    public Camera eventCamera;
    public bool interactable;
    public readonly List<string> phrases=new List<string>();
    public event Action Changed;
    public event Action<string> Notice;
    public bool IsSelecting=>pointerStart>=0||keyboardStart>=0;
    readonly List<Vector2Int> ranges=new List<Vector2Int>();
    readonly List<Image> marks=new List<Image>();
    RectTransform layer;
    int pointerStart=-1,pointerEnd=-1,keyboardStart=-1,keyboardWord,count;
    bool keyboardMode;
    string original="";
    CaseData reviewed;
    static readonly Color Amber=new Color(1,.79f,.21f,.55f),Preview=new Color(1,.79f,.21f,.27f),Green=new Color(.28f,.69f,.44f,.42f),Red=new Color(.9f,.32f,.22f,.35f);
    void Awake()
    {
        if(text==null)text=GetComponent<TMP_Text>();
        layer=new GameObject("Marker strokes",typeof(RectTransform)).GetComponent<RectTransform>();
        layer.SetParent(text.transform.parent,false);layer.SetSiblingIndex(text.transform.GetSiblingIndex());
        text.OnPreRenderText+=Rebuild;
    }
    public void SetText(string value)
    {
        original=value??"";phrases.Clear();ranges.Clear();pointerStart=pointerEnd=keyboardStart=-1;keyboardWord=0;keyboardMode=false;reviewed=null;
        text.richText=false;text.text=original;RefreshHighlight();Changed?.Invoke();
    }
    int Word(Vector2 point)
    {
        if(!RectTransformUtility.RectangleContainsScreenPoint(text.rectTransform,point,eventCamera)||text.textInfo.wordCount==0)return -1;
        int word=TMP_TextUtilities.FindIntersectingWord(text,point,eventCamera);
        return word;
    }
    public void OnInitializePotentialDrag(PointerEventData e){e.useDragThreshold=false;}
    public void OnBeginDrag(PointerEventData e){OnDrag(e);}
    public void OnDrag(PointerEventData e){if(interactable&&pointerStart>=0&&e.button==PointerEventData.InputButton.Left){pointerEnd=Word(e.position);Draw();}}
    public void OnEndDrag(PointerEventData e){Commit(e);}
    public void OnPointerDown(PointerEventData e)
    {
        if(!interactable||e.button!=PointerEventData.InputButton.Left)return;
        keyboardMode=false;keyboardStart=-1;pointerStart=pointerEnd=Word(e.position);Draw();
    }
    public void OnPointerUp(PointerEventData e){Commit(e);}
    void Commit(PointerEventData e)
    {
        if(e.button!=PointerEventData.InputButton.Left||pointerStart<0)return;
        int first=pointerStart,last=Word(e.position);pointerStart=pointerEnd=-1;
        if(interactable&&last>=0)Select(first,last);else Draw();
    }
    void Update()
    {
        if(!interactable||!Application.isFocused){if(IsSelecting){pointerStart=pointerEnd=keyboardStart=-1;Draw();}return;}
        var keys=Keyboard.current;if(keys==null||text.textInfo.wordCount==0)return;
        bool moved=keys.leftArrowKey.wasPressedThisFrame||keys.rightArrowKey.wasPressedThisFrame;
        if(moved||keys.spaceKey.wasPressedThisFrame)keyboardMode=true;
        if(keys.leftArrowKey.wasPressedThisFrame)keyboardWord=Mathf.Max(0,keyboardWord-1);
        if(keys.rightArrowKey.wasPressedThisFrame)keyboardWord=Mathf.Min(text.textInfo.wordCount-1,keyboardWord+1);
        if(keys.spaceKey.wasPressedThisFrame){if(keyboardStart<0)keyboardStart=keyboardWord;else{int first=keyboardStart;keyboardStart=-1;Select(first,keyboardWord);}}
        if(keys.escapeKey.wasPressedThisFrame)keyboardStart=pointerStart=pointerEnd=-1;
        if(moved||keys.spaceKey.wasPressedThisFrame||keys.escapeKey.wasPressedThisFrame)Draw();
    }
    public void Select(int first,int last)
    {
        if(!interactable)return;text.ForceMeshUpdate();int from=Mathf.Min(first,last),to=Mathf.Max(first,last);
        if(from<0||to>=text.textInfo.wordCount)return;
        for(int i=ranges.Count-1;i>=0;i--)if(from<=ranges[i].y&&to>=ranges[i].x){Remove(i);return;}
        if(ranges.Count>=3){Notice?.Invoke("Three marks is the limit. Remove one or clear your evidence.");Draw();return;}
        int begin=text.textInfo.characterInfo[text.textInfo.wordInfo[from].firstCharacterIndex].index;
        var final=text.textInfo.characterInfo[text.textInfo.wordInfo[to].lastCharacterIndex];
        ranges.Add(new Vector2Int(from,to));phrases.Add(original.Substring(begin,final.index+final.stringLength-begin));Draw();Changed?.Invoke();
    }
    public void Remove(int i){if(!interactable||i<0||i>=ranges.Count)return;ranges.RemoveAt(i);phrases.RemoveAt(i);Draw();Changed?.Invoke();}
    public void Clear(){if(!interactable)return;ranges.Clear();phrases.Clear();pointerStart=pointerEnd=keyboardStart=-1;Draw();Changed?.Invoke();}
    public void Reveal(CaseData data){reviewed=data;pointerStart=pointerEnd=keyboardStart=-1;keyboardMode=false;RefreshHighlight();}
    public void RefreshHighlight(){text.ForceMeshUpdate();Draw();}
    void Rebuild(TMP_TextInfo info){Draw();}
    void Draw()
    {
        if(layer==null)return;var source=text.rectTransform;
        layer.anchorMin=source.anchorMin;layer.anchorMax=source.anchorMax;layer.pivot=source.pivot;layer.sizeDelta=source.sizeDelta;layer.anchoredPosition=source.anchoredPosition;
        layer.localScale=source.localScale;layer.localRotation=source.localRotation;count=0;
        if(reviewed==null)
        {
            foreach(var range in ranges)WordStroke(range.x,range.y,Amber);
            if(pointerStart>=0&&pointerEnd>=0)WordStroke(Mathf.Min(pointerStart,pointerEnd),Mathf.Max(pointerStart,pointerEnd),Preview);
            if(keyboardMode&&keyboardWord<text.textInfo.wordCount)WordStroke(keyboardStart<0?keyboardWord:Mathf.Min(keyboardStart,keyboardWord),keyboardStart<0?keyboardWord:Mathf.Max(keyboardStart,keyboardWord),Preview);
        }
        else
        {
            for(int i=0;i<ranges.Count;i++)WordStroke(ranges[i].x,ranges[i].y,Array.Exists(reviewed.tells,t=>CaseScoring.Normalize(t.phrase)==CaseScoring.Normalize(phrases[i]))?Green:Red);
            foreach(var tell in reviewed.tells)
            {
                if(phrases.Exists(p=>CaseScoring.Normalize(p)==CaseScoring.Normalize(tell.phrase)))continue;
                int begin=original.IndexOf(tell.phrase,StringComparison.Ordinal),a=-1,b=-1;
                for(int i=0;i<text.textInfo.characterCount;i++){int index=text.textInfo.characterInfo[i].index;if(index>=begin&&index<begin+tell.phrase.Length){if(a<0)a=i;b=i;}}
                if(begin>=0&&a>=0)Stroke(a,b,Amber);
            }
        }
        for(int i=count;i<marks.Count;i++)marks[i].gameObject.SetActive(false);
    }
    void WordStroke(int a,int b,Color color){if(a<0||b>=text.textInfo.wordCount)return;Stroke(text.textInfo.wordInfo[a].firstCharacterIndex,text.textInfo.wordInfo[b].lastCharacterIndex,color);}
    void Stroke(int first,int last,Color color)
    {
        int line=-1;float left=0,right=0,top=0,bottom=0;
        for(int i=first;i<=last;i++)
        {
            var c=text.textInfo.characterInfo[i];if(char.IsWhiteSpace(c.character))continue;
            if(c.lineNumber!=line){if(line>=0)Mark(left,right,bottom,top,color);line=c.lineNumber;left=c.origin;right=c.xAdvance;top=c.ascender;bottom=c.descender;}
            else{left=Mathf.Min(left,c.origin);right=Mathf.Max(right,c.xAdvance);top=Mathf.Max(top,c.ascender);bottom=Mathf.Min(bottom,c.descender);}
        }
        if(line>=0)Mark(left,right,bottom,top,color);
    }
    void Mark(float left,float right,float bottom,float top,Color color)
    {
        if(count==marks.Count){var go=new GameObject("Marker",typeof(RectTransform),typeof(Image));go.transform.SetParent(layer,false);var image=go.GetComponent<Image>();image.raycastTarget=false;marks.Add(image);}
        var mark=marks[count++];mark.gameObject.SetActive(true);mark.color=color;var rect=mark.rectTransform;
        rect.anchorMin=rect.anchorMax=text.rectTransform.pivot;rect.anchoredPosition=new Vector2((left+right)/2,(bottom+top)/2);rect.sizeDelta=new Vector2(right-left+7,top-bottom+3);
    }
    void OnDestroy(){if(text!=null)text.OnPreRenderText-=Rebuild;if(layer!=null)Destroy(layer.gameObject);}
}
