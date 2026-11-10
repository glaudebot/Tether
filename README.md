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

## 실행

```bash
dotnet build
dotnet test
```

```bash
src/Tether.Host/bin/Debug/net10.0-windows/tether-host.exe
```

UI 없이 실행하려면 `Tether.Cli`를 씁니다. Host와 Cli를 동시에 켜지 마세요(같은 outbox를 둘이 처리합니다).

## 요청 보내기

Tether가 시작하면 `C:\Tether\exchange\PROTOCOL.md`에 사용 설명서를 만듭니다. 요청은 파일 하나입니다.

`C:\Tether\exchange\outbox\claude-code\cat.md`

```
to: gemini
우주복 입은 고양이 수채화 이미지를 만들어줘
```

- 첫 줄은 정확히 `to: <앱 id>`, 둘째 줄부터가 Gemini에 입력될 프롬프트입니다.
- 결과는 `packets\<6자리 번호>\`에 `request.md`, `status.json`, `reply-1.png`로 저장됩니다.
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
