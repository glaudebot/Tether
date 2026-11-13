# Tether

여러 Windows AI 데스크톱 앱(Claude, Gemini)을 한 창에 **도킹**하고, **공유 폴더의 파일**로 앱 사이를 이어 주는 Windows 전용 프로그램입니다.
API나 MCP 서버 없이 화면 UI 자동화(UIA)와 클립보드만 씁니다.

```
Claude / Claude Code ──(요청 파일)──▶ Tether ──(붙여넣기)──▶ Gemini
        ▲                              │
        └──(도착 알림 / 결과 파일)◀──────┘  이미지 저장 → packets/<번호>/
```

## 기능

- **도킹**: 앱 창을 Tether의 슬롯 A/B에 끌어다 놓으면 Tether 창을 따라 움직입니다. 해제·종료 시 원래 위치로 돌아갑니다.
  세로로 긴 창에서는 A / 연결 화면 / B 순서로 쌓입니다. `Embed (실험)`을 켜면 창을 자식 창으로 넣습니다.
- **연결**: Requester 앱이 `outbox/<앱>/*.md` 파일을 쓰면 Tether가 Gemini 입력창에 붙여 넣고, 생성이 끝나면 이미지를 원본 크기로 저장합니다.
- **Link View**: 가운데에 앱 사이 연결선, 오가는 패킷, 상태 색, 타임라인을 보여 줍니다. 타임라인을 누르면 요청 원문과 응답 이미지를 미리 봅니다.

## 요구 사항

- Windows 10/11, [.NET 10 SDK](https://dotnet.microsoft.com/)
- Gemini 데스크톱 앱, Claude 데스크톱 앱(알림을 받을 때) 또는 Claude Code
- UI 셀렉터는 한국어 UI 기준입니다. 다른 언어면 `config/tether.json`의 이름을 바꿔야 합니다(`docs/uia-notes.md` 참고).

## 설치

개발용 실행:

```bash
dotnet build
dotnet test
src/Tether.Host/bin/Debug/net10.0-windows/tether-host.exe
```

평소에 쓸 exe 하나로 묶기(설정 파일 `config\tether.json`이 옆에 함께 나옵니다):

```bash
dotnet publish src/Tether.Host -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -p:DebugType=None -o C:\Tether\app
```

`C:\Tether\app\tether-host.exe`의 바로가기를 바탕화면에 만들어 두면 됩니다. 설정을 바꾸려면 `C:\Tether\app\config\tether.json`을 고친 뒤 Tether를 다시 켭니다.

UI 없이 실행하려면 `Tether.Cli`를 씁니다. Host와 Cli를 동시에 켜지 마세요(같은 outbox를 둘이 처리합니다).

## 빠른 시작

1. Gemini 데스크톱 앱을 켜고 로그인합니다(최소화하지 마세요. 최소화된 창은 자동화할 수 없습니다).
2. Tether를 켭니다. `C:\Tether\exchange\`와 그 안의 `PROTOCOL.md`가 생깁니다.
3. Gemini 창을 Tether의 슬롯 B로 끌어다 놓습니다(도킹은 선택 사항이고, 연결은 도킹 없이도 동작합니다).
4. 메모장으로 `C:\Tether\exchange\outbox\claude-code\test.md`를 만들고 아래 두 줄을 저장합니다.

   ```
   to: gemini
   빨간 사과 한 개, 흰 배경 사진을 만들어줘
   ```

5. Link View에 패킷이 지나가고, 잠시 뒤 `C:\Tether\exchange\packets\000001\reply-1.png`가 생기면 성공입니다.

## AI와 함께 쓰기

Tether는 파일만 봅니다. 그래서 "요청 파일을 쓰고, 결과 폴더를 읽는" 일을 AI에게 맡기면 AI끼리 대화하게 됩니다.
아래 프롬프트를 각 AI의 첫 메시지(또는 프로젝트 지침, `CLAUDE.md`)에 붙여 넣으세요.

### Claude Code (요청하는 쪽)

Claude Code는 파일을 직접 읽고 쓸 수 있으니 별도 설정이 없습니다. 알림은 받지 않으므로 결과를 스스로 확인하게 알려 줍니다.

```
Tether로 Gemini에게 일을 맡길 수 있어. 규칙은 C:\Tether\exchange\PROTOCOL.md 에 있으니 먼저 읽어.
- 요청: C:\Tether\exchange\outbox\claude-code\<이름>.md 파일을 쓴다. 첫 줄 "to: gemini", 둘째 줄부터 프롬프트.
  텍스트 답이 필요하면 "reply: text", 새 대화로 시작하려면 "thread: new" 머리줄을 쓰고 빈 줄 뒤에 본문을 쓴다.
- 결과: 요청 직후 C:\Tether\exchange\packets\ 에서 가장 큰 번호 폴더의 status.json을
  백그라운드로 확인한다. status가 Replied/Notified 면 reply.md(텍스트)와 reply-N.png(이미지)를 읽고,
  Failed 면 error를 보고한다. 이미지는 몇 분 걸릴 수 있다.
- packets\ 아래 파일은 읽기만 한다. 요청은 한 번에 하나씩 보낸다.
```

### Claude 데스크톱 (요청하는 쪽)

1. 설정 → 확장 프로그램에서 **Filesystem** 확장을 켜고 허용 폴더에 `C:\Tether\exchange`를 넣습니다.
2. Claude 앱을 **완전히 종료했다가 다시 켭니다**(트레이 아이콘까지 종료). 재시작 전에는 폴더 설정이 적용되지 않습니다.
3. Claude 창을 Tether의 슬롯 A에 도킹하고, 알림을 받을 대화를 화면에 열어 둡니다. 결과 알림은 **지금 열려 있는 대화**에 붙여 넣어집니다.
4. 대화에 아래 프롬프트를 보냅니다.

```
Filesystem 도구로 C:\Tether\exchange\PROTOCOL.md 를 읽고 그 규칙대로 Gemini에게 요청해 줘.
요청 파일은 C:\Tether\exchange\outbox\claude\ 아래에 쓴다.
"[Tether] #번호 ... 응답 도착" 메시지가 오면 그 packets 폴더의 reply.md와 reply-N.png를 열어서 확인하고 이어서 작업해.
```

### Gemini (응답하는 쪽)

Gemini는 Tether가 붙여 넣는 글을 사람의 입력처럼 받습니다. 설정은 필요 없지만, 첫 대화에 아래를 보내 두면 답이 깔끔해집니다.

```
앞으로 이 대화에는 다른 AI(Claude)가 Tether라는 프로그램을 통해 보낸 요청이 들어온다.
- 이미지 요청이면 이미지를 바로 생성하고 긴 설명은 붙이지 마.
- 텍스트 요청이면 답만 마크다운으로 써. 되묻지 말고 합리적인 가정을 적은 뒤 답해.
```

`thread: new` 요청은 새 채팅에서 시작하므로 이 안내가 사라집니다. 매번 필요하면 요청 본문에 함께 넣으세요.

### 다른 AI

파일을 쓰고 읽을 수 있는 AI라면 무엇이든 요청하는 쪽이 될 수 있습니다. `outbox\<아무 id>\`에 요청 파일을 쓰고 Claude Code처럼 `status.json`을 확인하면 됩니다.
응답하는 쪽으로 새 앱을 붙이려면 아래 [구조](#구조)를 보세요.

## 요청 보내기

Tether가 시작하면 `C:\Tether\exchange\PROTOCOL.md`에 사용 설명서를 만듭니다. 요청은 파일 하나입니다.

`C:\Tether\exchange\outbox\claude-code\cat.md`

```
to: gemini
우주복 입은 고양이 수채화 이미지를 만들어줘
```

- 첫 줄은 정확히 `to: <앱 id>`, 둘째 줄부터가 Gemini에 입력될 프롬프트입니다.
- 선택 머리줄: `to:` 다음에 `키: 값`을 쓰고 빈 줄 하나 뒤에 본문을 씁니다. 머리줄은 Gemini에 붙여 넣지 않습니다.

  ```
  to: gemini
  reply: text
  thread: new

  이 설계안을 검토해줘 ...
  ```

  | 키 | 값 (기본값 먼저) | 뜻 |
  |---|---|---|
  | `reply` | `image` / `text` | `text`면 이미지를 기다리지 않고 텍스트 답변이 끝나면 완료 |
  | `thread` | `continue` / `new` | `new`면 붙여 넣기 전에 Gemini의 새 채팅을 엽니다 |

  모르는 키나 잘못된 값은 `Failed`로 거절합니다. 본문 첫 줄이 `Note: ...`처럼 생겼다면 `to:` 다음에 빈 줄을 넣으세요.
- 결과는 `packets\<6자리 번호>\`에 `request.md`, `status.json`, `reply.md`(답변 텍스트, 마크다운), `reply-1.png`(새 이미지)로 저장됩니다.
- 상태: `Queued → Delivering → Generating → Replied → Notified`, 실패하면 `Failed`와 `error`.
- **Claude 데스크톱**(`outbox\claude\`)은 Tether가 입력창에 도착 알림을 붙여 넣어 줍니다.
- **Claude Code**(`outbox\claude-code\`)는 알림이 없으니 `status.json`을 직접 확인합니다.

## 구조

```
src/
  Tether.Core/        요청 파싱, PacketStore, Router (UI 의존 없음, 단위 테스트 대상)
  Tether.Automation/  UIA·클립보드·Win32: GeminiResponder, ClaudeNotifier, OutboxWatcher
  Tether.Cli/         헤드리스 실행
  Tether.Host/        WPF 호스트: 도킹, Link View
tests/Tether.Core.Tests/
config/tether.json    앱 id, 프로세스 이름, UIA 셀렉터, 타임아웃
spikes/               초기 검증 스크립트
docs/                 UIA 메모, 구현 계획
```

새 앱은 `IResponder` 또는 `INotifier` 어댑터와 `config/tether.json` 항목을 추가해 연결합니다.

## 주의

- 개인용 도구입니다. 소비자용 앱의 UI를 자동화하므로 앱이 업데이트되면 셀렉터가 깨질 수 있습니다.
- 자동화 중에는 클립보드를 잠시 사용합니다(백업 후 복원).
- 실행당 패킷은 최대 20개입니다(`maxPacketsPerRun`).
- Embed 모드에서 Tether를 강제 종료하면 넣어 둔 앱 창도 함께 닫힙니다. 창의 닫기 버튼으로 종료하세요.
