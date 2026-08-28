using System;
using UnityEngine;

namespace Soup.Employees
{
    /// <summary>
    /// Shared "which employee type am I assigning?" selection for play HUD and station +/-.
    /// </summary>
    public static class EmployeeAssignSelection
    {
        private static string _typeId = EmployeeManager.ElfId;

        public static string SelectedTypeId => _typeId;

        public static event Action Changed;

        public static EmployeeItem Current
        {
            get
            {
                var em = EmployeeManager.Instance;
                if (em == null) return null;

                var selected = em.GetById(_typeId);
                if (selected != null && selected.CanPlayerAssign)
                    return selected;

                // 当前 _typeId 指向不可分配类型（如蘑菇人）时回退精灵，
                // 并同步写回 _typeId，保证 SelectedTypeId 与 Current 一致，
                // 避免 UI 高亮与实际分配对象错位。
                if (em.ElfType != null)
                    _typeId = em.ElfType.Id;
                return em.ElfType;
            }
        }

        public static void Select(string typeId)
        {
            string next = string.IsNullOrEmpty(typeId) ? EmployeeManager.ElfId : typeId;
            if (_typeId == next) return;
            _typeId = next;
            Changed?.Invoke();
        }

        public static void Select(EmployeeItem type)
        {
            if (type == null || !type.CanPlayerAssign) return;
            Select(type.Id);
        }

        public static void EnsureValid()
        {
            var current = Current;
            if (current != null)
                _typeId = current.Id;
            else
                _typeId = EmployeeManager.ElfId;
        }
    }
}
