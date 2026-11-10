namespace Tether.Core;

/// <summary>Writes exchange/PROTOCOL.md: the instructions a requester app (Claude) reads.</summary>
public static class ProtocolDoc
{
    public static void Write(string exchangeRoot, IEnumerable<string> responderIds)
    {
        var apps = string.Join("\n", responderIds.Select(id => $"- `{id}`"));
        var text = $"""
            # Tether 교환 프로토콜

            이 폴더는 Tether가 관리한다. 다른 앱에게 일을 요청하려면 아래 규칙대로 파일을 하나 쓴다.

            ## 요청하기

            경로: `{exchangeRoot}\outbox\<내 앱 id>\<아무 이름>.md` (예: `outbox\claude\cat.md`)

            ```
            to: gemini
            우주복 입은 고양이 수채화 이미지를 만들어줘
            ```

            - 첫 줄은 정확히 `to: <앱 id>`
            - 둘째 줄부터가 그 앱에 그대로 입력될 프롬프트
            - 파일은 Tether가 읽은 뒤 지운다

            ## 사용 가능한 앱

            {apps}

            ## 응답 받기

            응답이 준비되면 Tether가 대화창에 이런 알림을 넣는다.

            ```
            [Tether] #000001 gemini 응답 도착: {exchangeRoot}\packets\000001 (reply-1.png)
            [Tether] #000002 실패: gemini 창을 찾을 수 없습니다
            ```

            알림을 받으면 `packets\<번호>\` 폴더의 파일(`reply.md`, `reply-N.png`)을 읽어서 확인한다.

            **Claude Code (`outbox\claude-code\`)는 알림을 받지 않는다.** 요청 파일을 쓴 뒤
            가장 큰 번호의 새 `packets\<번호>\status.json`을 찾아, `status`가 `Replied`/`Notified`/`Failed`가
            될 때까지 백그라운드로 확인한다(이미지는 몇 분 걸릴 수 있다). `Failed`면 `error`에 이유가 있다.
            `packets\` 아래 파일은 Tether만 쓴다. 읽기만 할 것.
            """;
        Directory.CreateDirectory(exchangeRoot);
        var path = Path.Combine(exchangeRoot, "PROTOCOL.md");
        File.WriteAllText(path + ".tmp", text);
        File.Move(path + ".tmp", path, overwrite: true);
    }
}
