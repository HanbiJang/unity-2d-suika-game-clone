<img width="833" height="465" alt="Image" src="https://github.com/user-attachments/assets/9d674e58-4fcc-4c30-b86b-5d653d9c18ce" />

## 🎮 프로젝트 개요

| 항목 | 내용 |
|------|------|
| 장르 | 물리 퍼즐 2D 게임 |
| 엔진 | Unity (C#) |
| 개발 기간 | 2024.04 ~ 2024.05, 2026.04 |
| 개발 인원 | 1인 |
| 핵심 목표 | 물리 기반 과일 합체 시스템 |

---

## ⚙️ 핵심 기술 및 구현 내용

### 1. 물리 기반 과일 합체 시스템 (`Fruit.cs`)

같은 레벨의 과일끼리 충돌 시 합체가 이루어지며, `OnCollisionEnter2D`를 통해 충돌을 감지합니다.
합체 처리 시 중복 실행을 막기 위해 `isMerge` 플래그를 활용하며, 위치 비교(x, y좌표)를 통해 두 과일 중 어느 쪽이 살아남을지 결정합니다.

합체 애니메이션은 `IEnumerator MergeOther`(코루틴)로 구현되어 있으며, 상대 과일을 20프레임에 걸쳐 `Vector3.Lerp`로 끌어당긴 뒤 파괴합니다. 합체 전후로 `Rigidbody2D.simulated`를 `false/true`로 전환해 물리 시뮬레이션을 일시 제어합니다.

- 합체 조건: 동일 레벨, 양측 모두 `isMerge == false`
- 생존 기준: x좌표 비교 시 왼쪽, y좌표 동일 시 아래쪽 과일이 레벨업
- 레벨업 시 스프라이트 및 `localScale` 변경, 최대 레벨 갱신

### 2. 과일 생성 및 드롭 제어 (`FruitManager.cs`)

마우스 클릭 위치를 `CursorControl.cs`에서 월드 좌표로 변환해 과일 드롭 X좌표를 결정합니다. 생성된 과일은 벽 안쪽 경계(`leftBorder`, `rightBorder`)를 초과하지 않도록 클램핑 처리됩니다.

과일 생성 시 `SizeUpAnim` 코루틴으로 0.1 스케일에서 목표 크기까지 점진적으로 확대되는 등장 애니메이션이 적용됩니다. 다음에 나올 과일 미리보기는 `ShowNextFruitModel()`을 통해 별도 위치에 표시됩니다.

- 드롭 후 1초간 `isReady = false` 처리로 연속 투하 방지 (`InitTime1f` 코루틴)
- 사운드 채널 배열(`SoundChannels`)을 순환 사용해 다중 효과음 동시 재생 지원

### 3. 게임 오버 판정 시스템 (`GameOverLine.cs`)

화면 상단에 트리거 영역(`GameOverLine`)을 두고, `OnTriggerStay2D`로 과일이 해당 라인에 닿으면 타이머를 시작합니다. 2초(GAME_OVER_TIME) 동안 과일이 계속 라인에 머물면 게임 오버가 발동됩니다.

현재 드롭 중인 과일(`newFruitGameObject`)은 판정에서 제외해 오판정을 방지하며, 과일이 라인에서 벗어나면 리스트에서 제거해 타이머를 초기화합니다.

### 4. 점수 및 로컬 랭킹 시스템 (`RankingManager.cs`, `RankingNameInputUI.cs`)

점수는 합체 시 `(현재 레벨 × scoreStandard)` 공식으로 산출되어 누적됩니다. 게임 오버 시 현재 점수가 상위 3위 안에 드는지 `RankingManager.IsInTop3()`로 확인하며, 해당되면 이름 입력 UI를 표시합니다.

랭킹 데이터는 `RankingEntry`(이름, 점수) 리스트를 `JsonUtility.ToJson()`으로 직렬화하여 `PlayerPrefs`에 저장합니다. 최대 3개 항목만 유지하며, 점수 내림차순으로 정렬됩니다.
```csharp
// 랭킹 저장 흐름
List<RankingEntry> → 정렬(내림차순) → 상위 3개만 유지 → JsonUtility.ToJson → PlayerPrefs.SetString
```

### 5. 비동기 씬 로딩 + 커튼 트랜지션 (`LoadingSceneController.cs`)

`SceneManager.LoadSceneAsync()`로 백그라운드에서 씬을 로딩하며, `allowSceneActivation = false`로 로딩 완료 후에도 즉시 전환하지 않고 대기합니다. 로딩이 끝나면 커튼 닫힘 애니메이션(`CurtainCloseAnim`)이 재생되고, 마우스 클릭 시 커튼 열림 애니메이션과 함께 씬이 활성화됩니다.

`DontDestroyOnLoad`를 적용해 로딩 컨트롤러가 씬 전환 중에도 파괴되지 않도록 처리했습니다.

### 6. 게임 흐름 제어 (`GameFlowManager.cs`)

게임 오버 발생 시 `Time.timeScale = 0` 대신 모든 과일의 `Rigidbody2D.simulated = false`를 사용해 물리를 일시 정지합니다. 이 방식으로 UI 애니메이션은 정상 작동하면서도 게임 물리만 멈추는 효과를 구현합니다.

랭킹 등록 완료 후 또는 등록 조건 미달 시 각각 다른 콜백(`OnRankingUploadComplete`, `OnSimpleGameOver`)을 거쳐 1초 지연 후 게임 오버 캔버스를 활성화합니다.

---

## 🔑 주요 구현 포인트

- **합체 중복 방지**: `isMerge` 플래그로 동일 과일이 여러 번 합체 처리되는 문제 차단
- **물리 일시 제어**: 합체 애니메이션 중 `Rigidbody2D.simulated = false`로 물리 비활성화 후 완료 시 재활성화
- **게임 오버 판정 유예**: 2초 타이머 기반 오버 판정으로 순간적 접촉에 의한 오판정 방지
- **UI 독립 물리 정지**: `timeScale = 0` 대신 개별 Rigidbody 비활성화로 UI 애니메이션과 게임 물리를 분리
- **사운드 멀티채널**: 배열 기반 사운드 채널 순환 방식으로 효과음 겹침 재생 처리
- **로컬 랭킹 영속성**: JSON 직렬화 + PlayerPrefs를 이용한 상위 3위 랭킹 로컬 저장
- **씬 전환 연출**: 비동기 로딩 + 커튼 애니메이션으로 자연스러운 씬 전환 구현
