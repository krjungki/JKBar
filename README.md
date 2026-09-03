# JKBar

macOS 노치를 흉내 낸 Windows 상단 표시 바. 화면 위쪽에 노치 모양 창을 띄우고 그 안에 아이콘, 숫자, 텍스트를
개별 항목으로 구성해 상시 표시하거나 알림으로 띄운다.

Status: SCAFFOLD. 아직 실행 가능한 산출물이 없다. 정본 계획은
[`.appdev/plans/20260903_appdev_JKBar.md`](../../.appdev/plans/20260903_appdev_JKBar.md)이다.

## Platform Support

| OS | Status | Architecture | Runtime | Artifact | Verification |
|---|---|---|---|---|---|
| Windows | not-targeted | — | — | — | 스캐폴드 단계다. 실행 가능한 산출물과 실기 검증 증거가 생기면 `supported`로 바꾸고 Plan의 `## Platform Verification`에 `PASS` 행을 남긴다. |
| macOS | not-targeted | — | — | — | Win32 레이어드 창과 트레이에 의존하는 Windows 전용 도구다. |
| Linux | not-targeted | — | — | — | 같은 이유로 범위 밖이다. |

Windows를 `supported`로 선언하지 않은 것은 의도적이다. 저장소 계약상 `supported`는 실제 OS 스모크와 artifact
해시 증거를 요구하는데, 지금은 빌드할 소스가 없다.

## 무엇을 만드는가

- 화면 상단에 노치 모양 창을 그린다. 아래쪽 두 모서리만 둥글다.
- 배경색, 폭, 높이, 상단 좌/중앙/우 정렬을 설정에서 바꾼다.
- 표시되는 항목은 각각 독립된 구성요소다. 추가, 삭제, 순서 변경, 개별 설정이 가능하다.
- 겹침 처리를 세 방식 중에서 고른다. 최상위 표시 / 상단 영역 예약 / 바탕화면 고정.
- 시간·날짜와 시스템 수치를 내장 제공자로 표시한다.
- 트레이 아이콘에서 설정을 열다.
- 창은 클릭을 통과시킨다. 표시 전용이다.

## 범위 밖

- **Windows 알림 읽기.** 해당 API가 패키지 ID를 요구해 포터블 배포가 깨지므로 범위에서 제외했다.
- 항목 클릭으로 동작을 실행하는 기능.
- macOS, Linux 지원.

## 시스템 수치 제공자

JKMon의 수집기를 **이식**해 쓴다. 저장소를 합치거나 라이브러리로 묶지 않고 재사용 가능한 부분만 가져온다.
대가는 이중 유지보수이므로 이식한 파일에는 출처를 주석으로 남긴다.

## Prerequisites

- Windows 11 x64
- .NET 10 SDK (`C:\Program Files\dotnet`)

## Build / Test / Run

소스가 아직 없다. 스캐폴드가 채워지면 이 절에 restore, build, test, run, package, smoke 명령을 그대로 복사해
실행할 수 있게 기록한다.

## Privacy

로컬 전용이다. 수집한 값과 로그는 사용자 기기를 벗어나지 않는다. 앱이 읽는 것은 시스템 성능 수치와 시간뿐이며
사용자 문서나 알림 본문은 읽지 않는다.

## Known limitations

- 스카폴드 단계라 동작하는 기능이 없다.
- macOS 노치와 달리 Windows는 화면 상단을 예약해 주지 않는다. 겹침 처리 방식은 설정으로 고른다.

## License

미정. 공개 여부와 라이선스는 Product owner 결정 사항이라 스카폴드 단계에서는 파일을 두지 않는다.
JKMon에서 이식한 코드가 들어오므로 공개 시 MIT 고지 유지 여부를 함께 정한다.
