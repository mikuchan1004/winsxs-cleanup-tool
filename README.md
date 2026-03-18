# 📜 WinSxS Cleanup Tool (C#)

![Platform: Windows 10/11](https://img.shields.io/badge/Platform-Windows%2010%2F11-blue) ![Framework: .NET](https://img.shields.io/badge/Framework-.NET%208.0%20(Windows)-purple) ![License: MIT](https://img.shields.io/badge/License-MIT-green) ![Release: v3.0.0](https://img.shields.io/badge/Release-v3.0.0-blue) [![VirusTotal: 1/68](https://img.shields.io/badge/VirusTotal-1%2F73-orange)](https://www.virustotal.com/gui/file/db7b52550fdd627fead4e0627bcf1dc44159276579c2308ea360e344d004749c)

**DISM 기반 WinSxS(Component Store) 분석 및 정리 GUI 유틸리티**
Windows 기본 명령을 사용하여 불필요한 백업이나 구버전 데이터를 안전하게 분석하고 정리합니다.

> 💡 본 프로그램은 Google Gemini의 도움을 받아 C#으로 제작되었으며, 
> 네트워크 통신이나 백그라운드 상주 없이 투명한 '단일 실행'을 지향합니다.

---

<img width="791" height="443" alt="스크린샷 2026-03-19 035222" src="https://github.com/user-attachments/assets/364a323d-bee9-47b8-9ea1-fc1e0d152b62" />


### ✨ 주요 기능
* **DISM 기반 WinSxS 분석**: 안전한 컴포넌트 분석(AnalyzeComponentStore) 수행
* **핵심 절감 용량 측정 및 표시**: 실제 제거 가능한 용량을 정밀하게 계산
* **안전 요소 정리**: 권장되는 시스템 정리(StartComponentCleanup) 실행
* **ResetBase 지원**: 누적된 업데이트 패키지 기반을 초기화하여 용량 확보 극대화
* **사용자 친화적 UX/UI**: 
  * 정리 전/후 결과 요약 카드 제공
  * 실시간 작업 로그 및 진행률 표시
  * 관리자 권한 자동 확인 및 안내

---

### 🔍 "예상 실감 용량"에 대해
Windows의 DISM 엔진은 단순한 파일 크기 합계가 아닌 '논리적 절감량'을 제공하므로, 
본 도구에서는 다음과 같이 표시됩니다.

* **정리 가능 상태**: DISM 보고 결과에 따라 '권장' 및 '예상 절감량' 표시
* **실제 절감량**: 작업 완료 후 실제 디스크 점유율 변화를 추적하여 계산
  * *주의: 하드링크(Hardlink) 구조 특성상 탐색기 상의 수치와 실제 물리적 용량은 차이가 있을 수 있습니다.*

---

### 💻 시스템 요구사항
* **OS**: Windows 10 / 11 (빌드 17763 이상)
* **런타임**: .NET 8.0 Desktop Runtime (Self-contained 배포 시 불필요)
* **권한**: 관리자 권한 필수

---

### 🚀 사용 방법
1. `WinSxSCleanupTool.exe`를 **관리자 권한**으로 실행합니다.
2. [분석] 버튼을 눌러 현재 시스템의 정리 가능 용량을 확인합니다.
3. [정리] 또는 [ResetBase]를 실행합니다.
4. 작업 완료 후 제공되는 **요약 보고서**를 확인합니다.

---

### ⚠️ 주의 사항
* **ResetBase는 되돌릴 수 없습니다**: 이전 업데이트로의 롤백이 불가능해지므로 신중히 사용하세요.
* **Windows 업데이트**: 업데이트가 대기 중이거나 설치 중일 때는 작동하지 않을 수 있습니다.
* **시간 소요**: 시스템 성능 및 정리 데이터 양에 따라 수 분에서 수십 분이 소요됩니다.

---

### 🛡️ 보안 / 오탐 관련
* **네트워크 통신** ❌
* **백그라운드 상주** ❌
* **사용자 데이터 수집** ❌
* **오탐 안내**: 시스템 파일을 다루는 도구 특성상 일부 백신에서 `Agent.JIN` 등으로 오탐될 수 있으나, 오픈소스로 공개된 안전한 도구입니다.

---

### 📦 배포 형태
* **Self-contained**: 별도의 .NET 설치 없이 즉시 실행 가능
* **단일 파일**: 모든 리소스를 포함한 단일 `.exe` 구성
* **다이어트 패키징**: 런타임 최적화로 배포 용량 최소화

---

### 🛠️ v3.0.0 체인지로그 (요약)

#### ✨ 주요 기능 개선 (Major Improvements)
* **분석 로직 고도화**: WinSXS 알고리즘 최적화로 정리 효율 및 식별 정밀도 향상
* **비동기 엔진 도입**: 작업 중 UI 프리징 방지 및 실시간 로그 시스템 가이드 강화

#### 🛠️ 버그 및 안정성 수정 (Bug Fixes)
* **권한 예외 처리**: 특정 환경에서의 비정상 종료 현상 수정 및 권한 획득 로직 개선
* **경로 인식 오류 해결**: 특수문자나 공백이 포함된 실행 경로에서의 논리 오류 수정

#### 📦 배포 및 기타 (Maintenance)
* **아티팩트 최적화**: 배포 패키지에서 불필요한 소스/캐시를 제거하여 용량 최적화 완료
* **보안 투명성**: 백신 오탐 대응을 위한 코드 정제 및 VirusTotal 안내 추가

🔗 **전체 변경 내역**: [CHANGELOG.md](./CHANGELOG.md)
