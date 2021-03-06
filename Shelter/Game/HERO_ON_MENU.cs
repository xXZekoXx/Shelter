using Game;
using UnityEngine;

// ReSharper disable once CheckNamespace
public class HERO_ON_MENU : MonoBehaviour
{
    private Vector3 cameraOffset;
    private Transform cameraPref;
    public int costumeId;
    private Transform head;
    public float headRotationX;
    public float headRotationY;

    private void LateUpdate()
    {
        this.head.rotation = Quaternion.Euler(this.head.rotation.eulerAngles.x + this.headRotationX, this.head.rotation.eulerAngles.y + this.headRotationY, this.head.rotation.eulerAngles.z);
        if (this.costumeId == 9)
        {
            GameObject.Find("MainCamera_Mono").transform.position = this.cameraPref.position + this.cameraOffset;
        }
    }

    private void Start()
    {
        HERO_SETUP component = gameObject.GetComponent<HERO_SETUP>();
        component.myCostume = HeroCostume.costume[this.costumeId];
        component.setCharacterComponent();
        this.head = transform.Find("Amarture/Controller_Body/hip/spine/chest/neck/head");
        this.cameraPref = transform.Find("Amarture/Controller_Body/hip/spine/chest/shoulder_R/upper_arm_R");
        if (this.costumeId == 9)
        {
            this.cameraOffset = GameObject.Find("MainCamera_Mono").transform.position - this.cameraPref.position;
        }
        if (component.myCostume.sex == Sex.Female)
        {
            animation.Play("stand");
            animation["stand"].normalizedTime = Random.Range(0f, 1f);
        }
        else
        {
            animation.Play("stand_levi");
            animation["stand_levi"].normalizedTime = Random.Range(0f, 1f);
        }
        const float num = 0.5f;
        animation["stand"].speed = num;
        animation["stand_levi"].speed = num;
    }
}

