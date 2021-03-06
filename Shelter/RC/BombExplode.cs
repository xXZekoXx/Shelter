using Mod;
using Mod.GameSettings;
using UnityEngine;

// ReSharper disable once CheckNamespace
public class BombExplode : Photon.MonoBehaviour
{
    public GameObject myExplosion;

    public void Start()
    {
        if (photonView != null)
        {
            Player owner = photonView.owner;
            if (GameManager.settings.TeamSort > TeamSort.Off)
            {
                switch (owner.Properties.RCTeam)
                {
                    case 1:
                        GetComponent<ParticleSystem>().startColor = Color.cyan;
                        break;
                    case 2:
                        GetComponent<ParticleSystem>().startColor = Color.magenta;
                        break;
                    default:
                        GetComponent<ParticleSystem>().startColor = owner.Properties.RCBombColor;
                        break;
                }
            }
            else
            {
                GetComponent<ParticleSystem>().startColor = owner.Properties.RCBombColor;
            }
            GetComponent<ParticleSystem>().startSize = Mathf.Clamp(owner.Properties.RCBombRadius, 40f, 120f);
        }
    }
}

