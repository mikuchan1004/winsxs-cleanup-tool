# 📜 WinSxS Cleanup Tool (C#)

![Platform: Windows 10/11](https://img.shields.io/badge/Platform-Windows%2010%2F11-blue) ![Framework: .NET](https://img.shields.io/badge/Framework-.NET%208.0%20(Windows)-purple) ![License: MIT](https://img.shields.io/badge/License-MIT-green) [![Latest Release](https://img.shields.io/github/v/release/mikuchan1004/winsxs-cleanup-tool?logo=github)](https://github.com/mikuchan1004/winsxs-cleanup-tool/releases/latest)
[![VirusTotal Safe](https://img.shields.io/badge/VirusTotal-Safe-brightgreen?style=flat-square&logo=virustotal)](https://www.virustotal.com/gui/file/bdea7cad6c3404897f12fccc4e402f7d2b29514c235f4a1f949e5325d7e3a6b1?nocache=1)
![Assisted by Gemini](https://img.shields.io/badge/Assisted%20by-Google%20Gemini-blue?logo=googlegemini&logoColor=white)

**DISM 기반 WinSxS(Component Store) 분석 및 정리 GUI 유틸리티**
Windows 기본 명령을 사용하여 불필요한 백업이나 구버전 데이터를 안전하게 분석하고 정리합니다.

> 💡 본 프로그램은 Google Gemini의 도움을 받아 C#으로 제작되었으며, 
> 네트워크 통신이나 백그라운드 상주 없이 투명한 '단일 실행'을 지향합니다.

---


<img width="786" height="443" alt="스크린샷 2026-03-22 205950" src="https://github.com/user-attachments/assets/50ab6a74-1498-461b-bef4-a75ae09a9bc2" />


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

## 📝 v3.1.0 업데이트 내역 (Changelog)

이번 버전에서는 진단 로직을 고도화하여 사용자 편의성을 높이고, 프로그램 실행 환경을 더욱 가볍게 최적화했습니다.

### ✨ 더 똑똑해진 상태 진단
* **진단 정확도 개선**: 내 컴퓨터에 실제 청소가 필요한지 판단하는 분석 로직을 정교화했습니다.
* **직관적인 안내**: 분석 후 **"📢 지금 정리를 추천해요"** 또는 **"✅ 아주 깨끗한 상태예요"** 문구와 아이콘을 통해 조치 필요 여부를 한눈에 알 수 있습니다.
* **디자인 시인성 확보**: 용량 수치와 단위(GB)를 명확히 구분하여 가독성을 높이고, 화면 표시가 겹치던 현상을 해결했습니다.

### ⚡ 성능 및 용량 최적화
* **용량 경량화**: 프로그램 구조 최적화를 통해 전체 용량을 **70MB대**로 줄여 가벼운 실행 환경을 구축했습니다.
* **분석 속도 향상**: 최신 최적화 기술을 적용하여 시스템 찌꺼기를 찾아내는 속도가 이전 버전보다 더 빨라졌습니다.

### 🛠️ 사용성 및 안정성 강화
* **단일 파일 배포**: 별도의 설치나 라이브러리 파일 없이, **EXE 파일 하나**만으로 즉시 실행됩니다.
* **실행 안정성 개선**: 특정 환경에서 데이터가 정상적으로 표시되지 않던 문제를 해결하여 프로그램의 신뢰도를 높였습니다.

---

🔗 **전체 변경 내역**: [CHANGELOG.md](./CHANGELOG.md)
