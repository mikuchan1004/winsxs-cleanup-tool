# 🧹 WinSxS Cleanup Tool

Windows 10 / 11 환경에서  
**WinSxS(Component Store)** 상태를 분석하고 안전하게 정리할 수 있는 GUI 도구입니다.

> DISM 명령어를 기반으로 하며,  
> 복잡한 명령 없이 클릭 한 번으로 정리할 수 있도록 설계되었습니다.

---

## ✨ 주요 기능

- WinSxS 상태 분석 (Analyze)
- 예상 절감 가능 용량 표시
- 안전한 정리 실행 (StartComponentCleanup)
- ResetBase 옵션 지원 (되돌릴 수 없음)
- 정리 중 진행 시간 표시
- UI 멈춤처럼 보이지 않는 진행 상태 표시
- 다크 모드 지원 (설정 자동 저장)

---

<img width="823" height="551" alt="스크린샷 2025-12-29 171813" src="https://github.com/user-attachments/assets/84c9852d-4db2-4254-bf04-1cf1aa76b49d" />

## 🖥 실행 화면

- 분석 및 정리 진행 상황 로그 출력
- 진행 중에도 응답하는 UI
- ProgressBar + 시간 표시로 안정감 강화

---

## 📦 다운로드

GitHub Releases 페이지에서 최신 버전을 다운로드하세요.

제공 파일:
- `WinSxS_Cleanup_Tool.exe`
- `WinSxS_Cleanup_Tool_v1.0.3.zip`

---

## ⚠️ 주의 사항

- **관리자 권한 필수**
- **Windows 10 / 11 전용**
- ResetBase 사용 시:
  - 기존 업데이트 제거 불가
  - 되돌릴 수 없음

> 정리 대상이 없는 시스템에서는  
> Cleanup 버튼이 비활성화될 수 있으며 이는 정상 동작입니다.

---

## 🛡 Windows SmartScreen 안내

본 도구는 개인 개발 도구로 서명되지 않았습니다.  
처음 실행 시 SmartScreen 경고가 표시될 수 있습니다.

- "추가 정보" → "실행" 선택 시 정상 실행됩니다.

---

## 🧩 사용 기술

- PowerShell
- Windows Forms
- DISM (Deployment Image Servicing and Management)

---

## 📅 릴리즈 정책

- 짧은 주기의 소규모 릴리즈 지향
- UI/UX 및 안정성 개선 중심 업데이트

---

## 🙌 Credits

Powered by ChatGPT  
Designed & Built with care

---

> 이 도구는 시스템 유지 관리를 돕기 위한 유틸리티이며  
> 사용 결과에 대한 책임은 사용자에게 있습니다.
