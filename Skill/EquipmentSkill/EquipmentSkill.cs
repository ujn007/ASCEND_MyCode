using System;
using System.Collections.Generic;
using PJH.Core;
using Sirenix.OdinInspector;
using UnityEngine;

namespace PJH.EquipmentSkillSystem
{
    public delegate void CooldownInfoEvent(float current, float total);

    public abstract class EquipmentSkill : MonoBehaviour
    {
        [BoxGroup("BaseSkillInfo")] public bool skillEnabled = false;

        [BoxGroup("BaseSkillInfo")] [SerializeField]
        protected float _cooldown;

        [BoxGroup("BaseSkillInfo")] [SerializeField]
        protected int _maxCheckEnemy = 5;

        [BoxGroup("BaseSkillInfo")] [SerializeField]
        protected int _maxSkillUpgradeCount = 5;

        protected int _currentSkillUpgradeCount = 0;
        protected float _cooldownTimer;
        protected Agent.Player.Player _player;
        protected Collider[] _colliders;
        protected Equipment.Equipment _equipment;
        public bool IsCooldown => _cooldownTimer > 0f;
        public CooldownInfoEvent OnCooldownEvent;
        public event Action StartCooldownEvent;
        public event Action EndCooldownEvent;

        public virtual void Init(Agent.Player.Player player, Equipment.Equipment equipment)
        {
            _equipment = equipment;
            _player = player;
            _colliders = new Collider[_maxCheckEnemy];
        }

        protected virtual void Update()
        {
            if (_cooldownTimer > 0)
            {
                _cooldownTimer -= Time.deltaTime;

                if (_cooldownTimer <= 0)
                {
                    EndCooldownEvent?.Invoke();

                    _cooldownTimer = 0;
                }

                OnCooldownEvent?.Invoke(_cooldownTimer, _cooldown);
            }
        }

        public virtual bool AttemptUseSkill(bool isHolding = false)
        {
            if (_cooldownTimer <= 0 && skillEnabled)
            {
                if (!isHolding)
                {
                    StartCooldownEvent?.Invoke();
                    _cooldownTimer = _cooldown;
                }

                UseSkill(isHolding);
                return true;
            }

            Debug.Log("Skill cooldown or locked");          
            return false;
        }

        public virtual void UseSkill(bool isHolding)
        {
            _player.IsUsingSkill = true;
            _player.CurrentUsingSkillTypeName = GetType().Name;
        }


        public virtual Enemy FindClosestEnemy(Transform checkTransform, float radius)
        {
            Enemy closestEnemy = null;
            int cnt = Physics.OverlapSphereNonAlloc(checkTransform.position, radius,
                _colliders, Define.MLayerMask.WhatIsEnemy);

            float closestDistance = Mathf.Infinity;

            for (int i = 0; i < cnt; ++i)
            {
                if (_colliders[i].TryGetComponent(out Enemy enemy))
                {
                    // if (enemy.isDead) continue;
                    float distanceToEnemy = Vector2.Distance(checkTransform.position, enemy.transform.position);
                    if (distanceToEnemy < closestDistance)
                    {
                        closestDistance = distanceToEnemy;
                        closestEnemy = enemy;
                    }
                }
            }

            return closestEnemy;
        }

        public List<Enemy> FindEnemiesInRange(Transform checkTransform, float radius)
        {
            int cnt = Physics.OverlapSphereNonAlloc(checkTransform.position, radius,
                _colliders, Define.MLayerMask.WhatIsEnemy);
            List<Enemy> list = new List<Enemy>();

            for (int i = 0; i < cnt; ++i)
            {
                if (_colliders[i].TryGetComponent(out Enemy enemy))
                {
                    if (!enemy.HealthCompo.IsDead)
                        list.Add(enemy);
                }
            }

            return list;
        }

        public virtual bool TryUpgradeSkill()
        {
            if (_currentSkillUpgradeCount < _maxSkillUpgradeCount)
            {
                _currentSkillUpgradeCount++;
                UpgradeSkill();
                return true;
            }

            return false;
        }

        protected virtual void UpgradeSkill()
        {
        }
    }
}