# PuzleBattleGame 서버 통신 참고 문서

서버 API 연동, 공유 DTO, 네트워크 레이어 작업 시 참고.

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
네임스페이스: `PuzleBattleShared.Models`

### UserData (class)
유저 기본 정보. 서버 응답 및 클라이언트 보관용.

| 필드 | 타입 | 설명 |
|------|------|------|
| `uid` | string | 유저 고유 ID |
| `nickname` | string | 닉네임 |
| `freeCoin` | int | 무료 코인 |
| `paidCoin` | int | 유료 코인 |
| `freeDia` | int | 무료 다이아 |
| `paidDia` | int | 유료 다이아 |

### RewardData (class)
보상 데이터.

| 필드 | 타입 | 설명 |
|------|------|------|
| `rewardType` | RewardType | 보상 종류 (열거형) |
| `count` | int | 보상 수량 |
| `subType01` | string | 보조 타입 1 |
| `subType02` | string | 보조 타입 2 |

### RewardType (enum)

| 값 | 이름 | 설명 |
|----|------|------|
| 0 | None | 없음 |
| 1 | FreeCoin | 무료 코인 |
| 2 | PaidCoin | 유료 코인 |
| 3 | FreeDia | 무료 다이아 |
| 4 | PaidDia | 유료 다이아 |

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
└─ IdentityLayer (순수 C# 클래스)
    └─ UserData (PuzleBattleShared.Models.UserData)
```

### UserDataManager API

| 메서드 | 용도 |
|--------|------|
| `Initialize()` | 레이어 인스턴스 생성 |
| `LoadIdentity(onComplete, onFailed, forceRefresh?)` | Identity 서버 로드 |
| `ClearAll()` | 전체 데이터 초기화 (로그아웃) |
| `Identity` | IdentityLayer 프로퍼티 접근 |

### IdentityLayer
- 엔드포인트: `GET user/identity`
- 응답 DTO: `UserData`
- 서버 연결 실패 시 더미 데이터로 폴백 (uid=`"dummy_user"`, nickname=`"Guest"`)
- `Data`: 로드된 `UserData` 인스턴스
- `IsLoaded`: 서버 로드 완료 여부

---

## API 엔드포인트 목록

| 메서드 | 경로 | 요청 DTO | 응답 DTO | 사용처 |
|--------|------|----------|----------|--------|
| GET | `user/identity` | — | `UserData` | `IdentityLayer.Request()` |

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
- `NetworkManager.Initialize()` → `UserDataManager.Initialize()` → `LoadIdentity()` 순서 보장 필요.
- `Initialize()` 전 API 호출 시 에러 콜백으로 안내 메시지 반환.

### 서버 연결 실패 폴백
- `IdentityLayer`는 서버 실패 시 더미 데이터로 폴백하여 게임 진행 가능.
- 새 레이어 추가 시 폴백 정책 결정 필요 (더미 데이터 / 에러 표시 / 재시도).

### 공유 DLL 갱신 절차
1. 서버 프로젝트에서 `PuzleBattleShared` 빌드
2. 빌드된 `PuzleBattleShared.dll`을 `Assets/Plugins/`에 덮어쓰기
3. Unity 에디터 리프레시 (`Ctrl+R` 또는 자동 감지)
4. 클라이언트 코드에서 새 타입 참조 가능
