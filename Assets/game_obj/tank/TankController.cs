using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class TankController : NetworkBehaviour
{
    public GameObject turret;
    // Start is called before the first frame update
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {
        //判定当前是否处于联机模式，如果处于联机模式，则判断当前对象是否为本地玩家所拥有，若不是则不进行后续操作
        if (IsServer || IsClient)
        { 
            if (!IsLocalPlayer) return;
        }
        //deal with tank movement
        if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow))
        {
            forward();
            if (IsClient&&!IsServer)updateMovement_ServerRpc(KeyCode.W);
        }
        else if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow))
        {
            backward();
            if (IsClient && !IsServer) updateMovement_ServerRpc(KeyCode.S);
        }
        if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow))
        {
            turnLeft();
            if(IsClient && !IsServer) updateMovement_ServerRpc(KeyCode.A);
        }
        else if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow))
        {
            turnRight();
            if (IsClient && !IsServer) updateMovement_ServerRpc(KeyCode.D);
        }

        if (Input.GetKey(KeyCode.Comma))
        {
            turretLeft();
            if(IsClient && !IsServer) updateMovement_ServerRpc(KeyCode.Comma);
        }
        else if (Input.GetKey(KeyCode.Period))
        {
            turretRight();
            if(IsClient && !IsServer) updateMovement_ServerRpc(KeyCode.Period);
        }
    }

    void forward() { transform.Translate(Vector3.up * Time.deltaTime * 5); }
    void backward() { transform.Translate(Vector3.down * Time.deltaTime * 5); }
    void turnLeft() { transform.Rotate(Vector3.forward, 180 * Time.deltaTime); }
    void turnRight() { transform.Rotate(Vector3.forward, -180 * Time.deltaTime); }
    void turretLeft() { turret.transform.Rotate(Vector3.forward, 180 * Time.deltaTime); }
    void turretRight() { turret.transform.Rotate(Vector3.forward, -180 * Time.deltaTime); }

    [ServerRpc]
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
}
