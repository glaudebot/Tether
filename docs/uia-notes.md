# UIA Notes (M0, 2026-11-08)

도구: PowerShell `System.Windows.Automation` (spikes/*.ps1)

## Gemini 데스크톱 (1.18.0)

- 경로: `%LOCALAPPDATA%\Google\Gemini\app-1.18.0\Gemini.exe`, 프로세스명 `Gemini`, 창 클래스 `Chrome_WidgetWin_1`, 창 제목 `Gemini`
- Electron. **창이 최소화돼 있으면 웹 콘텐츠 트리가 생성되지 않는다** → 작업 전 `ShowWindow(SW_RESTORE)` 필요. 첫 접근 후 수 초 뒤 트리가 채워짐(재시도 필요)
- 셀렉터 (모두 ControlType + Name, AutomationId 없음):

| 용도 | ControlType | Name | 비고 |
|---|---|---|---|
| input | Edit | `Gemini 프롬프트 입력` | class `ql-editor ...`, SetFocus → Ctrl+V → Enter 동작 확인 |
| stopButton | Button | `대답 생성 중지` | 생성 중에만 존재 |
| imageDownload | Button | `원본 크기 이미지 다운로드` | 마지막(가장 아래) 요소 사용 |
| imageCopy | Button | `이미지 복사` | 마지막 요소 사용 |

- 테스트: "이미지 생성: 빨간 사과 한 개, 흰 배경 사진" → 약 12초 후 이미지 생성
- 완료 판정: `대답 생성 중지` 소멸 + `원본 크기 이미지 다운로드` 개수가 전송 전보다 증가 (이미지 요청일 때)
- 이미지 회수:
  - **복사**(Invoke `이미지 복사` → 클립보드 이미지): 즉시, **1024×572** (축소본)
  - **다운로드**(Invoke `원본 크기 이미지 다운로드`): **2752×1536 / 6.4MB**. 클릭 약 2초 후 Windows **다른 이름으로 저장** 대화상자(#32770, 제목 `blob:null/<guid>`, Gemini 프로세스 소유)가 뜬다. (처음 측정한 "60초"는 사람이 대화상자를 처리한 시간이었음)
  - 자동 처리 (검증됨, 총 4초): Gemini PID 소유의 보이는 `#32770` 창을 EnumWindows로 찾음 → SetForegroundWindow → 클립보드에 저장 경로 → `Alt+N`(파일 이름) → `Ctrl+A` → `Ctrl+V` → `Enter`. 지정한 경로(패킷 폴더)에 바로 저장됨
  - 안 되는 방법: 대화상자 컨트롤의 UIA 패턴(Value/Invoke) "Unsupported Pattern", `WM_SETTEXT`는 파일 대화상자 내부 이름에 반영 안 됨(기본 이름으로 저장 시도), SendKeys로 경로 타이핑은 한글 IME에 깨짐 → 반드시 클립보드 붙여넣기
  - 같은 이름이 있으면 `다른 이름으로 저장 확인`(#32770) 창 → 패킷마다 새 경로를 쓰므로 정상 흐름에선 안 뜸. 뜨면 `Alt+N`(아니요)으로 닫고 실패 처리
- 결론: 기본은 다운로드(원본, 대화상자 자동 처리), 실패·시간 초과(대화상자 10초, 파일 30초) 시 복사로 폴백

## Claude 데스크톱 (4.23118.0.0, MSIX)

- 경로: `C:\Program Files\WindowsApps\Claude_4.23118.0.0_x64__pzs8sxrjxfjjc\app\Claude.exe`, 프로세스명 `claude`, 창 클래스 `Chrome_WidgetWin_1`
- 트리는 두 번째 접근부터 채워짐 (`--force-renderer-accessibility` 불필요)
- input: Edit, Name `프롬프트`, class `tiptap ProseMirror`
- **주의**: Claude 앱 창은 하나이고 Chat/Code 탭이 같은 창에 있다. 알림은 **현재 화면에 열린 대화**로 들어간다 → 알림 전에 올바른 대화가 열려 있는지 사용자가 보장해야 함 (Task 2에서 확인)

### Task 2 결과 (2026-11-08)

- Chat 탭 input: Edit, Name `Claude에게 프롬프트를 입력하세요.` (Code 탭은 `프롬프트`) → 셀렉터는 Name에 `프롬프트` 포함 + 화면에 보이는 Edit. 빈 입력창 값은 `"\n"` → Trim 후 비교
- 대화 식별: ControlType Document의 Name = `<대화 제목> - Claude` (Code 세션은 `... - Claude Code`). 새 채팅은 첫 메시지 후 제목 생성 → 알림 가드는 제목 기준으로 동작 확인
- 붙여넣기+Enter 전송 OK (`SENT to 'Greeting - Claude'`). 전송 전 클립보드가 비어 있으면 복원할 값이 없음(정상)
- Filesystem 확장: 허용 폴더 `C:\Tether\exchange` 저장 후 **앱 완전 재시작**해야 적용됨 (재시작 전엔 채팅이 MCP roots로 `C:\dev\Claude`를 보내 덮어씀). 재시작 후 Claude가 `outbox/claude/test.md`(첫 줄 `to: gemini`) 작성 + 이미지 열기 성공