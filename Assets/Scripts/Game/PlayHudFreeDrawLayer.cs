using UnityEngine;

namespace Soup.Game
{
    /// <summary>
    /// 玩法场景（采集 / 处理 / 烹饪）里给你自由摆 UI 的层。
    /// 把 Image、Text、按钮拖到这个物体下即可；运行时逻辑不会改它的子物体位置或销毁它。
    /// 不要铺满全屏并勾选 Raycast Target，否则会挡住岗位点击。
    /// </summary>
    public sealed class PlayHudFreeDrawLayer : MonoBehaviour
    {
    }
}
