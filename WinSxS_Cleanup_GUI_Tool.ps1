#requires -version 5.1
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing

# =====================================================
# App Info / Paths
# =====================================================
$AppName    = "WinSxS Cleanup Tool"
$AppVersion = "v1.0.4"
$Vendor     = "Powered by ChatGPT"

# ✅ 설정 파일(JSON) - System.Text.Json 없이 ConvertTo/From-Json로 처리
$ConfigPath = Join-Path $env:APPDATA "WinSxS_Cleanup_Tool.json"

# (선택) 외부 ico 파일로 폼 아이콘을 강제 지정하고 싶다면 경로 지정
# $IconPath = "C:\Path\broom.ico"
$IconPath = $null

# =====================================================
# Admin / OS Check
# =====================================================
function Show-Err([string]$msg, [string]$title="오류") {
    [System.Windows.Forms.MessageBox]::Show($msg, $title, [System.Windows.Forms.MessageBoxButtons]::OK, [System.Windows.Forms.MessageBoxIcon]::Error) | Out-Null
}

try {
    $principal = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
    if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
        Show-Err "관리자 권한으로 실행해주세요." "권한 오류"
        exit
    }

    $build = [int](Get-CimInstance Win32_OperatingSystem).BuildNumber
    if ($build -lt 10240) {
        Show-Err "Windows 10 / 11에서만 지원됩니다." "지원되지 않는 OS"
        exit
    }
} catch {
    Show-Err ("환경 체크 중 오류: " + $_.Exception.Message)
    exit
}

# =====================================================
# Helpers (UI invoke / JSON config / size parsing)
# =====================================================
function UI([System.Windows.Forms.Control]$ctl, [ScriptBlock]$sb) {
    if ($ctl.InvokeRequired) {
        $null = $ctl.BeginInvoke([Action]{
            try { & $sb } catch {}
        })
    } else {
        & $sb
    }
}

function Load-Config {
    $default = @{
        Theme = "Light"   # "Dark" or "Light"
    }
    if (-not (Test-Path $ConfigPath)) { return $default }

    try {
        $raw = Get-Content -Path $ConfigPath -Raw -ErrorAction Stop
        $obj = $raw | ConvertFrom-Json -ErrorAction Stop
        if (-not $obj.Theme) { $obj.Theme = $default.Theme }
        return $obj
    } catch {
        return $default
    }
}

function Save-Config($cfg) {
    try {
        $json = ($cfg | ConvertTo-Json -Depth 5)
        $dir  = Split-Path $ConfigPath -Parent
        if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Path $dir | Out-Null }
        Set-Content -Path $ConfigPath -Value $json -Encoding UTF8
    } catch {
        # 저장 실패는 치명적이지 않으니 조용히 무시
    }
}

function Convert-SizeToMB([string]$text) {
    # ex) "1.23 GB" / "512 MB" / "0 bytes" / "1,024 KB"
    if ([string]::IsNullOrWhiteSpace($text)) { return 0.0 }

    $t = $text.Trim()

    if ($t -match '0\s*bytes' -or $t -match '^0\s*B') { return 0.0 }

    # 숫자/단위 분리
    # 허용 예: "12.03GB", "12,034 MB", "1024 KB"
    if ($t -match '([0-9\.,]+)\s*([A-Za-z가-힣]+)') {
        $numRaw = $matches[1].Replace(",", "")
        $unit   = $matches[2].Trim()

        $num = 0.0
        [double]::TryParse($numRaw, [ref]$num) | Out-Null

        switch -Regex ($unit) {
            'TB|테라' { return $num * 1024 * 1024 }
            'GB|기가' { return $num * 1024 }
            'MB|메가' { return $num }
            'KB|킬로' { return $num / 1024 }
            'B|바이트|bytes' { return $num / 1024 / 1024 }
            default { return $num } # 알 수 없으면 MB로 간주
        }
    }

    return 0.0
}

function Format-MB([double]$mb) {
    if ($mb -ge 1024) {
        $gb = $mb / 1024
        return ("{0:N2} GB" -f $gb)
    }
    return ("{0:N0} MB" -f $mb)
}

function Parse-Reclaimable([string[]]$lines) {
    # DISM 출력(한/영)에서 reclaimable 라인 찾기
    $patterns = @(
        'Reclaimable Package Size',
        'Reclaimable',
        '정리 가능',
        '정리\s*가능\s*패키지',
        '구성\s*요소\s*저장소.*정리\s*가능'
    )

    foreach ($ln in $lines) {
        foreach ($p in $patterns) {
            if ($ln -match $p) {
                # ":" 뒤만 추출 시도
                if ($ln -match ':\s*(.+)$') {
                    return $matches[1].Trim()
                }
                # 콜론이 없다면 전체에서 숫자/단위만 잡기
                if ($ln -match '([0-9\.,]+\s*(TB|GB|MB|KB|bytes|바이트|기가|메가|킬로|테라))') {
                    return $matches[1].Trim()
                }
            }
        }
    }
    return $null
}

# =====================================================
# Theme
# =====================================================
function Apply-Theme([string]$mode) {
    if ($mode -eq "Dark") {
        $bg  = [System.Drawing.Color]::FromArgb(32,32,32)
        $fg  = [System.Drawing.Color]::White
        $btn = [System.Drawing.Color]::FromArgb(64,64,64)
        $box = [System.Drawing.Color]::FromArgb(24,24,24)
    } else {
        $bg  = [System.Drawing.Color]::White
        $fg  = [System.Drawing.Color]::Black
        $btn = [System.Drawing.Color]::Gainsboro
        $box = [System.Drawing.Color]::White
    }

    $form.BackColor = $bg

    # 컨트롤 순회 (간단 버전)
    foreach ($c in $form.Controls) {
        if ($c -is [System.Windows.Forms.Button]) {
            $c.BackColor = $btn
            $c.ForeColor = $fg
        } elseif ($c -is [System.Windows.Forms.CheckBox] -or $c -is [System.Windows.Forms.Label]) {
            $c.ForeColor = $fg
        } elseif ($c -is [System.Windows.Forms.TextBox]) {
            $c.BackColor = $box
            $c.ForeColor = $fg
        } elseif ($c -is [System.Windows.Forms.ProgressBar]) {
            # ProgressBar는 OS 테마 영향이라 색상 직접 제어가 제한적
        }
    }
}

# =====================================================
# State
# =====================================================
$script:IsBusy      = $false
$script:IsCleaning  = $false
$script:StartTime   = $null
$script:LastAnalyzeLines = @()
$script:LastReclaimText  = $null
$script:LastReclaimMB    = 0.0

# 분석/정리 공통: 경과 시간 표시용 타이머
$script:ElapsedTimer = New-Object System.Windows.Forms.Timer
$script:ElapsedTimer.Interval = 500

# =====================================================
# UI
# =====================================================
$form = New-Object System.Windows.Forms.Form
$form.Text = "$AppName $AppVersion"
$form.Size = New-Object System.Drawing.Size(880, 580)
$form.StartPosition = "CenterScreen"
$form.MaximizeBox = $false

# ✅ 아이콘: (1) 외부 ico가 있으면 그걸 우선, (2) 없으면 exe에 포함된 아이콘 추출
try {
    if ($IconPath -and (Test-Path $IconPath)) {
        $form.Icon = New-Object System.Drawing.Icon($IconPath)
    } else {
        $form.Icon = [System.Drawing.Icon]::ExtractAssociatedIcon([System.Diagnostics.Process]::GetCurrentProcess().MainModule.FileName)
    }
} catch { }

$btnAnalyze = New-Object System.Windows.Forms.Button
$btnAnalyze.Text = "분석"
$btnAnalyze.Location = New-Object System.Drawing.Point(20,20)
$btnAnalyze.Size = New-Object System.Drawing.Size(120,35)

$btnCleanup = New-Object System.Windows.Forms.Button
$btnCleanup.Text = "정리 시작"
$btnCleanup.Location = New-Object System.Drawing.Point(150,20)
$btnCleanup.Size = New-Object System.Drawing.Size(120,35)
$btnCleanup.Enabled = $false

$btnHelp = New-Object System.Windows.Forms.Button
$btnHelp.Text = "도움말"
$btnHelp.Location = New-Object System.Drawing.Point(280,20)
$btnHelp.Size = New-Object System.Drawing.Size(90,35)

$chkResetBase = New-Object System.Windows.Forms.CheckBox
$chkResetBase.Text = "ResetBase (되돌릴 수 없음)"
$chkResetBase.Location = New-Object System.Drawing.Point(390,26)
$chkResetBase.Size = New-Object System.Drawing.Size(220,24)

$chkDark = New-Object System.Windows.Forms.CheckBox
$chkDark.Text = "다크 모드"
$chkDark.Location = New-Object System.Drawing.Point(640,26)
$chkDark.AutoSize = $true

$logBox = New-Object System.Windows.Forms.TextBox
$logBox.Location = New-Object System.Drawing.Point(20,70)
$logBox.Size = New-Object System.Drawing.Size(820,380)
$logBox.Multiline = $true
$logBox.ReadOnly = $true
$logBox.ScrollBars = "Vertical"

$progress = New-Object System.Windows.Forms.ProgressBar
$progress.Location = New-Object System.Drawing.Point(20,470)
$progress.Size = New-Object System.Drawing.Size(820,18)
$progress.Style = 'Blocks'
$progress.Value = 0

$statusLabel = New-Object System.Windows.Forms.Label
$statusLabel.Location = New-Object System.Drawing.Point(20,500)
$statusLabel.Size = New-Object System.Drawing.Size(380,20)
$statusLabel.Text = "상태: 대기 중"

$reclaimLabel = New-Object System.Windows.Forms.Label
$reclaimLabel.Location = New-Object System.Drawing.Point(420,500)
$reclaimLabel.Size = New-Object System.Drawing.Size(420,20)
$reclaimLabel.Text = "예상 절감 용량: -"

$form.Controls.AddRange(@(
    $btnAnalyze,$btnCleanup,$btnHelp,$chkResetBase,$chkDark,
    $logBox,$progress,$statusLabel,$reclaimLabel
))

# =====================================================
# Init 메시지 / 설정 로드
# =====================================================
$cfg = Load-Config
$form.Add_Shown({
    $logBox.Clear()
    $logBox.AppendText("$AppName $AppVersion`r`n")
    $logBox.AppendText("$Vendor`r`n`r`n")
    $logBox.AppendText("▶ [분석]을 눌러 WinSxS 상태를 확인하세요.`r`n")
    $logBox.AppendText("▶ 분석/정리 중 UI가 멈춘 듯 보여도 정상입니다. (실시간 로그로 진행 확인)`r`n")
    $logBox.AppendText("▶ ResetBase는 되돌릴 수 없습니다.`r`n`r`n")

    if ($cfg.Theme -eq "Dark") {
        $chkDark.Checked = $true
        Apply-Theme "Dark"
    } else {
        Apply-Theme "Light"
    }
})

# =====================================================
# Timer: 경과시간 표시 (분석/정리 공통)
# =====================================================
$script:ElapsedTimer.Add_Tick({
    if (-not $script:IsBusy -or -not $script:StartTime) { return }
    $el = (Get-Date) - $script:StartTime
    $m = [int]$el.TotalMinutes
    $s = $el.Seconds

    if ($script:IsCleaning) {
        $statusLabel.Text = "상태: 정리 중... (${m}분 ${s}초 경과)"
    } else {
        $statusLabel.Text = "상태: 분석 중... (${m}분 ${s}초 경과)"
    }
})

# =====================================================
# Help
# =====================================================
$btnHelp.Add_Click({
    [System.Windows.Forms.MessageBox]::Show(@"
[기능]
• 분석: WinSxS(Component Store) 상태를 확인하고 예상 절감 용량을 표시합니다.
• 정리 시작: StartComponentCleanup 실행으로 정리 작업을 수행합니다.
• ResetBase: 되돌릴 수 없는 옵션입니다. (2단 경고 후 실행)

[소요 시간]
• 분석: 보통 1~5분
• 정리: 보통 5~20분 이상 (환경에 따라 더 오래 걸릴 수 있음)

[참고]
• 분석/정리 중에는 DISM 실행 때문에 UI가 멈춘 것처럼 보일 수 있으나 정상입니다.
• 본 툴은 Windows 10/11 + 관리자 권한이 필요합니다.
"@,
    "도움말",
    [System.Windows.Forms.MessageBoxButtons]::OK,
    [System.Windows.Forms.MessageBoxIcon]::Information
    ) | Out-Null
})

# =====================================================
# Dark mode toggle (JSON 저장)
# =====================================================
$chkDark.Add_CheckedChanged({
    if ($chkDark.Checked) {
        $cfg.Theme = "Dark"
        Apply-Theme "Dark"
    } else {
        $cfg.Theme = "Light"
        Apply-Theme "Light"
    }
    Save-Config $cfg
})

# =====================================================
# DISM runner (Task 기반 비동기 + 실시간 라인 파싱)
# =====================================================
function Run-DismAsync {
    param(
        [Parameter(Mandatory)][string]$Arguments,   # dism arguments only (no "dism")
        [Parameter(Mandatory)][System.Windows.Forms.Control]$UiControl,
        [Parameter(Mandatory)][ScriptBlock]$OnLine,
        [Parameter(Mandatory)][ScriptBlock]$OnDone
    )

    # UI thread에서 호출됨을 가정
    $script:IsBusy = $true

    # Task에서 실행할 액션
    $action = [Action]{
        $allLines = New-Object System.Collections.Generic.List[string]

        try {
            $psi = New-Object System.Diagnostics.ProcessStartInfo
            $psi.FileName = "dism.exe"
            $psi.Arguments = $Arguments
            $psi.RedirectStandardOutput = $true
            $psi.RedirectStandardError  = $true
            $psi.UseShellExecute = $false
            $psi.CreateNoWindow  = $true
            $psi.StandardOutputEncoding = [System.Text.Encoding]::UTF8
            $psi.StandardErrorEncoding  = [System.Text.Encoding]::UTF8

            $p = New-Object System.Diagnostics.Process
            $p.StartInfo = $psi
            $null = $p.Start()

            # stdout 실시간
            while (($line = $p.StandardOutput.ReadLine()) -ne $null) {
                $allLines.Add($line)

                $lnCopy = $line
                UI $UiControl {
                    try { & $OnLine $lnCopy } catch {}
                }
            }

            # stderr도 수집 (남아있으면)
            while (($eline = $p.StandardError.ReadLine()) -ne $null) {
                if (-not [string]::IsNullOrWhiteSpace($eline)) {
                    $allLines.Add("[ERR] $eline")
                    $eCopy = $eline
                    UI $UiControl {
                        try { & $OnLine ("[ERR] " + $eCopy) } catch {}
                    }
                }
            }

            $p.WaitForExit()

            $exitCode = $p.ExitCode
            UI $UiControl {
                try { & $OnDone $exitCode ($allLines.ToArray()) } catch {}
            }
        }
        catch {
            $msg = $_.Exception.Message
            UI $UiControl {
                try { & $OnDone 1 @("[ERR] $msg") } catch {}
            }
        }
    }

    # ✅ "Run 오버로드 모호" 방지: StartNew([Action]) 사용
    $null = [System.Threading.Tasks.TaskFactory]::new().StartNew($action)
}

# =====================================================
# UI state helpers
# =====================================================
function Lock-UI([string]$mode) {
    # mode: "Analyze" or "Cleanup"
    $btnAnalyze.Enabled = $false
    $btnCleanup.Enabled = $false
    $btnHelp.Enabled = $false
    $chkResetBase.Enabled = $false
    $chkDark.Enabled = $false

    $progress.Style = 'Marquee'
    $progress.MarqueeAnimationSpeed = 30
    $progress.Value = 0

    $script:StartTime = Get-Date
    $script:ElapsedTimer.Start()

    if ($mode -eq "Cleanup") {
        $script:IsCleaning = $true
        $statusLabel.ForeColor = [System.Drawing.Color]::DarkRed
        $statusLabel.Text = "상태: 정리 중..."
    } else {
        $script:IsCleaning = $false
        $statusLabel.ForeColor = [System.Drawing.Color]::Black
        $statusLabel.Text = "상태: 분석 중..."
    }
}

function Unlock-UI {
    $progress.Style = 'Blocks'
    $progress.Value = 100

    $btnAnalyze.Enabled = $true
    $btnHelp.Enabled = $true
    $chkResetBase.Enabled = $true
    $chkDark.Enabled = $true

    # 정리 가능 여부는 마지막 분석 결과 기반으로 갱신
    if (-not $script:IsCleaning -and $script:LastReclaimMB -gt 0) {
        $btnCleanup.Enabled = $true
    } else {
        $btnCleanup.Enabled = $false
    }

    $script:ElapsedTimer.Stop()
    $script:StartTime = $null
    $script:IsBusy = $false
    $script:IsCleaning = $false
}

function Append-Log([string]$text) {
    $logBox.AppendText($text + "`r`n")
    $logBox.SelectionStart = $logBox.TextLength
    $logBox.ScrollToCaret()
}

# =====================================================
# Analyze click (비동기 + 실시간 로그)
# =====================================================
$btnAnalyze.Add_Click({
    if ($script:IsBusy) { return }

    $reclaimLabel.Text = "예상 절감 용량: 계산 중..."
    $script:LastAnalyzeLines = @()
    $script:LastReclaimText  = $null
    $script:LastReclaimMB    = 0.0

    Append-Log "▶ WinSxS 분석 시작..."

    Lock-UI "Analyze"

    Run-DismAsync `
        -Arguments "/Online /Cleanup-Image /AnalyzeComponentStore" `
        -UiControl $form `
        -OnLine {
            param($line)

            # 보기 좋은 로그만 골라서 보여주기(너무 지저분하면 필터링)
            if ($line.Trim().Length -gt 0) {
                Append-Log $line
            }

            # 진행 중 reclaimable 라인을 미리 잡아두기 (한/영/부분)
            if (-not $script:LastReclaimText) {
                if ($line -match 'Reclaimable|정리 가능') {
                    # ":" 뒤 텍스트 추출 시도
                    if ($line -match ':\s*(.+)$') {
                        $script:LastReclaimText = $matches[1].Trim()
                    }
                }
            }
        } `
        -OnDone {
            param($exitCode, $lines)

            $script:LastAnalyzeLines = $lines

            # 파싱(완료 후 정확 파싱)
            $reclaimText = Parse-Reclaimable $lines
            if (-not $reclaimText) { $reclaimText = $script:LastReclaimText }

            if ($reclaimText) {
                $mb = Convert-SizeToMB $reclaimText
                $script:LastReclaimText = $reclaimText
                $script:LastReclaimMB   = $mb

                $reclaimLabel.Text = "예상 절감 용량: $(Format-MB $mb)"

                if ($mb -gt 0) {
                    Append-Log "✔ 분석 완료: 정리 가능 (예상 절감 용량: $(Format-MB $mb))"
                } else {
                    Append-Log "✔ 분석 완료: 정리 가능 항목 없음 (0 MB)"
                }
            } else {
                $reclaimLabel.Text = "예상 절감 용량: (파싱 실패)"
                Append-Log "⚠ 분석은 완료됐지만, 예상 절감 용량 정보를 찾지 못했습니다."
            }

            $statusLabel.ForeColor = [System.Drawing.Color]::DarkGreen
            $statusLabel.Text = "상태: 분석 완료"

            Unlock-UI
        }
})

# =====================================================
# Cleanup click (ResetBase 2단 경고 + 비동기 + 실시간 로그)
# =====================================================
$btnCleanup.Add_Click({
    if ($script:IsBusy) { return }

    # 분석 결과 기반: reclaim <= 0 이면 방지
    if ($script:LastReclaimMB -le 0) {
        [System.Windows.Forms.MessageBox]::Show(
            "현재 정리 가능한 항목이 없어 정리를 진행하지 않습니다.",
            "안내",
            [System.Windows.Forms.MessageBoxButtons]::OK,
            [System.Windows.Forms.MessageBoxIcon]::Information
        ) | Out-Null
        return
    }

    # ResetBase 2단 경고
    if ($chkResetBase.Checked) {
        $r1 = [System.Windows.Forms.MessageBox]::Show(
            "ResetBase는 되돌릴 수 없습니다.`r`n(업데이트 제거가 불가능해질 수 있음)`r`n정말 진행할까요?",
            "ResetBase 경고 (1/2)",
            [System.Windows.Forms.MessageBoxButtons]::YesNo,
            [System.Windows.Forms.MessageBoxIcon]::Warning
        )
        if ($r1 -ne [System.Windows.Forms.DialogResult]::Yes) { return }

        $r2 = [System.Windows.Forms.MessageBox]::Show(
            "마지막 확인입니다.`r`nResetBase 실행 후에는 되돌릴 수 없습니다.`r`n진행할까요?",
            "ResetBase 경고 (2/2)",
            [System.Windows.Forms.MessageBoxButtons]::YesNo,
            [System.Windows.Forms.MessageBoxIcon]::Warning
        )
        if ($r2 -ne [System.Windows.Forms.DialogResult]::Yes) { return }
    }

    Append-Log "▶ WinSxS 정리 시작..."

    Lock-UI "Cleanup"

    $args = "/Online /Cleanup-Image /StartComponentCleanup"
    if ($chkResetBase.Checked) { $args += " /ResetBase" }

    Run-DismAsync `
        -Arguments $args `
        -UiControl $form `
        -OnLine {
            param($line)
            if ($line.Trim().Length -gt 0) {
                Append-Log $line
            }
        } `
        -OnDone {
            param($exitCode, $lines)

            if ($exitCode -eq 0) {
                Append-Log "✔ 정리 완료"
                $statusLabel.ForeColor = [System.Drawing.Color]::DarkGreen
                $statusLabel.Text = "상태: 정리 완료"
            } else {
                Append-Log "⚠ 정리 종료 (ExitCode: $exitCode)"
                $statusLabel.ForeColor = [System.Drawing.Color]::DarkRed
                $statusLabel.Text = "상태: 정리 종료(오류 가능)"
            }

            # 정리 후에는 다시 분석하도록 유도
            Append-Log "ℹ 정리 후 정확한 결과 확인을 위해 [분석]을 다시 실행해보세요."
            $reclaimLabel.Text = "예상 절감 용량: -"

            # 정리 완료 후 버튼은 분석부터 다시 하게
            $script:LastReclaimMB = 0
            Unlock-UI
        }
})

# =====================================================
# 실행
# =====================================================
[void]$form.ShowDialog()
