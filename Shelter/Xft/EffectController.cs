using UnityEngine;

// ReSharper disable once CheckNamespace
public class EffectController : MonoBehaviour
{
    private XffectCache EffectCache;
    public Transform ObjectCache;

    private Vector3 GetFaceDirection()
    {
        return transform.TransformDirection(Vector3.forward);
    }

    private void OnEffect(string eftname)
    {
        Xffect component;
        switch (eftname)
        {
            case "lightning":
                for (int i = 0; i < 9; i++)
                {
                    component = this.EffectCache.GetObject(eftname).GetComponent<Xffect>();
                    Vector3 zero = Vector3.zero;
                    zero.x = Random.Range(-2.2f, 2.3f);
                    zero.z = Random.Range(-2.1f, 2.1f);
                    component.SetEmitPosition(zero);
                    component.Active();
                }

                break;
            case "cyclone":
                component = this.EffectCache.GetObject(eftname).GetComponent<Xffect>();
                component.SetDirectionAxis(this.GetFaceDirection().normalized);
                component.Active();
                break;
            case "crystal":
                this.EffectCache.GetObject("crystal_surround").GetComponent<Xffect>().Active();
                component = this.EffectCache.GetObject("crystal").GetComponent<Xffect>();
                component.SetEmitPosition(new Vector3(0f, 1.9f, 1.4f));
                component.Active();
                component = this.EffectCache.GetObject("crystal_lightn").GetComponent<Xffect>();
                component.SetDirectionAxis(new Vector3(-1.5f, 1.8f, 0f));
                component.Active();
                component = this.EffectCache.GetObject("crystal").GetComponent<Xffect>();
                component.SetEmitPosition(new Vector3(0f, 1.5f, -1.2f));
                component.Active();
                component = this.EffectCache.GetObject("crystal_lightn").GetComponent<Xffect>();
                component.SetDirectionAxis(new Vector3(1.4f, 1.4f, 0f));
                component.Active();
                break;
            default:
                this.EffectCache.GetObject(eftname).GetComponent<Xffect>().Active();
                break;
        }
    }

    private void OnGUI()
    {
        GUI.Box(new Rect(0f, 0f, 100f, 225f), "Effect List");
        GUI.Label(new Rect(150f, 0f, 350f, 25f), "alt+left mouse button to rotation.  mouse wheel to zoom.");
        if (GUI.Button(new Rect(10f, 20f, 80f, 20f), "Effect1"))
        {
            this.OnEffect("crystal");
        }
        if (GUI.Button(new Rect(10f, 45f, 80f, 20f), "Effect2"))
        {
            this.OnEffect("rage_explode");
        }
        if (GUI.Button(new Rect(10f, 70f, 80f, 20f), "Effect3"))
        {
            this.OnEffect("cyclone");
        }
        if (GUI.Button(new Rect(10f, 95f, 80f, 20f), "Effect4"))
        {
            this.OnEffect("lightning");
        }
        if (GUI.Button(new Rect(10f, 120f, 80f, 20f), "Effect5"))
        {
            this.OnEffect("hit");
        }
        if (GUI.Button(new Rect(10f, 145f, 80f, 20f), "Effect6"))
        {
            this.OnEffect("firebody");
        }
        if (GUI.Button(new Rect(10f, 170f, 80f, 20f), "Effect7"))
        {
            this.OnEffect("explode");
        }
        if (GUI.Button(new Rect(10f, 195f, 80f, 20f), "Effect8"))
        {
            this.OnEffect("rain");
        }
    }

    private void Start()
    {
        this.EffectCache = this.ObjectCache.GetComponent<XffectCache>();
    }
}

