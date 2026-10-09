using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
public sealed class DeskVerdictButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    public SusGame game;
    public bool suspicious;
    public GameObject hoverCaption;
    Vector3 home;
    Material material;
    Color baseColor;
    bool hovering,pressing;
    void Awake(){home=transform.localPosition;material=GetComponent<Renderer>().material;baseColor=material.color;}
    void Update()
    {
        if(hoverCaption!=null)hoverCaption.SetActive(hovering&&game.CanAnswer);
        float strength=game.CanAnswer?(hovering?.16f:0):-.28f;
        material.color=Color.Lerp(material.color,strength>=0?Color.Lerp(baseColor,Color.white,strength):Color.Lerp(baseColor,new Color(.18f,.22f,.22f),-strength),1-Mathf.Exp(-20*Time.unscaledDeltaTime));
    }
    public void OnPointerEnter(PointerEventData e){hovering=true;}
    public void OnPointerExit(PointerEventData e){hovering=false;}
    // EventSystem and direct-input fallback share one gated raycast and one submission per frame.
    public void OnPointerClick(PointerEventData e){if(e.button==PointerEventData.InputButton.Left)game.SubmitVerdictPointer(e.position);}
    public void Pulse(){if(!pressing&&isActiveAndEnabled)StartCoroutine(Press());}
    IEnumerator Press()
    {
        pressing=true;
        if(game.reducedMotion){pressing=false;yield break;}
        for(float t=0;t<.14f;t+=Time.unscaledDeltaTime){float down=Mathf.Sin(t/.14f*Mathf.PI);transform.localPosition=home+Vector3.forward*(down*.006f);yield return null;}
        transform.localPosition=home;pressing=false;
    }
    void OnDisable(){hovering=false;pressing=false;transform.localPosition=home;if(hoverCaption!=null)hoverCaption.SetActive(false);}
    void OnDestroy(){if(material!=null)Destroy(material);}
}
