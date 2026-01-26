# DupSweep 개발 가이드

## 개요

DupSweep은 Windows용 중복 파일 탐지 및 제거 프로그램입니다. 사진, 영상, 음성 파일의 중복을 해시 비교 및 유사도 분석을 통해 찾아냅니다.

## 개발 환경 요구사항

### 필수 소프트웨어

| 소프트웨어 | 버전 | 설치 링크 |
|-----------|------|----------|
| .NET SDK | 8.0 이상 (9.0 권장) | [다운로드](https://dotnet.microsoft.com/download) |
| Visual Studio 2022 또는 Cursor IDE | 최신 | [VS 다운로드](https://visualstudio.microsoft.com/) |
| FFmpeg (선택) | 최신 | [다운로드](https://ffmpeg.org/download.html) |

### .NET SDK 설치 확인

```powershell
dotnet --version
```

## 프로젝트 구조

```
DupSweep/
├── DupSweep.sln                          # 솔루션 파일
├── src/
│   ├── DupSweep.App/                     # WPF 애플리케이션 (UI)
│   │   ├── Views/                        # XAML 뷰
│   │   ├── ViewModels/                   # MVVM 뷰모델
│   │   ├── Converters/                   # XAML 컨버터
│   │   └── App.xaml                      # 앱 진입점
│   │
│   ├── DupSweep.Core/                    # 핵심 비즈니스 로직
│   │   ├── Models/                       # 데이터 모델
│   │   ├── Services/                     # 서비스 인터페이스/구현
│   │   ├── Algorithms/                   # 파일 스캔, 해시, 유사도 알고리즘
│   │   └── Processors/                   # 이미지/비디오/오디오 프로세서
│   │
│   └── DupSweep.Infrastructure/          # 인프라스트럭처
│       ├── Hashing/                      # Blake3, xxHash 구현
│       ├── Caching/                      # 썸네일/해시 캐시 (LiteDB)
│       ├── FileSystem/                   # 파일 삭제 서비스
│       └── DependencyInjection/          # DI 설정
│
└── tests/                                # 단위 테스트 (예정)
```

## 빌드 방법

### 명령줄에서 빌드

```powershell
# 프로젝트 루트 디렉토리로 이동
cd C:\Users\JUN\OneDrive\바탕 화면\Workspace\myProjects\DupSweep

# 전체 솔루션 빌드
dotnet build

# Release 모드로 빌드
dotnet build -c Release
```

### Visual Studio / Cursor IDE에서 빌드

1. `DupSweep.sln` 파일 열기
2. `빌드` > `솔루션 빌드` (또는 `Ctrl+Shift+B`)

## 실행 방법

### 명령줄에서 실행

```powershell
# Debug 모드 실행
dotnet run --project src/DupSweep.App

# Release 모드 실행
dotnet run --project src/DupSweep.App -c Release
```

### IDE에서 실행

1. `DupSweep.App` 프로젝트를 시작 프로젝트로 설정
2. `F5` 키를 눌러 디버깅 시작 (또는 `Ctrl+F5`로 디버깅 없이 실행)

## 기능별 테스트

### 1. 폴더 선택 (Home 화면)

1. 프로그램 실행 후 "Home" 탭 선택
2. "Browse Folders" 버튼 클릭하여 스캔할 폴더 선택
3. 탐지 옵션 설정:
   - Hash comparison: 정확한 중복 탐지
   - Image/Video/Audio similarity: 유사도 기반 탐지
   - Similarity threshold: 유사도 임계값 (50-100%)
4. 파일 유형 필터 선택 (Images, Videos, Audio)

### 2. 스캔 (Scan 화면)

1. "Start Scan" 버튼 클릭
2. 진행률 및 통계 확인:
   - 총 파일 수
   - 처리된 파일 수
   - 발견된 중복 그룹 수
3. 일시정지/취소 가능

### 3. 결과 확인 (Results 화면)

1. 스캔 완료 후 자동으로 Results 탭으로 이동
2. 중복 그룹별로 파일 확인
3. 필터링:
   - All: 모든 결과
   - Images/Videos/Audio: 유형별 필터
4. Auto Select 기능:
   - Keep First: 첫 번째 파일 유지
   - Keep Newest: 최신 파일 유지
   - Keep Oldest: 가장 오래된 파일 유지

### 4. 파일 삭제

1. 삭제할 파일 체크박스 선택
2. 삭제 방법 선택:
   - "Move to Trash": 휴지통으로 이동 (복구 가능)
   - "Delete Permanently": 영구 삭제 (복구 불가)

## 설정 (Settings 화면)

| 설정 | 설명 |
|------|------|
| Dark Theme | 다크/라이트 테마 전환 |
| Move to Trash by Default | 기본 삭제 방식 |
| Show Confirmation Dialog | 삭제 전 확인 대화상자 |
| Thumbnail Size | 썸네일 크기 (px) |
| Parallel Threads | 병렬 처리 스레드 수 |
| FFmpeg Path | FFmpeg 실행 파일 경로 |

## 캐시 위치

캐시 데이터는 다음 위치에 저장됩니다:

```
%LOCALAPPDATA%\DupSweep\cache\
├── thumbnails.db     # 썸네일 캐시
└── hashes.db         # 해시 캐시
```

캐시를 삭제하려면 위 폴더를 삭제하면 됩니다.

## 문제 해결

### 빌드 오류

1. **NuGet 패키지 복원 실패**
   ```powershell
   dotnet restore
   ```

2. **nuget.org 소스가 없는 경우**
   ```powershell
   dotnet nuget add source https://api.nuget.org/v3/index.json -n nuget.org
   ```

### 실행 오류

1. **.NET Runtime이 설치되지 않은 경우**
   - [.NET Desktop Runtime](https://dotnet.microsoft.com/download) 설치

2. **FFmpeg 관련 오류**
   - Settings에서 FFmpeg 경로 설정
   - 또는 `tools/ffmpeg/` 폴더에 ffmpeg.exe 배치

## 주요 NuGet 패키지

| 패키지 | 용도 |
|--------|------|
| MaterialDesignThemes | Material Design UI |
| CommunityToolkit.Mvvm | MVVM 패턴 |
| SixLabors.ImageSharp | 이미지 처리 |
| Blake3 | BLAKE3 해시 |
| System.IO.Hashing | xxHash |
| LiteDB | 로컬 캐시 DB |
| FFMpegCore | FFmpeg 래퍼 |
| MetadataExtractor | EXIF 메타데이터 |

## 디버깅 팁

### 브레이크포인트 추천 위치

- `ScanService.ScanAsync()`: 스캔 로직 시작점
- `DuplicateDetector.DetectDuplicatesAsync()`: 중복 탐지 로직
- `PerceptualHash.ComputeHash()`: 이미지 해시 계산
- `ResultsViewModel.LoadResults()`: 결과 로드

### 로깅

현재 로깅이 구현되어 있지 않습니다. 필요시 `Microsoft.Extensions.Logging`을 추가하세요.

## 테스트 데이터 준비

중복 파일 탐지 테스트를 위한 샘플 데이터:

1. **정확한 중복**: 동일한 파일을 다른 이름으로 복사
2. **유사 이미지**: 같은 이미지를 다른 해상도/품질로 저장
3. **유사 영상**: 같은 영상을 다른 코덱/비트레이트로 인코딩

## 추가 개발 작업 (TODO)

- [ ] 단위 테스트 작성
- [ ] BKTree 알고리즘 구현 (O(log n) 유사도 검색)
- [ ] 로깅 시스템 추가
- [ ] MSIX 패키징
- [ ] 자동 업데이트 기능

## 라이선스

이 프로젝트는 비공개입니다. 라이선스 정보는 LICENSE 파일을 참조하세요.
