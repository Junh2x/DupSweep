<h1 align="center">DupSweep</h1>
<p align="center">중복·유사 파일 탐지 및 디스크 정리 도구</p>

<p align="center">
  <a href="https://dotnet.microsoft.com/download/dotnet/9.0"><img src="https://img.shields.io/badge/.NET-9.0-512BD4?style=flat&logo=dotnet" alt=".NET 9.0" /></a>
  <a href="https://github.com/dotnet/wpf"><img src="https://img.shields.io/badge/WPF-Windows-0078D6?style=flat&logo=windows" alt="WPF" /></a>
  <a href="LICENSE.md"><img src="https://img.shields.io/badge/license-MIT-green?style=flat" alt="MIT License" /></a>
</p>

---

## 소개

**DupSweep**은 폴더를 스캔하여 중복 파일을 탐지하고, 안전하게 정리할 수 있는 Windows 애플리케이션입니다.<br>
중복 조건을 선택해 스캔할 수 있으며, **이미지**(dHash)와 **비디오**(FFmpeg) 유사도 분석으로 미디어 파일도 탐지가 가능합니다.<br>
디스크 정리 및 분석에 도움이 되는 다양한 기능을 제공합니다.

---

## 주요 기능

| 기능 | 설명 |
|------|------|
| **2단계 해싱 비교** | XxHash64 빠른 필터링 + BLAKE3 정밀 비교 |
| **이미지 유사도** | dHash 퍼셉추얼 해싱으로 유사 이미지 그룹핑 |
| **비디오 유사도** | FFmpeg 프레임 분석 기반 유사 영상 탐지 |
| **안전 삭제** | 휴지통 이동, 보호 폴더/확장자 차단, dry-run 모드 |
| **폴더 트리** | 디스크 사용량 계층 시각화 |
| **적응형 병렬 처리** | SSD/HDD 자동 감지 후 병렬도 최적화 |
| **스캔 제어** | 일시정지 / 재개 / 취소 |
| **다국어 지원** | 한국어 / English |

---

## 스크린샷

> 추후 추가 예정

---

## 시작하기

### 요구 사항

- Windows 10 / 11
- [.NET 9.0 Runtime](https://dotnet.microsoft.com/download/dotnet/9.0)
- (선택) [FFmpeg](https://ffmpeg.org/) — 비디오 유사도 비교 시 필요

### 설치

```bash
git clone https://github.com/Junh2x/DupSweep.git
cd DupSweep
dotnet build -c Release
dotnet run --project src/DupSweep.App
```

---

## 사용법

1. **폴더 선택** — 스캔 대상 폴더 추가 (드래그 앤 드롭 지원)
2. **옵션 설정** — 탐지 조건, 파일 유형, 유사도 임계값 설정
3. **스캔 시작** — 중복 파일 탐지 실행
4. **결과 확인** — 그룹별 중복 파일 비교 및 선택
5. **정리** — 휴지통 이동 또는 영구 삭제

---

## 기술 스택

| 구분 | 기술 |
|------|------|
| 언어 | C# |
| 프레임워크 | .NET 9.0, WPF |
| UI | MaterialDesignThemes |
| 해싱 | XxHash64, BLAKE3 |
| 이미지 유사도 | dHash (Difference Hash) |
| 이미지 처리 | SixLabors.ImageSharp |
| 영상 처리 | FFMpegCore |

---

## 프로젝트 구조

```
DupSweep/
├── src/
│   ├── DupSweep.Core             # 도메인 모델, 알고리즘, 서비스 인터페이스
│   ├── DupSweep.Infrastructure   # 해싱, 미디어 처리, 로깅, Shell API
│   └── DupSweep.App              # WPF UI, ViewModel, 테마
└── tests/
    └── DupSweep.Tests            # 단위 / 통합 테스트
```

---

## 라이선스

[MIT License](LICENSE.md)
