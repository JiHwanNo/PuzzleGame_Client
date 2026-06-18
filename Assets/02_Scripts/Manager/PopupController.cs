using System;
using UnityEngine;

/// <summary>
/// 각 씬에서 팝업의 생성과 제거를 직접 담당하는 컨트롤러 베이스 클래스.
/// 씬별 팝업 루트 Transform을 보유하며, DomainManager의 요청을 받아 실제 오브젝트를 관리합니다.
/// </summary>
public abstract class PopupController : MonoBehaviour
{
    /// <summary> 컨트롤러 식별 이름 (씬 이름과 대응) </summary>
    public abstract string ControllerName { get; }

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
    /// 팝업 프리팹을 Resources에서 로드하여 _popupRoot 하위에 생성합니다.
    /// </summary>
    /// <param name="popupName">팝업 이름 (Resources 경로: "Prefab/UI/Popup/{popupName}")</param>
    /// <param name="onCreated">생성 완료 시 PopupBase 인스턴스를 전달하는 콜백</param>
    public void CreatePopup(string popupName, Action<PopupBase> onCreated)
    {
        string address = $"Prefab/UI/Popup/{popupName}";

        AssetManager.Instance.LoadGameObjectFromResourcesAsync(
            new AssetManager.AssetArguments<GameObject>
            {
                address = address,
                successCallback = (instance) =>
                {
                    PopupBase popup = instance.GetComponent<PopupBase>();
                    if (popup == null)
                    {
                        popup = instance.AddComponent<PopupBase>();
                    }

                    popup.SetPopupName(popupName);
                    onCreated?.Invoke(popup);
                },
                failedCallback = () =>
                {
                    Debug.LogError($"[{ControllerName}PopupController] 팝업 로드 실패: {popupName} (주소: {address})");
                }
            },
            this.transform
        );
    }

    /// <summary>
    /// 팝업의 닫힘 연출을 실행한 뒤 오브젝트를 파괴합니다.
    /// </summary>
    /// <param name="popup">제거할 팝업</param>
    public void DestroyPopup(PopupBase popup)
    {
        if (popup == null || popup.gameObject == null)
        {
            return;
        }

        popup.Close(() =>
        {
            if (popup != null && popup.gameObject != null)
            {
                Destroy(popup.gameObject);
            }
        });
    }
}
