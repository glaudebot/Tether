# Tether — 프로젝트 아웃라인

> 여러 Windows AI 데스크톱 앱(Claude, Gemini, 추후 Codex·ChatGPT)을 하나의 호스트 창에 **도킹**하고,
> **공유 폴더의 파일**로 앱 사이를 연결(tether)해 텍스트·이미지를 주고받게 하며, 그 **연결과 흐름을 시각화**하는 Windows 전용 프로그램.

---

## 1. 목표

- 호스트 창에 앱 슬롯 2개(MVP)를 두고, 앱 창을 드래그해 놓으면 도킹된다.
- 앱 사이 통신의 공통 규약은 **`exchange/` 폴더의 파일**이다. 외부 API 호출 없음. Tether 자체는 MCP를 구현하지 않는다.
- 가운데 **Link View**에서 앱 간 연결선, 오가는 패킷, 현재 단계를 실시간으로 보여준다.
- 대표 시나리오: Claude가 요청 파일 작성 → Tether가 Gemini에 입력 → Gemini 이미지 생성 → Tether가 결과를 폴더에 저장하고 Claude 입력창에 도착 알림 → Claude가 이미지 확인 → 필요 시 다음 요청 파일 작성.
- 새 앱은 **어댑터 + 설정 항목 추가**만으로 연결한다.

## 2. 비목표 (MVP)

- 클라우드 API 직접 호출, Tether 전용 MCP 서버
- Claude → Gemini 방향의 이미지 첨부 (MVP는 텍스트 요청만)
- UIA 앱이 먼저 요청을 시작하는 흐름 (UIA 앱은 응답만 한다)
- 3개 이상 슬롯, 패킷 편집 UI
- Codex·ChatGPT 어댑터 (MVP 이후)
- macOS / Linux

## 3. 기술 스택

| 영역 | 선택 |
|---|---|
| 언어 / 런타임 | C# / .NET 10 |
| UI | WPF (MVVM, CommunityToolkit.Mvvm) |
| 창 조작 | Win32 P/Invoke: SetWinEventHook, GetAncestor, SetWindowPos, SetParent(실험) |
| UIA 앱 제어 | FlaUI.UIA3 |
| 입력 전송 | 클립보드 + SendInput |
| 테스트 | xUnit (Tether.Core만) |
| DPI | Per-Monitor V2 |

## 4. 앱 연결 방식

| 앱 | 요청 보내기 (앱 → Tether) | 결과 받기 (Tether → 앱) |
|---|---|---|
| **Claude** | 공식 **Filesystem 확장**(허용 경로 = `exchange/`)으로 `outbox/claude/*.md` 작성 | Tether가 Claude 입력창에 **도착 알림 문장**을 붙여넣고 Enter. Claude가 Filesystem 확장으로 결과 파일을 읽음 |
| **Gemini** | (요청 시작 안 함) | Tether가 입력창에 프롬프트 붙여넣기 + Enter → 완료 감지 → 텍스트·이미지 회수 |
| Codex (추후) | 기본 파일 쓰기 기능으로 `outbox/codex/` 작성 | Claude와 같은 알림 방식 |

- 앱 역할은 두 가지다. **Requester**(파일로 요청하고 알림을 받음)와 **Responder**(Tether가 입력하고 결과를 회수함).
- 앱별 식별자·셀렉터·타임아웃은 `config/tether.json`에 둔다.

```json
{
  "exchangeRoot": "C:\\Tether\\exchange",
  "maxPacketsPerRun": 20,
  "apps": [
    { "id": "claude", "role": "requester", "process": "TBD",
      "selectors": { "input": {} } },
    { "id": "gemini", "role": "responder", "process": "TBD",
      "selectors": { "input": {}, "stopButton": {}, "lastResponse": {}, "imageDownload": {} },
      "timeouts": { "responseSec": 180, "stableSec": 3 } }
  ]
}
```

> `TBD` 값은 M0에서 Inspect.exe / Accessibility Insights로 확인해 채우고 `docs/uia-notes.md`에 기록한다.

## 5. Exchange 프로토콜

```
exchange/
├─ PROTOCOL.md                 # Tether가 시작 시 생성. Claude에게 읽히는 사용 설명서
├─ outbox/claude/*.md          # Requester가 쓰는 요청 (파일명 자유)
└─ packets/000001/             # Tether만 쓴다
   ├─ request.md               # 처리한 요청 원문
   ├─ status.json              # 상태
   ├─ reply.md                 # 응답 텍스트 (있으면)
   └─ reply-1.png              # 응답 이미지 (있으면)
```

**요청 파일 형식** (첫 줄만 규칙, 나머지는 본문):
```
to: gemini
고양이가 우주복을 입고 달 위를 걷는 수채화 일러스트, 16:9
```

**규칙**
- 보낸 앱은 파일이 놓인 폴더 이름(`outbox/<from>/`)으로 정한다.
- Tether는 요청 파일을 읽은 뒤 `packets/<seq>/request.md`로 옮긴다. outbox는 비워진다.
- `packets/`의 모든 파일은 **Tether만 쓴다**. 쓰기는 `*.tmp`에 쓴 뒤 rename한다.
- Seq는 기존 패킷의 최댓값 + 1이다. 재시작해도 이어진다.
- 패킷 폴더가 곧 세션 로그다. Link View는 이 폴더만 읽어도 재생할 수 있다.

**상태 전이**
```
Queued → Delivering → Generating → Replied → Notified
   └──────────┴───────────┴──────────┴──→ Failed (error 기록, 요청 앱에 실패 알림)
```

**도착 알림 문장** (Claude 입력창에 붙여넣는 정확한 문구)
```
[Tether] #000001 gemini 응답 도착: C:\Tether\exchange\packets\000001 (reply.md, reply-1.png)
```
```
[Tether] #000001 실패: <error>
```

## 6. 아키텍처

```
┌──────────────────────── Tether Host (WPF) ────────────────────────┐
│ ┌─ Slot A ──────┐  ┌──── Link View ────┐  ┌─ Slot B ──────┐ │
│ │ Claude (도킹)  │  │ [Claude]══●══[Gemini] │  │ Gemini (도킹)  │ │
│ │               │  │ 타임라인 / 미리보기  │  │               │ │
│ └───────────────┘  └───────────────────┘  └───────────────┘ │
│ 상태: 대기 → 전달 → 생성 중 → 응답 → 알림       [도킹 해제]   │
└───────────────────────────────────────────────────────────────────┘

OutboxWatcher ─▶ RequestParser ─▶ Router ─▶ IResponder (GeminiUia)
                                    │  └──▶ INotifier (ClaudePaste)
                                    ├──▶ PacketStore (packets/)
                                    └──▶ PacketChanged 이벤트 ─▶ Link View
```

### 폴더 구조

```
Tether/
├─ Tether.sln
├─ src/
│  ├─ Tether.Core/          # 요청 파싱, PacketStore, Router (UI·Win32 의존 없음)
│  ├─ Tether.Automation/    # UIA·클립보드·Native: GeminiResponder, ClaudeNotifier, OutboxWatcher
│  ├─ Tether.Cli/           # 헤드리스 실행 (UI 없이 E2E 확인용)
│  └─ Tether.Host/          # WPF: 슬롯, 도킹, Link View
├─ tests/Tether.Core.Tests/
├─ spikes/                  # M0 스파이크 콘솔 앱
└─ docs/uia-notes.md
```

## 7. 핵심 인터페이스

```csharp
public interface IResponder
{
    string AppId { get; }
    Task DeliverAsync(string prompt, CancellationToken ct);
    Task<AppReply> CollectReplyAsync(CancellationToken ct);   // 완료 대기 + 회수
}

public interface INotifier
{
    string AppId { get; }
    Task NotifyAsync(string message, CancellationToken ct);
}

public record AppReply(string? Text, IReadOnlyList<string> ImagePaths); // 임시 경로, PacketStore가 복사
```

## 8. 모듈 상세

### 8.1 GeminiResponder (UIA)
- **전달**: 입력창 포커스 → 클립보드 백업 → 붙여넣기 → Enter → 클립보드 복원.
- **완료 감지**: Stop 버튼 소멸 **그리고** 응답 영역이 `stableSec` 동안 변화 없음. `responseSec` 초과 시 실패.
- **이미지 회수**: M0에서 정한 방식(다운로드 버튼 + 다운로드 폴더 감시, 또는 이미지 복사 + 클립보드).

### 8.2 ClaudeNotifier
- 입력창을 찾아 포커스 → 클립보드로 알림 문장 붙여넣기 → Enter → 클립보드 복원.

### 8.3 Router
- 대상 앱별로 한 번에 패킷 하나만 처리한다(앱별 직렬화).
- 실행 1회당 `maxPacketsPerRun` 초과 시 요청을 `Failed`("패킷 상한 초과")로 기록한다.
- 알림 실패는 응답을 잃지 않는다. 상태는 `Replied`로 유지하고 오류를 기록한다.

### 8.4 DockManager
- `EVENT_SYSTEM_MOVESIZEEND` 훅으로 드롭을 감지하고, 커서가 슬롯 안이면 도킹한다.
- **Follow 모드(기본)**: 호스트 이동·리사이즈·최소화에 맞춰 앱 창을 슬롯 위치로 따라 붙인다.
- **Embed 모드(실험)**: SetParent.
- **불변 조건**: 해제·종료·예외 등 어떤 경로에서도 원래 위치·스타일로 복원한다.

### 8.5 Link View
- 노드(앱), 엣지(연결), 엣지 위를 흐르는 패킷 점 애니메이션, 상태별 엣지 색.
- 하단 타임라인에서 패킷을 클릭하면 요청 원문과 응답 텍스트·이미지를 미리 본다.

## 9. 마일스톤

| # | 범위 | 완료 기준 |
|---|---|---|
| **M0** | 스파이크 3개 | ① Gemini에 프롬프트 전송 → 완료 감지 → 이미지 파일 저장 ② Claude 입력창에 문장 붙여넣기 + Enter ③ Claude가 Filesystem 확장으로 `outbox/` 파일을 쓰고 png를 열어 본다. **하나라도 실패하면 설계를 재검토한다** |
| M1 | Core (TDD) | 요청 파싱, PacketStore, Router 단위 테스트 통과 |
| M2 | 헤드리스 E2E | `Tether.Cli` 실행 상태에서 Claude 요청 → Gemini 이미지 → Claude 알림 → Claude 확인까지 사람 개입 없이 1회 왕복 |
| M3 | 호스트 + Follow 도킹 | 메모장으로 도킹·리사이즈·해제·종료 복원 확인 후 Claude/Gemini로 확인 |
| M4 | Link View | 왕복이 연결선 애니메이션과 타임라인으로 보이고, 패킷 클릭 시 미리보기 |
| M5 | Embed (실험) | Claude/Gemini 각각 동작 여부를 기록 |

## 10. 리스크 & 대응

| 리스크 | 대응 |
|---|---|
| Gemini UIA 접근 불가 / 이미지 회수 불가 | M0에서 먼저 검증 |
| Claude가 요청 형식을 틀림 | 형식을 한 줄 규칙으로 최소화, `PROTOCOL.md` 제공, 실패 시 이유를 알림으로 돌려줌 |
| 사용자가 Claude 입력 중일 때 알림 붙여넣기 | 알림 직전 입력창이 비었는지 확인, 비어 있지 않으면 2초 간격으로 최대 60초 대기 |
| 요청 파일을 다 쓰기 전에 읽음 | 읽기 실패나 크기 변화 시 재시도 |
| 앱 업데이트로 셀렉터 변경 | 셀렉터를 설정 파일로 분리, 실패 시 명확한 오류 |
| 클립보드 덮어쓰기 | 백업 후 복원 |
| 폭주(사용량 소모) | 실행당 패킷 상한 20 |
| Electron 앱 SetParent 깨짐 | Follow 모드를 기본으로 |
| 소비자 앱 UI 자동화 약관 | 개인용 도구로 한정 |

## 11. 작업 규칙

- Windows에서 `dotnet build` / `dotnet test` / `dotnet run`으로 확인하며 진행한다.
- 마일스톤 순서대로, 완료 기준을 직접 실행해 확인한 뒤 넘어간다.
- TDD는 `Tether.Core`만 적용한다. UI·도킹·UIA는 실제 실행으로 확인한다.
- P/Invoke는 `Native/`에만 둔다. 셀렉터·식별자·타임아웃은 하드코딩하지 않는다.
- 도킹한 창은 어떤 종료 경로에서도 복원한다.
