using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// 게임 내 도메인(팝업, 탭) 시스템을 중앙 관리하는 싱글톤 매니저.
/// 웹 URL과 유사한 도메인 경로 방식으로 현재 화면 상태를 추적합니다.
/// 예: /Lobby/Shop/ItemDetail
/// </summary>
public class DomainManager : MonoBehaviour
{
    private static DomainManager _instance;

    /// <summary> 전역 접근을 위한 싱글톤 인스턴스 </summary>
    public static DomainManager Instance
    {
        get
        {
            return _instance;
        }
    }

    /// <summary> 도메인 항목을 순서대로 추적하는 스택 (팝업 + 탭) </summary>
    private readonly List<IDomainNode> _domainStack = new List<IDomainNode>();

    /// <summary> 등록된 팝업 컨트롤러 목록 (씬 이름 → 컨트롤러) </summary>
    private readonly Dictionary<string, PopupController> _popupControllers = new Dictionary<string, PopupController>();

    /// <summary> 등록된 탭 컨트롤러 목록 (씬 이름 → 컨트롤러) </summary>
    private readonly Dictionary<string, TabController> _tabControllers = new Dictionary<string, TabController>();

    /// <summary> 현재 활성화된 팝업 컨트롤러 </summary>
    private PopupController _activePopupController;

    /// <summary> 현재 활성화된 탭 컨트롤러 </summary>
    private TabController _activeTabController;

    /// <summary> 현재 씬 이름 (도메인 경로의 루트) </summary>
    private string _currentSceneName = "";

    /// <summary> CurrentPath 재구성용 재사용 StringBuilder </summary>
    private readonly StringBuilder _pathBuilder = new StringBuilder(128);

    /// <summary> CurrentPath 캐시 (스택 변경 시 무효화) </summary>
    private string _cachedPath;

    /// <summary> 캐시 무효화 여부 </summary>
    private bool _pathDirty = true;

    /// <summary> 현재 도메인 항목이 존재하는지 여부 </summary>
    public bool HasDomain
    {
        get
        {
            return _domainStack.Count > 0;
        }
    }

    /// <summary> 현재 도메인 깊이 (스택에 쌓인 항목 수) </summary>
    public int DomainDepth
    {
        get
        {
            return _domainStack.Count;
        }
    }

    /// <summary>
    /// 현재 도메인 경로를 URL 형태로 반환합니다.
    /// 예: "/Lobby/Shop/ItemDetail"
    /// </summary>
    public string CurrentPath
    {
        get
        {
            if (!_pathDirty)
            {
                return _cachedPath;
            }

            _pathBuilder.Clear();
            _pathBuilder.Append("/");
            _pathBuilder.Append(_currentSceneName);

            for (int i = 0; i < _domainStack.Count; i++)
            {
                _pathBuilder.Append("/");
                _pathBuilder.Append(_domainStack[i].DomainName);
            }

            _cachedPath = _pathBuilder.ToString();
            _pathDirty = false;
            return _cachedPath;
        }
    }

    /// <summary>
    /// 싱글톤 인스턴스 등록 및 중복 방지
    /// </summary>
    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
    }

    #region 도메인 열기 / 닫기 / 뒤로가기

    /// <summary>
    /// 팝업을 이름으로 엽니다. 활성 PopupController를 통해 프리팹을 생성합니다.
    /// 도메인 스택에 팝업이 추가되어 경로가 확장됩니다.
    /// </summary>
    /// <param name="popupName">팝업 이름 (Resources 경로: "Prefab/UI/Popup/{popupName}")</param>
    /// <param name="onOpened">팝업 열림 완료 시 콜백</param>
    public void OpenPopup(string popupName, Action<PopupBase> onOpened = null)
    {
        if (_activePopupController == null)
        {
            Debug.LogError("[DomainManager] 활성화된 PopupController가 없습니다.");
            return;
        }

        _activePopupController.CreatePopup(popupName, (popup) =>
        {
            _domainStack.Add(popup);
            _pathDirty = true;
            popup.Open();

            Debug.Log($"[DomainManager] 팝업 열림: {popupName} | 경로: {CurrentPath}");
            onOpened?.Invoke(popup);
        });
    }

    /// <summary>
    /// 탭을 이름으로 엽니다. 활성 TabController를 통해 탭을 활성화합니다.
    /// 기존 탭 위의 팝업을 모두 닫고, 기존 탭을 비활성화한 후 새 탭으로 교체합니다.
    /// </summary>
    /// <param name="tabName">탭 이름 (게임오브젝트 이름과 대응)</param>
    /// <param name="onOpened">탭 활성화 완료 시 콜백</param>
    public void OpenTab(string tabName, Action<TabBase> onOpened = null)
    {
        if (_activeTabController == null)
        {
            Debug.LogError("[DomainManager] 활성화된 TabController가 없습니다.");
            return;
        }

        // 스택 최상단에서 팝업들을 제거하고 마지막 탭도 비활성화
        CloseAboveLastTab();

        _activeTabController.ActivateTab(tabName, (tab) =>
        {
            _domainStack.Add(tab);
            _pathDirty = true;

            Debug.Log($"[DomainManager] 탭 열림: {tabName} | 경로: {CurrentPath}");
            onOpened?.Invoke(tab);
        });
    }

    /// <summary>
    /// 특정 이름의 도메인 항목을 닫습니다.
    /// 해당 항목 위에 쌓인 모든 항목도 함께 닫힙니다.
    /// </summary>
    /// <param name="domainName">닫을 도메인 이름</param>
    public void Close(string domainName)
    {
        int index = FindDomainIndex(domainName);
        if (index < 0)
        {
            Debug.LogWarning($"[DomainManager] 닫을 도메인을 찾을 수 없습니다: {domainName}");
            return;
        }

        for (int i = _domainStack.Count - 1; i >= index; i--)
        {
            RemoveDomainAt(i);
        }

        Debug.Log($"[DomainManager] 도메인 닫힘: {domainName} | 경로: {CurrentPath}");
    }

    /// <summary>
    /// 가장 위에 있는 도메인 항목을 닫고 이전 상태로 돌아갑니다.
    /// 팝업이면 파괴하고, 탭이면 비활성화합니다.
    /// </summary>
    public void Back()
    {
        if (_domainStack.Count == 0)
        {
            Debug.LogWarning("[DomainManager] 닫을 도메인이 없습니다.");
            return;
        }

        string closedName = _domainStack[_domainStack.Count - 1].DomainName;
        RemoveDomainAt(_domainStack.Count - 1);

        Debug.Log($"[DomainManager] Back: {closedName} 닫힘 | 경로: {CurrentPath}");
    }

    /// <summary>
    /// 열려있는 모든 도메인 항목을 닫습니다.
    /// </summary>
    public void CloseAll()
    {
        for (int i = _domainStack.Count - 1; i >= 0; i--)
        {
            RemoveDomainAt(i);
        }

        Debug.Log($"[DomainManager] 모든 도메인 닫힘 | 경로: {CurrentPath}");
    }

    #endregion

    #region 도메인 조회

    /// <summary>
    /// 현재 최상위 도메인 항목을 반환합니다.
    /// </summary>
    /// <returns>최상위 도메인 항목, 없으면 null</returns>
    public IDomainNode Peek()
    {
        if (_domainStack.Count == 0)
        {
            return null;
        }

        return _domainStack[_domainStack.Count - 1];
    }

    /// <summary>
    /// 특정 이름의 도메인 항목이 현재 열려있는지 확인합니다.
    /// </summary>
    /// <param name="domainName">확인할 도메인 이름</param>
    /// <returns>열려있으면 true</returns>
    public bool IsOpen(string domainName)
    {
        return FindDomainIndex(domainName) >= 0;
    }

    /// <summary>
    /// 특정 이름의 도메인 항목 인스턴스를 가져옵니다.
    /// PopupBase, TabBase 등 IDomainNode를 구현한 타입으로 캐스팅하여 반환합니다.
    /// </summary>
    /// <typeparam name="T">IDomainNode를 구현한 타입</typeparam>
    /// <param name="domainName">도메인 이름</param>
    /// <returns>해당 도메인 인스턴스, 없으면 null</returns>
    public T GetDomain<T>(string domainName) where T : class, IDomainNode
    {
        int index = FindDomainIndex(domainName);
        if (index >= 0)
        {
            return _domainStack[index] as T;
        }

        return null;
    }

    #endregion

    #region 컨트롤러 등록/해제

    /// <summary>
    /// 팝업 컨트롤러를 등록합니다. 등록 시 활성 팝업 컨트롤러로 설정됩니다.
    /// </summary>
    /// <param name="controller">등록할 팝업 컨트롤러</param>
    public void Register(PopupController controller)
    {
        string key = controller.ControllerName;
        if (!_popupControllers.ContainsKey(key))
        {
            _popupControllers.Add(key, controller);
        }

        _activePopupController = controller;
        UpdateSceneName(key);

        Debug.Log($"[DomainManager] 팝업 컨트롤러 등록: {key}");
    }

    /// <summary>
    /// 팝업 컨트롤러 등록을 해제합니다. 활성 컨트롤러였다면 해제됩니다.
    /// </summary>
    /// <param name="controller">해제할 팝업 컨트롤러</param>
    public void Unregister(PopupController controller)
    {
        _popupControllers.Remove(controller.ControllerName);

        if (_activePopupController == controller)
        {
            _activePopupController = null;
        }

        Debug.Log($"[DomainManager] 팝업 컨트롤러 해제: {controller.ControllerName}");
    }

    /// <summary>
    /// 탭 컨트롤러를 등록합니다. 등록 시 활성 탭 컨트롤러로 설정됩니다.
    /// </summary>
    /// <param name="controller">등록할 탭 컨트롤러</param>
    public void Register(TabController controller)
    {
        string key = controller.ControllerName;
        if (!_tabControllers.ContainsKey(key))
        {
            _tabControllers.Add(key, controller);
        }

        _activeTabController = controller;
        UpdateSceneName(key);

        Debug.Log($"[DomainManager] 탭 컨트롤러 등록: {key}");
    }

    /// <summary>
    /// 탭 컨트롤러 등록을 해제합니다. 활성 컨트롤러였다면 해제됩니다.
    /// </summary>
    /// <param name="controller">해제할 탭 컨트롤러</param>
    public void Unregister(TabController controller)
    {
        _tabControllers.Remove(controller.ControllerName);

        if (_activeTabController == controller)
        {
            _activeTabController = null;
        }

        Debug.Log($"[DomainManager] 탭 컨트롤러 해제: {controller.ControllerName}");
    }

    /// <summary>
    /// 이름으로 팝업 컨트롤러를 가져옵니다.
    /// </summary>
    /// <param name="controllerName">컨트롤러 이름</param>
    /// <returns>해당 팝업 컨트롤러, 없으면 null</returns>
    public PopupController GetPopupController(string controllerName)
    {
        _popupControllers.TryGetValue(controllerName, out PopupController controller);
        return controller;
    }

    /// <summary>
    /// 이름으로 탭 컨트롤러를 가져옵니다.
    /// </summary>
    /// <param name="controllerName">컨트롤러 이름</param>
    /// <returns>해당 탭 컨트롤러, 없으면 null</returns>
    public TabController GetTabController(string controllerName)
    {
        _tabControllers.TryGetValue(controllerName, out TabController controller);
        return controller;
    }

    #endregion

    #region 내부 유틸리티

    /// <summary>
    /// 스택에서 특정 이름의 도메인 인덱스를 찾습니다.
    /// </summary>
    /// <param name="domainName">찾을 도메인 이름</param>
    /// <returns>인덱스, 없으면 -1</returns>
    private int FindDomainIndex(string domainName)
    {
        for (int i = 0; i < _domainStack.Count; i++)
        {
            if (_domainStack[i].DomainName == domainName)
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>
    /// 스택에서 도메인 항목을 제거하고 타입에 따라 닫힘 처리를 위임합니다.
    /// 팝업이면 파괴하고, 탭이면 비활성화합니다.
    /// </summary>
    /// <param name="index">스택 인덱스</param>
    private void RemoveDomainAt(int index)
    {
        IDomainNode node = _domainStack[index];
        _domainStack.RemoveAt(index);
        _pathDirty = true;

        if (node is PopupBase popup)
        {
            if (_activePopupController != null)
            {
                _activePopupController.DestroyPopup(popup);
            }
            else
            {
                Debug.LogWarning($"[DomainManager] 활성 PopupController가 없어 팝업을 정리할 수 없습니다: {popup.PopupName}");
            }
        }
        else if (node is TabBase tab)
        {
            if (_activeTabController != null)
            {
                _activeTabController.DeactivateTab(tab);
            }
            else
            {
                Debug.LogWarning($"[DomainManager] 활성 TabController가 없어 탭을 정리할 수 없습니다: {tab.TabName}");
            }
        }
    }

    /// <summary>
    /// 스택 최상단의 팝업들을 모두 닫고, 마지막 탭이 있으면 비활성화하여 제거합니다.
    /// 탭 전환 시 기존 상태를 정리하기 위해 사용됩니다.
    /// </summary>
    private void CloseAboveLastTab()
    {
        // 최상단에서 팝업들을 모두 제거
        for (int i = _domainStack.Count - 1; i >= 0; i--)
        {
            if (_domainStack[i].DomainType == DomainType.Tab)
            {
                break;
            }

            RemoveDomainAt(i);
        }

        // 마지막 탭이 있으면 비활성화하고 제거
        if (_domainStack.Count > 0 && _domainStack[_domainStack.Count - 1].DomainType == DomainType.Tab)
        {
            RemoveDomainAt(_domainStack.Count - 1);
        }
    }

    /// <summary>
    /// 컨트롤러 이름이 "Shared"가 아닌 경우 현재 씬 이름을 갱신합니다.
    /// </summary>
    /// <param name="controllerName">컨트롤러 이름</param>
    private void UpdateSceneName(string controllerName)
    {
        if (controllerName != "Shared")
        {
            _currentSceneName = controllerName;
            _pathDirty = true;
        }
    }

    #endregion
}
