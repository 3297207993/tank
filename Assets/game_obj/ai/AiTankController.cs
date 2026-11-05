using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

//服务器端的电脑玩家：感知、决策、导航、瞄准全部在服务器上跑。
//规则内尽可能强：瞬时反应、零瞄准误差、直线视线锁射；仍然受墙、视线、转向速度、弹药和子弹飞行时间限制。
public class AiTankController : MonoBehaviour
{
    enum AiState
    {
        Patrol,
        Hunt,
        Engage,
    }

    const float PerceptionInterval = 0.08f;
    const float PathInterval = 0.35f;
    const float DodgeCheckInterval = 0.06f;
    const float DodgeHorizon = 1.15f;
    const float DodgeStep = 0.07f;
    const float PreferredMinRange = 3.5f;
    const float PreferredMaxRange = 8.5f;
    const float FireCooldown = 0.18f;
    const float AimTolerance = 0.7f;
    const float MaxInterceptTime = 2.6f;
    const float MemoryDuration = 4f;
    const float TankRadius = 0.75f;
    const float PathClearance = 0.95f;
    const float MoveProbe = 0.7f;
    const float BulletSpawnOffset = 1.2f;

    MapGenerator mapGenerator;
    MapDataContainer map;
    TankController tank;
    Transform turret;
    AiNavigation navigation;

    AiState state = AiState.Patrol;
    TankController target;
    Vector3 targetPosition;
    Vector3 targetVelocity;
    Vector3 lastSeenPosition;
    float lastSeenTime = -999f;
    float lastTargetSampleTime;

    float nextPerceptionTime;
    float nextPathTime;
    float nextFireTime;
    float nextDodgeCheckTime;
    float nextStrafeSwitch;
    int strafeSign = 1;

    Vector3 pathGoal;
    bool hasPathGoal;

    Vector2Int patrolCell;
    bool hasPatrolGoal;

    bool hasInvestigatePoint;
    Vector3 investigatePoint;

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

    void UpdateTarget(float now)
    {
        if (now < nextPerceptionTime) return;
        nextPerceptionTime = now + PerceptionInterval;

        TankController best = null;
        float bestDistance = float.MaxValue;
        foreach (var obj in GameObject.FindGameObjectsWithTag("Tank"))
        {
            var candidate = obj.GetComponent<TankController>();
            if (candidate == null || candidate == tank) continue;
            var candidateObject = obj.GetComponent<NetworkObject>();
            if (candidateObject != null && !candidateObject.IsSpawned) continue;
            float distance = Vector2.Distance(transform.position, candidate.transform.position);
            if (distance >= bestDistance) continue;
            if (!HasLineOfSight(candidate)) continue;
            best = candidate;
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
                //限制异常值，避免刚重置或瞬移时算错提前量
                if (instantVelocity.sqrMagnitude > 36f) instantVelocity = instantVelocity.normalized * 6f;
                targetVelocity = Vector3.Lerp(targetVelocity, instantVelocity, 0.65f);
                lastTargetSampleTime = now;
            }
            targetPosition = best.transform.position;
            lastSeenPosition = best.transform.position;
            lastSeenTime = now;
            state = AiState.Engage;
            hasInvestigatePoint = false;
        }
        else
        {
            target = null;
            targetVelocity = Vector3.zero;
            state = now - lastSeenTime < MemoryDuration ? AiState.Hunt : AiState.Patrol;
        }
    }

    bool HasLineOfSight(TankController candidate)
    {
        Vector2 origin = transform.position;
        Vector2 delta = (Vector2)candidate.transform.position - origin;
        float distance = delta.magnitude;
        if (distance <= 0.01f) return true;

        var hits = Physics2D.RaycastAll(origin, delta / distance, distance);
        if (hits == null || hits.Length == 0) return true;
        Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (var hit in hits)
        {
            if (hit.collider == null) continue;
            var hitTransform = hit.collider.transform;
            if (hitTransform == transform || hitTransform.IsChildOf(transform)) continue;
            if (hit.collider.CompareTag("Bullet")) continue;
            return hit.collider.GetComponentInParent<TankController>() == candidate;
        }
        return true;
    }

    //短时模拟场上子弹，包括墙壁反弹，判断是否会在近期打到自己
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

            //看不见人时，按子弹飞来的方向去调查枪声来源，这是玩家能看到的信息
            var bulletController = bulletObj.GetComponent<CommonBulletController>();
            if (!hasInvestigatePoint && target == null && bulletController != null
                && bulletController.shooterClientId != tank.playerId
                && now - lastSeenTime > MemoryDuration
                && Vector2.Distance(pos, me) < 20f)
            {
                investigatePoint = pos - vel.normalized * 6f;
                hasInvestigatePoint = true;
            }

            SimulateBullet(bulletObj, pos, vel, speed, me,
                ref bestImpact, ref bestDirection, ref bestBulletPos);
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

    void SimulateBullet(GameObject bulletObj, Vector2 pos, Vector2 vel, float speed, Vector2 me,
        ref float bestImpact, ref Vector2 bestDirection, ref Vector2 bestBulletPos)
    {
        Vector2 dir = vel / speed;
        float elapsed = 0f;
        int bounces = 0;

        while (elapsed < DodgeHorizon && bounces < 5)
        {
            float segment = speed * DodgeStep;
            Vector2 next = pos + dir * segment;

            var hits = Physics2D.CircleCastAll(pos, 0.16f, dir, segment);
            RaycastHit2D? nearest = null;
            if (hits != null)
            {
                foreach (var hit in hits)
                {
                    if (hit.collider == null) continue;
                    var hitTransform = hit.collider.transform;
                    if (hitTransform == bulletObj.transform || hitTransform.IsChildOf(bulletObj.transform)) continue;
                    if (hit.collider.CompareTag("Bullet")) continue;
                    if (nearest == null || hit.distance < nearest.Value.distance) nearest = hit;
                }
            }

            if (nearest.HasValue)
            {
                var hit = nearest.Value;
                var hitTank = hit.collider.GetComponentInParent<TankController>();
                if (hitTank == tank)
                {
                    RegisterThreat(elapsed, dir, pos, ref bestImpact, ref bestDirection, ref bestBulletPos);
                    return;
                }
                if (hitTank != null)
                {
                    //被别的坦克挡住，子弹会消失
                    return;
                }
                next = hit.point + hit.normal * 0.02f;
                dir = Vector2.Reflect(dir, hit.normal);
                bounces++;
            }

            //已经飞过自己的子弹不算威胁，否则会在身边来回抽搐
            if (Vector2.Dot(me - pos, dir) > 0f && DistancePointToSegment(me, pos, next) < 0.9f)
            {
                RegisterThreat(elapsed, dir, pos, ref bestImpact, ref bestDirection, ref bestBulletPos);
                return;
            }

            pos = next;
            elapsed += DodgeStep;
        }
    }

    static void RegisterThreat(float elapsed, Vector2 dir, Vector2 bulletPos,
        ref float bestImpact, ref Vector2 bestDirection, ref Vector2 bestBulletPos)
    {
        if (elapsed >= bestImpact) return;
        bestImpact = elapsed;
        bestDirection = dir;
        bestBulletPos = bulletPos;
    }

    static float DistancePointToSegment(Vector2 point, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float lengthSq = ab.sqrMagnitude;
        if (lengthSq < 0.0001f) return Vector2.Distance(point, a);
        float t = Mathf.Clamp01(Vector2.Dot(point - a, ab) / lengthSq);
        return Vector2.Distance(point, a + ab * t);
    }

    bool DecideMovement(float now, out Vector3 direction)
    {
        direction = Vector3.zero;

        //没子弹时以脱离为主，等 30 秒的补给；即使看不见人也按最后目击位置保持距离
        if (tank.Ammo <= 0)
        {
            Vector3 threat = transform.position;
            if (target != null) threat = target.transform.position;
            else if (now - lastSeenTime < 30f) threat = lastSeenPosition;

            Vector3 away = transform.position - threat;
            away.z = 0f;
            if (away.sqrMagnitude > 0.01f)
            {
                away.Normalize();
                if (CanMove(away)) { direction = away; return true; }
            }
        }

        if (target != null)
        {
            Vector3 toTarget = target.transform.position - transform.position;
            float distance = toTarget.magnitude;
            if (distance < 0.01f) return false;
            Vector3 dirTo = toTarget / distance;

            if (distance > PreferredMaxRange)
            {
                if (CanMove(dirTo)) { direction = dirTo; return true; }
                if (FollowPath(now, target.transform.position, out direction)) return true;
            }
            else if (distance < PreferredMinRange)
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
            return FollowPath(now, target.transform.position, out direction);
        }

        //追最后看到的位置 / 调查枪声
        Vector3 goal = Vector3.zero;
        bool hasGoal = false;
        if (now - lastSeenTime < MemoryDuration)
        {
            goal = lastSeenPosition;
            hasGoal = true;
        }
        else if (hasInvestigatePoint)
        {
            goal = investigatePoint;
            hasGoal = true;
        }

        if (hasGoal)
        {
            if (Vector2.Distance(transform.position, goal) > 1.0f)
            {
                if (FollowPath(now, goal, out direction)) return true;
            }
            else
            {
                if (now - lastSeenTime >= MemoryDuration) hasInvestigatePoint = false;
                lastSeenTime = -999f;
            }
        }

        //巡逻
        if (!hasPatrolGoal)
        {
            PickPatrolGoal();
        }
        if (hasPatrolGoal && FollowPath(now, mapGenerator.CellCenter(patrolCell), out direction)) return true;

        PickPatrolGoal();
        return hasPatrolGoal && FollowPath(now, mapGenerator.CellCenter(patrolCell), out direction);
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

    void PickPatrolGoal()
    {
        hasPatrolGoal = false;
        var currentMap = mapGenerator.CurrentMap;
        if (currentMap == null) return;

        Vector2Int here = mapGenerator.WorldToCell(transform.position);
        Vector2Int farthest = here;
        int farthestDistance = -1;
        for (int i = 0; i < 24; i++)
        {
            Vector2Int cell = new Vector2Int(
                UnityEngine.Random.Range(0, currentMap.xCount),
                UnityEngine.Random.Range(0, currentMap.yCount));
            if (cell == here) continue;
            int distance = (cell - here).sqrMagnitude;
            if (distance <= farthestDistance) continue;
            farthest = cell;
            farthestDistance = distance;
        }
        if (farthestDistance >= 0)
        {
            patrolCell = farthest;
            hasPatrolGoal = true;
            return;
        }

        patrolCell = new Vector2Int((here.x + 1) % currentMap.xCount, here.y);
        hasPatrolGoal = true;
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

    void Aim(float now)
    {
        if (target != null)
        {
            Vector3 muzzle = turret.position;
            //子弹实际生成在炮口前 1.2，提前量必须从生成点算，否则目标横移会打偏
            Vector3 spawn = muzzle + turret.up * BulletSpawnOffset;
            Vector3 predicted;
            float travelTime;
            if (SolveIntercept(spawn, target.transform.position, targetVelocity, out predicted, out travelTime)
                && travelTime <= MaxInterceptTime)
            {
                Vector3 aimDirection = predicted - muzzle;
                tank.AimTurretTowards(aimDirection);

                if (now >= nextFireTime && tank.Ammo > 0
                    && Vector3.Angle(turret.up, aimDirection) <= AimTolerance
                    && IsShotPathClear(muzzle, predicted)
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

        if (now - lastSeenTime < MemoryDuration)
        {
            tank.AimTurretTowards(lastSeenPosition - turret.position);
            return;
        }

        if (wantsToMove)
        {
            tank.AimTurretTowards(transform.up);
        }
    }

    static bool SolveIntercept(Vector3 muzzle, Vector3 targetPos, Vector3 targetVel, out Vector3 point, out float time)
    {
        Vector3 d = targetPos - muzzle;
        float bulletSpeed = CommonBulletController.bulletSpeed;
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
    bool IsShotPathClear(Vector3 from, Vector3 to)
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
            return hit.collider.GetComponentInParent<TankController>() == target;
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
            hasPatrolGoal = false;
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

        Gizmos.color = state == AiState.Engage ? Color.red : Color.yellow;
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
