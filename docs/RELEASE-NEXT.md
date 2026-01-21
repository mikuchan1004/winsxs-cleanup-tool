# Next Release Plan — v1.2.0 (Minor)

## Goal
- ResetBase 옵션의 위험성 인지 강화
- 실행 전/후 사용자 안내 명확화
- 릴리즈 산출물의 신뢰도 및 검증 정보 정리

## Scope

### Must-have
- [ ] ResetBase 2중 경고 UX 개선
- [ ] 실행 전 프리체크 메시지 보강
- [ ] 작업 결과 요약 문구 정제
- [ ] CHANGELOG Unreleased 섹션 도입
- [ ] 릴리즈 노트 템플릿 통일
- [ ] SHA-256 / VirusTotal 검증 안내 정리

### Nice-to-have
- [ ] ResetBase 타이핑 확인 옵션 (예: `RESETBASE`)
- [ ] 로그 저장 경로 선택 UX 개선
- [ ] 예상 절감 용량 문구 개선 (추정치 강조)

### Out of scope
- WinSxS 정리 로직 변경
- DISM 옵션/기본값 변경
- 대규모 UI 리워크

## User-facing changes
- ResetBase 옵션에 대해 되돌릴 수 없음을 더 직설적으로 안내
- 성공/실패/중단 상태를 명확히 구분
- 로그 저장 위치를 결과 화면에 명시

## Risk & Safety
- ResetBase는 사용자의 명확한 의도 확인 후에만 실행
- 실패 시 시스템 영향 여부를 명확히 안내
- 네트워크/백그라운드 동작 없음 명시 유지

## Release checklist
- [ ] 버전 번호 업데이트 (csproj / assembly)
- [ ] CHANGELOG.md 업데이트
- [ ] Windows 10/11 스모크 테스트
- [ ] Release ZIP 생성
- [ ] SHA-256 생성 및 첨부
- [ ] GitHub Release 노트 작성
- [ ] VirusTotal 링크 추가 (선택)

## Notes / Decisions
- Minor 릴리즈로 기능 동작은 유지
- UX / 안전 / 신뢰도 중심 개선
