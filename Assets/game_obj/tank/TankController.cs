using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

public class TankController : NetworkBehaviour
{
    public GameObject turret;
    public GameObject bullet;
    public GameObject destoryEffect;
    NetworkVariable<int> bulletCount = new(5);
    //炮塔本地角度，服务器写入，其他客户端用它复现炮塔朝向
    //初值 270 对应 prefab 里炮塔的 -90 度，避免开局或中途加入的客户端先把炮塔掰到 0 度
    NetworkVariable<float> turretAngle = new(270f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    //服务器端的身份标记：电脑玩家不是真实连接，用虚拟 id 计分
    [System.NonSerialized] public bool isBot;
    [System.NonSerialized] public ulong playerId;

    public int Ammo => bulletCount.Value;

    /// <summary>子弹初速度，取自子弹 prefab；取不到子弹时返回 0。</summary>
    public float BulletSpeed
    {
        get
        {
            if (bulletTemplate == null && bullet != null) bulletTemplate = bullet.GetComponent<BulletController>();
            return bulletTemplate != null ? bulletTemplate.BulletSpeed : 0f;
        }
    }

    BulletController bulletTemplate;

    public override void OnNetworkSpawn()
    {
        playerId = OwnerClientId;
        turretAngle.OnValueChanged += OnTurretAngleChanged;
        if (IsServer)
        {
            //开局先把真实炮塔角度写进去；之后定期兜底，保证中途加入的客户端也能拿到正确朝向
            SyncTurretAngle();
            InvokeRepeating(nameof(SyncTurretAngle), 0.25f, 0.25f);
        }
        else if (!IsOwner)
        {
            ApplyTurretAngle(turretAngle.Value);
        }
    }

    public override void OnNetworkDespawn()
    {
        turretAngle.OnValueChanged -= OnTurretAngleChanged;
        CancelInvoke(nameof(SyncTurretAngle));
    }

    public void SetPlayerId(ulong id)
    {
        playerId = id;
    }

    void OnTurretAngleChanged(float previous, float current)
    {
        //本机自己转向，不走服务器回声；其他客户端按服务器角度复现
        if (IsOwner) return;
        ApplyTurretAngle(current);
    }

    void ApplyTurretAngle(float angle)
    {
        if (turret == null) return;
        turret.transform.localRotation = Quaternion.Euler(0f, 0f, angle);
    }

    // Start is called before the first frame update
    void Start()
    {
        if (IsOwner && !isBot)
        {
            var coms = gameObject.GetComponentsInChildren<SpriteRenderer>();
            foreach (var com in coms)
            {
                com.color = new Color(0.9f, 0.9f, 1f);
            }
        }
        fixedNetworkTransform = GetComponent<FixedNetworkTransform>();
    }

    // Update is called once per frame
    void Update()
    {
        if (!IsLocalPlayer || isBot) return;
        if(Input.GetKeyDown(KeyCode.Space))
        {
            if(bulletCount.Value>0)
                fire_ServerRpc();
        }
    }

    private void FixedUpdate()
    {
        if (!IsLocalPlayer || isBot) return;

        float throttle = 0f;
        if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) throttle += 1f;
        else if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) throttle -= 1f;

        float steer = 0f;
        if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) steer += 1f;
        else if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) steer -= 1f;

        if (throttle != 0f || steer != 0f) ApplyDrive(throttle, steer);

        if (Input.GetKey(KeyCode.Comma))
        {
            ApplyTurret(1f);
            if (!IsServer) updateMovement_ServerRpc(KeyCode.Comma);
        }
        else if (Input.GetKey(KeyCode.Period))
        {
            ApplyTurret(-1f);
            if (!IsServer) updateMovement_ServerRpc(KeyCode.Period);
        }
    }

    FixedNetworkTransform fixedNetworkTransform;

    /// <summary>
    /// 本地/服务器通用的驾驶接口。throttle 为前后，steer 为左右，范围 -1..1。
    /// </summary>
    public void ApplyDrive(float throttle, float steer)
    {
        if (fixedNetworkTransform == null) fixedNetworkTransform = GetComponent<FixedNetworkTransform>();
        if (fixedNetworkTransform == null) return;
        if (throttle != 0f)
            fixedNetworkTransform.addMovement(Vector3.up * (throttle * Time.fixedDeltaTime * 5f));
        if (steer != 0f)
            fixedNetworkTransform.addRotate(Vector3.forward * (steer * 180f * Time.fixedDeltaTime));
    }

    public void ApplyTurret(float steer)
    {
        if (turret == null || steer == 0f) return;
        turret.transform.Rotate(Vector3.forward, steer * 180f * Time.fixedDeltaTime);
        SyncTurretAngle();
    }

    /// <summary>
    /// 以不超过 180 度/秒的速度把炮塔转向一个世界方向，电脑玩家用它做精确瞄准。
    /// </summary>
    public void AimTurretTowards(Vector3 worldDirection)
    {
        if (turret == null) return;
        worldDirection.z = 0f;
        if (worldDirection.sqrMagnitude < 0.0001f) return;
        float delta = Vector3.SignedAngle(turret.transform.up, worldDirection, Vector3.forward);
        float step = Mathf.Clamp(delta, -180f * Time.fixedDeltaTime, 180f * Time.fixedDeltaTime);
        if (Mathf.Abs(step) < 0.0001f) return;
        turret.transform.Rotate(Vector3.forward, step);
        SyncTurretAngle();
    }

    void SyncTurretAngle()
    {
        if (!IsServer || !IsSpawned || turret == null) return;
        turretAngle.Value = turret.transform.localEulerAngles.z;
    }

    void turretLeft() { ApplyTurret(1f); }
    void turretRight() { ApplyTurret(-1f); }
    [ServerRpc]
    void fire_ServerRpc()
    {
        TryFire();
    }

    /// <summary>
    /// 服务器端开火。真人通过 ServerRpc 调用，电脑玩家直接调用。
    /// </summary>
    public bool TryFire()
    {
        if (!IsServer || !IsSpawned) return false;
        if (bulletCount.Value <= 0) return false;

        RaycastHit2D[] hits = new RaycastHit2D[1];
        GetComponent<Collider2D>().Raycast(turret.transform.up, hits);
        var hit = hits[0];
        var obj = Instantiate(bullet, turret.transform.position + turret.transform.up * Mathf.Min(hit.distance, 1.2f), turret.transform.rotation);
        var bulletController = obj.GetComponent<BulletController>();
        //记录射手，命中时用于记击杀
        bulletController.shooterClientId = playerId;
        obj.GetComponent<NetworkObject>().Spawn(true);
        bulletCount.Value -= 1;
        Invoke(nameof(bulletCountAdd), bulletController.BulletLifeTime);
        return true;
    }

    void bulletCountAdd()
    {
        bulletCount.Value += 1;
    }

    [ServerRpc(Delivery = RpcDelivery.Unreliable)]
    void updateMovement_ServerRpc(KeyCode key) {
        switch (key)
        {
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
                var playerListManager = FindObjectOfType<PlayerListManager>();
                if (playerListManager != null)
                {
                    //击杀记给射手；自己打中自己不算击杀
                    var bulletController = collision.gameObject.GetComponent<BulletController>();
                    if (bulletController != null
                        && bulletController.shooterClientId != ulong.MaxValue
                        && bulletController.shooterClientId != playerId)
                    {
                        playerListManager.AddKill(bulletController.shooterClientId);
                    }
                    //不管是死在谁手里，被击中的坦克都记一次死亡
                    playerListManager.AddDeath(playerId);
                }

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
