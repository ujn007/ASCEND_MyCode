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
        //������ ������ �浹 ���� ���̾ ���� SwordSkillCollision ����

        enemyList = enemySpawnManager.EnemyList.OfType<Enemy>().ToList();
        bossList = enemySpawnManager.EnemyList.OfType<BossEnemy>().ToList();
        //EnemyList�� Ÿ���� Enemy,BossEnemy �� �θ� Ŭ������ ���� �������� 
        //�������� ���� Linq�� OfType(�÷��ǿ��� T Ÿ���� ��Ҹ� ��󳻴� ���� �Լ�)��
        //����Ͽ����ϴ�.

        bossList.ForEach(e => bossSpeed = e.BossStat.moveSpeed);

        SetEnemySlow(true);
        //��ų�� ����ϸ� �ð��� �������µ� �̰��� TimScale �� ���ΰ��� �ƴ϶�
        //������ ��� �ӵ�(����, ������, �ִϸ��̼� �ӵ�)�� �ٿ����ϴ�.

        _player.PlayerInput.EnablePlayerInput(false);

        SpawnMirror(swordPar.transform);
        StartCoroutine(Moveing(swordPar));
        //�ſ� ��ȯ, 
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
            //�ﰢ�Լ��� Ȱ���� �ձ۰� �ſ� ����

            Mirror mirror = poolManager.Pop(bigSwordType) as Mirror;
            RuntimeManager.PlayOneShot(_spawnMirrorEventReference, position);
            mirror.transform.DOMove(position, mirrorSpawnMoveDuration).ChangeStartValue(position + Vector3.up * 50)
                .SetEase(Ease.Linear);
            mirror.transform.LookAt(parent);
            mirror.transform.rotation = Quaternion.Euler(0, mirror.transform.rotation.eulerAngles.y, 0);
            //�ſ� ������ ������ �Ʒ��� �������� ��� Dotween���� ó��
            //���� ����	

            mirrorList.Add(mirror);

            var evt = GameEvents.CameraImpulse;
            evt.strength = 0.2f;
            eventSO.RaiseEvent(evt);
            //ī�޶� ����

            await Task.Delay((int)(mirrorSpawnTime * 1000));
        }
    }

    private IEnumerator Moveing(SwordSkillCollision par)
    {
        yield return YieldCache.WaitForSeconds(mirrorSpawnTime * mirrorSpawnCount + 1);

        PlayerDissolver(true);

        yield return YieldCache.WaitForSeconds(1);

        MoveTwoPos moveTwoPos = new MoveTwoPos();
        //���������� �������� ������ �ִ� Ŭ����
        var evt = GameEvents.CameraPerlin;
        evt.strength = 2;
        evt.increaseDur = 0.2f;
        eventSO.RaiseEvent(evt);
        //ī�޶� �������� ����

        for (int i = 0; i < 70; i++)
        {
            SwordPlayerClone clone = poolManager.Pop(cloneType) as SwordPlayerClone;
            //Ŭ�� Ǯ��
            GetNextMirror(moveTwoPos);
            //Ŭ���� ������ ���� ��ǥ ���ϱ�
            clone.MoveToWhere(moveTwoPos);
            //Ŭ�� ������
            RuntimeManager.PlayOneShot(_mirrorSlashEventReference, moveTwoPos.endPos);
            //����

            if (i % 6 == 0)
                par.DetectEnemy();
            //���� �������� �ִ°��� �����ϱ� ���� 6������ ������ �ֱ�

            yield return YieldCache.WaitForSeconds(0.05f);
        }

        evt.strength = 0;
        eventSO.RaiseEvent(evt);
        Destroy(par.gameObject);
        //ī�޶� ���� ��ž

        SpawnLastBigSword();
    }

    private void GetNextMirror(MoveTwoPos moveTwoPos)
    {
        //MoveTwoPos�� Class�� �������� �̱� ������ ���⼭ ��������, ������ ����
        Mirror mirrorStart = mirrorList[mirrorIndex];

        mirrorIndex += mirrorPlusIndex;
        if (mirrorIndex >= mirrorSpawnCount) mirrorIndex -= mirrorSpawnCount;

        Mirror mirrorNext = mirrorList[mirrorIndex];
        //������ �ſ���� ������ �ִ� List�ȿ��� �������� �ſ�� ������ �ſ� ���ϱ�

        moveTwoPos.startPos = mirrorStart.transform.position;
        moveTwoPos.endPos = mirrorNext.transform.position;
        //����
    }

    private void SpawnLastBigSword()
    {
        PlayerDissolver(false);
        _player.PlayerInput.EnablePlayerInput(true);
        _player.IsUsingSkill = false;
        //�÷��̾� ������ Ǯ��
        LastBigSword bigSword = poolManager.Pop(lastBigSwordType) as LastBigSword;
        Sequence sq = DOTween.Sequence();
        RuntimeManager.PlayOneShot(_spawnBigSwordEventReference, spawnPos);
        //�Ŵ��� �� ����

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