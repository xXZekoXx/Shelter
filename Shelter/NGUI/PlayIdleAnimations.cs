using System.Collections.Generic;
using NGUI.Internal;
using UnityEngine;

[AddComponentMenu("NGUI/Examples/Play Idle Animations")]
// ReSharper disable once CheckNamespace
public class PlayIdleAnimations : MonoBehaviour
{
    private Animation mAnim;
    private List<AnimationClip> mBreaks = new List<AnimationClip>();
    private AnimationClip mIdle;
    private int mLastIndex;
    private float mNextBreak;

    private void Start()
    {
        this.mAnim = GetComponentInChildren<Animation>();
        if (this.mAnim == null)
        {
            Debug.LogWarning(NGUITools.GetHierarchy(gameObject) + " has no Animation component");
            Destroy(this);
        }
        else
        {
            foreach (AnimationState current in mAnim)
            {
                if (current == null)
                    continue;
                
                if (current.clip.name == "idle")
                {
                    current.layer = 0;
                    this.mIdle = current.clip;
                    this.mAnim.Play(this.mIdle.name);
                }
                else if (current.clip.name.StartsWith("idle"))
                {
                    current.layer = 1;
                    this.mBreaks.Add(current.clip);
                }
            }
        }
    }

    private void Update()
    {
        if (this.mNextBreak < Time.time)
        {
            if (this.mBreaks.Count == 1)
            {
                AnimationClip clip = this.mBreaks[0];
                this.mNextBreak = Time.time + clip.length + UnityEngine.Random.Range(5f, 15f);
                this.mAnim.CrossFade(clip.name);
            }
            else
            {
                int num = UnityEngine.Random.Range(0, this.mBreaks.Count - 1);
                if (this.mLastIndex == num)
                {
                    num++;
                    if (num >= this.mBreaks.Count)
                    {
                        num = 0;
                    }
                }
                this.mLastIndex = num;
                AnimationClip clip2 = this.mBreaks[num];
                this.mNextBreak = Time.time + clip2.length + UnityEngine.Random.Range(2f, 8f);
                this.mAnim.CrossFade(clip2.name);
            }
        }
    }
}

