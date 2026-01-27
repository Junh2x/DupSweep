namespace DupSweep.Core.Models;

/// <summary>
/// 병렬 처리 설정 옵션.
/// 드라이브 유형 및 시스템 리소스에 따라 최적의 병렬화를 지원합니다.
/// </summary>
public class ParallelProcessingOptions
{
    /// <summary>
    /// 자동 병렬도 조절 활성화 여부
    /// </summary>
    public bool EnableAutomaticParallelism { get; set; } = true;

    /// <summary>
    /// 최대 CPU 바운드 작업 병렬도 (해시 계산 등)
    /// 0이면 CPU 코어 수를 사용
    /// </summary>
    public int MaxCpuBoundParallelism { get; set; } = 0;

    /// <summary>
    /// 최대 I/O 바운드 작업 병렬도 (파일 읽기 등)
    /// </summary>
    public int MaxIoBoundParallelism { get; set; } = 4;

    /// <summary>
    /// SSD 드라이브용 I/O 병렬도
    /// </summary>
    public int SsdIoParallelism { get; set; } = 8;

    /// <summary>
    /// HDD 드라이브용 I/O 병렬도 (순차 접근이 효율적)
    /// </summary>
    public int HddIoParallelism { get; set; } = 2;

    /// <summary>
    /// 네트워크 드라이브용 I/O 병렬도
    /// </summary>
    public int NetworkIoParallelism { get; set; } = 4;

    /// <summary>
    /// 작은 파일 우선 처리 활성화 (빠른 피드백을 위해)
    /// </summary>
    public bool EnableSmallFilesPriority { get; set; } = true;

    /// <summary>
    /// 작은 파일로 간주하는 최대 크기 (바이트)
    /// </summary>
    public long SmallFileThreshold { get; set; } = 10 * 1024 * 1024; // 10MB

    /// <summary>
    /// 대용량 파일로 간주하는 최소 크기 (바이트)
    /// </summary>
    public long LargeFileThreshold { get; set; } = 100 * 1024 * 1024; // 100MB

    /// <summary>
    /// 대용량 파일 병렬도 제한 (메모리 보호)
    /// </summary>
    public int LargeFileParallelism { get; set; } = 2;

    /// <summary>
    /// 최대 CPU 사용률 (0-100, 0은 무제한)
    /// </summary>
    public int MaxCpuUsagePercent { get; set; } = 80;

    /// <summary>
    /// 최대 메모리 사용률 (0-100, 0은 무제한)
    /// </summary>
    public int MaxMemoryUsagePercent { get; set; } = 70;

    /// <summary>
    /// 리소스 모니터링 간격 (밀리초)
    /// </summary>
    public int ResourceMonitorIntervalMs { get; set; } = 1000;

    /// <summary>
    /// 리소스 과부하 시 자동 스로틀링 활성화
    /// </summary>
    public bool EnableAutoThrottling { get; set; } = true;

    /// <summary>
    /// 배치 처리 크기 (한 번에 처리할 파일 수)
    /// </summary>
    public int BatchSize { get; set; } = 100;

    /// <summary>
    /// 진행률 업데이트 간격 (처리된 파일 수)
    /// </summary>
    public int ProgressUpdateInterval { get; set; } = 10;

    /// <summary>
    /// 실제 CPU 바운드 병렬도를 계산합니다.
    /// </summary>
    public int GetEffectiveCpuParallelism()
    {
        if (MaxCpuBoundParallelism > 0)
            return MaxCpuBoundParallelism;

        return Math.Max(1, Environment.ProcessorCount - 1);
    }

    /// <summary>
    /// 드라이브 유형에 따른 I/O 병렬도를 반환합니다.
    /// </summary>
    public int GetIoParallelism(StorageDriveType driveType)
    {
        return driveType switch
        {
            StorageDriveType.Ssd => SsdIoParallelism,
            StorageDriveType.Hdd => HddIoParallelism,
            StorageDriveType.Network => NetworkIoParallelism,
            StorageDriveType.Removable => HddIoParallelism,
            _ => MaxIoBoundParallelism
        };
    }

    /// <summary>
    /// 기본 설정
    /// </summary>
    public static ParallelProcessingOptions Default => new();

    /// <summary>
    /// 고성능 설정 (리소스 사용량 높음)
    /// </summary>
    public static ParallelProcessingOptions HighPerformance => new()
    {
        MaxCpuBoundParallelism = 0, // 모든 코어 사용
        SsdIoParallelism = 16,
        HddIoParallelism = 4,
        NetworkIoParallelism = 8,
        MaxCpuUsagePercent = 95,
        MaxMemoryUsagePercent = 85,
        EnableAutoThrottling = false
    };

    /// <summary>
    /// 절전 설정 (리소스 사용량 낮음)
    /// </summary>
    public static ParallelProcessingOptions PowerSaving => new()
    {
        MaxCpuBoundParallelism = 2,
        SsdIoParallelism = 4,
        HddIoParallelism = 1,
        NetworkIoParallelism = 2,
        MaxCpuUsagePercent = 50,
        MaxMemoryUsagePercent = 50,
        EnableAutoThrottling = true,
        BatchSize = 50
    };
}

/// <summary>
/// 드라이브 유형
/// </summary>
public enum StorageDriveType
{
    /// <summary>
    /// 알 수 없음
    /// </summary>
    Unknown,

    /// <summary>
    /// SSD (Solid State Drive)
    /// </summary>
    Ssd,

    /// <summary>
    /// HDD (Hard Disk Drive)
    /// </summary>
    Hdd,

    /// <summary>
    /// 네트워크 드라이브 (NAS, SMB 등)
    /// </summary>
    Network,

    /// <summary>
    /// 이동식 드라이브 (USB 등)
    /// </summary>
    Removable
}
