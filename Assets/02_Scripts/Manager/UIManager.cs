using UnityEngine;

/// <summary>
/// 공통 UI 연출(페이드, 로딩 화면 등) 및 씬별 UI 전환을 관리하는 싱글톤 매니저
/// </summary>
public class UIManager : MonoBehaviour
{
    private static UIManager _instance;

    /// <summary> 전역 접근을 위한 싱글톤 인스턴스 </summary>
    public static UIManager Instance => _instance;

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
}
