# 물리 기반 멀티플레이 퍼즐 게임

**Photon PUN2 기반 실시간 2인 대전과 스냅샷 보간 물리 동기화를 구현한 수박 게임 클론 프로젝트**

[![Unity](https://img.shields.io/badge/Unity-000000?style=flat&logo=unity&logoColor=white)](https://unity.com/)
[![C#](https://img.shields.io/badge/C%23-239120?style=flat&logo=csharp&logoColor=white)](https://learn.microsoft.com/en-us/dotnet/csharp/)

[![게임플레이 영상](https://img.youtube.com/vi/2_wYaGhcukQ/0.jpg)](https://youtu.be/2_wYaGhcukQ?si=hhrDG_GGVv4SRZM7)

> 이미지를 클릭하면 게임플레이 영상을 볼 수 있습니다.
[![YouTube](https://img.shields.io/badge/YouTube-FF0000?style=flat&logo=youtube&logoColor=white)](https://youtu.be/2_wYaGhcukQ?si=hhrDG_GGVv4SRZM7)
> 
---

## 프로젝트 정보

| 항목 | 내용 |
|---|---|
| 장르 | 2D 물리 퍼즐 멀티플레이 게임 |
| 개발 기간 | 2024.04 ~ 2024.05, 2026.04 |
| 팀 구성 | 1인 개발 |
| 사용 기술 | Unity (C#), Photon PUN2 |
| 모티브 | 수박 게임 |

**본인 담당 파트:** 전체

---

## 프로젝트 개요

같은 레벨의 과일끼리 충돌하면 합체되어 레벨업하는 물리 기반 퍼즐 게임입니다. Photon PUN2로 실시간 2인 대전을 구현했으며, 로컬 물리 시뮬레이션과 스냅샷 보간을 조합해 상대방 화면의 물리 연산 간섭 없이 동기화를 처리했습니다.

---

## 코드 상세

### 멀티플레이어 (`Scripts/Multiplayer/`)

| 파일 | 역할 |
|---|---|
| [`Multiplayer/PhotonNetworkManager.cs`](./Suika%20Game/Assets/Scripts/Multiplayer/PhotonNetworkManager.cs) | Photon 연결, 룸 생성/참가/빠른 매칭, 방 목록 실시간 갱신(`OnRoomListUpdate`). 방장(MasterClient)이 2인 입장 감지 시 `EVT_MATCH_START` 이벤트와 Room CustomProperties 이중 전파로 매치 시작 동기화 |
| [`Multiplayer/MultiGameManager.cs`](./Suika%20Game/Assets/Scripts/Multiplayer/MultiGameManager.cs) | 멀티 게임 진행 총괄. 과일 생성/드롭/머지/점수/게임오버 이벤트(`EVT_FRUIT_CREATE`, `EVT_FRUIT_DROP`, `EVT_FRUIT_STATE`, `EVT_FRUIT_MERGE`, `EVT_SCORE_UPDATE`, `EVT_GAME_OVER`) 송수신 처리. 양쪽 모두 게임오버 후 점수 비교로 승패 결정 |
| [`Multiplayer/LobbyUI.cs`](./Suika%20Game/Assets/Scripts/Multiplayer/LobbyUI.cs) | 로비 화면 UI. 방 생성, 이름으로 참가, 빠른 매칭 세 가지 진입 방식 제공 |
| [`Multiplayer/RoomListItem.cs`](./Suika%20Game/Assets/Scripts/Multiplayer/RoomListItem.cs) | 방 목록 항목 UI 컴포넌트 |

### 게임플레이 (`Scripts/`)

| 파일 | 역할 |
|---|---|
| [`Fruit.cs`](./Suika%20Game/Assets/Scripts/Fruit.cs) | 과일 핵심 로직. 로컬 물리 시뮬레이션(`Rigidbody2D.simulated = true`), 상대방 과일은 물리 비활성화 후 스냅샷 Lerp 보간(`LERP_SPEED = 20f`). `OnCollisionEnter2D`로 동일 레벨 충돌 감지, 코루틴으로 20프레임 흡수 애니메이션 후 레벨업. `dropId`로 네트워크 양단에서 동일 과일 특정 |
| [`FruitManager.cs`](./Suika%20Game/Assets/Scripts/FruitManager.cs) | 과일 풀 관리 및 생성. `_nextDropId` 순차 증가로 dropId 발급. `OnFruitCreated`, `OnFruitDropped`, `OnFruitMerged` 등 C# Action 이벤트로 싱글/멀티 씬 모두 호환 |
| [`GameOverLine.cs`](./Suika%20Game/Assets/Scripts/GameOverLine.cs) | 게임오버 라인. 과일이 닿으면 해당 플레이어만 먼저 종료, 상대 결과 대기 후 최종 승패 결정 |
| [`GameFlowManager.cs`](./Suika%20Game/Assets/Scripts/GameFlowManager.cs) | 싱글 플레이 게임 흐름 관리. `suppressGameOverUI` 플래그로 멀티 모드에서 싱글용 랭킹 UI 억제 |
| [`GroundControl.cs`](./Suika%20Game/Assets/Scripts/GroundControl.cs) | 과일 드롭 조준 및 낙하 제어 |
| [`CursorControl.cs`](./Suika%20Game/Assets/Scripts/CursorControl.cs) | 커서 위치 기반 과일 드롭 위치 지정 |
| [`LoadingSceneController.cs`](./Suika%20Game/Assets/Scripts/LoadingSceneController.cs) | 씬 전환 시 커튼 닫힘/열림 애니메이션 재생. `DontDestroyOnLoad`로 씬 간 유지. 멀티 전환 시 커튼이 완전히 닫힌 후 `PhotonNetwork.LoadLevel()` 호출 |

### 랭킹 / UI (`Scripts/`)

| 파일 | 역할 |
|---|---|
| [`RankingManager.cs`](./Suika%20Game/Assets/Scripts/RankingManager.cs) | 로컬 상위 3개 랭킹 관리. `PlayerPrefs` + `JsonUtility`로 영속 저장, 게임오버 시 Top 3 진입 여부 자동 판별 |
| [`RankingNameInputUI.cs`](./Suika%20Game/Assets/Scripts/RankingNameInputUI.cs) | Top 3 진입 시 이름 입력 UI |
| [`RankingPage.cs`](./Suika%20Game/Assets/Scripts/RankingPage.cs) | 랭킹 조회 화면 |
| [`RetryHandler.cs`](./Suika%20Game/Assets/Scripts/RetryHandler.cs) | 게임 재시작 처리 |
| [`BtnStart.cs`](./Suika%20Game/Assets/Scripts/BtnStart.cs) | 싱글 플레이 시작 버튼 |
| [`BtnMulti.cs`](./Suika%20Game/Assets/Scripts/BtnMulti.cs) | 멀티플레이 진입 버튼 |
| [`BtnRank.cs`](./Suika%20Game/Assets/Scripts/BtnRank.cs) | 랭킹 조회 버튼 |
| [`BtnCloseRanking.cs`](./Suika%20Game/Assets/Scripts/BtnCloseRanking.cs) | 랭킹 화면 닫기 버튼 |

---

## 핵심 구현

### 물리 동기화 전략 - 스냅샷 보간

로컬 플레이어만 실제 물리 시뮬레이션을 수행하고, 상대방 화면에는 `Rigidbody2D.simulated = false`로 물리를 완전히 배제한 채 0.1초 간격 위치 스냅샷을 `Vector3.Lerp`로 보간해 재현합니다. 상대방 과일의 불필요한 물리 연산과 로컬 충돌 간섭을 제거하는 것이 핵심입니다.

스냅샷 이벤트(`EVT_FRUIT_STATE`)는 Unreliable로 전송합니다. 0.1초 간격으로 연속 전송되므로 패킷이 유실되더라도 다음 패킷이 즉시 보정하여, 재전송 오버헤드를 제거하고 지연을 최소화합니다.

### 매치 시작 이중 동기화

방장이 2인 입장을 감지하면 `PhotonNetwork.RaiseEvent(EVT_MATCH_START)`와 Room CustomProperties `MatchStarted = true`를 동시에 전파합니다. 이벤트 유실이나 늦은 입장으로 이벤트를 놓친 경우도 `OnRoomPropertiesUpdate`로 복구합니다. `hasHandledMatchStart` 플래그로 중복 처리를 방지합니다.

### dropId 시스템

과일마다 고유 `dropId`를 부여해 네트워크 양단에서 동일한 과일을 특정합니다. `FruitManager`에서 순차 증가 카운터로 발급하며, 상대방은 `Dictionary<int, Fruit>`으로 매핑합니다.

### 싱글 플레이 랭킹

`PlayerPrefs` + `JsonUtility`로 로컬 상위 3개 기록을 영속 저장합니다. 게임오버 시 Top 3 진입 여부를 자동 판별하며, 진입 시 이름 입력 UI를 표시하고 미진입 시 랭킹 조회 화면으로 바로 전환합니다.

---

## 폴더 구조

- `Suika Game/Assets/Scripts/Multiplayer/` : Photon 네트워크, 로비, 멀티 게임 매니저
- `Suika Game/Assets/Scripts/` : 과일 물리, 게임 흐름, 랭킹, UI 버튼 등
