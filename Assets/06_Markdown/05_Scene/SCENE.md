# PuzleBattleGame 씬/매니저/인프라 참고 문서

## 콘텐츠와 C# 매핑

[전체 작업 지도](../MAP.md) · [문서 목록](../README.md) · [게임 사양서](../../../docs/SPEC.md) · [구현 현황](../../../docs/IMPLEMENTATION.md)

파일 링크는 이 문서 기준 상대 경로이며, 마지막 열의 이름을 파일 안에서 검색하면 구현 위치를 찾을 수 있습니다.

| 찾는 콘텐츠 | 담당 C# 파일 | 주요 메서드·타입·멤버 |
|---|---|---|
| SharedScene 로드·씬 전환·플레이어 종료 정리 | [Main.cs](../../02_Scripts/Manager/Main.cs) | `SceneEnum`, `AutoCreate`, `MoveScene`, `CoMoveScene`, `IsMovingScene`, `OnApplicationQuit` |
| 타이틀의 젤리 성·로고 연출과 로비 진입 | [TitleMain.cs](../../02_Scripts/Title/TitleMain.cs) | `Start`, `ShowCastle` |
| 로비 튜토리얼·대전 진입 | [LobbyMain.cs](../../02_Scripts/Lobby/LobbyMain.cs) | `OnClickPlay`, `PrepareBattle` |
| 에셋 로딩 요청 공유·캐시·해제 | [AssetManager.cs](../../02_Scripts/Manager/AssetManager.cs) | `AssetArguments`, `LoadAsset`, `LoadAssetAsync`, `MarkPersistent`, `ReleaseAll` |
| 오브젝트 풀 대여·반환·씬 전환 시 정리 | [PoolManager.cs](../../02_Scripts/Manager/PoolManager.cs) | `Get`, `Release`, `ClearIfCreated`, `Clear` |
| 메인 카메라 접근·중복 처리 | [CameraController.cs](../../02_Scripts/CameraController.cs) | `MainCamera` |
| 사운드 매니저 틀 (스텁) | [SoundManager.cs](../../02_Scripts/Manager/SoundManager.cs) | `Awake` |
| 게임 데이터 매니저 틀 (스텁; 실제 사양 로드는 StageInjection) | [GameDataManager.cs](../../02_Scripts/Manager/GameDataManager.cs) | `Awake` |
| 공통 UI 매니저 틀 (스텁; 팝업/탭은 DomainManager) | [UIManager.cs](../../02_Scripts/Manager/UIManager.cs) | `Awake` |
| 다국어 매니저 틀 (스텁) | [LocalizationManager.cs](../../02_Scripts/Manager/LocalizationManager.cs) | `Awake` |

게임 씬의 Controller는 [INGAME.md](../02_Ingame/INGAME.md), 팝업/탭 매니저는 [UI.md](../04_UI/UI.md), 네트워크/유저 데이터 매니저는 [SERVER.md](../06_Server/SERVER.md)에 매핑되어 있습니다.

## 씬 구성

| 씬 | 역할 | 메인 스크립트 |
|----|------|-------------|
| SharedScene | 영구 상주 (매니저, 카메라, EventSystem) | Main.cs |
| [TitleScene](../../01_Scenes/TitleScene.unity) | 짙은 청록 바탕에 메인 탭의 젤리 성과 크림색·민트색 로고를 표시한 뒤 로비로 이동 | TitleMain.cs |
| [LoadingScene](../../01_Scenes/LoadingScene.unity) | 씬이 바뀌는 동안 같은 청록 바탕의 오버레이에 ‘불러오는 중…’을 표시 | — |
| LobbyScene | 방어 전장·하단 탭을 구성하고 플레이 입력으로 퍼즐 진입 준비 | LobbyMain.cs |
| GameScene | 인게임 퍼즐 플레이 | PuzzleGameController.cs, ReplayController.cs |
| ToolScene | 스테이지 맵 편집 도구 (에디터 전용) | StageMapToolController.cs |

## SharedScene (영구 상주)

### 자동 로드
- `Main`의 `[RuntimeInitializeOnLoadMethod(AfterSceneLoad)]`로 앱 시작 시 자동 Additive 로드.
- Active Scene으로 설정 → 다른 씬의 언로드 보장.

### 에디터에서 시작할 씬
- 전체 흐름을 확인하려면 `TitleScene`에서 Play를 누른다. `Main.AutoCreate()`는 현재 씬에 `Main`이 없으면 `SharedScene`을 추가 로드한다. 타이틀에서 로비로 이동하는 호출은 `TitleMain.Start()`의 CI 연출 끝에 있다.

### 포함 오브젝트
- Main이 씬 전환을 관리하고 CameraController가 중복 카메라를 제거한다.
- `SharedScene`의 `EventSystem`만 활성화한다. 타이틀·게임·툴 씬의 로컬 입력 오브젝트는 비활성 상태로 두며, 에디터에서 각 씬을 직접 실행해도 공용 씬이 입력을 담당한다.
- 플레이어 종료 시 `Main.OnApplicationQuit()`이 2D Animation 13.0.6의 `ClearFallbackBuffer()`를 호출한다. 패키지가 스프라이트 스킨 없이도 할당하는 기본 본 버퍼를 해제하며, 빌드 스트리핑에서 이 메서드를 보존한다.
- 상주 매니저와 공용 팝업의 SharedPopupController를 둔다.

## 씬 전환 (Main.MoveScene)

```
Main.MoveScene(preScene, nextScene)
  → DomainManager.CloseAll()  ← 모든 팝업/탭 닫기
    → LoadingScene Additive 로드
      → preScene 언로드 후 기존 풀 정리 및 비영구 에셋 해제
        → nextScene 비동기 로드 (allowSceneActivation = false)
          → 90% 대기
            → nextScene 활성화
              → LoadingScene 언로드
```

- `_isMovingScene`으로 전환 중 `CoMoveScene` 중복 시작을 막는다.

## 매니저 시스템

공통 매니저는 상주하는 `SharedScene` 또는 `DontDestroyOnLoad` 오브젝트에서 접근한다. 예를 들어 `Main.Awake()`는 직접 `DontDestroyOnLoad`를 호출하고 `UserDataManager.Awake()`는 싱글톤 참조를 등록한다.

### 구현 완료
| 매니저 | 역할 | 주요 API |
|--------|------|----------|
| Main | 씬 전환, 게임 흐름 | `MoveScene(pre, next)` |
| AssetManager | 에셋 로드/캐싱 (데이터: Addressables / 프리팹: Resources) | 아래 상세 |
| PoolManager | 오브젝트 풀링 | 아래 상세 |
| DomainManager | 팝업/탭 도메인 관리 | `UI.md` 참고 |
| CameraController | 메인 카메라 싱글톤 | `MainCamera` 정적 접근 |

네트워크와 유저 데이터는 구현되어 있습니다. `NetworkManager` → `HttpNetworkClient`, `UserDataManager` → `IdentityLayer`의 API와 초기화 순서는 [SERVER.md](../06_Server/SERVER.md)를 참고하세요.

### 스텁 (미구현)
| 매니저 | 예정 역할 |
|--------|----------|
| SoundManager | 사운드 재생/정지 |
| GameDataManager | 게임 설정 데이터 관리 |
| UIManager | 공통 UI 연출 (토스트, 로딩 등) |
| LocalizationManager | 다국어 지원 |

**스텁 매니저 패턴**: Awake에서 싱글톤 등록 + DontDestroyOnLoad. 구현 시 이 패턴 유지.

## AssetManager 상세

에셋 로드 래핑 싱글톤. 모든 에셋 로드는 이 매니저를 통해 수행.
**데이터 에셋(스프라이트·TextAsset)은 Addressables, 프리팹은 Resources**로 로드한다.

**Addressables 계열 (데이터 에셋)**
| 메서드 | 용도 |
|--------|------|
| `LoadAsset<T>(address)` | 동기 로드 (WaitForCompletion) |
| `LoadAssetAsync<T>(AssetArguments<T>)` | 비동기 로드 (콜백 기반) |
| `LoadGameObject(address, parent?)` | 동기 프리팹 인스턴스 생성 (범용 유틸, 현재 미사용) |
| `LoadGameObjectAsync(AssetArguments<GameObject>, parent?)` | 비동기 프리팹 인스턴스 생성 (범용 유틸, 현재 미사용) |
| `MarkPersistent(address)` | 씬 전환 시에도 캐시 유지할 에셋 등록 |
| `ReleaseAll()` | Persistent 제외 전체 Addressable 캐시 해제 (씬 전환 시 Main에서 호출) |

- 공유 요청: `_requests` (주소 → 로딩 핸들·완료 결과·대기 콜백). 동기/비동기 호출이 같은 주소의 핸들을 공유하며 타입 불일치는 오류로 반환한다.
- `AssetArguments<T>`: `address`, `successCallback`, `failedCallback` 구조체
- `LoadAsset<T>`: 빈 주소 전달 시 `Debug.LogError` 출력 후 `default` 반환
- `ReleaseAll()`: 영구 주소를 제외한 완료 캐시와 진행 중인 요청을 분리·해제하고, 취소된 요청에는 실패 콜백을 전달한다. 실패한 로드는 핸들을 해제하여 재시도할 수 있다.
- **직접 `Addressables.LoadAssetAsync` 호출 금지** — 반드시 AssetManager 경유.

- 캐시: `_addressablePacket` (주소 → 에셋), `_handlePacket` (주소 → 핸들), `_resourcePacket` (Resources 경로 → 에셋)
- `AssetArguments<T>`: `address`, `successCallback`, `failedCallback` 구조체 (Resources 계열은 `address`에 Resources 상대 경로 지정)
- `LoadAsset<T>` / `LoadResource<T>`: 빈 주소 전달 시 `Debug.LogError` 출력 후 `default`/`null` 반환
- `ReleaseAll()`: 내부 `_releaseBuffer` 리스트를 재사용하여 LINQ/리스트 할당 방지. **Resources 캐시(`_resourcePacket`)는 해제하지 않음** (공용 프리팹은 앱 수명 동안 유지).
- **직접 `Addressables.LoadAssetAsync` / `Resources.Load` 호출 금지** — 반드시 AssetManager 경유.

### 주요 로드 경로 패턴
| 대상 | 로드 방식 | 경로 패턴 |
|------|-----------|-----------|
| 팝업 프리팹 | Resources | `Prefab/UI/Popup/{팝업이름}` |
| 셀 프리팹 | Resources | `Prefab/Puzzle/CellPrefab` |
| 블럭 프리팹 | Resources | `Prefab/Puzzle/BlockPrefab` |
| 블럭 스프라이트 | Addressables | `Block_{blockId}` |
| Rule JSON | Addressables | Addressable에 등록된 이름 |
| Stage JSON | Addressables | Addressable에 등록된 이름 |

> 프리팹 실물은 `Assets/Resources/Prefab/` 하위에 위치한다. Resources 폴더 내 에셋은 Addressable로 중복 등록하지 않는다.

## PoolManager 상세

오브젝트 풀링 싱글톤. 셀/블럭 프리팹 반복 생성/파괴 최적화.

| 메서드 | 용도 |
|--------|------|
| `Get(prefab, parent?)` | 풀에서 꺼내기 (없으면 Instantiate) |
| `Release(instance)` | 풀로 반환 (SetActive false) |
| `Clear()` | 전체 풀 정리 |

- 인스턴스 → 프리팹 역참조 딕셔너리로 반환 시 올바른 풀에 배치.
- 이미 풀에서 대기 중인 인스턴스의 중복 반환은 무시한다. 대여할 때는 큐에서 파괴된 항목을 건너뛰고 남은 인스턴스를 먼저 재사용한다.
- 이전 씬의 대여 객체가 언로드된 후 `ClearIfCreated()`가 대기 객체를 파괴 예약하고 역참조를 비운다. 이어서 `AssetManager.ReleaseAll()`이 비영구 에셋을 해제한다.

## CameraController 상세

메인 카메라 싱글톤. SharedScene에 배치.

- `CameraController.MainCamera`: 전역 카메라 접근.
- `_isMainCamera` 플래그 기반으로 중복 카메라 자동 제거.
- 씬 로드 시 새 카메라가 있으면 SharedScene의 카메라만 유지.

## 씬별 초기화 흐름

### TitleScene
타이틀 연출이 끝나면 로딩 씬을 거쳐 로비로 이동한다. [씬 전환 구현](../../../docs/SCENE.md)

### LobbyScene

로비 구성과 퍼즐 입장 조건은 [로비 사양서](../../../docs/LOBBY-DEFENSE.md#로비-화면과-탭)와 [인게임 사양서](../../../docs/INGAME-BATTLE.md#한-판의-진행)를 따른다.

```
LobbyMain.OnClickPlay()
  → LobbyMain.PrepareBattle(null)로 GameSpec과 상대 기록 준비
  → PlayerLives.TrySpend 성공 시 Main.MoveScene(LobbyScene, GameScene)
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
    → ReplayStorage.Save(replayData) — 모드/시간/완료 틱/점수 포함
      → 결과 버튼에서 Main.MoveScene 호출 (GameScene 재시작 또는 LobbyScene 이동)
```

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
