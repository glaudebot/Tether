# Tether MVP Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Claude 데스크톱이 파일로 요청 → Tether가 Gemini 데스크톱에 입력·결과 회수 → Claude에 도착 알림, 그 흐름을 도킹된 호스트 창에서 시각화한다.

**Architecture:** `Tether.Core`(파싱·패킷 저장·라우팅, 순수 로직)를 중심에 두고, `Tether.Automation`(UIA·클립보드·폴더 감시)이 앱과 연결한다. 헤드리스 `Tether.Cli`로 연결부터 검증한 뒤, `Tether.Host`(WPF)가 도킹과 Link View를 얹는다.

**Tech Stack:** C# / .NET 10, WPF + CommunityToolkit.Mvvm, FlaUI.UIA3, System.Text.Json, xUnit

**Spec:** `PROJECT_OUTLINE.md`

## Global Constraints

- .NET 10 (`net10.0`, Host/Automation은 `net10.0-windows`)
- `Tether.Core`는 UI·Win32·FlaUI를 참조하지 않는다
- P/Invoke(`DllImport`/`LibraryImport`)는 `src/Tether.Automation/Native/`에만
- 앱 식별자·UIA 셀렉터·타임아웃은 `config/tether.json`에만 (하드코딩 금지)
- 실행 1회당 패킷 상한 기본값 `20`
- `packets/` 아래 파일은 Tether만 쓰고, 항상 `*.tmp` → rename
- 패킷 폴더명은 `D6` 숫자(`000001`)
- 도킹한 창은 어떤 종료 경로에서도 원래 위치로 복원
- 클립보드 작업은 전용 STA 스레드에서 수행하고, 작업 전 백업·작업 후 복원
- 커밋은 사용자가 요청할 때만 한다 (각 Task 끝의 커밋 단계는 "커밋 준비 완료" 보고로 대체)

## Review Focus

1. 요청 파일을 Claude가 다 쓰기 전에 읽는 경우 → 잠금이 풀릴 때까지 재시도 후 전체 내용을 읽어야 한다 (Task 3)
2. 형식이 틀린 요청 / 없는 앱 이름 → 크래시 없이 `Failed` 패킷과 이유가 담긴 실패 알림 (Task 5)
3. Tether 재시작 시 기존 패킷 → Seq가 이어지고 덮어쓰지 않는다 (Task 4)
4. Gemini 창이 닫혀 있거나 못 찾음 → 명확한 오류로 `Failed` (Task 5 단위 테스트 + Task 7 실제 확인)
5. 사용자가 Claude 입력창에 타이핑 중 → 알림이 입력 내용을 덮어쓰지 않는다 (Task 7 실제 확인)

---

## M0 — 스파이크 (go / no-go)

### Task 1: Gemini UIA 스파이크

**Files:**
- Create: `spikes/GeminiSpike/GeminiSpike.csproj`, `spikes/GeminiSpike/Program.cs`
- Create: `docs/uia-notes.md`

**Interfaces:**
- Produces: `docs/uia-notes.md`의 Gemini 항목 (프로세스명, 창 클래스, 셀렉터 4개 `input`/`stopButton`/`lastResponse`/`imageDownload`, 이미지 회수 방식). Task 6이 이 값을 그대로 쓴다.

- [ ] **Step 1:** Inspect.exe / Accessibility Insights로 Gemini 데스크톱의 프로세스명·창 클래스와 입력창·Stop 버튼·마지막 응답·이미지 다운로드(또는 복사) 버튼의 AutomationId / Name / ControlType을 확인해 `docs/uia-notes.md`에 기록
- [ ] **Step 2:** `dotnet new console`(net10.0-windows) + FlaUI.UIA3. `GeminiSpike.exe "<prompt>" <outDir>`가 창 찾기 → 클립보드 붙여넣기 → Enter → 완료 감지(Stop 소멸 + 3초 무변화) → 이미지 회수 → `<outDir>`에 png 저장
- [ ] **Step 3:** 실행 확인

Run: `dotnet run --project spikes/GeminiSpike -- "빨간 사과 한 개, 흰 배경 사진" C:\Temp\spike`
Expected: Gemini에 프롬프트가 입력되고, 생성 후 `C:\Temp\spike\*.png`가 생기며, 콘솔에 `DONE <path>` 출력

- [ ] **Step 4:** 한글 프롬프트, 생성 중 다른 창으로 포커스가 옮겨간 경우 결과를 `docs/uia-notes.md`에 기록. 이미지 회수가 불가능하면 **여기서 멈추고 사용자에게 보고**

### Task 2: Claude 입력·파일 스파이크

**Files:**
- Create: `spikes/ClaudeSpike/ClaudeSpike.csproj`, `spikes/ClaudeSpike/Program.cs`
- Modify: `docs/uia-notes.md` (Claude 항목 추가)

**Interfaces:**
- Produces: `docs/uia-notes.md`의 Claude 항목 (프로세스명, `input` 셀렉터, 입력창이 비었는지 판별하는 방법). Task 6이 쓴다.

- [ ] **Step 1:** Claude 데스크톱 입력창의 셀렉터와 현재 입력 내용을 읽는 방법(ValuePattern / TextPattern)을 확인해 기록. UIA 트리가 비어 있으면 `--force-renderer-accessibility`로 재실행해 확인
- [ ] **Step 2:** `ClaudeSpike.exe "<text>"`가 입력창이 비었는지 출력한 뒤, 클립보드 붙여넣기 + Enter
- [ ] **Step 3:** 실행 확인

Run: `dotnet run --project spikes/ClaudeSpike -- "[Tether] 스파이크 테스트입니다. '확인'이라고만 답해줘"`
Expected: Claude에 메시지가 전송되고 Claude가 답한다. 실행 전 클립보드 내용이 실행 후에도 그대로다

- [ ] **Step 4:** Claude 데스크톱에 Filesystem 확장을 켜고 허용 경로를 `C:\Tether\exchange`로 지정. Claude에게 "`C:\Tether\exchange\outbox\claude\test.md`에 `to: gemini` 첫 줄과 아무 문장을 써줘", "Task 1에서 저장한 png를 열어서 무엇이 보이는지 말해줘"를 요청해 둘 다 되는지 기록
- [ ] **Step 5 (게이트):** Task 1·2 결과를 사용자에게 보고하고 M1 진행 여부를 확인받는다

---

## M1 — Core (TDD)

### Task 3: 솔루션 골격 + 요청 파싱

**Files:**
- Create: `Tether.sln`, `src/Tether.Core/Tether.Core.csproj`, `tests/Tether.Core.Tests/Tether.Core.Tests.csproj`
- Create: `src/Tether.Core/AppRequest.cs`, `src/Tether.Core/RequestParser.cs`, `src/Tether.Core/RequestFile.cs`
- Test: `tests/Tether.Core.Tests/RequestParserTests.cs`, `tests/Tether.Core.Tests/RequestFileTests.cs`

**Interfaces:**
- Produces:
  - `record AppRequest(string From, string To, string Body)`
  - `static AppRequest RequestParser.Parse(string from, string content)` — 실패 시 `RequestFormatException(string message)`
  - `static Task<string> RequestFile.ReadWhenReadyAsync(string path, int attempts = 10, TimeSpan? delay = null)` — 기본 지연 200ms, `IOException`이면 재시도, 다 실패하면 마지막 예외를 던짐

- [ ] **Step 1: 실패하는 테스트 작성**

```csharp
[Fact] void Parse_Valid() {
    var r = RequestParser.Parse("claude", "to: Gemini\n고양이 그림\n두 번째 줄");
    Assert.Equal(new AppRequest("claude", "gemini", "고양이 그림\n두 번째 줄"), r);
}
[Fact] void Parse_BomCrlfAndLeadingBlankLines() {
    var r = RequestParser.Parse("claude", "\uFEFF\r\n\r\nTO:gemini\r\n본문\r\n");
    Assert.Equal("gemini", r.To); Assert.Equal("본문", r.Body);
}
[Fact] void Parse_MissingToLine() =>
    Assert.Equal("첫 줄은 'to: <앱id>' 형식이어야 합니다",
        Assert.Throws<RequestFormatException>(() => RequestParser.Parse("claude", "그냥 본문")).Message);
[Fact] void Parse_EmptyBody() =>
    Assert.Equal("요청 본문이 비어 있습니다",
        Assert.Throws<RequestFormatException>(() => RequestParser.Parse("claude", "to: gemini\n  \n")).Message);

[Fact] async Task ReadWhenReady_WaitsForLockRelease() {
    // 파일을 FileShare.None으로 연 채 300ms 뒤 "to: gemini\n완성본"을 쓰고 닫는다
    // ReadWhenReadyAsync 결과 == "to: gemini\n완성본"
}
```

- [ ] **Step 2:** `dotnet test` → 컴파일 실패 확인
- [ ] **Step 3:** 구현. 규칙: BOM 제거, 줄바꿈 정규화(`\n`), 빈 줄 건너뛴 첫 줄이 `^to:\s*(\S+)\s*$`(대소문자 무시), To는 소문자, Body는 나머지를 Trim
- [ ] **Step 4:** `dotnet test` → 전부 PASS
- [ ] **Step 5:** 커밋 준비 완료 보고

### Task 4: PacketStore

**Files:**
- Create: `src/Tether.Core/PacketStatus.cs`, `src/Tether.Core/PacketInfo.cs`, `src/Tether.Core/AppReply.cs`, `src/Tether.Core/PacketStore.cs`
- Test: `tests/Tether.Core.Tests/PacketStoreTests.cs`

**Interfaces:**
- Consumes: 없음
- Produces:
  - `enum PacketStatus { Queued, Delivering, Generating, Replied, Notified, Failed }`
  - `record PacketInfo(long Seq, string From, string To, PacketStatus Status, string? Error, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, IReadOnlyList<string> ReplyFiles)`
  - `record AppReply(string? Text, IReadOnlyList<string> ImagePaths)`
  - `PacketStore(string exchangeRoot, TimeProvider clock)`
    - `PacketInfo Create(string from, string to, string rawRequest)` → `packets/{seq:D6}/request.md` + `status.json`(Queued)
    - `PacketInfo SetStatus(long seq, PacketStatus status, string? error = null)`
    - `PacketInfo SaveReply(long seq, AppReply reply)` → `reply.md`(Text가 있으면), 이미지를 `reply-{i}{원래 확장자}`로 복사(i는 1부터). 상태는 바꾸지 않음
    - `PacketInfo Get(long seq)`, `IReadOnlyList<PacketInfo> List()`(Seq 오름차순), `string GetPacketDir(long seq)`
  - `status.json`은 `PacketInfo`를 camelCase + enum 문자열로 직렬화

- [ ] **Step 1: 실패하는 테스트 작성** (임시 폴더 + `FakeTimeProvider`)

```csharp
[Fact] void Create_First_IsSeq1_WritesFiles() {
    var p = store.Create("claude", "gemini", "to: gemini\n사과");
    Assert.Equal(1, p.Seq); Assert.Equal(PacketStatus.Queued, p.Status);
    Assert.Equal("to: gemini\n사과", File.ReadAllText(Path.Combine(root, "packets", "000001", "request.md")));
    Assert.Equal(p, store.Get(1));
}
[Fact] void Create_AfterRestart_ContinuesSeq() {
    // 기존 store로 2개 생성 → 같은 root로 new PacketStore → Create().Seq == 3, 000001 내용 불변
}
[Fact] void SetStatus_UpdatesStatusErrorAndTime() {
    // clock 5초 진행 후 SetStatus(1, Failed, "x") → Status Failed, Error "x", UpdatedAt == CreatedAt + 5s
}
[Fact] void SaveReply_WritesTextAndCopiesImages() {
    var p = store.SaveReply(1, new AppReply("설명", [tmpPng, tmpJpg]));
    Assert.Equal(["reply.md", "reply-1.png", "reply-2.jpg"], p.ReplyFiles);
}
[Fact] void Writes_LeaveNoTmpFiles() =>
    Assert.Empty(Directory.GetFiles(Path.Combine(root, "packets"), "*.tmp", SearchOption.AllDirectories));
```

- [ ] **Step 2:** `dotnet test` → FAIL 확인
- [ ] **Step 3:** 구현. Seq는 생성자에서 `packets/` 하위 6자리 폴더명의 최댓값으로 초기화하고, `Create`는 `lock`으로 증가
- [ ] **Step 4:** `dotnet test` → PASS
- [ ] **Step 5:** 커밋 준비 완료 보고

### Task 5: Router

**Files:**
- Create: `src/Tether.Core/IResponder.cs`, `src/Tether.Core/INotifier.cs`, `src/Tether.Core/Router.cs`
- Test: `tests/Tether.Core.Tests/RouterTests.cs` (+ 같은 파일 안에 `FakeResponder`, `FakeNotifier`)

**Interfaces:**
- Consumes: Task 3 `RequestParser`, Task 4 `PacketStore` / `PacketInfo` / `AppReply`
- Produces:
  - `interface IResponder { string AppId { get; } Task DeliverAsync(string prompt, CancellationToken ct); Task<AppReply> CollectReplyAsync(CancellationToken ct); }`
  - `interface INotifier { string AppId { get; } Task NotifyAsync(string message, CancellationToken ct); }`
  - `Router(PacketStore store, IEnumerable<IResponder> responders, IEnumerable<INotifier> notifiers, int maxPacketsPerRun = 20)`
    - `Task<PacketInfo> SubmitAsync(string from, string rawContent, CancellationToken ct)` — 예외를 밖으로 던지지 않음(취소 제외)
    - `event Action<PacketInfo>? PacketChanged` — 상태가 바뀔 때마다 발생
  - 알림 문구 (정확히):
    - 성공: `[Tether] #{seq:D6} {to} 응답 도착: {packetDir} ({files})` — files는 `ReplyFiles`를 `", "`로 연결, 비어 있으면 `응답 파일 없음`
    - 실패: `[Tether] #{seq:D6} 실패: {error}`

- [ ] **Step 1: 실패하는 테스트 작성**

```csharp
[Fact] async Task Submit_Valid_FlowsToNotified() {
    // responder("gemini")는 AppReply(null, [tmpPng]) 반환
    var p = await router.SubmitAsync("claude", "to: gemini\n사과", default);
    Assert.Equal(PacketStatus.Notified, p.Status);
    Assert.Equal([Queued, Delivering, Generating, Replied, Notified], changedStatuses);
    Assert.Equal("사과", gemini.Prompts.Single());
    Assert.Equal($"[Tether] #000001 gemini 응답 도착: {store.GetPacketDir(1)} (reply-1.png)", claude.Messages.Single());
}
[Fact] async Task Submit_BadFormat_FailsAndNotifies() {
    // "그냥 본문" → Failed, To == "?", 알림 "[Tether] #000001 실패: 첫 줄은 'to: <앱id>' 형식이어야 합니다"
}
[Fact] async Task Submit_UnknownApp_Fails() { /* "to: dalle\nx" → Error "알 수 없는 앱: dalle" */ }
[Fact] async Task Submit_ResponderThrows_Fails() {
    // CollectReplyAsync가 InvalidOperationException("gemini 창을 찾을 수 없습니다") → Error 동일, 실패 알림 발송
}
[Fact] async Task Submit_OverLimit_Fails() {
    // maxPacketsPerRun = 1, 두 번째 Submit → Error "패킷 상한 초과 (1)", gemini.Prompts.Count == 1
}
[Fact] async Task Submit_NotifierThrows_StaysReplied() {
    // claude.NotifyAsync가 예외("x") → Status Replied, Error "알림 실패: x", 응답 파일은 남아 있음
}
[Fact] async Task Submit_SameTarget_IsSerialized() {
    // 두 SubmitAsync 동시 실행, FakeResponder가 동시 실행 수 최댓값을 기록 → 1
}
```

- [ ] **Step 2:** `dotnet test` → FAIL 확인
- [ ] **Step 3:** 구현. 대상 앱별 `SemaphoreSlim(1)`로 Delivering~Replied 구간을 직렬화. 파싱 전에 패킷을 만들어 실패도 기록. 요청 앱의 notifier가 없으면 알림 없이 종료
- [ ] **Step 4:** `dotnet test` → PASS
- [ ] **Step 5:** 커밋 준비 완료 보고

---

## M2 — 헤드리스 E2E

### Task 6: 설정 + Automation (GeminiResponder, ClaudeNotifier)

**Files:**
- Create: `src/Tether.Core/Config/TetherConfig.cs`
- Create: `src/Tether.Automation/Tether.Automation.csproj` (net10.0-windows, FlaUI.UIA3, `UseWPF`=true — 클립보드는 `System.Windows.Clipboard`)
- Create: `src/Tether.Automation/Native/User32.cs`, `src/Tether.Automation/Sta.cs`, `src/Tether.Automation/ClipboardScope.cs`, `src/Tether.Automation/UiaFinder.cs`
- Create: `src/Tether.Automation/GeminiResponder.cs`, `src/Tether.Automation/ClaudeNotifier.cs`
- Create: `config/tether.json` (Task 1·2에서 기록한 실제 값)

**Interfaces:**
- Consumes: Task 5 `IResponder`, `INotifier`, `AppReply`; `docs/uia-notes.md`
- Produces:
  - `record TetherConfig(string ExchangeRoot, int MaxPacketsPerRun, IReadOnlyList<AppConfig> Apps)` + `static TetherConfig Load(string path)`
  - `record AppConfig(string Id, string Role, string Process, IReadOnlyDictionary<string, SelectorConfig> Selectors, TimeoutConfig? Timeouts)`
  - `record SelectorConfig(string? AutomationId, string? Name, string? ControlType, string? ClassName)`
  - `record TimeoutConfig(int ResponseSec = 180, int StableSec = 3)`
  - `static T Sta.Run<T>(Func<T> f)` — 전용 STA 스레드에서 실행
  - `GeminiResponder(AppConfig cfg) : IResponder`, `ClaudeNotifier(AppConfig cfg) : INotifier`
  - 오류 문구: 창이 없으면 `"{id} 창을 찾을 수 없습니다"`, 시간 초과는 `"{id} 응답 시간 초과 ({ResponseSec}초)"`

- [ ] **Step 1:** 스파이크 코드를 위 클래스로 옮기고, 셀렉터·타임아웃을 `AppConfig`에서 읽게 바꾼다
- [ ] **Step 2:** `ClaudeNotifier`: 붙여넣기 전 입력창이 비었는지 확인하고, 비어 있지 않으면 2초 간격으로 최대 60초 대기한 뒤 `"claude 입력창이 사용 중입니다"`로 실패
- [ ] **Step 3:** `dotnet build` → 경고 없이 성공

### Task 7: OutboxWatcher + Tether.Cli + PROTOCOL.md

**Files:**
- Create: `src/Tether.Automation/OutboxWatcher.cs`, `src/Tether.Core/ProtocolDoc.cs`
- Create: `src/Tether.Cli/Tether.Cli.csproj`, `src/Tether.Cli/Program.cs`

**Interfaces:**
- Consumes: Task 3 `RequestFile`, Task 4 `PacketStore`, Task 5 `Router`, Task 6 전부
- Produces:
  - `OutboxWatcher(string exchangeRoot, Func<string from, string content, Task> onRequest) : IDisposable` + `void Start()` — `outbox/*/*.md`를 감시하고, 시작 시 이미 있는 파일도 처리. `RequestFile.ReadWhenReadyAsync`로 읽은 뒤 원본을 삭제하고 `onRequest` 호출
  - `static void ProtocolDoc.Write(string exchangeRoot, IEnumerable<string> responderIds)` → `exchange/PROTOCOL.md` (요청 형식, 사용 가능한 앱 목록, 알림 문구 예시, "응답이 오면 packets/<seq>/의 파일을 읽어 확인" 안내)
  - CLI:
    - `tether run [--config config/tether.json]` — PROTOCOL.md 작성, Router + Watcher 시작, `PacketChanged`를 `#000001 Delivering` 형식으로 콘솔에 출력, Ctrl+C로 종료
    - `tether test-gemini "<prompt>"`, `tether test-notify "<text>"` — 어댑터를 단독으로 확인

- [ ] **Step 1:** 구현 후 `dotnet build` 성공
- [ ] **Step 2: E2E 확인**

Run: `dotnet run --project src/Tether.Cli -- run`
그 다음 Claude에게: "`C:\Tether\exchange\PROTOCOL.md`를 읽고, gemini에게 '우주복 입은 고양이 수채화' 이미지를 요청한 뒤 알림이 오면 이미지를 확인해서 평가해줘"
Expected: 콘솔에 `Queued → Delivering → Generating → Replied → Notified`가 출력되고, `packets/000001/reply-1.png`가 생기며, Claude가 알림을 받고 이미지를 열어 평가한다

- [ ] **Step 3: Review Focus 확인** — (a) Gemini를 닫은 상태로 요청하면 Claude가 `실패: gemini 창을 찾을 수 없습니다` 알림을 받는다 (b) Claude 입력창에 글자를 쳐둔 상태에서 응답이 도착하면 입력 내용이 보존되고, 지우면 그 뒤에 알림이 들어간다 (c) 형식이 틀린 파일을 outbox에 두면 실패 알림이 온다
- [ ] **Step 4:** 결과를 사용자에게 보고 (M2 완료)

---

## M3 — 호스트 + Follow 도킹

### Task 8: 호스트 셸

**Files:**
- Create: `src/Tether.Host/Tether.Host.csproj`, `App.xaml(.cs)`, `MainWindow.xaml(.cs)`, `app.manifest`(PerMonitorV2), `MainViewModel.cs`

**Interfaces:**
- Consumes: Task 7의 Router/Watcher 조립 방식 (Cli의 `run`과 같은 순서)
- Produces: `MainViewModel.Router`, `MainViewModel.Store`, 슬롯 2개의 `FrameworkElement`(`SlotA`, `SlotB`), 상태바 텍스트 바인딩 `StatusText`

- [ ] **Step 1:** 3열 Grid(Slot A | Link View 자리 | Slot B) + 하단 상태바 + [도킹 해제] 버튼. 시작 시 Cli `run`과 같은 방식으로 Router/Watcher 시작, `PacketChanged` → `StatusText`
- [ ] **Step 2:** 실행 확인 — `dotnet run --project src/Tether.Host`로 Task 7 Step 2 시나리오를 다시 돌려 상태바가 단계별로 바뀐다

### Task 9: Follow 도킹 + 복원

**Files:**
- Create: `src/Tether.Host/Docking/DockManager.cs`, `DockedWindow.cs`, `FollowStrategy.cs`
- Modify: `src/Tether.Automation/Native/User32.cs` (SetWinEventHook, GetAncestor, SetWindowPos, GetWindowRect, IsWindow — `public static`으로 노출, Host가 Automation을 참조)

**Interfaces:**
- Consumes: Task 8 `SlotA`, `SlotB`
- Produces: `DockManager(Window host, IReadOnlyList<FrameworkElement> slots)` + `void UndockAll()`; `event Action<int slot, string? appId>? SlotChanged` (appId는 `tether.json`의 process로 매칭, 모르면 null)

- [ ] **Step 1:** `EVENT_SYSTEM_MOVESIZEEND` 훅 → 루트 HWND + 커서 위치가 슬롯 안이면 도킹. 자기 자신·이미 도킹된 창은 무시
- [ ] **Step 2:** Follow: 호스트 `LocationChanged`/`SizeChanged`/`StateChanged`에 맞춰 `SetWindowPos`로 슬롯 화면 좌표(물리 픽셀)로 이동, 호스트 바로 위 z-order 유지
- [ ] **Step 3:** 복원: 도킹 전 rect 저장. [도킹 해제], `Closing`, `DispatcherUnhandledException`, `AppDomain.UnhandledException`에서 `UndockAll()`. 도킹 창이 종료되면(`IsWindow == false`) 슬롯 비움
- [ ] **Step 4: 실행 확인 (메모장 먼저)** — 메모장 2개를 각 슬롯에 드롭 → 호스트 이동·리사이즈·최소화에 따라옴 → 해제 시 원래 위치 → 다시 도킹 후 호스트 종료 시 원래 위치. 그다음 Claude/Gemini로 같은 확인 + 125%/150% 배율 모니터 확인

---

## M4 — Link View

### Task 10: Link View

**Files:**
- Create: `src/Tether.Host/LinkView/LinkView.xaml(.cs)`, `LinkViewModel.cs`, `PacketItem.cs`
- Modify: `MainWindow.xaml` (가운데 열에 배치)

**Interfaces:**
- Consumes: Task 4 `PacketStore.List()` / `GetPacketDir`, Task 5 `PacketChanged`, Task 9 `SlotChanged`
- Produces: 없음 (최종 UI)

- [ ] **Step 1:** 상단: 앱 노드 2개 + 연결선. 노드는 `SlotChanged`에 따라 도킹됨/비어 있음 표시
- [ ] **Step 2:** 연결선 색: Queued 회색, Delivering·Generating 파랑, Replied·Notified 초록, Failed 빨강. `Delivering`에 요청 앱→대상 앱, `Replied`에 대상 앱→요청 앱 방향으로 점이 흐르는 애니메이션(약 0.8초). `Generating` 동안 대상 노드에 펄스
- [ ] **Step 3:** 하단 타임라인: 시작 시 `List()`로 기존 패킷 로드, 이후 `PacketChanged`로 갱신. 항목 클릭 시 `request.md`, `reply.md`, 이미지 미리보기
- [ ] **Step 4: 실행 확인** — Task 7 Step 2 시나리오에서 점 애니메이션·색 변화·타임라인·미리보기가 보이고, 실패 시나리오에서 빨간 선과 오류 문구가 보인다. 호스트를 다시 켜면 이전 패킷이 타임라인에 남아 있다

---

## M5 — Embed (실험)

### Task 11: Embed 모드

**Files:**
- Create: `src/Tether.Host/Docking/EmbedStrategy.cs`
- Modify: `DockManager.cs` (모드 토글), `docs/uia-notes.md` (결과)

- [ ] **Step 1:** 원래 parent·style·exstyle·rect 저장 → `WS_CAPTION|WS_THICKFRAME` 제거, `WS_CHILD` 추가 → `SetParent(slotHwnd)`. 복원 경로는 Task 9와 동일
- [ ] **Step 2: 실행 확인** — 메모장 → Claude → Gemini 순서로 Embed 도킹, 입력·스크롤·렌더링·Tether 자동화가 정상인지 `docs/uia-notes.md`에 기록. 문제가 있으면 기본값은 Follow로 유지
