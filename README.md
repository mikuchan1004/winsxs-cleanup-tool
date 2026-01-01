# 🧹 WinSxS Cleanup Tool (C#)

[![Windows](https://img.shields.io/badge/Platform-Windows%2010%2B-blue?logo=windows)](#)
[![.NET](https://img.shields.io/badge/.NET-net10.0--windows-blueviolet?logo=dotnet)](#)
[![License](https://img.shields.io/badge/License-MIT-green)](#license)
[![Release](https://img.shields.io/github/v/release/USERNAME/REPO?include_prereleases&label=Release)](https://github.com/mikuchan1004/winsxs-cleanup-tool/releases/)
[![VirusTotal](https://img.shields.io/badge/VirusTotal-0%2F63-brightgreen?logo=virustotal&logoColor=white)](
https://www.virustotal.com/gui/file/8e24e6073c5639f82514667a7e0921821d4dd3c059573f54dc7806385745b500
)

**DISM 기반 WinSxS(Component Store) 분석·정리 GUI 유틸리티**  
Windows 기본 명령만 사용하며, 불필요한 백그라운드 동작이나 네트워크 통신이 없습니다.


Windows의 **WinSxS(Component Store)** 를  
DISM 공식 명령어만 사용해 **분석 및 정리**하는 WinForms GUI 유틸리티입니다.

> ⚙ 개인이 제작한 도구이며, 스크립트/코드는 ChatGPT의 도움을 받아 작성되었습니다.  
> 🌐 네트워크 통신, 백그라운드 상주, 광고 등은 **일절 없습니다**.

---

<img width="908" height="609" alt="스크린샷 2026-01-01 213536" src="https://github.com/user-attachments/assets/c129c7fa-e55f-4b10-8f0a-2fd67a9443cd" />


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

## 📦 배포 형태

- 단일 실행 파일 (`.exe`)
- 별도 설치 불필요
- .NET Runtime 포함(Self-contained)

---

## 🧾 라이선스

이 프로젝트는 개인 학습/공유 목적의 도구입니다.  
상업적 사용 시 책임은 사용자에게 있습니다.

---

## 🛠 제작 정보

- Language: C#
- Framework: .NET (Windows)
- UI: WinForms
- Vendor: Powered by ChatGPT
