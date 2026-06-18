# PuzleBattleGame 공유 DTO 상세

`SERVER.md`의 공유 DLL(`PuzleBattleShared.dll`) DTO 정의 문서.
네임스페이스: `PuzleBattleShared.Models`. 서버와 클라이언트가 동일 DTO를 공유한다.

> `JsonUtility` 호환을 위해 모든 DTO는 **public 필드**로 선언한다(프로퍼티 X).

---

## UserData (class)
유저 기본 정보. 서버 응답 및 클라이언트 보관용.

| 필드 | 타입 | 설명 |
|------|------|------|
| `uid` | string | 유저 고유 ID |
| `nickname` | string | 닉네임 |
| `freeCoin` | int | 무료 코인 |
| `paidCoin` | int | 유료 코인 |
| `freeDia` | int | 무료 다이아 |
| `paidDia` | int | 유료 다이아 |

## RewardData (class)
보상 데이터.

| 필드 | 타입 | 설명 |
|------|------|------|
| `rewardType` | RewardType | 보상 종류 (열거형) |
| `count` | int | 보상 수량 |
| `subType01` | string | 보조 타입 1 |
| `subType02` | string | 보조 타입 2 |

## RewardType (enum)

| 값 | 이름 | 설명 |
|----|------|------|
| 0 | None | 없음 |
| 1 | FreeCoin | 무료 코인 |
| 2 | PaidCoin | 유료 코인 |
| 3 | FreeDia | 무료 다이아 |
| 4 | PaidDia | 유료 다이아 |

## AuthProvider (enum)

| 값 | 이름 | 설명 |
|----|------|------|
| 0 | None | 없음 |
| 1 | Guest | 게스트 로그인 |
| 2 | Google | 구글 로그인 |
| 3 | Apple | 애플 로그인 |
| 4 | Facebook | 페이스북 로그인 |

## AuthLoginRequest (class)

| 필드 | 타입 | 설명 |
|------|------|------|
| `provider` | AuthProvider | 로그인 제공자 |
| `providerUserId` | string | provider별 고유 유저 ID |
| `uid` | string | 기존 게스트 유저에 연동할 때 사용 |
| `nickname` | string | 신규 유저 기본 닉네임 |
| `email` | string | 소셜 이메일 |

## AuthLoginResponse (class)

| 필드 | 타입 | 설명 |
|------|------|------|
| `user` | UserData | 로그인된 유저 데이터 |
| `accessToken` | string | 서버 토큰 자리. 현재는 빈 문자열 |
| `isNewUser` | bool | 신규 생성 여부 |
| `isLinked` | bool | 계정 연결 여부 |
| `provider` | AuthProvider | 로그인 제공자 |
