<img width="1919" height="1079" alt="Image" src="https://github.com/user-attachments/assets/57689fc0-011f-4aba-8fb7-071bea467f6f" />
- 시작 화면
<img width="714" height="399" alt="Image" src="https://github.com/user-attachments/assets/8a539a02-5c52-43e8-8cdf-ad6f87acf1b4" />
- 로비 화면
<img width="1915" height="1079" alt="Image" src="https://github.com/user-attachments/assets/e8050b65-6e4b-4e75-a042-1b59e453065e" />
- 멀티 플레이 화면
<img width="833" height="465" alt="Image" src="https://github.com/user-attachments/assets/9d674e58-4fcc-4c30-b86b-5d653d9c18ce" />
- 싱글 플레이 화면

## 📋 프로젝트 정보

| 항목 | 내용 |
|------|------|
| 장르 | 2D 물리 퍼즐  멀티플레이 게임 |
| 엔진 | Unity (C#) |
| 개발 기간 | 2024.04 ~ 2024.05, 2026.04 |
| 개발 인원 | 1인 |
| 핵심 목표 | 물리 기반 과일 합체 시스템, photon 멀티플레이 기능 구현 |
| 모티브 게임 | 수박 게임 |

---

## ⚙ 핵심 기술 및 구현 내용

### 🌐 Photon PUN2 기반 실시간 멀티플레이어

**Photon PUN2 (Photon Unity Networking 2)** 를 활용하여 2인 실시간 대전 시스템을 구현했습니다.

#### 룸 매칭 시스템
- 방 직접 생성 / 방 이름으로 참가 / 빠른 매칭(JoinRandomRoom) 세 가지 참가 방식 지원
- 방 목록 실시간 갱신 (`OnRoomListUpdate` 콜백 + 로컬 캐시 유지)
- 방 정원은 2인으로 제한, `MaxPlayers = 2` 설정

#### 매치 시작 동기화 (이중 안전장치)
방장(MasterClient)이 2인 입장을 감지하면 0.5초 딜레이 후 두 가지 방식으로 동시에 매치 시작을 전파합니다.


PhotonNetwork.RaiseEvent(EVT_MATCH_START, ..., EventCaching.AddToRoomCache)
→ 룸 캐시에 적재되어 나중에 입장한 플레이어도 수신 가능

Room CustomProperties["MatchStarted"] = true
→ 이벤트 수신 전에 입장하거나 이벤트가 유실된 경우에도 OnRoomPropertiesUpdate로 복구


두 경로 모두 `hasHandledMatchStart` 플래그로 중복 처리를 방지합니다.

---

### 🎮 물리 동기화 전략 - 스냅샷 보간 방식

과일 물리는 **로컬 플레이어만 실제 시뮬레이션**하고, 상대방 화면에는 물리 연산을 완전히 배제한 채 **위치 스냅샷을 Lerp 보간**으로 재현합니다.


[내 화면] [상대 화면]
Rigidbody2D.simulated = true Rigidbody2D.simulated = false
Collider2D 활성 Collider2D 비활성
Unity 물리 엔진 계산 스냅샷 수신 → Lerp 이동


#### 이벤트 체계

| 이벤트 코드 | 전송 방식 | 설명 |
|-------------|-----------|------|
| `EVT_FRUIT_CREATE (106)` | Reliable | 과일 생성(들기) 시 level, 위치, 회전, dropId 전송 |
| `EVT_FRUIT_DROP (102)` | Reliable | 드롭(놓기) 시 최종 위치/회전 전송 |
| `EVT_FRUIT_STATE (107)` | **Unreliable** | 0.1초마다 모든 과일의 위치 스냅샷 브로드캐스트 |
| `EVT_FRUIT_MERGE (105)` | Reliable | 머지 발생 시 survivor/other dropId, 새 레벨 전송 |
| `EVT_SCORE_UPDATE (103)` | Reliable | 점수 변경 시 전송 |
| `EVT_GAME_OVER (104)` | Reliable | 게임오버 시 최종 점수 전송 |

> **`EVT_FRUIT_STATE`를 Unreliable로 전송하는 이유**: 0.1초 간격으로 연속 전송되므로 패킷 유실이 발생해도 다음 패킷이 즉시 보정합니다. 재전송 오버헤드를 제거해 지연(latency)을 최소화합니다.

#### dropId 시스템
과일마다 고유 `dropId`를 부여해 네트워크 양단에서 동일한 과일을 특정합니다. `FruitManager`에서 순차 증가 카운터(`_nextDropId`)로 발급하며, 상대방은 `Dictionary<int, Fruit>` 로 매핑합니다.

#### Lerp 보간 (Fruit.cs)
```csharp
// 상대방 과일의 Update()
if (isOtherPlayerFruit && hasNetworkTarget)
{
    transform.localPosition = Vector3.Lerp(
        transform.localPosition, networkTargetLocalPos, Time.deltaTime * LERP_SPEED); // LERP_SPEED = 20f

    float newRot = Mathf.LerpAngle(currentRot, networkTargetRot, Time.deltaTime * LERP_SPEED);
    transform.localRotation = Quaternion.Euler(0f, 0f, newRot);
}
```

#### Merge 처리
로컬 과일: 충돌 감지 → 코루틴으로 흡수 애니메이션 → 레벨업
상대방 과일: 물리·충돌 없음 → EVT_FRUIT_MERGE 수신 즉시 ApplyNetworkMerge() 호출 → 애니메이션 없이 즉시 스프라이트/크기 갱신. 위치는 다음 스냅샷이 자연스럽게 보정

#### 게임 종료 조건
게임오버 라인(GameOverLine)에 과일이 닿으면 해당 플레이어만 먼저 종료됩니다. 두 플레이어 모두 게임오버가 된 시점에 점수를 비교해 최종 승패를 결정합니다.

먼저 게임오버된 플레이어: 자신의 화면을 회색 처리 후 상대 결과 대기
모두 게임오버 시: ShowResultByScore(myScore, theirScore) 로 승/패 판정

####씬 전환 — 커튼 애니메이션
LoadingSceneController가 씬 전환 시 커튼 닫힘/열림 애니메이션을 재생합니다. 멀티 모드 전환은 StartAutoTransitionToMulti() 를 통해 커튼이 완전히 닫힌 뒤 PhotonNetwork.LoadLevel() 을 호출하고, 씬 로드 완료 후 커튼을 자동으로 엽니다.

#### 싱글 플레이 랭킹 시스템
RankingManager (static class) 가 PlayerPrefs + JsonUtility 를 이용해 로컬 상위 3개 기록을 저장합니다.

게임오버 시 Top 3 진입 여부 자동 판별
Top 3 진입 시 이름 입력 UI(RankingNameInputUI) 표시 후 등록
비진입 시 랭킹 조회 화면 바로 표시

#### 물리 기반 과일 합체 시스템 (`Fruit.cs`)

같은 레벨의 과일끼리 충돌 시 합체가 이루어지며, `OnCollisionEnter2D`를 통해 충돌을 감지합니다.
합체 처리 시 중복 실행을 막기 위해 `isMerge` 플래그를 활용하며, 위치 비교(x, y좌표)를 통해 두 과일 중 어느 쪽이 살아남을지 결정합니다.

합체 애니메이션은 `IEnumerator MergeOther`(코루틴)로 구현되어 있으며, 상대 과일을 20프레임에 걸쳐 `Vector3.Lerp`로 끌어당긴 뒤 파괴합니다. 합체 전후로 `Rigidbody2D.simulated`를 `false/true`로 전환해 물리 시뮬레이션을 일시 제어합니다.

- 합체 조건: 동일 레벨, 양측 모두 `isMerge == false`
- 생존 기준: x좌표 비교 시 왼쪽, y좌표 동일 시 아래쪽 과일이 레벨업
- 레벨업 시 스프라이트 및 `localScale` 변경, 최대 레벨 갱신



## 🔑 주요 구현 포인트

- 상대방 과일의 Rigidbody2D.simulated 및 Collider2D를 완전 비활성화해 불필요한 물리 연산 및 로컬 충돌 간섭을 제거
- 과일 위치 스냅샷을 0.1초 간격으로 Unreliable 전송하여 신뢰성 패킷 대비 지연을 줄이고 연속 전송으로 유실을 자연 보정
- 각 과일에 dropId를 부여해 네트워크 양단에서 동일 과일을 안정적으로 특정
- EVT_MATCH_START 이벤트와 Room CustomProperties "MatchStarted" 를 이중으로 전파해 늦은 입장자나 이벤트 유실 상황을 모두 처리
- hasHandledMatchStart 플래그로 매치 시작 이벤트 중복 처리 방지
- PhotonNetwork.AutomaticallySyncScene = true 설정으로 방장의 LoadLevel 호출 한 번으로 모든 클라이언트가 동시에 씬 전환
- FruitManager에서 멀티 전용 이벤트(OnFruitCreated, OnFruitDropped, OnFruitMerged 등)를 C# Action으로 노출해 싱글/멀티 씬 모두 호환되는 구조
- suppressGameOverUI 플래그로 멀티 모드에서는 싱글용 랭킹 UI를 억제하고 MultiGameManager가 결과를 독립 처리
- 게임오버를 즉시 공유하지 않고 양쪽 모두 게임오버 후 점수 비교로 승패를 결정해 한 플레이어가 먼저 끝나도 상대방 플레이가 계속되는 구조
- 상대방 과일의 머지를 물리·충돌 없이 즉시(ApplyNetworkMerge) 처리하고 다음 스냅샷으로 위치 자연 보정
- PlayerPrefs + JSON 직렬화로 로컬 상위 3개 랭킹을 영속 저장하고 게임오버 시 Top 3 진입 여부를 자동 판별
- 씬 전환 시 커튼 애니메이션을 DontDestroyOnLoad 오브젝트로 유지해 씬 간 자연스러운 전환 연출
