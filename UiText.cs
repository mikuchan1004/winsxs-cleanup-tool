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
            "이 옵션을 실행하면:\n" +
            "• Windows 업데이트 제거가 불가능해집니다\n" +
            "• 시스템 변경 사항을 되돌릴 수 없습니다\n" +
            "• 향후 문제 발생 시 복구가 어려워질 수 있습니다\n\n" +
            "이 옵션은 숙련된 사용자에게만 권장됩니다.\n" +
            "정말로 ResetBase 옵션을 사용하시겠습니까?";

        // =========================
        // ResetBase - final confirm form
        // =========================
        public const string ResetBaseFinalTitle =
            "🚫 ResetBase 최종 확인";

        public const string ResetBaseFinalMessage =
            "이 작업은 되돌릴 수 없습니다.\n\n" +
            "ResetBase 옵션을 실행하면:\n" +
            "• 현재 설치된 Windows 구성 요소만 유지됩니다\n" +
            "• 이전 상태로 복구할 수 없습니다\n" +
            "• 문제가 발생해도 되돌릴 수 없습니다\n\n" +
            "위 내용을 충분히 이해하였으며,\n" +
            "모든 책임을 인지한 상태에서 실행하시겠습니까?";

        public const string ResetBaseConfirmCheck =
            "위 내용을 이해했으며 ResetBase를 실행하겠습니다.";

        public const string ResetBaseExecuteButtonText =
            "예, 실행합니다";

        public const string CancelButtonText =
            "취소";
    }
}
