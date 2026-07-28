namespace Ft.App.Services;

public enum HelpLanguage
{
    Korean,
    English,
}

public enum HelpLineKind
{
    /// <summary>Plain paragraph.</summary>
    Text,
    /// <summary>Bulleted line.</summary>
    Bullet,
    /// <summary>Monospaced example (compose expressions, hex).</summary>
    Code,
}

public sealed record HelpLine(string Text, HelpLineKind Kind = HelpLineKind.Bullet);

public sealed record HelpSection(string Title, IReadOnlyList<HelpLine> Lines);

public sealed record HelpDocument(
    string WindowTitle,
    string Heading,
    string Intro,
    string ToggleLabel,
    string CloseLabel,
    string Footnote,
    IReadOnlyList<HelpSection> Sections);

/// <summary>
/// Bilingual quick manual shown by the Help window. Kept as data (not XAML)
/// so both languages stay structurally identical and the window can swap
/// them with one button.
/// </summary>
public static class HelpContent
{
    public static HelpDocument For(HelpLanguage language) =>
        language == HelpLanguage.Korean ? Korean() : English();

    public static HelpLanguage Other(HelpLanguage language) =>
        language == HelpLanguage.Korean ? HelpLanguage.English : HelpLanguage.Korean;

    private static HelpDocument Korean() => new(
        WindowTitle: "간단 사용설명서",
        Heading: "FrameTerm 간단 사용설명서",
        Intro: "바이트 단위 시리얼·TCP 프로토콜 디버깅 도구입니다. 프레임을 한 번 정의해두면 수신 스트림이 자동으로 잘리고, 체크섬 검증·필드 파싱·색상 표시까지 됩니다.",
        ToggleLabel: "English",
        CloseLabel: "닫기",
        Footnote: "이 창은 툴바의 [Help] 버튼으로 언제든 다시 열 수 있습니다.",
        Sections:
        [
            new HelpSection("1. 장비 없이 바로 체험 (Demo)",
            [
                new HelpLine("① 툴바 [Demo] 버튼 클릭 — 내장 샘플 프로토콜이 바로 흐릅니다"),
                new HelpLine("② Frames 표에 프레임이 쌓이고 Checksum 열에 OK/FAIL이 표시됩니다 (10개마다 1개는 일부러 CRC를 깨뜨려 FAIL을 보여줍니다)"),
                new HelpLine("③ 프레임을 클릭하면 아래에 hex 덤프와 파싱된 필드 표가 나옵니다"),
                new HelpLine("④ 다시 [Demo]를 누르면 정지합니다"),
            ]),

            new HelpSection("2. 실제 장비 연결",
            [
                new HelpLine("툴바 [Connect] → 상단 Mode에서 연결 방식을 고릅니다:"),
                new HelpLine("• Serial port: 포트 선택 후 보레이트(직접 입력 가능)·패리티·데이터/정지비트·흐름제어 설정. DTR/RTS 체크박스도 있습니다"),
                new HelpLine("• TCP client: 장비 IP와 포트 입력"),
                new HelpLine("• TCP server (listen): 이 PC가 서버가 되어 장비 접속을 기다립니다"),
                new HelpLine("포트 목록이 비어 있으면 USB-시리얼 컨버터 드라이버(FTDI/CH340 등)가 설치되지 않은 상태입니다."),
            ]),

            new HelpSection("3. 프레임 정의 — 이 도구의 핵심",
            [
                new HelpLine("툴바 [Frame def]에서 스트림을 어떻게 프레임으로 자를지 정합니다. 장비 프로토콜에 맞는 모드를 고르세요:"),
                new HelpLine("• Delimiter (구분자): 시작/끝 바이트가 있는 프로토콜. 예) STX(02) ~ ETX(03). 시작 시퀀스는 생략 가능하고, 이스케이프 바이트를 지정하면 그 뒤 1바이트는 종결자로 보지 않습니다"),
                new HelpLine("• Fixed length (고정 길이): 항상 같은 길이의 프레임"),
                new HelpLine("• Length field (길이 필드): 헤더 안에 길이 값이 들어 있는 프로토콜. 길이 필드의 위치(offset)·크기(1/2/4)·엔디안을 지정하고, 총길이 = 길이값 + 보정치(adjust)로 맞춥니다. 헤더나 CRC가 길이에 포함되는지 여부를 이 보정치로 흡수합니다"),
                new HelpLine("• Silence gap (침묵 갭): 구분자도 길이도 없고, 일정 시간 조용하면 한 프레임으로 보는 방식 (Modbus RTU 스타일)"),
                new HelpLine("• None: 프레이밍 없이 원시 스트림만 보고 싶을 때"),
                new HelpLine("스트림이 어떻게 쪼개져 들어오든(1바이트씩 들어와도) 결과 프레임은 동일하게 보장됩니다.", HelpLineKind.Text),
            ]),

            new HelpSection("4. 체크섬 검증",
            [
                new HelpLine("같은 [Frame def] 창의 Checksum 항목에서 설정합니다:"),
                new HelpLine("• Preset: CRC16_MODBUS, CRC16_CCITT_FALSE, CRC32, CRC8, XOR8, SUM8 중 선택 (None이면 검사 안 함)"),
                new HelpLine("• Offset from end: 체크섬이 프레임 끝에서 몇 바이트 앞에 있는지. 맨 끝 2바이트면 2, 뒤에 ETX가 1바이트 더 있으면 3"),
                new HelpLine("• Byte order: 체크섬 값의 바이트 순서 (Modbus RTU는 LE)"),
                new HelpLine("• Coverage start / end offset: 체크섬이 커버하는 범위. 예) STX를 제외하려면 start=1, CRC와 ETX를 빼려면 end offset=3"),
                new HelpLine("설정이 맞으면 Frames 표에 OK, 틀리거나 통신이 깨지면 빨간 FAIL이 뜹니다.", HelpLineKind.Text),
            ]),

            new HelpSection("5. 필드 파싱 · 색상 규칙",
            [
                new HelpLine("Fields 표에 [+]로 행을 추가하고 이름 / offset / 타입 / 엔디안을 입력합니다"),
                new HelpLine("타입: u8, s8, u16, s16, u32, s32, f32 · 엔디안: LE 또는 BE"),
                new HelpLine("예) 3번 바이트부터 부호있는 16비트 빅엔디안 온도값 → 이름 temp, offset 3, 타입 s16, 엔디안 BE"),
                new HelpLine("Highlight rules에서 조건에 맞는 프레임에 색을 칠할 수 있습니다:"),
                new HelpLine("• 바이트 패턴: A5 ?? 01 처럼 ??는 아무 바이트나 매칭", HelpLineKind.Bullet),
                new HelpLine("• 필드 조건: 필드 이름 + 연산자(=, !=, >, <) + 값 (예: status != 0)"),
                new HelpLine("• 위에서부터 첫 번째로 맞는 규칙의 색이 적용됩니다"),
            ]),

            new HelpSection("6. 송신 · 매크로",
            [
                new HelpLine("하단 입력창에 hex와 문자열을 섞어 쓰고 [Send]를 누릅니다:"),
                new HelpLine("A5 01 {len} \"CMD\" {crc16}", HelpLineKind.Code),
                new HelpLine("• 16진 바이트는 공백으로 구분, 큰따옴표 안은 ASCII 문자열"),
                new HelpLine("• {len} = 체크섬을 뺀 전체 길이 (자기 자신 포함), {len+2} 처럼 보정 가능"),
                new HelpLine("• {crc16} = CRC-16/MODBUS(LE), {crc:프리셋명} = 원하는 프리셋, {sum8}, {xor8}"),
                new HelpLine("• 길이와 체크섬은 보낼 때 자동 계산되므로 손으로 안 맞춰도 됩니다"),
                new HelpLine("[Macros]에서 자주 쓰는 명령을 최대 20개까지 저장하고 F1~F12 단축키를 붙일 수 있습니다"),
                new HelpLine("[Repeat]를 켜면 옆 칸의 주기(ms)마다 반복 송신합니다 (최소 10ms)"),
            ]),

            new HelpSection("7. 로그 · 필터 · 원시 뷰",
            [
                new HelpLine("[Log] 버튼: 송수신 원시 바이트를 시각과 함께 파일로 기록합니다. 화면 표시 제한과 무관하게 전부 기록됩니다"),
                new HelpLine("[Errors only]: 체크섬 FAIL 프레임만 걸러서 봅니다"),
                new HelpLine("Filter 입력칸: A5 ?? 01 형식의 패턴으로 특정 프레임만 봅니다"),
                new HelpLine("[Raw] 버튼: 프레임 목록 대신 연속 hex+ASCII 덤프로 전환합니다. 프레이밍 설정이 맞는지 눈으로 확인할 때 유용합니다"),
                new HelpLine("[Clear]: 화면의 프레임과 덤프를 비웁니다 (기록 중인 로그 파일은 유지)"),
            ]),

            new HelpSection("8. 프로젝트 저장 · 자동 응답",
            [
                new HelpLine("[Save]: 연결 설정, 프레이밍, 체크섬, 필드, 색상 규칙, 매크로를 .ftproj 파일 하나로 저장합니다"),
                new HelpLine("[Open]: 다음에 그 파일만 열면 설정이 그대로 복원됩니다"),
                new HelpLine("[Frame def] 창의 Auto-respond: 특정 패턴의 프레임을 받으면 정해둔 응답을 자동으로 보냅니다 (지연 ms 설정 가능). 장비 흉내나 핸드셰이크 자동화에 씁니다"),
            ]),

            new HelpSection("9. 잘 안 될 때",
            [
                new HelpLine("프레임이 안 잘림 → [Raw] 뷰로 실제 바이트를 보고 구분자/길이 필드 설정을 다시 확인하세요"),
                new HelpLine("전부 FAIL → 체크섬 프리셋, Offset from end, Coverage 범위, 바이트 순서(LE/BE)를 점검하세요"),
                new HelpLine("필드 값이 이상함 → 엔디안(LE/BE)과 offset을 바꿔보세요. 32비트 값은 특히 순서 문제가 잦습니다"),
                new HelpLine("아무것도 안 들어옴 → 보레이트, 배선(TX/RX 교차), 포트 번호를 확인하세요"),
                new HelpLine("하단 상태줄의 drops 숫자가 올라가면 수신 폭주로 일부 데이터가 버려진 것입니다"),
            ]),
        ]);

    private static HelpDocument English() => new(
        WindowTitle: "Quick guide",
        Heading: "FrameTerm quick guide",
        Intro: "A byte-level debugging tool for serial and TCP protocols. Define your frame once and the incoming stream is split automatically, with checksum verification, field parsing and color rules applied.",
        ToggleLabel: "한국어",
        CloseLabel: "Close",
        Footnote: "You can reopen this window any time with the [Help] button in the toolbar.",
        Sections:
        [
            new HelpSection("1. Try it without hardware (Demo)",
            [
                new HelpLine("① Click [Demo] in the toolbar — a built-in sample protocol starts flowing"),
                new HelpLine("② Frames fill the table and the Checksum column shows OK/FAIL (every 10th frame has a deliberately corrupted CRC so you can see FAIL)"),
                new HelpLine("③ Click a frame to see its hex dump and parsed field table below"),
                new HelpLine("④ Click [Demo] again to stop"),
            ]),

            new HelpSection("2. Connecting to a device",
            [
                new HelpLine("Toolbar [Connect] → pick the connection type in the Mode dropdown:"),
                new HelpLine("• Serial port: choose the port, then baud rate (custom values allowed), parity, data/stop bits and flow control. DTR/RTS checkboxes are there too"),
                new HelpLine("• TCP client: enter the device host and port"),
                new HelpLine("• TCP server (listen): this PC listens and waits for the device to connect"),
                new HelpLine("An empty port list usually means the USB-serial driver (FTDI/CH340, …) is not installed."),
            ]),

            new HelpSection("3. Frame definition — the heart of the tool",
            [
                new HelpLine("Toolbar [Frame def] is where you describe how the stream is cut into frames. Pick the mode that matches your protocol:"),
                new HelpLine("• Delimiter: protocols with start/end bytes, e.g. STX(02) … ETX(03). The start sequence is optional, and an escape byte makes the following byte literal so it never terminates the frame"),
                new HelpLine("• Fixed length: every frame has the same length"),
                new HelpLine("• Length field: the header carries the length. Set its offset, size (1/2/4) and endianness; total length = length value + adjust. Use the adjust value to absorb whether the header/CRC are counted"),
                new HelpLine("• Silence gap: no delimiters, no length — a quiet period ends the frame (Modbus RTU style)"),
                new HelpLine("• None: no framing, raw stream only"),
                new HelpLine("However the stream arrives — even one byte at a time — the resulting frames are guaranteed to be identical.", HelpLineKind.Text),
            ]),

            new HelpSection("4. Checksum verification",
            [
                new HelpLine("Set this in the Checksum block of the same [Frame def] window:"),
                new HelpLine("• Preset: CRC16_MODBUS, CRC16_CCITT_FALSE, CRC32, CRC8, XOR8, SUM8 (None disables the check)"),
                new HelpLine("• Offset from end: how many bytes from the end the checksum sits. 2 if it is the last two bytes; 3 if one ETX byte follows it"),
                new HelpLine("• Byte order: byte order of the checksum value (Modbus RTU uses LE)"),
                new HelpLine("• Coverage start / end offset: the range the checksum covers. E.g. start=1 to skip STX, end offset=3 to exclude the CRC and ETX"),
                new HelpLine("When it matches you get OK in the Frames table; a mismatch or a corrupted link shows a red FAIL.", HelpLineKind.Text),
            ]),

            new HelpSection("5. Field parsing and color rules",
            [
                new HelpLine("Add rows to the Fields table with [+] and enter name / offset / type / endianness"),
                new HelpLine("Types: u8, s8, u16, s16, u32, s32, f32 · Endianness: LE or BE"),
                new HelpLine("Example: a signed 16-bit big-endian temperature at byte 3 → name temp, offset 3, type s16, endian BE"),
                new HelpLine("Highlight rules color frames that match a condition:"),
                new HelpLine("• Byte pattern: A5 ?? 01 — ?? matches any byte"),
                new HelpLine("• Field condition: field name + operator (=, !=, >, <) + value, e.g. status != 0"),
                new HelpLine("• The first matching rule from the top wins"),
            ]),

            new HelpSection("6. Sending and macros",
            [
                new HelpLine("Type hex and text in the bottom bar and press [Send]:"),
                new HelpLine("A5 01 {len} \"CMD\" {crc16}", HelpLineKind.Code),
                new HelpLine("• Hex bytes are space-separated; text in double quotes is sent as ASCII"),
                new HelpLine("• {len} = total length excluding the checksum (counting itself); {len+2} adds an offset"),
                new HelpLine("• {crc16} = CRC-16/MODBUS (LE), {crc:PRESET} = any preset, plus {sum8} and {xor8}"),
                new HelpLine("• Length and checksum are computed at send time, so you never hand-calculate them"),
                new HelpLine("[Macros] stores up to 20 frequently used commands, each with an optional F1–F12 hotkey"),
                new HelpLine("[Repeat] sends continuously at the period (ms) in the box next to it (minimum 10 ms)"),
            ]),

            new HelpSection("7. Logging, filters, raw view",
            [
                new HelpLine("[Log]: records raw traffic with timestamps to a file — everything is written regardless of the on-screen limit"),
                new HelpLine("[Errors only]: shows just the frames that failed the checksum"),
                new HelpLine("Filter box: show only frames matching a pattern like A5 ?? 01"),
                new HelpLine("[Raw]: switches from the frame list to a continuous hex+ASCII dump — useful for checking whether your framing settings are right"),
                new HelpLine("[Clear]: empties the on-screen frames and dump (an active log file keeps recording)"),
            ]),

            new HelpSection("8. Projects and auto-respond",
            [
                new HelpLine("[Save]: stores the connection, framing, checksum, fields, color rules and macros in a single .ftproj file"),
                new HelpLine("[Open]: reopening that file restores the whole setup"),
                new HelpLine("Auto-respond in the [Frame def] window: when a frame matches a pattern, a defined reply is sent automatically (with an optional delay). Handy for emulating a device or automating handshakes"),
            ]),

            new HelpSection("9. Troubleshooting",
            [
                new HelpLine("Frames are not being split → switch to [Raw], look at the actual bytes and re-check the delimiter / length field settings"),
                new HelpLine("Everything shows FAIL → verify the checksum preset, offset from end, coverage range and byte order (LE/BE)"),
                new HelpLine("Field values look wrong → try the other endianness and re-check the offset; 32-bit values are especially order-sensitive"),
                new HelpLine("Nothing arrives at all → check the baud rate, wiring (TX/RX crossover) and the port"),
                new HelpLine("A rising drops counter in the status bar means incoming data was discarded during a burst"),
            ]),
        ]);
}
