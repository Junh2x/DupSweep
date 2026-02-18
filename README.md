# DupSweep

Windows용 중복 파일 탐지 및 관리 도구 (v2.0.0)

![.NET](https://img.shields.io/badge/.NET-9.0-512BD4)
![Platform](https://img.shields.io/badge/Platform-Windows-0078D6)
![License](https://img.shields.io/badge/License-MIT-green)

## 소개

DupSweep은 폴더를 스캔하여 중복 및 유사 파일을 탐지하고, 안전하게 정리할 수 있는 Windows 데스크톱 애플리케이션입니다.

## 주요 기능

- **2단계 해싱** - XxHash64(빠른 필터링) + BLAKE3(정밀 비교)로 정확한 중복 탐지
- **유사 미디어 탐지** - dHash 퍼셉추얼 해싱으로 유사 이미지/영상 그룹핑
- **안전한 삭제** - 휴지통 이동, 보호 폴더/확장자 차단, 쿨다운, 시뮬레이션 모드
- **폴더 트리 분석** - 디스크 사용량 계층적 시각화
- **캐시 시스템** - LiteDB 기반 해시/썸네일 캐시로 재스캔 속도 향상
- **일시정지/재개/취소** - 스캔 중 제어 가능

## 요구 사항

- Windows 10/11
- .NET 9.0 Runtime
- (선택) FFmpeg - 비디오 유사도 비교 시 필요

## 설치 및 실행

### 릴리즈 다운로드
[Releases](../../releases) 페이지에서 최신 버전 다운로드

### 소스에서 빌드
```bash
git clone https://github.com/username/DupSweep.git
cd DupSweep
dotnet build -c Release
dotnet run --project src/DupSweep.App/DupSweep.App.csproj
```

## 사용법

1. **폴더 선택** - 스캔할 폴더를 하나 이상 추가 (드래그 앤 드롭 지원)
2. **옵션 설정** - 파일 유형, 유사도 임계값 등 설정
3. **스캔 시작** - 중복 파일 탐지 실행
4. **결과 확인** - 그룹별 중복 파일 확인 및 선택
5. **삭제** - 선택한 파일 삭제 (휴지통 또는 영구 삭제)

## 스캔 옵션

| 옵션 | 설명 |
|------|------|
| 모든 파일 | 모든 파일 유형 대상 해시 비교 |
| 이미지 유사도 | JPG, PNG 등 이미지 유사도 비교 |
| 비디오 유사도 | MP4, AVI 등 비디오 유사도 비교 |
| 유사도 임계값 | 0-100% (기본값: 85%) |
| 최소/최대 파일 크기 | 스캔 대상 파일 크기 제한 |
| 숨김 파일 포함 | 숨김 속성 파일 포함 여부 |

## 기술 스택

| 구분 | 기술 |
|------|------|
| 언어 | C# |
| 프레임워크 | .NET 9.0 (App) / .NET 8.0 (Core, Infrastructure) |
| UI | WPF + MaterialDesignThemes |
| 아키텍처 | Clean Architecture + MVVM |
| 해싱 | XxHash64, BLAKE3 |
| 이미지 처리 | ImageSharp |
| 영상 처리 | FFmpeg (FFMpegCore) |
| 캐시 | LiteDB |
| 로깅 | Serilog |
| 테스트 | xUnit, Moq |

## 프로젝트 구조

```
DupSweep.sln
├── src/
│   ├── DupSweep.Core           # 도메인 로직 (알고리즘, 모델, 서비스)
│   ├── DupSweep.Infrastructure # 인프라 (해싱, 캐시, 파일시스템, Shell API)
│   └── DupSweep.App            # WPF UI (뷰, 뷰모델, 테마)
└── tests/
    └── DupSweep.Tests          # 단위/통합 테스트
```

## 키보드 단축키

| 단축키 | 동작 |
|--------|------|
| `Ctrl+S` | 스캔 시작 |
| `Ctrl+A` | 전체 선택 |
| `Delete` | 선택 파일 삭제 |
| `F1` | 도움말 |

## 라이선스

MIT License - [LICENSE.md](LICENSE.md) 참조
