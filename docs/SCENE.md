# PuzleBattleGame 씬/매니저/인프라 참고 문서

씬 전환, SharedScene, 매니저, AssetManager, PoolManager 관련 작업 시 참고.

---

## 씬 구성

| 씬 | 역할 | 메인 스크립트 |
|----|------|-------------|
| SharedScene | 영구 상주 (매니저, 카메라, EventSystem) | Main.cs |
| TitleScene | CI 연출 후 로비로 이동 | TitleMain.cs |
| LoadingScene | 씬 전환 시 로딩 화면 | — |
| LobbyScene | 스테이지 선택, 게임 준비 | LobbyMain.cs |
| GameScene | 인게임 퍼즐 플레이 | PuzzleGameController.cs, ReplayController.cs |

---

## SharedScene (영구 상주)

### 자동 로드
- `Main`의 `[RuntimeInitializeOnLoadMethod(AfterSceneLoad)]`로 앱 시작 시 자동 Additive 로드.
- Active Scene으로 설정 → 다른 씬의 언로드 보장.
- `SceneEnum` enum: None, TitleScene, LoadingScene, LobbyScene, GameScene (Main 클래스 외부 선언).

### 에디터 전용
- 어떤 씬에서 플레이해도 SharedScene 자동 로드 → TitleScene 강제 이동.
- `[Conditional("UNITY_EDITOR")]`로 빌드에 미포함.

### 포함 오브젝트
- Main (씬 전환 싱글톤)
- CameraController (카메라 싱글톤, 중복 카메라 자동 제거)
- EventSystem (중복 자동 제거: `Main.OnSceneLoaded`)
- 모든 매니저 (DontDestroyOnLoad)
- SharedPopupController (공용 팝업)

---

## 씬 전환 (Main.MoveScene)

```
Main.MoveScene(preScene, nextScene)
  → DomainManager.CloseAll()  ← 모든 팝업/탭 닫기
    → LoadingScene Additive 로드
      → preScene 언로드
        → nextScene 비동기 로드 (allowSceneActivation = false)
          → 90% 대기
            → nextScene 활성화
              → LoadingScene 언로드
```

- `_isMovingScene` 플래그로 중복 호출 방지.
- `CoMoveScene` 코루틴 기반.

---

## 매니저 시스템

모든 매니저는 SharedScene에 배치, MonoBehaviour 싱글톤, DontDestroyOnLoad.

### 구현 완료
| 매니저 | 역할 | 주요 API |
|--------|------|----------|
| Main | 씬 전환, 게임 흐름 | `MoveScene(pre, next)` |
| AssetManager | Addressables 에셋 로드/캐싱 | 아래 상세 |
| PoolManager | 오브젝트 풀링 | 아래 상세 |
| DomainManager | 팝업/탭 도메인 관리 | `UI.md` 참고 |
| CameraController | 메인 카메라 싱글톤 | `MainCamera` 정적 접근 |

### 스텁 (미구현)
| 매니저 | 예정 역할 |
|--------|----------|
| SoundManager | 사운드 재생/정지 |
| GameDataManager | 게임 설정 데이터 관리 |
| UserDataManager | 유저 프로필/진행 상태 저장 |
| UIManager | 공통 UI 연출 (토스트, 로딩 등) |
| NetworkManager | 서버 통신 |
| LocalizationManager | 다국어 지원 |

**스텁 매니저 패턴**: Awake에서 싱글톤 등록 + DontDestroyOnLoad. 구현 시 이 패턴 유지.

---

## AssetManager 상세

Addressables 래핑 싱글톤. 모든 에셋 로드는 이 매니저를 통해 수행.

| 메서드 | 용도 |
|--------|------|
| `LoadAsset<T>(address)` | 동기 로드 (WaitForCompletion) |
| `LoadAssetAsync<T>(AssetArguments<T>)` | 비동기 로드 (콜백 기반) |
| `LoadGameObject(address, parent?)` | 동기 프리팹 인스턴스 생성 |
| `LoadGameObjectAsync(AssetArguments<GameObject>, parent?)` | 비동기 프리팹 인스턴스 생성 |
| `MarkPersistent(address)` | 씬 전환 시에도 캐시 유지할 에셋 등록 |
| `ReleaseAll()` | Persistent 제외 전체 에셋 캐시 해제 (씬 전환 시 Main에서 호출) |

- 캐시: `_addressablePacket` (주소 → 에셋), `_handlePacket` (주소 → 핸들)
- `AssetArguments<T>`: `address`, `successCallback`, `failedCallback` 구조체
- `LoadAsset<T>`: 빈 주소 전달 시 `Debug.LogError` 출력 후 `default` 반환
- `ReleaseAll()`: 내부 `_releaseBuffer` 리스트를 재사용하여 LINQ/리스트 할당 방지
- **직접 `Addressables.LoadAssetAsync` 호출 금지** — 반드시 AssetManager 경유.

### 주요 Addressable 주소 패턴
| 대상 | 주소 패턴 |
|------|-----------|
| 팝업 프리팹 | `Popup/{팝업이름}` |
| 블럭 스프라이트 | `Block_{blockId}` |
| 셀 프리팹 | `CellPrefab` |
| 블럭 프리팹 | `BlockPrefab` |
| Rule JSON | Addressable에 등록된 이름 |
| Stage JSON | Addressable에 등록된 이름 |

---

## PoolManager 상세

오브젝트 풀링 싱글톤. 셀/블럭 프리팹 반복 생성/파괴 최적화.

| 메서드 | 용도 |
|--------|------|
| `Get(prefab, parent?)` | 풀에서 꺼내기 (없으면 Instantiate) |
| `Release(instance)` | 풀로 반환 (SetActive false) |
| `Clear()` | 전체 풀 정리 |

- 인스턴스 → 프리팹 역참조 딕셔너리로 반환 시 올바른 풀에 배치.

---

## CameraController 상세

메인 카메라 싱글톤. SharedScene에 배치.

- `CameraController.MainCamera`: 전역 카메라 접근.
- `_isMainCamera` 플래그 기반으로 중복 카메라 자동 제거.
- 씬 로드 시 새 카메라가 있으면 SharedScene의 카메라만 유지.

---

## 씬별 초기화 흐름

### TitleScene
```
TitleMain.Start()
  → DOTween Sequence: CI 텍스트 색상 트윈 (검정 → #00FF80)
    → 여운 대기
      → Main.MoveScene(TitleScene, LobbyScene)
```

### LobbyScene
```
LobbyMain.OnClickStartStage()
  → DomainManager.OpenPopup("PopupReady")
    → [시작 버튼] PopupReady.OnClickStart()
      → StageInjection.MakeGameSpec(rulePath, stagePath) (+ 랜덤 시드 생성)
        → Main.MoveScene(LobbyScene, GameScene)
    → [리플레이 버튼] PopupReady.OnClickReplay()
      → 최근 리플레이 JSON 로드 → StageInjection.SetReplayData()
        → MakeGameSpec() → Main.MoveScene(LobbyScene, GameScene)
```

### GameScene
```
PuzzleGameController.Start()
  → StageInjection.GetGameSpec()
    → puzzleType에 따라 Board 생성 (ThreeMatch/Link/TapMatch)
      → board.Initialize(gameSpec) (GameSpec.randomSeed 적용)
        → boardView.DrawBoard(board)
          → 게임 루프 시작
  → StageInjection.GetReplayData()
    → (리플레이 있으면) ReplayController.Initialize(replayData)
      → 리플레이 보드 생성 + 우측 상단 축소 배치 + 자동 재생

PuzzleGameController.Update()
  → BoardState.Finish 감지
    → ReplayStorage.Save(replayData) — 리플레이 JSON 자동 저장
      → Main.MoveScene(GameScene, LobbyScene)
```

---

## 새 매니저 구현 시 체크리스트

1. `Manager/` 폴더에 클래스 작성
2. MonoBehaviour 싱글톤 패턴 적용 (Awake에서 Instance 할당)
3. DontDestroyOnLoad 설정
4. SharedScene 하이어라키에 빈 GameObject 추가 후 컴포넌트 부착
5. 필요 시 AssetManager를 통해 리소스 로드

## 새 씬 추가 시 체크리스트

1. `Assets/01_Scenes/`에 씬 파일 생성
2. `SceneEnum`에 값 추가 (Main.cs 외부)
3. Build Settings에 씬 등록
4. `Main.CoMoveScene`의 로딩 파이프라인 확인
5. 필요 시 해당 씬용 PopupController/TabController 추가 (`UI.md` 참고)
