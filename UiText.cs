#nullable enable

namespace WinSxSCleanupTool
{
    internal static class UiText
    {
        // =========================
        // Admin privilege
        // =========================
        public const string AdminRequiredTitle =
            "관리자 권한 필요";

        public const string AdminRequiredMessage =
            "이 작업은 Windows 시스템 정리를 포함하므로\n" +
            "관리자 권한이 필요합니다.\n\n" +
            "관리자 권한으로 다시 실행해 주세요.";

        // =========================
        // General
        // =========================
        public const string AppReadyStatus =
            "대기 (작업을 선택해 주세요)";

        public const string AnalyzeRunningStatus =
            "WinSxS 분석 중입니다. 잠시만 기다려 주세요...";

        public const string CleanupRunningStatus =
            "Windows 정리 중입니다. PC를 종료하지 마세요.";

        public const string ResetBaseRunningStatus =
            "ResetBase 정리 중입니다. PC를 종료하지 마세요.";

        public const string CompletedStatus =
            "완료";

        public const string CanceledStatus =
            "취소됨";

        public const string ErrorStatus =
            "오류";

        // =========================
        // Pre-cleanup hint
        // =========================
        public const string CleanupWithoutAnalyzeTitle =
            "분석 없이 정리 실행";

        public const string CleanupWithoutAnalyzeMessage =
            "분석을 먼저 실행하면:\n" +
            "• 예상 절감량(상한)\n" +
            "• 정리 전/후 비교(실제 절감량)\n" +
            "을 더 정확히 확인할 수 있습니다.\n\n" +
            "그래도 지금 바로 정리를 실행하시겠습니까?";

        // =========================
        // General Cleanup (non-ResetBase)
        // =========================
        public const string CleanupStartLog =
            "▶ Windows 구성 요소 정리를 시작합니다...";

        public const string CleanupInProgressStatus =
            "Windows 정리 진행 중";

        // =========================
        // ResetBase - Logs/Status
        // =========================
        public const string ResetBaseStartLog =
            "▶ ResetBase 정리를 시작합니다. (되돌릴 수 없음)";

        public const string ResetBaseInProgressStatus =
            "ResetBase 정리 진행 중";

        // =========================
        // ResetBase - 1st warning dialog
        // =========================
        public const string ResetBaseWarnTitle =
            "⚠️ ResetBase 옵션 주의";

        public const string ResetBaseWarnMessage =
            "ResetBase 옵션은 Windows 구성 요소 저장소(WinSxS)를\n" +
            "되돌릴 수 없는 상태로 정리합니다.\n\n" +
            "ResetBase 실행 시:\n" +
            "• Windows 업데이트 제거가 불가능해집니다\n" +
            "• 시스템 롤백/되돌리기가 불가능합니다\n" +
            "• 문제 발생 시 복구가 더 어려워질 수 있습니다\n\n" +
            "이 옵션은 숙련된 사용자에게만 권장됩니다.\n" +
            "정말로 ResetBase를 진행하시겠습니까?";

        // =========================
        // ResetBase - final confirm form
        // =========================
        public const string ResetBaseFinalTitle =
            "🚫 ResetBase 최종 확인";

        public const string ResetBaseFinalMessage =
            "⚠ 이 작업은 되돌릴 수 없습니다.\n\n" +
            "ResetBase 실행 시:\n" +
            "• Windows 업데이트 제거 불가\n" +
            "• 시스템 롤백(되돌리기) 불가\n" +
            "• 문제 발생 시 복구가 어려울 수 있음\n\n" +
            "위 내용을 이해하였고,\n" +
            "되돌릴 수 없음을 동의한 상태에서 진행하시겠습니까?";

        public const string ResetBaseConfirmCheck =
            "위 위험을 이해했으며, 되돌릴 수 없음을 동의합니다.";

        public const string ResetBaseExecuteButtonText =
            "예, 실행합니다";

        public const string CancelButtonText =
            "취소";

        // =========================
        // Save log
        // =========================
        public const string SaveLogTitle =
            "로그 저장";

        public const string SaveLogDoneTitle =
            "로그 저장 완료";

        public const string SaveLogDoneMessage =
            "로그 파일이 성공적으로 저장되었습니다.\n\n" +
            "문제 발생 시, 이 로그 파일을 함께 전달해 주세요.";
    }
}
