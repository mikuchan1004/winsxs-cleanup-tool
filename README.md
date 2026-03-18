# 🧹 WinSxS Cleanup Tool (C#)

[![Windows](https://img.shields.io/badge/Platform-Windows%2010%2B-blue?logo=windows)](#)
[![.NET](https://img.shields.io/badge/.NET-net8.0--windows-blueviolet?logo=dotnet)](#)
[![License](https://img.shields.io/badge/License-MIT-green)](#license)
[![Release](https://img.shields.io/github/v/release/mikuchan1004/winsxs-cleanup-tool?include_prereleases&label=Release)](
https://github.com/mikuchan1004/winsxs-cleanup-tool/releases
)
[![VirusTotal](https://img.shields.io/badge/VirusTotal-0%2F63-brightgreen?logo=virustotal&logoColor=white)](
https://www.virustotal.com/gui/file/db7b52550fdd627fead4e0627bcf1dc44159276579c2308ea360e344d004749c
)

**DISM 기반 WinSxS(Component Store) 분석·정리 GUI 유틸리티**  
Windows 기본 명령만 사용하며, 불필요한 백그라운드 동작이나 네트워크 통신이 없습니다.


Windows의 **WinSxS(Component Store)** 를  
DISM 공식 명령어만 사용해 **분석 및 정리**하는 WinForms GUI 유틸리티입니다.

> ⚙ 개인이 제작한 도구이며, 스크립트/코드는 Google Gemini의 도움을 받아 작성되었습니다.  
> 🌐 네트워크 통신, 백그라운드 상주, 광고 등은 **일절 없습니다**.

---


<img width="791" height="443" alt="스크린샷 2026-03-19 035222" src="https://github.com/user-attachments/assets/42e4146d-8fcf-4a6c-9b4a-a0ff42738265" />



## ✨ 주요 기능

- ✔ DISM 기반 WinSxS 분석 (`AnalyzeComponentStore`)
- ✔ 예상 절감 용량 파싱 및 표시
- ✔ 구성 요소 정리 (`StartComponentCleanup`)
- ✔ ResetBase 지원 (⚠ 되돌릴 수 없음)
- ✔ **정리 후 재분석 옵션**
  - 실제 절감량 계산
  - 정리 전 / 후 값 비교 표시
- ✔ 진행률 표시(가능한 범위 내)
- ✔ 상세 로그 출력
- ✔ 설정 자동 저장(JSON)
- ✔ 아이콘 포함 단일 실행 파일(EXE)

---

## 📌 “예상 절감 용량”에 대해

Windows의 DISM은 **정확한 ‘예상 절감 용량’을 제공하지 않습니다.**

본 도구에서는 다음과 같이 표시합니다:

- **정리 가능 상한**
  - DISM 분석 결과의  
    `백업 및 기능 사용 안 함 (Backups and Disabled Features)` 값
- **실제 절감량**
  - 정리 전/후 WinSxS 실제 크기를 비교하여 계산

> 즉,  
> **상한 = 이론적으로 정리 가능한 최대치**  
> **실제 절감량 = 실제로 줄어든 용량**

환경에 따라 두 값은 다를 수 있습니다.

## 🖥 시스템 요구사항

- Windows 10 1809 (빌드 17763) 이상
- x64 환경
- 관리자 권한 필요(UAC)

---

## 🚀 사용 방법

1. `WinSxSCleanupTool.exe` 실행 (관리자 권한)
2. **[분석]** 버튼 클릭
   - 예상 절감 용량 확인
3. **[정리]** 또는 **[ResetBase]** 실행
4. (선택) **정리 후 재분석** 체크 시
   - 실제 절감량 자동 계산

---

## ⚠️ 주의 사항

- **ResetBase는 되돌릴 수 없습니다**
- Windows 업데이트 제거가 불가능해질 수 있습니다
- 반드시 내용을 이해한 후 사용하세요
- DISM 출력 언어/형식에 따라 일부 환경에서는
  - 예상 절감 용량 파싱이 제한될 수 있습니다

---

## 🛡 보안 / 오탐 관련

- 네트워크 통신 ❌
- 백그라운드 상주 ❌
- PowerShell 스크립트 삽입 ❌
- Windows 공식 DISM만 사용

일부 백신에서 **관리자 권한 + 시스템 정리 도구 특성상 오탐**이 발생할 수 있습니다.

---

## 📦 배포 형태

- Self-contained (런타임 포함)
- 실행 파일 + 필수 네이티브 DLL만 포함
- 불필요한 언어 리소스 제거 (ko 전용)

---

## 🧾 라이선스

이 프로젝트는 개인 학습/공유 목적의 도구입니다.  
상업적 사용 시 책임은 사용자에게 있습니다.

---

## 🛠 제작 정보

- Language: C#
- Framework: .NET (Windows)
- UI: WPF
- Vendor: Powered by Google Gemini

---

## 📜 Changelog (요약)

## 🛠️ v3.0.0 체인지로그 (Changelog)

### ✨ 주요 기능 개선 (Major Improvements)
* **WinSXS 분석 알고리즘 고도화**: 시스템 컴포넌트 저장소 내의 불필요한 백업 및 구버전 파일을 더 안전하고 정밀하게 식별하여 **정리 효율을 극대화**했습니다.
* **비동기 작업 엔진 도입**: 대용량 파일 정리 시 UI가 멈추는 프리징 현상을 해결하고, 실시간 진행 상황을 확인할 수 있는 **상태 로그 시스템**을 강화했습니다.

### 🐛 버그 및 안정성 수정 (Bug Fixes)
* **관리자 권한 예외 처리**: 특정 환경에서 권한 문제로 인해 프로그램이 강제 종료되던 현상을 수정했습니다.
* **경로 인식 오류 해결**: 사용자 프로필이나 설치 경로에 특수문자가 포함된 경우 발생하던 논리적 '찐빠'를 완전히 해결했습니다.

### 📦 배포 및 기타 (Maintenance)
* **초경량 패키징**: 빌드 아티팩트 다이어트를 통해 소스 코드와 캐시를 제거하고, **순수 실행 파일 위주의 슬림한 배포판**을 구성했습니다. (용량 최적화 완료)
* **VirusTotal 오탐 대응**: 일부 백신 엔진의 Generic 진단(Agent.JIN 등)을 최소화하기 위한 코드 구조 정제 작업을 진행했습니다.

➡ 전체 변경 내역: [CHANGELOG.md](./CHANGELOG.md)






