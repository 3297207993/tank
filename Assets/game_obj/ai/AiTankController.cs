using System;
using Unity.Netcode;
using UnityEngine;

//服务器端的电脑玩家：感知、决策、导航、瞄准全部在服务器上跑。
//2D 全图可见，没有“看不见”的情况：直接锁定最近的敌方坦克，不再巡逻、记忆或听枪声。
//规则内尽可能强：瞬时反应、零瞄准误差、提前量射击；仍然受墙、转向速度、弹药和子弹飞行时间限制。
public class AiTankController : MonoBehaviour
{
    const float PerceptionInterval = 0.08f;
    const float PathInterval = 0.35f;
    const float DodgeCheckInterval = 0.06f;
    const float DodgeHorizon = 1.15f;
    const float DodgeRadius = 0.9f;
    const float PreferredMinRange = 3.5f;
    const float PreferredMaxRange = 8.5f;
    const float FireCooldown = 0.18f;
    const float AimTolerance = 0.7f;
    const float MaxInterceptTime = 2.6f;
    const float TankRadius = 0.75f;
    const float PathClearance = 0.95f;
    const float MoveProbe = 0.7f;
    const float BulletSpawnOffset = 1.2f;

    MapGenerator mapGenerator;
    MapDataContainer map;
    TankController tank;
    Transform turret;
    AiNavigation navigation;

    TankController target;
    Vector3 targetPosition;
    Vector3 targetVelocity;
    float lastTargetSampleTime;
    bool targetShotClear;

    float nextPerceptionTime;
    float nextPathTime;
    float nextFireTime;
    float nextDodgeCheckTime;
    float nextStrafeSwitch;
    int strafeSign = 1;

    Vector3 pathGoal;
    bool hasPathGoal;

    Vector3 dodgeDirection;
    float dodgeUntil;
    float unstuckUntil;
    float stuckCheckTime;
    Vector3 stuckCheckPosition;
    bool wantsToMove;
    bool tryingToMove;

    bool initialized;

    public void Initialize(MapGenerator generator)
    {
        mapGenerator = generator;
        map = generator != null ? generator.CurrentMap : null;
        tank = GetComponent<TankController>();
        turret = tank != null && tank.turret != null ? tank.turret.transform : null;
        navigation = new AiNavigation(generator);

        if (generator == null || NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer || tank == null || turret == null)
        {
            enabled = false;
            return;
        }

        stuckCheckPosition = transform.position;
        stuckCheckTime = Time.time + 0.5f;
        nextStrafeSwitch = Time.time + UnityEngine.Random.Range(0.5f, 1.2f);
        initialized = true;
    }

    void FixedUpdate()
    {
        if (!initialized || tank == null || turret == null) return;
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer) return;

        if (map == null)
        {
            map = mapGenerator.CurrentMap;
            if (map == null) return;
        }

        float now = Time.time;
        UpdateTarget(now);
        UpdateBulletThreat(now);

        Vector3 moveDirection = Vector3.zero;
        wantsToMove = false;
        if (now < dodgeUntil)
        {
            moveDirection = dodgeDirection;
            wantsToMove = moveDirection.sqrMagnitude > 0.0001f;
        }
        else
        {
            wantsToMove = DecideMovement(now, out moveDirection);
        }

        tryingToMove = false;
        if (wantsToMove) DriveToward(moveDirection);
        else tank.ApplyDrive(0f, 0f);

        Aim(now);
        CheckStuck(now);
    }

    //全图可见：直接锁定最近的敌方坦克，不再做视线筛选、记忆和巡逻
    void UpdateTarget(float now)
    {
        if (now < nextPerceptionTime) return;
        nextPerceptionTime = now + PerceptionInterval;

        //最近且能打到的目标优先；都被墙挡住时退而求其次选最近的
        TankController best = null;
        bool bestClear = false;
        float bestDistance = float.MaxValue;
        foreach (var obj in GameObject.FindGameObjectsWithTag("Tank"))
        {
            var candidate = obj.GetComponent<TankController>();
            if (candidate == null || candidate == tank) continue;
            var candidateObject = obj.GetComponent<NetworkObject>();
            if (candidateObject != null && !candidateObject.IsSpawned) continue;

            float distance = Vector2.Distance(transform.position, candidate.transform.position);
            bool clear = IsShotPathClear(turret.position, candidate.transform.position, candidate);
            if (clear == bestClear)
            {
                if (distance >= bestDistance) continue;
            }
            else if (!clear)
            {
                //已经有一个能打到的目标，忽略被挡住的
                continue;
            }
            best = candidate;
            bestClear = clear;
            bestDistance = distance;
        }

        if (best != null)
        {
            if (best != target)
            {
                target = best;
                targetPosition = best.transform.position;
                targetVelocity = Vector3.zero;
                lastTargetSampleTime = now;
            }
            else
            {
                float dt = Mathf.Max(0.001f, now - lastTargetSampleTime);
                Vector3 instantVelocity = (best.transform.position - targetPosition) / dt;
                instantVelocity.z = 0f;
                //限制异常值，避免开局瞬移或重生时算错提前量
                if (instantVelocity.sqrMagnitude > 36f) instantVelocity = instantVelocity.normalized * 6f;
                targetVelocity = Vector3.Lerp(targetVelocity, instantVelocity, 0.65f);
                lastTargetSampleTime = now;
            }
            targetPosition = best.transform.position;
            //移动决策每帧都要用，和感知一起缓存，避免每帧重复射线
            targetShotClear = bestClear;
        }
        else
        {
            target = null;
            targetVelocity = Vector3.zero;
            targetShotClear = false;
        }
    }

    //场上子弹都可见，直接判断子弹沿当前方向的直线路径是否会经过自己
    void UpdateBulletThreat(float now)
    {
        if (now < nextDodgeCheckTime) return;
        nextDodgeCheckTime = now + DodgeCheckInterval;

        Vector2 me = transform.position;
        float bestImpact = float.MaxValue;
        Vector2 bestDirection = Vector2.zero;
        Vector2 bestBulletPos = Vector2.zero;

        foreach (var bulletObj in GameObject.FindGameObjectsWithTag("Bullet"))
        {
            var body = bulletObj.GetComponent<Rigidbody2D>();
            if (body == null) continue;

            Vector2 pos = body.position;
            if (Vector2.Distance(pos, me) > 22f) continue;
            Vector2 vel = body.velocity;
            float speed = vel.magnitude;
            if (speed < 0.5f) continue;

            Vector2 dir = vel / speed;
            float along = Vector2.Dot(me - pos, dir);
            //已经飞过自己的子弹不算威胁，否则会在身边来回抽搐
            if (along <= 0f) continue;
            float impactTime = along / speed;
            if (impactTime > DodgeHorizon) continue;
            if (Vector2.Distance(pos + dir * along, me) > DodgeRadius) continue;

            if (impactTime < bestImpact)
            {
                bestImpact = impactTime;
                bestDirection = dir;
                bestBulletPos = pos;
            }
        }

        if (bestImpact >= float.MaxValue) return;

        Vector2 perp = new Vector2(-bestDirection.y, bestDirection.x);
        float side = Vector2.Dot(me - bestBulletPos, perp);
        if (Mathf.Abs(side) < 0.05f) side = Vector3.Dot(transform.up, perp);
        float sign = side >= 0f ? 1f : -1f;

        Vector3 candidate = new Vector3(perp.x * sign, perp.y * sign, 0f);
        if (!CanMove(candidate))
        {
            candidate = -candidate;
            if (!CanMove(candidate)) return;
        }
        dodgeDirection = candidate;
        dodgeUntil = now + 0.3f;
    }

    bool DecideMovement(float now, out Vector3 direction)
    {
        direction = Vector3.zero;
        if (target == null) return false;

        Vector3 targetPos = target.transform.position;
        Vector3 toTarget = targetPos - transform.position;
        float distance = toTarget.magnitude;
        if (distance < 0.01f) return false;
        Vector3 dirTo = toTarget / distance;

        //没子弹时以脱离为主，等 30 秒的补给
        if (tank.Ammo <= 0)
        {
            Vector3 away = -dirTo;
            if (CanMove(away)) { direction = away; return true; }
            return FollowPath(now, transform.position + away * 6f, out direction);
        }

        //隔着墙时先绕过去：侧向绕圈只适合双方能互相射击的情况
        if (!targetShotClear)
        {
            return FollowPath(now, targetPos, out direction);
        }

        if (distance > PreferredMaxRange)
        {
            if (CanMove(dirTo)) { direction = dirTo; return true; }
            return FollowPath(now, targetPos, out direction);
        }

        if (distance < PreferredMinRange)
        {
            if (CanMove(-dirTo)) { direction = -dirTo; return true; }
        }

        //侧向绕圈：保持炮塔独立瞄准的同时让对手更难预判
        if (now >= nextStrafeSwitch)
        {
            strafeSign = -strafeSign;
            nextStrafeSwitch = now + UnityEngine.Random.Range(0.7f, 1.4f);
        }
        Vector3 perp = new Vector3(-dirTo.y, dirTo.x, 0f) * strafeSign;
        if (CanMove(perp)) { direction = perp; return true; }
        perp = -perp;
        if (CanMove(perp)) { strafeSign = -strafeSign; direction = perp; return true; }
        if (CanMove(dirTo)) { direction = dirTo; return true; }
        return FollowPath(now, targetPos, out direction);
    }

    bool FollowPath(float now, Vector3 goal, out Vector3 direction)
    {
        direction = Vector3.zero;
        bool goalChanged = !hasPathGoal || (pathGoal - goal).sqrMagnitude > 1.5f;
        if (now >= nextPathTime || goalChanged)
        {
            navigation.Repath(transform.position, goal, IsClearForTank);
            nextPathTime = now + PathInterval;
            pathGoal = goal;
            hasPathGoal = true;
        }
        if (!navigation.HasPath) return false;

        Vector3 waypoint = navigation.CurrentWaypoint(transform.position, 0.45f);
        Vector3 delta = waypoint - transform.position;
        delta.z = 0f;
        if (delta.sqrMagnitude < 0.01f) return false;
        direction = delta.normalized;
        return true;
    }

    void DriveToward(Vector3 direction)
    {
        direction.z = 0f;
        if (direction.sqrMagnitude < 0.0001f) return;
        direction.Normalize();

        float signed = Vector3.SignedAngle(transform.up, direction, Vector3.forward);
        float abs = Mathf.Abs(signed);
        float throttle = abs < 45f ? 1f : abs < 95f ? 0.5f : 0f;
        float steer = Mathf.Clamp(signed / 12f, -1f, 1f);
        tryingToMove = throttle > 0f;
        tank.ApplyDrive(throttle, steer);
    }

    //瞄准与开火：用子弹速度解算目标的提前量命中点，把炮塔转过去；
    //角度误差、弹道是否被挡、炮口安全和冷却都满足时才开火。
    //解不出提前量时先对准目标当前位置；没有目标且正在移动时炮塔保持朝车头。
    void Aim(float now)
    {
        if (target != null)
        {
            Vector3 muzzle = turret.position;
            //子弹实际生成在炮口前 1.2，提前量必须从生成点算，否则目标横移会打偏
            Vector3 spawn = muzzle + turret.up * BulletSpawnOffset;
            Vector3 predicted;
            float travelTime;
            if (SolveIntercept(spawn, target.transform.position, targetVelocity, tank.BulletSpeed, out predicted, out travelTime)
                && travelTime <= MaxInterceptTime)
            {
                Vector3 aimDirection = predicted - muzzle;
                tank.AimTurretTowards(aimDirection);

                if (now >= nextFireTime && tank.Ammo > 0
                    && Vector3.Angle(turret.up, aimDirection) <= AimTolerance
                    && IsShotPathClear(muzzle, predicted, target)
                    && MuzzleSafe())
                {
                    if (tank.TryFire()) nextFireTime = now + FireCooldown;
                }
                return;
            }

            //解不出提前量时先跟住当前位置，等距离合适再开火
            tank.AimTurretTowards(target.transform.position - muzzle);
            return;
        }

        if (wantsToMove)
        {
            tank.AimTurretTowards(transform.up);
        }
    }

    static bool SolveIntercept(Vector3 muzzle, Vector3 targetPos, Vector3 targetVel, float bulletSpeed, out Vector3 point, out float time)
    {
        Vector3 d = targetPos - muzzle;
        if (bulletSpeed <= 0f) { point = targetPos; time = 0f; return false; }
        float a = Vector3.Dot(targetVel, targetVel) - bulletSpeed * bulletSpeed;
        float b = 2f * Vector3.Dot(d, targetVel);
        float c = Vector3.Dot(d, d);

        float t;
        if (Mathf.Abs(a) < 0.0001f)
        {
            if (Mathf.Abs(b) < 0.0001f) { point = targetPos; time = 0f; return false; }
            t = -c / b;
        }
        else
        {
            float discriminant = b * b - 4f * a * c;
            if (discriminant < 0f) { point = targetPos; time = 0f; return false; }
            float sqrt = Mathf.Sqrt(discriminant);
            float t1 = (-b - sqrt) / (2f * a);
            float t2 = (-b + sqrt) / (2f * a);
            t = Mathf.Min(t1, t2) > 0f ? Mathf.Min(t1, t2) : Mathf.Max(t1, t2);
        }

        if (t <= 0f) { point = targetPos; time = 0f; return false; }
        point = targetPos + targetVel * t;
        time = t;
        return true;
    }

    //检查炮塔到预测命中点的直线是否被墙或别的坦克挡住
    bool IsShotPathClear(Vector3 from, Vector3 to, TankController expected)
    {
        Vector2 delta = to - from;
        float distance = delta.magnitude;
        if (distance < 0.01f) return true;

        //留出目标自身半径，避免把目标身后的墙误判成挡路
        float checkDistance = Mathf.Max(0.1f, distance - 0.6f);
        var hits = Physics2D.RaycastAll(from, delta / distance, checkDistance);
        if (hits == null || hits.Length == 0) return true;
        Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (var hit in hits)
        {
            if (hit.collider == null) continue;
            var hitTransform = hit.collider.transform;
            if (hitTransform == transform || hitTransform.IsChildOf(transform)) continue;
            if (hit.collider.CompareTag("Bullet")) continue;
            return hit.collider.GetComponentInParent<TankController>() == expected;
        }
        return true;
    }

    //子弹生成点最多在炮口前 1.2；离墙太近时子弹会贴在墙上，满弹反弹可能打死自己
    bool MuzzleSafe()
    {
        var hits = Physics2D.CircleCastAll(turret.position, 0.18f, turret.up, 1.8f);
        if (hits == null || hits.Length == 0) return true;
        Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (var hit in hits)
        {
            if (hit.collider == null) continue;
            var hitTransform = hit.collider.transform;
            if (hitTransform == transform || hitTransform.IsChildOf(transform)) continue;
            if (hit.collider.CompareTag("Bullet")) continue;
            if (hit.collider.GetComponentInParent<TankController>() != null) return true;
            //子弹最远生成在 1.2 处，再留出子弹半径和余量，避免贴墙反弹
            return hit.distance >= 1.45f;
        }
        return true;
    }

    bool CanMove(Vector3 direction)
    {
        direction.z = 0f;
        if (direction.sqrMagnitude < 0.0001f) return false;
        direction.Normalize();

        var hits = Physics2D.CircleCastAll(transform.position, TankRadius, direction, MoveProbe);
        if (hits == null) return true;
        foreach (var hit in hits)
        {
            if (hit.collider == null) continue;
            var hitTransform = hit.collider.transform;
            if (hitTransform == transform || hitTransform.IsChildOf(transform)) continue;
            if (hit.collider.CompareTag("Bullet")) continue;
            return false;
        }
        return true;
    }

    bool IsClearForTank(Vector3 from, Vector3 to)
    {
        Vector2 delta = to - from;
        float distance = delta.magnitude;
        if (distance < 0.01f) return true;

        var hits = Physics2D.CircleCastAll(from, PathClearance, delta / distance, distance);
        if (hits == null) return true;
        foreach (var hit in hits)
        {
            if (hit.collider == null) continue;
            var hitTransform = hit.collider.transform;
            if (hitTransform == transform || hitTransform.IsChildOf(transform)) continue;
            if (hit.collider.CompareTag("Bullet")) continue;
            return false;
        }
        return true;
    }

    void CheckStuck(float now)
    {
        if (now < stuckCheckTime) return;
        float moved = Vector2.Distance(transform.position, stuckCheckPosition);

        if (tryingToMove && moved < 0.2f && now >= unstuckUntil)
        {
            navigation.Clear();
            unstuckUntil = now + 0.45f;
            Vector3 random = UnityEngine.Random.insideUnitCircle.normalized;
            Vector3 candidate = new Vector3(random.x, random.y, 0f);
            if (!CanMove(candidate)) candidate = -candidate;
            dodgeDirection = candidate;
            dodgeUntil = now + 0.45f;
        }

        stuckCheckPosition = transform.position;
        stuckCheckTime = now + 0.5f;
    }

    void OnDrawGizmos()
    {
        if (!Application.isPlaying || navigation == null) return;

        Gizmos.color = Color.yellow;
        Vector3 previous = transform.position;
        var points = navigation.Points;
        for (int i = 0; i < points.Count; i++)
        {
            Gizmos.DrawLine(previous, points[i]);
            previous = points[i];
        }

        if (target != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(transform.position, target.transform.position);
        }
    }
}
