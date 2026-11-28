# PuzleBattleGame 서버 통신 참고 문서

서버 API 연동, 공유 DTO, 네트워크 레이어 작업 시 참고.

## 콘텐츠와 C# 매핑

[전체 작업 지도](../MAP.md) · [문서 목록](../README.md) · [게임 사양서](../../../docs/SPEC.md) · [구현 현황](../../../docs/IMPLEMENTATION.md)

파일 링크는 이 문서 기준 상대 경로이며, 마지막 열의 이름을 파일 안에서 검색하면 구현 위치를 찾을 수 있습니다.

| 찾는 콘텐츠 | 담당 C# 파일 | 주요 메서드·타입·멤버 |
|---|---|---|
| 서버 URL·HTTP 초기화·GET/POST 진입점 | [NetworkManager.cs](../../02_Scripts/Manager/NetworkManager.cs) | `Initialize`, `Get`, `Post` |
| UnityWebRequest·JSON 직렬화·응답/오류 처리 | [HttpNetworkClient.cs](../../02_Scripts/Manager/Network/HttpNetworkClient.cs) | `CoGet`, `CoPost`, `HandleResponse`, `BuildUrl` |
| 유저 데이터 레이어 초기화·로드·초기화 해제 | [UserDataManager.cs](../../02_Scripts/Manager/UserDataManager.cs) | `Initialize`, `EnsureIdentityLoaded`, `LoadIdentity`, `ClearAll` |
| user/identity 요청·실패 시 더미 데이터 | [IdentityLayer.cs](../../02_Scripts/Manager/UserData/IdentityLayer.cs) | `Request`, `CreateDummyData`, `Clear` |

공유 DTO의 소스는 이 저장소의 C# 파일에 없습니다. 클라이언트는 [PuzleBattleShared.dll](../../Plugins/PuzleBattleShared.dll)을 참조하며, DTO 수정은 서버의 공유 프로젝트에서 진행합니다.

---

## 환경

| 항목 | 값 |
|------|-----|
| 서버 프레임워크 | ASP.NET Core Web API (.NET 10) |
| 클라이언트 HTTP | `UnityWebRequest` (코루틴 기반) |
| 통신 형식 | REST JSON |
| 기본 base URL | `http://localhost:7777` |
| 공유 DLL | `Assets/Plugins/PuzleBattleShared.dll` |

---

## 공유 DLL (PuzleBattleShared.dll)

서버와 클라이언트가 동일한 DTO를 사용하기 위한 공유 어셈블리.
네임스페이스: `PuzleBattleShared.Models`.

DTO 필드 정의(UserData / RewardData / RewardType / AuthProvider / AuthLoginRequest / AuthLoginResponse)는 [`SERVER_DTO.md`](SERVER_DTO.md) 참고.

---

## 네트워크 레이어 구조

```
NetworkManager (MonoBehaviour 싱글톤, SharedScene)
└─ HttpNetworkClient (순수 C# 클래스)
    ├─ Get<TResponse>(endpoint, onSuccess, onFailed)
    └─ Post<TRequest, TResponse>(endpoint, body, onSuccess, onFailed)
```

### NetworkManager API

| 메서드 | 용도 |
|--------|------|
| `Initialize(baseUrl?)` | HTTP 클라이언트 생성 (기본 `localhost:7777`) |
| `Get<TResponse>(endpoint, onSuccess, onFailed)` | GET 요청 |
| `Post<TRequest, TResponse>(endpoint, body, onSuccess, onFailed)` | POST 요청 |
| `BaseUrl` | 현재 서버 URL |
| `IsInitialized` | 클라이언트 생성 여부 |

### HttpNetworkClient 내부 동작
- `UnityWebRequest` + 코루틴 기반 비동기 통신
- 요청: `JsonUtility.ToJson(body)` → UTF-8 바이트 → `UploadHandlerRaw`
- 응답: `JsonUtility.FromJson<TResponse>(responseJson)` → 콜백 전달
- 에러: HTTP 실패 또는 JSON 파싱 실패 시 `onFailed(errorMessage)` 호출

---

## 유저 데이터 레이어

```
UserDataManager (MonoBehaviour 싱글톤, SharedScene)
├─ IdentityLayer (순수 C# 클래스)
│  └─ UserData (PuzleBattleShared.Models.UserData)
└─ AuthLayer (순수 C# 클래스)
   └─ AuthLoginResponse (PuzleBattleShared.Models.AuthLoginResponse)
```

### UserDataManager API

| 메서드 | 용도 |
|--------|------|
| `Initialize()` | 레이어 인스턴스 생성 |
| `LoadIdentity(onComplete, onFailed, forceRefresh?)` | 설치별 게스트 ID로 서버 로그인 |
| `Login(provider, providerUserId, nickname, email, onComplete, onFailed)` | provider 계정 로그인 |
| `LinkAuthAccount(provider, providerUserId, email, onComplete, onFailed)` | 현재 유저에 provider 계정 연동 |
| `ClearAll()` | 전체 데이터 초기화 (로그아웃) |
| `Identity` | IdentityLayer 프로퍼티 접근 |
| `Auth` | AuthLayer 프로퍼티 접근 |

### IdentityLayer
- 엔드포인트: `POST auth/login`
- 요청 DTO: `AuthLoginRequest` (`provider=Guest`, `providerUserId=PlayerPrefs` 저장값)
- 응답 DTO: `AuthLoginResponse`
- 서버 연결 실패 시 더미 데이터로 폴백 (uid=`"dummy_user"`, nickname=`"Guest"`)
- `Data`: 로드된 `UserData` 인스턴스
- `IsLoaded`: 서버 로드 완료 여부

### AuthLayer
- 엔드포인트: `POST auth/login`, `POST auth/link`
- `Login`: provider 계정 로그인. 기존 게스트 uid가 있으면 요청에 포함해 연동 가능
- `Link`: 현재 uid에 provider 계정 연결
- 실제 소셜 SDK 단계에서는 클라이언트 provider ID를 그대로 믿지 않고 서버 토큰 검증으로 교체해야 함

---

## API 엔드포인트 목록

| 메서드 | 경로 | 요청 DTO | 응답 DTO | 사용처 |
|--------|------|----------|----------|--------|
| POST | `auth/login` | `AuthLoginRequest` | `AuthLoginResponse` | `IdentityLayer.Request()`, `AuthLayer.Login()` |
| POST | `auth/link` | `AuthLoginRequest` | `AuthLoginResponse` | `AuthLayer.Link()` |

---

## 새 API 연동 추가 방법

### 1. 공유 DTO 추가 (서버 ↔ 클라이언트 공용)
1. 서버 프로젝트의 `PuzleBattleShared` 프로젝트에 모델 클래스 추가
2. 네임스페이스: `PuzleBattleShared.Models`
3. 빌드 후 `PuzleBattleShared.dll`을 `Assets/Plugins/`에 복사
4. **주의**: `JsonUtility` 호환을 위해 **public 필드** 사용 (프로퍼티 X)

### 2. 새 데이터 레이어 추가
1. `02_Scripts/Manager/UserData/`에 레이어 클래스 작성
```csharp
public class NewLayer
{
    private const string ENDPOINT = "api/endpoint";
    public ResponseDto Data { get; private set; }
    public bool IsLoaded { get; private set; }

    public void Request(Action onComplete, Action<string> onFailed)
    {
        NetworkManager.Instance.Get<ResponseDto>(
            ENDPOINT,
            (data) => { Data = data; IsLoaded = true; onComplete?.Invoke(); },
            (error) => { onFailed?.Invoke(error); }
        );
    }

    public void Clear() { Data = default; IsLoaded = false; }
}
```
2. `UserDataManager`에 레이어 프로퍼티 + `Initialize()`에 생성 + `ClearAll()`에 초기화 추가

### 3. POST 요청이 필요한 경우
```csharp
NetworkManager.Instance.Post<RequestDto, ResponseDto>(
    "api/endpoint",
    requestBody,
    (response) => { /* 성공 */ },
    (error) => { /* 실패 */ }
);
```

---

## 주의사항

### JsonUtility 호환
- `JsonUtility`는 **public 필드**만 직렬화/역직렬화. 프로퍼티(`{ get; set; }`)는 무시됨.
- 공유 DLL의 DTO 클래스는 반드시 public 필드로 선언해야 함.
- 서버 측 `System.Text.Json`의 네이밍 정책(camelCase)과 클라이언트 필드명이 일치해야 함.
- 서버에서 `PascalCase`로 직렬화하면 클라이언트에서 파싱 실패 → 서버 직렬화 설정 확인 필수.

### 네트워크 초기화 순서
- 로비 상단 바의 `RefreshResources()`는 `UserDataManager.EnsureIdentityLoaded()`로 계정 정보를 요청한다. 두 매니저는 필요할 때 초기화하고 진행 중인 요청과 이미 받은 정보를 재사용한다. 상단 바는 응답의 무료·유료 잼을 합산해 표시한다. [로비 프로필과 재화](../../../docs/LOBBY-DEFENSE.md#로비-화면과-탭)
- 현재 `LobbyMain.PrepareBattle`은 로컬 리플레이에서 상대를 찾는다. 상시 리그의 배치·승급과 티어 집계는 [배틀 리그 기획](../../../docs/INGAME-BATTLE.md#배틀-리그)을 따른다.
- `Initialize()` 전 API 호출 시 에러 콜백으로 안내 메시지 반환.

### 서버 연결 실패 폴백
- `IdentityLayer`는 서버 실패 시 더미 데이터로 폴백하여 게임 진행 가능.
- 새 레이어 추가 시 폴백 정책 결정 필요 (더미 데이터 / 에러 표시 / 재시도).

### 공유 DLL 갱신 절차
1. 서버 프로젝트에서 `PuzleBattleShared` 빌드
2. 빌드된 `PuzleBattleShared.dll`을 `Assets/Plugins/`에 덮어쓰기
3. Unity 에디터 리프레시 (`Ctrl+R` 또는 자동 감지)
4. 클라이언트 코드에서 새 타입 참조 가능
