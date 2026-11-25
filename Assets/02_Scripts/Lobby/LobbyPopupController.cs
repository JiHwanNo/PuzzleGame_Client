/// <summary>
/// LobbyScene 전용 팝업 컨트롤러.
/// 스테이지 선택, 설정 등 로비 팝업의 생성/제거를 담당합니다.
/// </summary>
public class LobbyPopupController : PopupController
{
    /// <summary> 컨트롤러 식별 이름 </summary>
    public override string ControllerName => "Lobby";

    /// <summary>로비를 직접 실행하면 공용 씬의 도메인 매니저가 준비된 뒤 등록합니다.</summary>
    protected override void OnEnable()
    {
        if (DomainManager.Instance == null)
        {
            StartCoroutine(RegisterWhenReady());
            return;
        }
        base.OnEnable();
    }

    /// <summary>공용 매니저를 기다렸다가 활성 로비 컨트롤러를 연결합니다.</summary>
    private System.Collections.IEnumerator RegisterWhenReady()
    {
        while (DomainManager.Instance == null)
        {
            yield return null;
        }
        base.OnEnable();
    }

    /// <summary>등록 대기를 취소하고 도메인 매니저에서 제거합니다.</summary>
    protected override void OnDisable()
    {
        StopAllCoroutines();
        base.OnDisable();
    }
}
