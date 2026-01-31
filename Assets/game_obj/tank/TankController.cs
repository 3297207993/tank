using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class TankController : NetworkBehaviour
{
    public GameObject turret;
    public GameObject bullet;
    public GameObject destoryEffect;
    NetworkVariable<int> bulletCount = new(5);
    // Start is called before the first frame update
    void Start()
    {
        if (IsOwner)
        {
            var coms = gameObject.GetComponentsInChildren<SpriteRenderer>();
            foreach (var com in coms)
            {
                com.color = new Color(0.9f, 0.9f, 1f);
            }
        }
    }

    // Update is called once per frame
    void Update()
    {
        if (!IsLocalPlayer) return;
        if(Input.GetKeyDown(KeyCode.Space))
        {
            if(bulletCount.Value>0)
                fire_ServerRpc();
        }
    }

    private void FixedUpdate()
    {
        if (!IsLocalPlayer) return;
        //deal with tank movement
        if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow))
        {
            forward();
            if (!IsServer) updateMovement_ServerRpc(KeyCode.W);
        }
        else if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow))
        {
            backward();
            if (!IsServer) updateMovement_ServerRpc(KeyCode.S);
        }
        if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow))
        {
            turnLeft();
            if (!IsServer) updateMovement_ServerRpc(KeyCode.A);
        }
        else if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow))
        {
            turnRight();
            if (!IsServer) updateMovement_ServerRpc(KeyCode.D);
        }

        if (Input.GetKey(KeyCode.Comma))
        {
            turretLeft();
            if (!IsServer) updateMovement_ServerRpc(KeyCode.Comma);
        }
        else if (Input.GetKey(KeyCode.Period))
        {
            turretRight();
            if (!IsServer) updateMovement_ServerRpc(KeyCode.Period);
        }
    }

    void forward() { transform.Translate(Vector3.up * Time.fixedDeltaTime * 5); }
    void backward() { transform.Translate(Vector3.down * Time.fixedDeltaTime * 5); }
    void turnLeft() { transform.Rotate(Vector3.forward, 180 * Time.fixedDeltaTime); }
    void turnRight() { transform.Rotate(Vector3.forward, -180 * Time.fixedDeltaTime); }
    void turretLeft() { turret.transform.Rotate(Vector3.forward, 180 * Time.fixedDeltaTime); }
    void turretRight() { turret.transform.Rotate(Vector3.forward, -180 * Time.fixedDeltaTime); }
    [ServerRpc]
    void fire_ServerRpc()
    {
        if(bulletCount.Value>0)
        {
            RaycastHit2D[] hits = new RaycastHit2D[1];
            GetComponent<Collider2D>().Raycast(turret.transform.up, hits);
            var hit = hits[0];
            var obj = Instantiate(bullet, turret.transform.position + turret.transform.up * Mathf.Min(hit.distance, 1.2f), turret.transform.rotation);
            obj.GetComponent<NetworkObject>().Spawn(true);
            bulletCount.Value -= 1;
            Invoke(nameof(bulletCountAdd), CommonBulletController.bulletLifeTime);
        }
    }

    void bulletCountAdd()
    {
        bulletCount.Value += 1;
    }

    [ServerRpc(Delivery = RpcDelivery.Unreliable)]
    void updateMovement_ServerRpc(KeyCode key) {
        switch (key)
        {
            case KeyCode.W:
                forward();
                break;
            case KeyCode.S:
                backward();
                break;
            case KeyCode.A:
                turnLeft();
                break;
            case KeyCode.D:
                turnRight();
                break;
            case KeyCode.Comma:
                turretLeft();
                break;
            case KeyCode.Period:
                turretRight();
                break;
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if(collision.gameObject.tag=="Bullet")
        {
            if (IsServer)
            {
                if (GameObject.FindGameObjectsWithTag("Tank").Length <= 2)
                {
                    MapGenerator gen = FindObjectOfType<MapGenerator>();
                    gen.ResetAndGenerateLater(2.0f);
                }
                showDestroyEffect_ClientRpc();
                gameObject.GetComponent<NetworkObject>().Despawn(true); 
            }
        }
    }

    [ClientRpc]
    void showDestroyEffect_ClientRpc()
    {
        Instantiate(destoryEffect, transform.position, Quaternion.identity);
    }
}
