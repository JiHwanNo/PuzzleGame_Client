using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>준비 팝업에서 미리 고른 퍼즐을 슬롯으로 공개하고 스테이지 또는 대전을 시작합니다.</summary>
public class PopupReady : PopupHandler
{
    [SerializeField] private TMP_Text _titleText;
    [SerializeField] private TMP_Text _statusText;
    [SerializeField] private TMP_Text _lifeText;
    [SerializeField] private RawImage _reel;
    [SerializeField] private Button _startButton;
    [SerializeField] private Button _closeButton;

    // 이미지의 왼쪽부터 링크, 3매치, 탭 매치 순서입니다.
    private static readonly string[] RulePaths = { "LinkMatchRule", "ThreeMatchRule", "TapMatchRule" };
    private static readonly string[] ModeNames = { "링크", "3매치", "탭 매치" };

    private Vector2 _previewSize;
    private int _selectedMode;
    private bool _spinning;
    private bool _isBattle;
    private long _lastSecond = -1;

    /// <summary>첫 프레임 전에 결과를 정하고 라이프 상태를 표시합니다.</summary>
    protected override void Awake()
    {
        base.Awake();
        _previewSize = _reel.rectTransform.sizeDelta;
        LagoonUI.Backplate(_reel.rectTransform, "ReelPlate", new Color32(41, 159, 170, 255), new Vector2(12f, 55f));
        LagoonUI.Backplate(_lifeText.rectTransform, "LifePlate", new Color32(180, 246, 229, 255), Vector2.zero);
        _selectedMode = UnityEngine.Random.Range(0, RulePaths.Length);
        RefreshLives(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
    }

    /// <summary>로비에서 대전을 선택한 팝업에 제목과 대전 진입 경로를 설정합니다.</summary>
    internal void ConfigureBattle()
    {
        _isBattle = true;
        _titleText.text = "90초 대전 준비";
    }

    /// <summary>팝업이 열린 동안 초 단위로 다음 회복 시각과 버튼 상태를 갱신합니다.</summary>
    private void Update()
    {
        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        if (now != _lastSecond)
        {
            RefreshLives(now);
        }
    }

    /// <summary>라이프 잔량과 다음 한 개가 충전될 때까지의 시간을 표시합니다.</summary>
    /// <param name="now">현재 UTC 초입니다.</param>
    private void RefreshLives(long now)
    {
        _lastSecond = now;
        int lives = PlayerLives.GetRemaining(now, out int seconds);
        _lifeText.text = $"라이프  {lives} / {PlayerLives.Capacity}";
        if (seconds > 0)
        {
            _lifeText.text += $"     +1까지  {seconds / 60:00}:{seconds % 60:00}";
        }
        _startButton.interactable = !_spinning && lives > 0;
    }

    /// <summary>중복 클릭을 막고 미리 선택된 모드의 슬롯 연출을 시작합니다.</summary>
    private void OnClickStart()
    {
        if (_spinning || _popup.IsAnimating || !_startButton.interactable)
        {
            return;
        }
        _spinning = true;
        _startButton.interactable = false;
        _closeButton.interactable = false;
        StartCoroutine(SpinAndStart());
    }

    /// <summary>릴을 감속해 선택된 칸에 정지한 뒤 데이터를 준비하고 라이프를 사용합니다.</summary>
    private IEnumerator SpinAndStart()
    {
        _statusText.text = "모드 추첨 중";
        const float spinSeconds = 3f;
        const float resultSeconds = 2f;
        int steps = RulePaths.Length * 4 + _selectedMode;
        double startedAt = Time.realtimeSinceStartupAsDouble;
        while (Time.realtimeSinceStartupAsDouble - startedAt < spinSeconds)
        {
            float elapsed = (float)(Time.realtimeSinceStartupAsDouble - startedAt);
            float t = Mathf.Clamp01(elapsed / spinSeconds);
            float position = steps * (1f - Mathf.Pow(1f - t, 2f));
            float zoom = Mathf.SmoothStep(0f, 1f, elapsed / 0.28f);
            Vector2 size = Vector2.Lerp(_previewSize, new Vector2(300f, 300f), zoom);
            _reel.rectTransform.sizeDelta = size;
            _reel.uvRect = new Rect(position / RulePaths.Length, 0f, size.x / (size.y * RulePaths.Length), 1f);
            yield return null;
        }
        _reel.rectTransform.sizeDelta = new Vector2(300f, 300f);
        _reel.uvRect = new Rect((float)_selectedMode / RulePaths.Length, 0f, 1f / RulePaths.Length, 1f);
        _statusText.text = $"<b><color=#00646A>{ModeNames[_selectedMode]}</color></b> 모드 당첨!";
        yield return new WaitForSecondsRealtime(resultSeconds);

        if (_popup.IsAnimating || Main.Instance == null || Main.Instance.IsMovingScene ||
            StageInjection.Instance == null || !PrepareSelectedGame())
        {
            Debug.LogError("[PopupReady] 게임 진입을 준비하지 못했습니다.");
            RestoreButtons("게임을 준비하지 못했습니다. 다시 시도해 주세요.");
            yield break;
        }
        if (!PlayerLives.TrySpend(DateTimeOffset.UtcNow.ToUnixTimeSeconds()))
        {
            RestoreButtons("라이프가 충전되면 시작할 수 있어요.");
            yield break;
        }
        Main.Instance.MoveScene(SceneEnum.LobbyScene, SceneEnum.GameScene);
    }

    /// <summary>릴에 표시한 규칙 주소로 요청된 종류의 게임을 준비합니다.</summary>
    private bool PrepareSelectedGame()
    {
        string ruleAddress = RulePaths[_selectedMode];
        if (_isBattle)
        {
            return LobbyMain.PrepareBattle(ruleAddress);
        }
        return StageInjection.Instance.MakeGameSpec(ruleAddress, "Stage");
    }

    /// <summary>시작 실패 시 선택 결과를 감추고 버튼을 다시 활성화합니다.</summary>
    /// <param name="message">팝업에 표시할 실패 사유입니다.</param>
    private void RestoreButtons(string message)
    {
        _spinning = false;
        _reel.rectTransform.sizeDelta = _previewSize;
        _reel.uvRect = new Rect(0f, 0f, 1f, 1f);
        _statusText.text = message;
        _closeButton.interactable = true;
        RefreshLives(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
    }

    /// <summary>추첨 전에는 라이프를 소모하지 않고 팝업을 닫습니다.</summary>
    private void OnClickClose()
    {
        if (!_spinning)
        {
            ClosePopup();
        }
    }
}
