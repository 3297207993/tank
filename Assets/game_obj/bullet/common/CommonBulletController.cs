using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class CommonBulletController : NetworkBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        GetComponent<Rigidbody2D>().AddRelativeForce(Vector2.up * 6,ForceMode2D.Impulse);
        Invoke(nameof(destroySelf), 30f);
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        if(collision.gameObject.tag=="Bullet"||collision.gameObject.tag=="Tank")
        {
            destroySelf();
        }
    }

    void destroySelf()
    {
        if(IsServer)GetComponent<NetworkObject>().Despawn();
    }
}
