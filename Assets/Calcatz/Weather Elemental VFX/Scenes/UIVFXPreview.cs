using UnityEngine;
using System.Collections.Generic;
using UnityEngine.UI;
using UnityEngine.VFX;
using TMPro;
public class UIVFXPreview : MonoBehaviour
{
    public List<VisualEffect> vfxList;
    public TMPro.TextMeshProUGUI vfxNameText;

    private int currentIndex = 0;

    private void Start()
    {

        for (int i = 0; i < vfxList.Count; i++)
        {
            vfxList[i].gameObject.SetActive(true);
            vfxList[i].Stop();
        }

        ShowCurrentVFX();
    }

    // void Update()
    // {
    //     if (Input.GetKeyDown(KeyCode.RightArrow))
    //     {
    //         StopCurrentVFX();
    //         currentIndex = (currentIndex + 1) % vfxList.Count;
    //         ShowCurrentVFX();
    //     }
    //     else if (Input.GetKeyDown(KeyCode.LeftArrow))
    //     {
    //         StopCurrentVFX();
    //         currentIndex = (currentIndex - 1 + vfxList.Count) % vfxList.Count;
    //         ShowCurrentVFX();
    //     }
    // }

    public void NextVFX(){
        StopCurrentVFX();
        currentIndex = (currentIndex + 1) % vfxList.Count;
        ShowCurrentVFX();
    }
    public void PreviousVFX(){
        StopCurrentVFX();
        currentIndex = (currentIndex - 1 + vfxList.Count) % vfxList.Count;
        ShowCurrentVFX();
    }

    public void ShowCurrentVFX(){
        vfxList[currentIndex].Play();
        vfxNameText.text = vfxList[currentIndex].name;
    }

    public void StopCurrentVFX(){
        vfxList[currentIndex].Stop();
    }

}
