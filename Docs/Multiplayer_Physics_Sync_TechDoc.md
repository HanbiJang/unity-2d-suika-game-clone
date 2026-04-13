# 기술 문서: 멀티플레이어 물리 동기화 구현 및 트러블슈팅

**프로젝트명**: Suika Game (수박 게임)  
**문서 유형**: 기술 포스트모템 (Technical Postmortem)  
**작성일**: 2026-04-14  
**담당 파트**: 멀티플레이어 네트워크 / 물리 시뮬레이션  
**사용 기술**: Unity 2D, Photon PUN2, Rigidbody2D

---

## 1. 개요

본 문서는 Suika Game 멀티플레이어 모드(MultiModeScene) 개발 과정에서 발생한  
**물리 시뮬레이션 동기화 문제**의 원인 분석, 시도한 해결 방법, 최종 해결책을 기록한다.

동일한 문제를 다시 마주치거나, 유사한 물리 기반 멀티플레이어 기능을 개발할 때  
참고 자료로 활용하는 것을 목적으로 한다.

---

## 2. 시스템 구조 요약

### 2.1 씬 구성

```
MultiModeScene
├── GameObjects          (로컬 플레이어 영역 - 왼쪽)
│   ├── FruitParent      (과일 오브젝트 부모)
│   ├── FruitManager     (게임 로직 컴포넌트)
│   └── ...
├── GameObjects_Other    (상대방 표시 영역 - 오른쪽)
│   ├── FruitParent      (상대방 과일 표시용)
│   └── ...
└── GameManagers
    └── MultiGameManager (네트워크 동기화 담당)
```

### 2.2 네트워크 방식

- **Photon PUN2** `PhotonNetwork.RaiseEvent` 사용 (PhotonView 불필요)
- 이벤트 기반 단방향 전송: 로컬 플레이어 → 상대방 화면

### 2.3 이벤트 코드 목록

| 코드 | 이름 | 설명 |
|------|------|------|
| 101 | EVT_NEXT_FRUIT | 다음 과일 미리보기 동기화 |
| 102 | EVT_FRUIT_DROP | 과일 드롭(낙하) 동기화 |
| 103 | EVT_SCORE_UPDATE | 점수 동기화 |
| 104 | EVT_GAME_OVER | 게임오버 통보 |
| 105 | EVT_FRUIT_MERGE | 과일 합치기(머지) 동기화 |
| 106 | EVT_FRUIT_CREATE | 과일 생성(들기 상태) 동기화 |
| 107 | EVT_FRUIT_STATE | 전체 위치 스냅샷 (0.1초 주기) |

---

## 3. 문제 발생 경위

### 3.1 초기 구현 (드롭만 동기화)

최초 구현에서는 **과일 드롭 이벤트만** 동기화했다.

```
[로컬] 과일 드롭 → EVT_FRUIT_DROP 전송
[상대] 수신 후 동일 위치에 과일 생성 + Rigidbody2D 물리 활성화
```

**문제**: 드롭 직후 과일이 물리 엔진에 의해 굴러가는데,  
두 컴퓨터의 물리 시뮬레이션이 **미세하게 다른 결과**를 내기 시작했다.  
과일이 쌓일수록 오차가 누적되어 전혀 다른 형태의 퍼즐이 만들어졌다.

**근본 원인**:  
Unity Rigidbody2D 물리 엔진은 **결정론적(deterministic) 재현을 보장하지 않는다.**  
동일한 초기값이라도 프레임 타이밍, 부동소수점 연산 순서 등에 의해 미세한 차이가 발생하고,  
이 차이가 프레임마다 누적되어 결국 완전히 다른 결과로 발산한다.

---

## 4. 트러블슈팅 히스토리

### 4.1 시도 1: 머지 이벤트 동기화 추가 (EVT_FRUIT_MERGE)

**가설**: 드롭뿐 아니라 머지(합치기)도 동기화하면 결과가 같아질 것이다.

**구현**:
- 각 과일에 `dropId` 고유 번호 부여
- 머지 발생 시 `(survivorDropId, otherDropId, newLevel)` 전송
- 수신측에서 dropId로 과일 찾아 `ApplyNetworkMerge()` 호출

**결과**: 개선됐으나 여전히 불일치 발생.

**실패 원인**:  
드롭과 머지 사이의 물리 시뮬레이션(굴러가는 궤적)은 여전히 양측이 독립적으로 계산했다.  
머지 자체는 맞더라도 **그 전 과정이 다르므로** 전체 퍼즐 모양은 달랐다.

---

### 4.2 시도 2: 위치 스냅샷 주기 전송 추가 (EVT_FRUIT_STATE)

**가설**: 주기적으로 모든 과일의 실제 위치를 전송해 수신측을 강제 보정하면 된다.

**구현**:
- 0.5초마다 모든 과일의 `(dropId, x, y, rotation, velocity)` 전송
- 수신측에서 해당 과일의 Rigidbody2D에 위치/속도 강제 적용

**결과**: 대략적인 형태는 비슷해졌으나 **떨림 현상** 발생.

**실패 원인**:  
물리 엔진과 스냅샷 보정이 **동시에 같은 과일의 위치를 결정**하려 하면서 충돌 발생.
- 물리 엔진: "이 과일은 왼쪽으로 가야 해"
- 스냅샷: "이 과일은 오른쪽으로 가야 해"
- 결과: 매 프레임 위치가 튀어 떨림 발생

---

### 4.3 시도 3: 머지 중 스냅샷 제외 + 전송 방식 변경

**가설**: 머지 애니메이션 중인 과일은 스냅샷에서 제외하면 된다.

**구현**:
- `IsMerging` 프로퍼티 추가
- 스냅샷 송신 시 `IsMerging == true`인 과일 제외
- 스냅샷 수신 시 `IsMerging == true`인 과일 업데이트 스킵
- `SendUnreliable` → `SendReliable` 변경 (순서 보장 시도)

**결과**: 여전히 연속 머지 시 과일이 합쳐지지 않고 튀는 현상 지속.

**실패 원인 1 (순서 역전)**:  
`SendReliable`로 변경했어도 **스냅샷과 머지 이벤트가 다른 시점에 발송**되므로  
네트워크 레이턴시에 따라 스냅샷이 머지보다 먼저 도착하는 경우가 여전히 존재했다.  
이때 "아직 합쳐지지 않은 두 과일이 겹친 위치"가 적용되어 물리 충돌로 튕겼다.

**실패 원인 2 (연속 머지)**:  
짧은 시간에 머지가 연속으로 발생하면 (예: 포도 A+B → 오렌지, 포도 C+D → 오렌지, 오렌지+오렌지 → 복숭아)  
첫 번째 머지의 `isMerge=true` 상태가 끝나기 전에 두 번째 머지 이벤트가 도착해  
`if (isMerge) return`으로 무시되었다.

**실패 원인 3 (재시도 코루틴의 한계)**:  
`RetryMerge` 코루틴으로 50ms 후 재시도하는 방식을 추가했으나,  
재시도 도중 스냅샷이 또 도착해 위치를 덮어씌우는 문제가 반복되었다.

---

## 5. 최종 해결책

### 5.1 설계 전환: "거울(Mirror)" 모델

**핵심 원칙 변경**:

```
[기존] 상대방 화면 = 독립 물리 시뮬레이션 + 이벤트 보정
[변경] 상대방 화면 = 순수 디스플레이 (물리 없음, 이벤트 그대로 표시)
```

상대방 영역의 과일은 **물리적 존재가 아니라 시각적 복사본**이다.  
자체 물리 계산을 일절 하지 않고, 오직 수신된 위치 데이터만 반영한다.

### 5.2 구현 내용

**Rigidbody2D 비활성화**
```csharp
// CreateOtherFruit()
Rigidbody2D rb = go.GetComponent<Rigidbody2D>();
rb.simulated = false; // 물리 완전 비활성

Collider2D col = go.GetComponent<Collider2D>();
col.enabled = false;  // 충돌 감지도 비활성 (과일끼리 밀어내지 않음)
```

**위치 보간 (Fruit.cs Update)**
```csharp
void Update()
{
    if (isOtherPlayerFruit && hasNetworkTarget)
    {
        transform.localPosition = Vector3.Lerp(
            transform.localPosition, networkTargetLocalPos, Time.deltaTime * LERP_SPEED);
        // 회전도 동일하게 보간
    }
}
```

**스냅샷 수신 시 보간 타겟만 갱신**
```csharp
// ApplyFruitSnapshot()
fruit.networkTargetLocalPos = new Vector3(x, y, 0f);
fruit.networkTargetRot      = rot;
fruit.hasNetworkTarget      = true;
// → Fruit.Update()가 매 프레임 부드럽게 따라감
```

**머지 즉시 처리 (애니메이션 제거)**
```csharp
// ApplyNetworkMerge()
// other 과일 즉시 파괴
otherFruit.FruitGameObject.SetActive(false);
Destroy(otherFruit.FruitGameObject);

// 자신 레벨업 (위치는 스냅샷이 담당)
gameObject.GetComponent<SpriteRenderer>().sprite = fruitSprite[newLevel - 1];
this.level = newLevel;
```

**스냅샷 주기 단축 + Unreliable 전송**
```
0.5초 Reliable → 0.1초 Unreliable
```
0.1초 간격이면 유실되어도 즉시 다음 패킷이 보정한다.  
Reliable의 재전송 대기 시간보다 Unreliable의 빠른 재도착이 더 효과적이다.

### 5.3 결과

| 항목 | 변경 전 | 변경 후 |
|------|---------|---------|
| 물리 떨림 | 발생 | 없음 |
| 연속 머지 오작동 | 발생 | 없음 |
| 과일 위치 불일치 | 누적 발산 | Lerp 보간으로 0.1초 내 수렴 |
| 코드 복잡도 | 높음 (RetryMerge 코루틴 등) | 낮음 |

---

## 6. 교훈 및 향후 적용 지침

### 6.1 물리 기반 멀티플레이어의 원칙

> **물리 엔진은 결정론적이지 않다.**  
> 두 클라이언트가 동일한 물리 시뮬레이션을 독립 실행하면 반드시 발산한다.

해결 방향은 크게 두 가지다:

| 방식 | 설명 | 적합한 게임 |
|------|------|------------|
| **권위 서버(Authoritative Server)** | 서버 1곳만 물리 계산. 클라이언트는 결과만 표시. | FPS, 격투게임 |
| **거울 모델(Mirror Display)** | 한 클라이언트가 계산. 나머지는 표시만. | 스코어 비교형 (본 게임) |

본 게임처럼 **각자 독립된 필드에서 점수를 겨루는 구조**에서는  
거울 모델이 구현 단순도와 정확성 양면에서 최적이다.

### 6.2 Photon PUN2 이벤트 전략

| 이벤트 종류 | 전송 방식 | 이유 |
|------------|----------|------|
| 생성/드롭/머지 | `SendReliable` | 유실 시 복구 불가능한 상태 변화 |
| 위치 스냅샷 | `SendUnreliable` | 유실돼도 다음 패킷이 즉시 보정 가능 |

### 6.3 이 문서에서 기록된 안티패턴

- **물리 ON 상태에서 위치 강제 적용**: 물리와 외부 위치 지정이 충돌하여 떨림 발생
- **머지 중 스냅샷 제외 후 복원**: 제외 타이밍과 이벤트 순서가 맞지 않아 불안정
- **RetryMerge 코루틴**: 재시도 도중 다른 스냅샷 간섭으로 상태 오염
- **Reliable 스냅샷**: 재전송 대기 시간이 오히려 순서 역전 가능성 증가

---

## 7. 관련 파일

| 파일 | 주요 변경 내용 |
|------|--------------|
| `Assets/Scripts/Fruit.cs` | `isOtherPlayerFruit`, `dropId`, `networkTargetLocalPos`, `ApplyNetworkMerge()` |
| `Assets/Scripts/FruitManager.cs` | `OnFruitCreated`, `OnFruitMerged`, `GetFruitStates()`, `NotifyMerge()` |
| `Assets/Scripts/Multiplayer/MultiGameManager.cs` | 전체 동기화 로직, 스냅샷 송수신, 거울 모델 구현 |
| `Assets/Scripts/GameOverLine.cs` | `isOtherPlayerLine` 플래그로 상대방 영역 게임오버 분리 |

---

*본 문서는 개발 과정에서 실제로 겪은 문제를 기반으로 작성되었으며,  
이후 유사 기능 개발 시 동일한 시행착오를 반복하지 않기 위한 내부 기술 참고 자료입니다.*
