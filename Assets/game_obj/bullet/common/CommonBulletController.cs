using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class CommonBulletController : NetworkBehaviour
{
    public static float bulletSpeed = 6f;
    public static float bulletLifeTime = 30f;
    // Start is called before the first frame update
    void Start()
    {
        GetComponent<Rigidbody2D>().AddRelativeForce(Vector2.up * bulletSpeed,ForceMode2D.Impulse);
        Invoke(nameof(destroySelf), bulletLifeTime);
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        if(collision.gameObject.tag=="Tank")
        {
            destroySelf();
        }
    }

    void destroySelf()
    {
        if(IsServer)GetComponent<NetworkObject>().Despawn();
    }
}
