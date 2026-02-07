using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class SpriteTalkController : MonoBehaviour
{



    Animator anim;

    void Awake()
    {
        anim = GetComponent<Animator>();
    }

    public void TalkA() => anim.SetTrigger("TalkA");
    public void TalkB() => anim.SetTrigger("TalkB");
    public void TalkC() => anim.SetTrigger("TalkC");
    public void StopTalk() => anim.SetTrigger("StopTalk");
}