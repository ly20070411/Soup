using Soup.Jobs;
using UnityEngine;

namespace Soup.Game
{
    /// <summary>采集/处理区空位：进阶巡视时可点击以解锁新岗位。</summary>
    public sealed class EmptyStationSlot : MonoBehaviour
    {
        [SerializeField] private JobType jobType;
        [SerializeField] private int slotIndex;

        public JobType JobType => jobType;
        public int SlotIndex => slotIndex;

        public void Configure(JobType type, int index)
        {
            jobType = type;
            slotIndex = index;
        }
    }
}
