using System;
using UnityEngine;

/// <summary>
/// 각 씬에서 탭의 활성화와 비활성화를 직접 담당하는 컨트롤러 베이스 클래스.
/// 씬별 탭 루트 Transform을 보유하며, DomainManager의 요청을 받아 실제 탭 전환을 관리합니다.
/// </summary>
public abstract class TabController : MonoBehaviour
{
    /// <summary> 컨트롤러 식별 이름 (씬 이름과 대응) </summary>
    public abstract string ControllerName { get; }

    /// <summary> 이 씬에서 탭이 위치하는 부모 Transform </summary>
    [SerializeField]
    protected Transform _tabRoot;

    /// <summary>
    /// 활성화 시 DomainManager에 등록
    /// </summary>
    protected virtual void OnEnable()
    {
        if (DomainManager.Instance != null)
        {
            DomainManager.Instance.Register(this);
        }
    }

    /// <summary>
    /// 비활성화 시 DomainManager에서 해제
    /// </summary>
    protected virtual void OnDisable()
    {
        if (DomainManager.Instance != null)
        {
            DomainManager.Instance.Unregister(this);
        }
    }

    /// <summary>
    /// 탭 루트 하위에서 해당 이름의 TabBase를 찾아 활성화합니다.
    /// </summary>
    /// <param name="tabName">활성화할 탭 이름 (게임오브젝트 이름과 대응)</param>
    /// <param name="onActivated">활성화 완료 시 TabBase 인스턴스를 전달하는 콜백</param>
    public void ActivateTab(string tabName, Action<TabBase> onActivated)
    {
        TabBase target = FindTab(tabName);

        if (target == null)
        {
            Debug.LogError($"[{ControllerName}TabController] 탭을 찾을 수 없습니다: {tabName}");
            return;
        }

        target.Activate();
        if (onActivated != null)
        {
            onActivated.Invoke(target);
        }
    }

    /// <summary>
    /// 탭을 비활성화합니다.
    /// </summary>
    /// <param name="tab">비활성화할 탭</param>
    /// <param name="onComplete">비활성화 완료 후 호출되는 콜백</param>
    public void DeactivateTab(TabBase tab, Action onComplete = null)
    {
        if (tab == null || tab.gameObject == null)
        {
            if (onComplete != null)
            {
                onComplete.Invoke();
            }
            return;
        }

        tab.Deactivate(onComplete);
    }

    /// <summary>
    /// 탭 루트 하위에서 이름으로 TabBase를 검색합니다.
    /// 비활성화된 게임오브젝트도 포함하여 검색합니다.
    /// </summary>
    /// <param name="tabName">찾을 탭 이름</param>
    /// <returns>해당 TabBase, 없으면 null</returns>
    private TabBase FindTab(string tabName)
    {
        TabBase[] tabs = _tabRoot.GetComponentsInChildren<TabBase>(true);

        for (int i = 0; i < tabs.Length; i++)
        {
            if (tabs[i].DomainName == tabName)
            {
                return tabs[i];
            }
        }

        return null;
    }
}
