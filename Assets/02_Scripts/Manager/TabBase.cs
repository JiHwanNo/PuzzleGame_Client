using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// 모든 탭 UI에 부착하는 베이스 클래스.
/// 탭의 활성/비활성 생명주기와 연출(애니메이션)을 담당합니다.
/// </summary>
public class TabBase : MonoBehaviour, IDomainNode
{
    /// <summary> 탭 식별 이름 (도메인 경로 추적에 사용) </summary>
    public string TabName { get; private set; }

    /// <inheritdoc/>
    public string DomainName => TabName;

    /// <inheritdoc/>
    public DomainType DomainType => DomainType.Tab;

    /// <summary> 탭 활성 완료 시 발생하는 이벤트 </summary>
    public event Action<TabBase> OnActivated;

    /// <summary> 탭 비활성 완료 시 발생하는 이벤트 </summary>
    public event Action<TabBase> OnDeactivated;

    /// <summary> 현재 연출 진행 중 여부 </summary>
    public bool IsAnimating { get; private set; }

    /// <summary>
    /// 오브젝트 파괴 시 이벤트 구독자를 정리합니다.
    /// </summary>
    private void OnDestroy()
    {
        StopAllCoroutines();
        OnActivated = null;
        OnDeactivated = null;
    }

    /// <summary>
    /// 초기화 시 탭 이름이 설정되지 않았으면 게임오브젝트 이름으로 자동 설정합니다.
    /// </summary>
    private void Awake()
    {
        if (string.IsNullOrEmpty(TabName))
        {
            TabName = gameObject.name;
        }
    }

    /// <summary>
    /// 탭 이름을 설정합니다. TabController에서 생성 시 호출됩니다.
    /// </summary>
    /// <param name="tabName">탭 식별 이름</param>
    public void SetTabName(string tabName)
    {
        TabName = tabName;
    }

    /// <summary>
    /// 탭 활성화 처리를 시작합니다. DomainManager에서 호출됩니다.
    /// 활성 연출이 끝난 후 OnActivated 이벤트가 발생합니다.
    /// </summary>
    public void Activate()
    {
        gameObject.SetActive(true);
        OnActivateStart();
        StartCoroutine(ActivateRoutine());
    }

    /// <summary>
    /// 탭 비활성화 처리를 시작합니다. DomainManager에서 호출됩니다.
    /// 비활성 연출이 끝난 후 OnDeactivated 이벤트가 발생하고 게임오브젝트가 비활성화됩니다.
    /// </summary>
    /// <param name="onComplete">비활성 연출 완료 후 호출되는 콜백</param>
    public void Deactivate(Action onComplete = null)
    {
        OnDeactivateStart();
        StartCoroutine(DeactivateRoutine(onComplete));
    }

    /// <summary>
    /// 활성 연출 코루틴. 하위 클래스에서 ActivateAnimation을 오버라이드하여 연출을 구현합니다.
    /// </summary>
    /// <returns>코루틴 열거자</returns>
    private IEnumerator ActivateRoutine()
    {
        IsAnimating = true;
        yield return StartCoroutine(ActivateAnimation());
        IsAnimating = false;

        OnActivateComplete();
        if (OnActivated != null)
        {
            OnActivated.Invoke(this);
        }
    }

    /// <summary>
    /// 비활성 연출 코루틴. 하위 클래스에서 DeactivateAnimation을 오버라이드하여 연출을 구현합니다.
    /// </summary>
    /// <param name="onComplete">연출 완료 후 호출되는 콜백</param>
    /// <returns>코루틴 열거자</returns>
    private IEnumerator DeactivateRoutine(Action onComplete)
    {
        IsAnimating = true;
        yield return StartCoroutine(DeactivateAnimation());
        IsAnimating = false;

        OnDeactivateComplete();
        if (OnDeactivated != null)
        {
            OnDeactivated.Invoke(this);
        }
        gameObject.SetActive(false);
        if (onComplete != null)
        {
            onComplete.Invoke();
        }
    }

    #region 하위 클래스 오버라이드 포인트

    /// <summary>
    /// 활성 연출 시작 직전에 호출됩니다. 초기 상태 설정 등에 활용합니다.
    /// </summary>
    protected virtual void OnActivateStart()
    {
    }

    /// <summary>
    /// 활성 연출이 완료된 후 호출됩니다. 버튼 활성화 등에 활용합니다.
    /// </summary>
    protected virtual void OnActivateComplete()
    {
    }

    /// <summary>
    /// 비활성 연출 시작 직전에 호출됩니다.
    /// </summary>
    protected virtual void OnDeactivateStart()
    {
    }

    /// <summary>
    /// 비활성 연출이 완료된 후 호출됩니다. 리소스 해제 등에 활용합니다.
    /// </summary>
    protected virtual void OnDeactivateComplete()
    {
    }

    /// <summary>
    /// 활성 연출 코루틴. 하위 클래스에서 오버라이드하여 애니메이션을 구현합니다.
    /// 기본 구현은 연출 없이 즉시 완료됩니다.
    /// </summary>
    /// <returns>코루틴 열거자</returns>
    protected virtual IEnumerator ActivateAnimation()
    {
        yield break;
    }

    /// <summary>
    /// 비활성 연출 코루틴. 하위 클래스에서 오버라이드하여 애니메이션을 구현합니다.
    /// 기본 구현은 연출 없이 즉시 완료됩니다.
    /// </summary>
    /// <returns>코루틴 열거자</returns>
    protected virtual IEnumerator DeactivateAnimation()
    {
        yield break;
    }

    #endregion
}
