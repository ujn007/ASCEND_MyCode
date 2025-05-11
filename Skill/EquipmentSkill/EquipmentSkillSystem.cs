using System;
using System.Collections.Generic;
using Game.Events;
using PJH.Agent.Player;
using PJH.EquipmentSkillSystem;
using UnityEngine;

namespace PJH.Equipment
{
    public class EquipmentSkillSystem : MonoBehaviour
    {
        public event Action GetSkillEvent;
        private Dictionary<string, EquipmentSkill> _skills;
        [SerializeField] private List<string> _skillTypeNames = new();
        [SerializeField] private GameEventChannelSO _gameEventChannel;
        private Agent.Player.Player _player;

        private Equipment _equipment;

        public void Init(Agent.Player.Player player, Equipment equipment)
        {
            _skills = new();

            _player = player;
            EquipmentSkill[] equipmentSkills = transform.Find("Skills").GetComponentsInChildren<EquipmentSkill>();
            _equipment = equipment;
            foreach (var skill in equipmentSkills)
            {
                _skills.Add(skill.GetType().Name, skill);
                skill.Init(player, equipment);
            }

            _gameEventChannel.AddListener<GetStoreSkill>(HandleGetSkill);
        }

        public void Equip()
        {
            _player.PlayerInput.Skill1Event += HandleSkill1Event;
            _player.PlayerInput.Skill2Event += HandleSkill2Event;
            _player.PlayerInput.Skill3Event += HandleSkill3Event;
        }

        public void UnEquip()
        {
            _player.PlayerInput.Skill1Event -= HandleSkill1Event;
            _player.PlayerInput.Skill2Event -= HandleSkill2Event;
            _player.PlayerInput.Skill3Event -= HandleSkill3Event;
            GetSkillEvent = null;
        }

        private void OnDestroy()
        {
            if (_player == null) return;
            GetSkillEvent = null;
            _gameEventChannel.RemoveListener<GetStoreSkill>(HandleGetSkill);

            _player.PlayerInput.Skill1Event -= HandleSkill1Event;
            _player.PlayerInput.Skill2Event -= HandleSkill2Event;
            _player.PlayerInput.Skill3Event -= HandleSkill3Event;
        }

        private void HandleGetSkill(GetStoreSkill evt)
        {
            var skill = GetSkill(evt.skillTypeName);
            if (skill == default) return;
            GetSkillEvent?.Invoke();
            _skillTypeNames[evt.keyIdx] = evt.skillTypeName;
            skill.skillEnabled = true;
        }

        private void HandleSkill3Event(bool isHolding)
        {
            var typeName = _skillTypeNames[2];
            UseSkill(typeName, isHolding);
        }

        private void HandleSkill2Event(bool isHolding)
        {
            var typeName = _skillTypeNames[1];
            UseSkill(typeName, isHolding);
        }

        private void HandleSkill1Event(bool isHolding)
        {
            var typeName = _skillTypeNames[0];
            UseSkill(typeName, isHolding);
        }

        private void UseSkill(string typeName, bool isHolding)
        {
            if (_player.IsHitting || _player.CheckUsingSkill(typeName)) return;
            if (_player.GetCompo<PlayerAttack>().IsAttacking) return;
            if (_player.GetCompo<PlayerMovement>().IsEvasion) return;
            var skill = GetSkill(typeName);
            if (skill == null)
            {
                Debug.LogError($"{typeName}의 스킬이 없습니다.");
                return;
            }

            skill.AttemptUseSkill(isHolding);
        }

        public T GetSkill<T>() where T : EquipmentSkill
        {
            Type t = typeof(T);
            if (_skills.TryGetValue(t.Name, out EquipmentSkill target))
            {
                return target as T;
            }

            return null;
        }

        public EquipmentSkill GetSkill(int idx)
        {
            var typeName = _skillTypeNames[idx];
            var skill = GetSkill(typeName);
            if (skill == null)
                Debug.LogError($"{typeName}의 스킬이 없습니다.");
            return skill;
        }

        public EquipmentSkill GetSkill(string type)
        {
            return _skills.GetValueOrDefault(type);
        }

        public bool SkillUpgrade<T>(ref int point) where T : EquipmentSkill
        {
            if (point <= 0) return false;
            var skill = GetSkill<T>();
            if (skill == null) return false;
            if (skill.TryUpgradeSkill())
            {
                point--;

                return true;
            }

            return false;
        }
    }
}