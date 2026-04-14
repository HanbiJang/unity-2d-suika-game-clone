# Photon Lobby Match Start Issue

## 개요

`LobbyScene`에서 2인 매칭이 완료된 뒤에도 방장과 참여자 모두 `MultiModeScene`으로 넘어가지 않고, 계속 같은 방에 머무르는 문제가 있었다.

이 문서는 해당 문제의 원인과, 이후 적용한 최종 해결 구조를 정리한 기술 문서다.

## 발생 증상

- `LobbyScene`에서 두 플레이어가 같은 방에 입장한다.
- 매칭 완료 후 커튼 트랜지션이 시작되지 않거나, 시작되더라도 `MultiModeScene`으로 넘어가지 않는다.
- 결과적으로 방장과 참여자 모두 방 안에 그대로 머무르게 된다.

## 초기 원인

초기 구현은 `매치 시작`을 Photon `RaiseEvent` 한 번에만 의존하고 있었다.

```csharp
private IEnumerator DelayedRaiseEvent()
{
    yield return new WaitForSeconds(0.5f);

    PhotonNetwork.RaiseEvent(
        EVT_MATCH_START,
        null,
        new RaiseEventOptions { Receivers = ReceiverGroup.All },
        SendOptions.SendReliable);
}
```

이 구조는 다음 문제가 있었다.

- 이벤트가 단발성이라 타이밍에 따라 일부 클라이언트가 놓칠 수 있다.
- 이벤트를 놓친 클라이언트는 매치 시작 여부를 복구할 방법이 없다.
- 씬 전환까지 각 클라이언트가 직접 책임져야 해서 흐름이 복잡해진다.

## 중간 보완

한 차례 보완으로 아래 두 가지를 추가했다.

- `MatchStarted = true` 룸 커스텀 프로퍼티 저장
- `EVT_MATCH_START`를 `EventCaching.AddToRoomCache`로 캐시 가능하게 전송

이 보완으로 이벤트 유실 가능성은 크게 줄었지만, 구조 자체는 여전히 `각 클라이언트가 직접 씬 전환을 관리`하는 방식이었다.

## 최종 해결 방향

구조를 더 단순하고 안정적으로 바꾸기 위해, 실제 씬 전환 책임을 다시 Photon에 맡기는 방식으로 리팩터링했다.

### 최종 흐름

1. 매칭 완료 신호를 모든 클라이언트가 받는다.
2. 각 클라이언트가 자기 화면에서 커튼 트랜지션을 재생한다.
3. 커튼이 완전히 닫히면 방장만 `PhotonNetwork.LoadLevel("MultiModeScene")`를 호출한다.
4. `PhotonNetwork.AutomaticallySyncScene = true` 상태에서 Photon이 나머지 클라이언트도 자동으로 같은 씬으로 동기화한다.
5. 씬이 로드된 뒤 `LoadingSceneController`가 커튼 오픈 애니메이션을 재생한다.

즉, `연출은 각자 로컬에서`, `실제 씬 전환은 방장 1명이`, `동기화는 Photon이` 담당하도록 역할을 분리했다.

## 왜 이 구조가 더 안정적인가

### 1. 씬 전환 책임이 한 곳으로 모인다

이전 방식은 각 클라이언트가 이벤트를 받고, 로컬에서 씬 전환까지 직접 처리해야 했다.

최종 방식은 다르다.

- 각 클라이언트: 커튼 연출만 담당
- 방장: `PhotonNetwork.LoadLevel()` 호출만 담당
- Photon: 나머지 클라이언트 씬 동기화 담당

이렇게 되면 누가 실제 씬을 여는지 명확해져서 흐름이 단순해진다.

### 2. Photon 내장 씬 동기화 기능을 다시 활용한다

Photon의 `AutomaticallySyncScene`은 원래 방장이 `LoadLevel()`을 호출했을 때 다른 플레이어들을 같은 씬으로 자동 맞춰주는 기능이다.

즉시 씬 전환이 잘 되던 기존 구조도 사실 이 기능 덕분이었다.

이번 리팩터링은 커튼 연출을 유지하면서도, 최종 씬 전환은 다시 이 안정적인 내장 기능을 사용하도록 되돌린 것이다.

### 3. 각자 `LoadLevel()` 하는 구조보다 덜 위험하다

모든 클라이언트가 애니메이션 종료 후 각자 `PhotonNetwork.LoadLevel()`을 호출하는 방식도 가능해 보일 수 있다.

하지만 그 구조는 다음 문제가 있다.

- 클라이언트마다 애니메이션 종료 시점이 미세하게 다를 수 있다.
- 씬 전환 호출 주체가 여러 명이 되어 동기화 책임이 분산된다.
- 타이밍 차이, 프레임 차이, 지연 상황에서 흐름이 다시 꼬일 수 있다.

반면 방장만 `LoadLevel()` 하는 구조는 이런 위험이 훨씬 적다.

## 적용된 코드 구조

### PhotonNetworkManager

- `PhotonNetwork.AutomaticallySyncScene = true`로 변경
- 매치 시작 신호는 계속 Photon 이벤트와 룸 프로퍼티로 안정적으로 전달
- 커튼 연출이 끝났을 때 방장만 `TryLoadMultiModeScene()`에서 `PhotonNetwork.LoadLevel("MultiModeScene")` 호출

### LobbyUI

- `OnMatchFound()`에서 `LoadingSceneController.StartAutoTransitionToMulti(...)` 호출
- 커튼이 닫힌 뒤 콜백에서 `PhotonNetworkManager.TryLoadMultiModeScene()` 실행
- 참가자는 콜백이 실행되어도 `LoadLevel()`을 직접 호출하지 않고 방장의 동기화를 기다림

### LoadingSceneController

- 멀티플레이 전용 흐름에서 로컬 `SceneManager.LoadSceneAsync()`를 사용하지 않음
- 커튼 닫기 애니메이션만 재생한 뒤 콜백 실행
- 새 씬이 실제로 로드되면 `sceneLoaded` 이벤트에서 커튼 열기 애니메이션 재생

## 수정 파일

- [PhotonNetworkManager.cs](/D:/GitRepo/SuikaGame/Suika%20Game/Assets/Scripts/Multiplayer/PhotonNetworkManager.cs)
- [LobbyUI.cs](/D:/GitRepo/SuikaGame/Suika%20Game/Assets/Scripts/Multiplayer/LobbyUI.cs)
- [LoadingSceneController.cs](/D:/GitRepo/SuikaGame/Suika%20Game/Assets/Scripts/LoadingSceneController.cs)

## 검증 포인트

멀티 클라이언트 테스트 시 아래 순서를 확인한다.

1. 방장과 참여자 모두 `OnMatchFound` 로그가 찍힌다.
2. 두 클라이언트 모두 커튼 닫기 애니메이션을 재생한다.
3. 커튼 닫기 종료 후 방장에서만 아래 로그가 찍힌다.

```text
[Photon] 방장이 PhotonNetwork.LoadLevel(MultiModeScene)을 호출합니다.
```

4. 참여자 쪽에서는 아래 로그처럼 대기만 한다.

```text
[Photon] 참가자는 방장의 PhotonNetwork.LoadLevel 동기화를 기다립니다.
```

5. 이후 Photon 자동 씬 동기화로 두 클라이언트 모두 `MultiModeScene`으로 이동한다.
6. 씬 로드 후 커튼 오픈 애니메이션이 재생된다.

## 결론

이번 문제의 본질은 `씬 전환`까지 각 클라이언트가 직접 책임지는 구조가 너무 복잡했다는 점이다.

최종 해결은 다음과 같이 역할을 단순화한 것이다.

- 매치 시작 신호: Photon 이벤트 + 룸 상태로 안정적으로 전달
- 커튼 연출: 각 클라이언트가 로컬에서 실행
- 실제 씬 전환: 방장만 `PhotonNetwork.LoadLevel()` 호출
- 씬 동기화: Photon `AutomaticallySyncScene` 사용

이 구조는 연출은 유지하면서도, 기존의 즉시 씬 전환 구조가 갖고 있던 Photon 내장 동기화의 안정성을 다시 활용할 수 있다는 장점이 있다.
