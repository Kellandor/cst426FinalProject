using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor.Animations;

public class AnimPlay : MonoBehaviour
{
    public GameObject input;
    ShaderManager man;
    public Animator anim;
    public AnimatorController controller;
    bool go;
    string[] tb;
    string[] tp;
    string prev;
    public bool playing = false;
    int counter = 0;
    float a1, a2;
    AnimatorControllerParameter[] list;

    int[] oldbones = new int[6];

    // Use this for initialization
    void Start () {
        man = input.GetComponent<ShaderManager>();
        anim = GetComponent<Animator>();
        list = controller.parameters;
        go = man.sAnim;
	}
	
    void lInt(float a, float b)
    {
            a1 = a + 0.25f * (b - a);
            a2 = 1 - a1;
    }

    void replace()
    {
        while (anim.GetFloat(prev) > 0)
        {
            lInt(anim.GetFloat(tb[0]), anim.GetFloat(prev));
            anim.SetFloat(tb[0], a1);
            anim.SetFloat(prev, a2);
        }
    }
	// Update is called once per frame
	void Update ()
    {
		if(man.sAnim )//&& playing == false
        {
            playing = true;
            man.sAnim = false;
            if (counter == 0)
            {
                for (int i = 1; i < man.bpmin.Length; i++)
                {
                    //tb = man.animationNames[man.animIndex].Split('.');
                    tb = man.animationNames[man.bpmin[i]].Split('.');
                    oldbones[i] = man.bpmin[i];
                    prev = tb[0];
                    anim.SetFloat(tb[0], 1f);
                    Debug.Log("CURRENT ANIMATION PLAYING: " + counter + " " + tb[0]);
                }
            }
            else if(counter > 0)
            {
                for (int i = 1; i < man.bpmin.Length; i++)
                {
                    //tb = man.animationNames[man.animIndex].Split('.');
                    tp = man.animationNames[oldbones[i]].Split('.');
                    anim.SetFloat(tp[0], 0f);
                    tb = man.animationNames[man.bpmin[i]].Split('.');
                    oldbones[i] = man.bpmin[i];
                    prev = tb[0];
                    anim.SetFloat(tb[0], 1f);
                    Debug.Log("CURRENT ANIMATION PLAYING: " + counter + " " + tb[0]);
                }
                /*
                if (prev != tb[0])
                {
                    Debug.Log("DEBUG: " + prev + " " + tb[0]);
                    anim.SetFloat(tb[0], 1f);
                    anim.SetFloat(prev, 0);
                    //replace();
                    prev = tb[0];
                    Debug.Log("CURRENT ANIMATION PLAYING: " + man.animIndex + " " + tb[0]);
                }*/
            }
            tb = man.animationNames[man.animIndex].Split('.');

            //counter++;
            
            //anim.Play(tb[0]);

           
            //playing = false;
            
            //anim.SetFloat(tb[0], 0.5f);
            
        }
    }

    IEnumerator blend()
    {
        yield return new WaitForSecondsRealtime(0.2f);
        anim.SetFloat(tb[0], 1f);
        anim.SetFloat(prev, 0.0f);
    }
    
    IEnumerator wait()
    {
        yield return new WaitForSecondsRealtime(0f);
        //man.sAnim = false;
        playing = false;
        counter++;
        for (int i = 1; i < man.bpmin.Length; i++)
        {
            //tb = man.animationNames[man.animIndex].Split('.');
            tb = man.animationNames[oldbones[i]].Split('.');
            prev = tb[0];
            anim.SetFloat(tb[0], 0f);
            Debug.Log("CURRENT ANIMATION PLAYING: " + man.animIndex + " " + tb[0]);
        }
    }
}
