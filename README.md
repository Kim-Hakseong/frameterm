# FrameTerm

**바이트 지향 시리얼 프로토콜 워크벤치.** 프레임을 한 번만 정의하면, 와이어에 흐르는 모든 바이트가 파싱되고 체크섬이 검증된 컬러 레코드로 표시됩니다. 스크립트는 필요 없습니다.

*A byte-oriented serial protocol workbench — declarative framing, CRC verification, field parsing, and highlighting. No scripting required.*

![FrameTerm 데모 모드 — CRC 검증, 필드 파싱, FAIL 프레임 하이라이트](docs/screenshot.png)

*데모 모드: 내장 샘플 프로토콜이 파이프라인을 통과하는 모습 — 프레임별 체크섬 OK/FAIL, 실시간 필드 파싱(seq/temp/status), 선택한 FAIL 프레임의 hex 덤프와 필드 테이블. 이 스크린샷은 `dotnet test tests/Ft.App.Tests --filter CaptureDemoModeScreenshot`로 재현할 수 있습니다.*

## 다운로드

[Releases 페이지](https://github.com/Kim-Hakseong/frameterm/releases)에서 플랫폼별 실행파일을 받을 수 있습니다. 설치 없이 압축을 풀고 바로 실행하면 되고, 하드웨어 없이도 툴바의 **Demo** 버튼으로 전체 기능을 체험할 수 있습니다.

| 플랫폼 | 실행 방법 |
|---|---|
| Windows x64 | 압축 해제 후 `FrameTerm.exe` 실행 |
| macOS (Apple Silicon) | 압축 해제 후 `./FrameTerm` (최초 실행 시 우클릭 → 열기) |
| Linux x64 | `chmod +x FrameTerm && ./FrameTerm` |

## 왜 필요한가

PuTTY, TeraTerm 같은 터미널 에뮬레이터는 텍스트용입니다. 바이너리 프로토콜 디버깅에는 프레이밍, 체크섬, 필드 디코딩, 바이트 단위의 정밀한 가시성이 필요합니다. FrameTerm은 정확히 그 일을 합니다.

## 주요 기능

- **선언형 프레임 정의** — 4가지 프레이밍 모드: 구분자(시작/끝 시퀀스 + 이스케이프), 고정 길이, 길이 필드(offset/size/endian/보정), 침묵 갭. 스트림을 어떻게 쪼개 넣어도 결과가 동일합니다(1바이트씩 넣어도 같은 프레임 — 테스트로 보장).
- **체크섬 엔진** — 완전 파라미터화 CRC(width 8/16/32, poly, init, refin/refout, xorout) + XOR8/SUM8. 프리셋: CRC-16/MODBUS, CRC-16/CCITT-FALSE, CRC-32, CRC-8 — 전부 공개 카탈로그 골든 벡터로 검증. 모든 프레임에 OK/FAIL 표시.
- **필드 파서** — offset/type/endian(u8…s32, f32)만 선언하면 프레임마다 필드 테이블이 실시간 렌더링됩니다.
- **하이라이트 룰** — `??` 와일드카드 바이트 패턴 또는 필드 조건(=, ≠, >, <) → 색상. 첫 매칭 적용.
- **송신 컴포저** — hex와 ASCII 혼합: `A5 01 {len} "CMD" {crc16}`. 길이·체크섬 플레이스홀더는 송신 시점에 자동 계산. F키 단축키 매크로 20개, 주기 반복 송신.
- **듀얼 뷰** — 오프셋 컬럼, 행당 바이트 수 조절, RX/TX 색 구분, ms 타임스탬프가 있는 Hex+ASCII 덤프.
- **로깅 & 필터** — raw 트래픽 파일 기록(hex + 타임스탬프), 에러만/패턴 필터, 10k 프레임 표시 링버퍼.
- **프로젝트 파일** — 세션 전체(포트, 프레이밍, 체크섬, 필드, 하이라이트, 매크로)를 JSON `.ftproj` 하나로 저장/복원.
- **TCP 지원** — 동일한 파이프라인이 TCP 클라이언트/서버에서도 동작. 자동 응답 룰(패턴 → 조합 응답, 지연 옵션)로 장비 에뮬레이션과 핸드셰이크 자동화.
- **데모 모드** — 클릭 한 번으로 내장 샘플 프로토콜이 에코 트랜스포트를 통해 흐릅니다. 하드웨어 없이 전체 UX 체험.

## 빌드 & 실행

[.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)가 필요합니다.

```bash
dotnet build FrameTerm.sln
dotnet test               # 오프라인 · 결정적 테스트
dotnet run --project src/Ft.App
```

### 릴리즈 패키징

```bash
dotnet publish src/Ft.App -c Release -r win-x64  --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -o publish/win-x64
dotnet publish src/Ft.App -c Release -r osx-arm64 --self-contained true -p:PublishSingleFile=true -o publish/osx-arm64
```

## 아키텍처

```
src/Ft.Core   — 엔진, UI 의존성 0
  Checksum/   파라미터화 CRC + 프리셋 + 배치 검증
  Framing/    프레이머 4종, 구조적으로 청킹 불변
  Parsing/    필드 파서, 바이트 패턴, 하이라이트 룰
  Compose/    hex/ascii/플레이스홀더 페이로드 컴포저
  Transport/  시리얼, TCP 클라이언트/서버, 에코 페이크 (Stream 추상화)
  Pipeline/   바운디드 큐 RX 파이프라인, 배치 UI 이벤트, 자동 응답
  Logging/    논블로킹 raw 로그 라이터
  Project/    .ftproj 모델 + JSON 직렬화
  Licensing/  RFC 8032 Ed25519, 오프라인 라이선스 키, 트라이얼 처리
src/Ft.App    — Avalonia 11 UI (Fluent, MVVM)
tests/        — xUnit: 골든 벡터, 불변성 스윕, TCP 루프백, 헤드리스 UI 스모크
```

중요하게 지킨 설계 원칙: UI 스레드 블로킹 I/O 없음. 수신 경로는 드랍 카운팅이 있는 바운디드 큐라 921600 bps 폭주에도 UI가 멈추지 않고 우아하게 열화됩니다. 시간 의존 로직은 주입된 클럭을 사용해 모든 테스트가 결정적입니다 — sleep 없이.

## 라이선스

MIT 라이선스로 배포합니다 — 상업적 사용을 포함해 자유롭게 사용·수정·재배포할 수 있습니다. 전문은 [LICENSE](LICENSE) 파일을 참고하세요.

---

© 2026 NEXYS Co., Ltd. · MIT licensed
