# 📜 Changelog

이 문서는 **WinSxS Cleanup Tool**의 모든 주요 변경 사항을 기록합니다.  
버전 형식은 [Semantic Versioning](https://semver.org/lang/ko/)을 따릅니다.

---

## [1.0.9] - 2026-01-04

### Improved
- UX 메시지 개선
- 분석 및 정리 결과 요약 로그 추가
- 화면 로그 가독성 향상 (중복/진행률 로그 제거)
- 로그 저장 시 전체 로그 유지 및 안정성 개선
- Publish 산출물 구조 정리

### Fixed
- 로그 출력 과다로 인한 UI 가독성 문제 개선

---

## [1.0.8] - 2026-01-04

### Added
- Self-contained 배포 방식 도입 (런타임 포함)
- ZIP 배포 지원
- 게시 결과물 압축 옵션 적용
- 한국어(`ko`) 리소스만 포함하도록 언어 리소스 제한

### Changed
- 버전 관리 구조를 `.csproj` 기준으로 일원화
- Publish 프로세스 안정화
- 실행 파일 생성 방식 개선 (SingleFile + 압축)
- 게시 산출물 파일 구성 정리 및 최소화
- README 문서 개편

### Fixed
- Release 빌드 시 버전이 올바르게 반영되지 않던 문제
- 이전 Publish 산출물 잔재로 인한 버전 혼동 문제
- 게시 결과에 불필요한 파일이 포함되던 문제

---

## [1.0.7] - 2026-01-03

### Added
- ResetBase 기능 추가 (고급 사용자용)
- 실제 절감 용량 계산 기능
- 관리자 권한 자동 확인

### Changed
- UI 가독성 개선
- 로그 출력 방식 개선

---

## [1.0.0] - Initial Release

### Added
- WinSxS(Component Store) 정리 기능
- DISM 기반 안전한 정리 로직
- WinForms GUI 제공
