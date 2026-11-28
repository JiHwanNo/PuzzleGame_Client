# 구현 현황

[프로젝트 문서](README.md) / 구현 현황 · [대응하는 게임 사양서](SPEC.md)

아래 상태는 현재 클라이언트 작업본의 실행 경로를 기준으로 한다. **구현**은 해당 동작이 게임 코드에 연결된 경우, **부분 구현**은 항목 중 일부만 연결된 경우, **기획**은 사양이나 제안이 있는 단계다.

정리 시점의 `Develop` 기준 커밋은 `f1d4c0e`다. 아래 카드의 종류별 기본값·합산 전투 능력치와 상세 수치 표시는 로컬 작업본까지 포함한 상태다. 해당 커밋에는 골드 뽑기, 개별 카드 보관·배치, 별 표시가 포함되어 있다. 피벗도 해당 커밋에서 작동하며, 사양서의 300점 충전과 활성 중 회복은 로컬 조정값이다.

## 로비

| 기능 | 상태 | 코드에서 확인할 수 있는 범위 |
|---|---|---|
| 로비 탭·재화·프로필 | 부분 구현 | 상점·인벤토리·메인·옵션, 라이프·골드·잼 표시와 닉네임 프로필이 연결돼 있다. 미확정 탭은 예비 화면이며 리그 티어는 기획 단계다. [LobbyNavigation](../Assets/02_Scripts/Lobby/LobbyNavigation.cs) |
| 방어 전투와 배치 | 구현 | 카드를 셀에 배치하면 20×10 보드에서 적이 이동하고 유닛이 공격한다. 스테이지 처치 보상·다음 스테이지 판정·저장 복구 경로가 있다. [방어 보드](../Assets/02_Scripts/Lobby/LobbyDefenseBoard.cs) · [전장 표시와 저장](../Assets/02_Scripts/Lobby/LobbyDefenseView.cs) |
| 라이프와 퍼즐 진입 | 구현 | 라이프 회복 시각을 저장하고 진입 준비에 성공했을 때 한 개를 차감한다. [PlayerLives](../Assets/02_Scripts/Lobby/PlayerLives.cs) · [LobbyMain](../Assets/02_Scripts/Lobby/LobbyMain.cs) |
| 자리 비움 수익 및 사이드 스테이지 해금 보상 | 기획 | 로비 밖에서는 방어 전투 시간이 멈춘다. 복귀 시 수익 추정과 별 보상·해금은 설계 항목이다. [자리 비움](LOBBY-DEFENSE.md#5-자리-비움과-복귀) · [해금과 재화 초기안](LOBBY-DEFENSE.md#사이드-퍼즐-해금과-재화에-관한-초기안) |

## 인게임

| 기능 | 상태 | 코드에서 확인할 수 있는 범위 |
|---|---|---|
| 퍼즐과 대전 시간 | 구현 | 링크·3매치·탭 매치 입력을 보드가 처리한다. 개별 퍼즐 스테이지에는 목표와 제한 시간을, 배틀에는 90초 제한을 적용한다. [보드별 C# 매핑](INGAME.md#콘텐츠와-c-매핑) · [GameSpec](../Assets/02_Scripts/PuzzleCore/Module/GameSpec.cs) |
| 득점·피버·피벗 | 구현 | 파괴 점수·콤보·피버를 계산한다. 배틀에서는 플레이어·상대 보드마다 피벗을 구동한다. [ObjectiveManager](../Assets/02_Scripts/PuzzleCore/Module/ObjectiveManager.cs) · [피벗 구현](INGAME-VIEW-AND-BATTLE.md#대전-전용-피벗-모드) |
| 리플레이 상대·승패 | 구현 | 로컬 완주 기록을 골라 같은 시드로 재생한다. 기록이 없으면 첫 기록 도전으로 진입한다. 결과 화면에서 승패·무승부를 표시하고 저장·재도전·로비 복귀를 처리한다. [LobbyMain](../Assets/02_Scripts/Lobby/LobbyMain.cs) · [경기 종료 처리](../Assets/02_Scripts/PuzzleCore/Controller/PuzzleGameController.cs) · [상대 재생](../Assets/02_Scripts/PuzzleCore/Controller/ReplayController.cs) |
| 리그·점수 몬스터 | 기획 | 서버 랭킹과 티어 집계, 상대 퍼즐 점수에 따른 몬스터 소환·방어 점수 합산은 후속 기능이다. [리그 사양](INGAME-BATTLE.md#배틀-리그) · [방어선 연동안](INGAME-BATTLE.md#방어선-연동안) |

## 카드

| 기능 | 상태 | 코드에서 확인할 수 있는 범위 |
|---|---|---|
| 골드 뽑기·개별 능력치·별 | 구현 | 종류별 기본값과 사거리를 두고, 보유한 한 장의 보너스 세 값으로 합산 능력치와 등급을 계산한다. 전투는 배치한 카드의 값을 읽는다. [카드 모델](../Assets/02_Scripts/Lobby/LobbyUnit.cs) · [뽑기와 전투](../Assets/02_Scripts/Lobby/LobbyDefenseBoard.cs) |
| 상점 도감·인벤토리·카드 상세 | 구현 | 상점은 종류 기본값을, 보유·배치 목록은 합산 능력치와 별을 표시한다. 상세 표에서 기본·보너스·합계를 비교한다. [상점](../Assets/02_Scripts/Lobby/LobbyNavigation.cs) · [카드 상세](../Assets/02_Scripts/Lobby/LobbyNavigation.Inventory.cs) |
| 분해·종류별 에너지·강화 | 기획 | 분해 트랜잭션, 에너지 잔액과 개별 강화 단계 저장, 강화 화면은 후속 구현 대상이다. 지급량·비용·최대 단계는 미정이다. [카드 강화 사양](LOBBY-CARDS.md#분해-에너지와-개별-카드-강화) |

## 구현 참고 문서

| 확인할 내용 | 문서 |
|---|---|
| 코드 구조와 실행 진입점 | [작업별 코드 지도](MAP.md#작업별-빠른-경로) · [아키텍처](ARCHITECTURE.md) · [씬과 리소스](SCENE.md) |
| 보드·연출·저장 | [인게임 모델](INGAME.md) · [뷰와 리플레이](INGAME-VIEW-AND-BATTLE.md) · [데이터 구조](DATA.md) · [규칙·스테이지 JSON](RULES-AND-STAGES.md) |
| 화면·스타일·서버 | [UI](UI.md) · [스타일 가이드와 PNG 원본](ART-STYLE.md) · [서버 연동](SERVER.md) |
| 개발 도구와 기록 | [Unity 프로젝트 기술 문서](../Assets/06_Markdown/README.md) · [변경 이력](CHANGELOG.md) |
