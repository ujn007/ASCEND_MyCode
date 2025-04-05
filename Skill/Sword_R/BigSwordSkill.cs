using DG.Tweening;
using FMODUnity;
using Game.Events;
using INab.Dissolve;
using PJH.Agent.Player;
using PJH.Core;
using PJH.Equipment;
using PJH.EquipmentSkillSystem;
using PJH.Manager;
using Sirenix.OdinInspector;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using YTH.Boss;

public class BigSwordSkill : EquipmentSkill
{
    [SerializeField] private GameEventChannelSO eventSO;
    private PoolManagerSO poolManager;

    [TabGroup("SwordInfo")]
    [SerializeField]
    private float mirrorSpawnRadius;

    [TabGroup("SwordInfo")]
    [SerializeField]
    private float mirrorSpawnTime;

    [TabGroup("SwordInfo")]
    [SerializeField]
    private float mirrorSpawnMoveDuration;

    [TabGroup("SwordInfo")]
    [SerializeField]
    private int mirrorSpawnCount;

    [TabGroup("SwordInfo")]
    [SerializeField]
    private int mirrorPlusIndex;

    [TabGroup("SwordInfo")]
    [SerializeField]
    private float dissolveTime;

    [TabGroup("PF")][SerializeField] private SwordSkillCollision swordPointParent;

    [TabGroup("Pool")][SerializeField] private PoolTypeSO bigSwordType, cloneType, lastBigSwordType;

    [TabGroup("Sound")]
    [SerializeField]
    private EventReference _spawnMirrorEventReference, _mirrorSlashEventReference, _spawnBigSwordEventReference;

    private int mirrorIndex;

    private int bossSpeed;
    private Vector3 spawnPos;

    private EnemySpawnManager enemySpawnManager => EnemySpawnManager.Instance;

    private List<Mirror> mirrorList = new();
    private List<Enemy> enemyList = new();
    private List<BossEnemy> bossList = new();

    public override void Init(Player player, Equipment equipment)
    {
        base.Init(player, equipment);
        poolManager = Managers.Addressable.Load<PoolManagerSO>("PoolManager");
    }

    public override void UseSkill(bool isHolding)
    {
        if (isHolding) return;
        base.UseSkill(isHolding);
        Vector3 mousePos = _player.PlayerInput.GetWorldMousePosition();

        SwordSkillCollision swordPar = Instantiate(swordPointParent);
        swordPar.Initialize(enemySpawnManager.EnemyList.Count, mousePos);
        spawnPos = swordPar.transform.position;
        //정해진 범위의 충돌 감지 레이어를 가진 SwordSkillCollision 스폰

        enemyList = enemySpawnManager.EnemyList.OfType<Enemy>().ToList();
        bossList = enemySpawnManager.EnemyList.OfType<BossEnemy>().ToList();
        //EnemyList의 타입이 Enemy,BossEnemy 의 부모 클래스라서 적과 보스적을 
        //가져오기 위해 Linq의 OfType(컬렉션에서 T 타입인 요소만 골라내는 필터 함수)를
        //사용하였습니다.

        bossList.ForEach(e => bossSpeed = e.BossStat.moveSpeed);

        SetEnemySlow(true);
        //스킬을 사용하면 시간이 느려지는데 이것은 TimScale 을 줄인것이 아니라
        //적들의 모든 속도(공속, 움직임, 애니메이션 속도)를 줄였습니다.

        _player.PlayerInput.EnablePlayerInput(false);

        SpawnMirror(swordPar.transform);
        StartCoroutine(Moveing(swordPar));
        //거울 소환, 
    }

    private async void SpawnMirror(Transform parent)
    {
        for (int i = 0; i < mirrorSpawnCount; i++)
        {
            float angle = i * Mathf.PI * 2f / mirrorSpawnCount;

            Vector3 position = new Vector3(
                Mathf.Cos(angle) * mirrorSpawnRadius + parent.position.x,
                parent.position.y,
                Mathf.Sin(angle) * mirrorSpawnRadius + parent.position.z
            );
            //삼각함수를 활용해 둥글게 거울 스폰

            Mirror mirror = poolManager.Pop(bigSwordType) as Mirror;
            RuntimeManager.PlayOneShot(_spawnMirrorEventReference, position);
            mirror.transform.DOMove(position, mirrorSpawnMoveDuration).ChangeStartValue(position + Vector3.up * 50)
                .SetEase(Ease.Linear);
            mirror.transform.LookAt(parent);
            mirror.transform.rotation = Quaternion.Euler(0, mirror.transform.rotation.eulerAngles.y, 0);
            //거울 스폰시 위에서 아래로 떨어지는 모션 Dotween으로 처리
            //각도 조절	

            mirrorList.Add(mirror);

            var evt = GameEvents.CameraImpulse;
            evt.strength = 0.2f;
            eventSO.RaiseEvent(evt);
            //카메라 떨림

            await Task.Delay((int)(mirrorSpawnTime * 1000));
        }
    }

    private IEnumerator Moveing(SwordSkillCollision par)
    {
        yield return YieldCache.WaitForSeconds(mirrorSpawnTime * mirrorSpawnCount + 1);

        PlayerDissolver(true);

        yield return YieldCache.WaitForSeconds(1);

        MoveTwoPos moveTwoPos = new MoveTwoPos();
        //시작지점과 끝지점을 가지고 있는 클래스
        var evt = GameEvents.CameraPerlin;
        evt.strength = 2;
        evt.increaseDur = 0.2f;
        eventSO.RaiseEvent(evt);
        //카메라 지속적인 떨림

        for (int i = 0; i < 70; i++)
        {
            SwordPlayerClone clone = poolManager.Pop(cloneType) as SwordPlayerClone;
            //클론 풀링
            GetNextMirror(moveTwoPos);
            //클론이 가야할 다음 좌표 구하기
            clone.MoveToWhere(moveTwoPos);
            //클론 움직임
            RuntimeManager.PlayOneShot(_mirrorSlashEventReference, moveTwoPos.endPos);
            //사운드

            if (i % 6 == 0)
                par.DetectEnemy();
            //많은 데미지를 주는것을 방지하기 위해 6번마다 데미지 주기

            yield return YieldCache.WaitForSeconds(0.05f);
        }

        evt.strength = 0;
        eventSO.RaiseEvent(evt);
        Destroy(par.gameObject);
        //카메라 떨림 스탑

        SpawnLastBigSword();
    }

    private void GetNextMirror(MoveTwoPos moveTwoPos)
    {
        //MoveTwoPos는 Class라 참조형식 이기 때문에 여기서 시작지점, 끝지점 저장
        Mirror mirrorStart = mirrorList[mirrorIndex];

        mirrorIndex += mirrorPlusIndex;
        if (mirrorIndex >= mirrorSpawnCount) mirrorIndex -= mirrorSpawnCount;

        Mirror mirrorNext = mirrorList[mirrorIndex];
        //스폰된 거울들을 가지고 있는 List안에서 시작지점 거울과 끝지점 거울 구하기

        moveTwoPos.startPos = mirrorStart.transform.position;
        moveTwoPos.endPos = mirrorNext.transform.position;
        //저장
    }

    private void SpawnLastBigSword()
    {
        PlayerDissolver(false);
        _player.PlayerInput.EnablePlayerInput(true);
        _player.IsUsingSkill = false;
        //플레이어 디졸브 풀기
        LastBigSword bigSword = poolManager.Pop(lastBigSwordType) as LastBigSword;
        Sequence sq = DOTween.Sequence();
        RuntimeManager.PlayOneShot(_spawnBigSwordEventReference, spawnPos);
        //거대한 검 스폰

        sq.Append(bigSword.transform.DOMove(spawnPos, 0.2f).ChangeStartValue(spawnPos + Vector3.up * 30).SetEase(Ease.Linear)
            .OnComplete(() =>
            {
                mirrorList.ForEach(s => s.Bomb());

                var evt = GameEvents.CameraImpulse;
                evt.strength = 4;
                eventSO.RaiseEvent(evt);

                SetEnemySlow(false);

                bigSword.DectectEnemy();

            }));
        sq.AppendInterval(3);
        sq.AppendCallback(() =>
        {
            Dissolve();
            ResetLastBigSword(bigSword);
        });
    }

    private void Dissolve()
    {
        mirrorList.ForEach(s => { s.Dissolve(); });

        mirrorList.Clear();
    }

    private async void ResetLastBigSword(LastBigSword sword)
    {
        if (sword.TryGetComponent(out Dissolver dissolver))
            dissolver.Dissolve();
        await Task.Delay(2000);
        poolManager.Push(sword);
    }

    private void PlayerDissolver(bool v)
    {
        Dissolver playerDissolve = _player.transform.Find("Dissolve").GetComponent<Dissolver>();
        Dissolver weaponDissolve = _player.GetCompo<PlayerEquipmentController>().GetWeapon().DissolverCompo;

        if (v)
        {
            playerDissolve.Dissolve();
            weaponDissolve?.Dissolve();
        }
        else
        {
            playerDissolve.Materialize();
            weaponDissolve?.Materialize();
        }
    }

    private void SetEnemySlow(bool v)
    {
        if (v)
        {
            enemyList.ForEach(e => e.SetSlow(true, 0.2f));
            foreach (BossEnemy boss in bossList)
            {
                boss.AnimatorCompo.speed = 0.2f;
                boss.BossStat.moveSpeed = 1;
            }
        }
        else
        {
            enemyList.ForEach(e => e.SetSlow(false));
            foreach (BossEnemy boss in bossList)
            {
                boss.AnimatorCompo.speed = 1;
                boss.BossStat.moveSpeed = bossSpeed;
            }
        }
    }
}