Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing

# =====================================================
# 메타 정보 / 전역 변수
# =====================================================
$AppName    = "WinSxS Cleanup Tool"
$AppVersion = "v1.0.3"
$Vendor     = "Powered by ChatGPT"

$script:IsCleaning = $false
$script:CleanupStartTime = $null

# ⏱ 경과 시간 타이머
$script:ElapsedTimer = New-Object System.Windows.Forms.Timer
$script:ElapsedTimer.Interval = 1000

# =====================================================
# 관리자 권한 체크
# =====================================================
$principal = New-Object Security.Principal.WindowsPrincipal(
    [Security.Principal.WindowsIdentity]::GetCurrent()
)
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    [System.Windows.Forms.MessageBox]::Show(
        "관리자 권한으로 실행해주세요.",
        "권한 오류",
        "OK",
        "Error"
    )
    exit
}

# =====================================================
# OS 체크 (Windows 10 / 11)
# =====================================================
$build = [int](Get-CimInstance Win32_OperatingSystem).BuildNumber
if ($build -lt 10240) {
    [System.Windows.Forms.MessageBox]::Show(
        "Windows 10 / 11에서만 지원됩니다.",
        "오류",
        "OK",
        "Error"
    )
    exit
}

# =====================================================
# 설정 파일 (다크 모드 저장)
# =====================================================
$ConfigPath = Join-Path $env:APPDATA "WinSxS_Cleanup_Tool.cfg"

# =====================================================
# 테마 관련 함수
# =====================================================
function Apply-Theme($mode) {
    if ($mode -eq "Dark") {
        $bg  = [System.Drawing.Color]::FromArgb(32,32,32)
        $fg  = [System.Drawing.Color]::White
        $btn = [System.Drawing.Color]::FromArgb(64,64,64)
    } else {
        $bg  = [System.Drawing.Color]::White
        $fg  = [System.Drawing.Color]::Black
        $btn = [System.Drawing.Color]::Gainsboro
    }

    $form.BackColor = $bg
    foreach ($c in $form.Controls) {
        if ($c -is [System.Windows.Forms.Button] -or
            $c -is [System.Windows.Forms.CheckBox] -or
            $c -is [System.Windows.Forms.Label]) {

            $c.ForeColor = $fg
            if ($c -is [System.Windows.Forms.Button]) {
                $c.BackColor = $btn
            }
        }

        if ($c -is [System.Windows.Forms.TextBox]) {
            $c.BackColor = $bg
            $c.ForeColor = $fg
        }
    }
}

function Save-Theme {
    if ($chkDarkMode.Checked) {
        Set-Content -Path $ConfigPath -Value "Dark" -Encoding UTF8
    } else {
        Set-Content -Path $ConfigPath -Value "Light" -Encoding UTF8
    }
}

# =====================================================
# UI 생성
# =====================================================
$form = New-Object System.Windows.Forms.Form
$form.Text = "$AppName $AppVersion"
$form.Size = New-Object Drawing.Size(840, 560)
$form.StartPosition = "CenterScreen"
$form.MaximizeBox = $false

# EXE 아이콘 연동
$form.Icon = [System.Drawing.Icon]::ExtractAssociatedIcon(
    [System.Diagnostics.Process]::GetCurrentProcess().MainModule.FileName
)

# 버튼
$btnAnalyze = New-Object System.Windows.Forms.Button
$btnAnalyze.Text = "Analyze (분석)"
$btnAnalyze.Location = New-Object Drawing.Point(20,20)
$btnAnalyze.Size = New-Object Drawing.Size(150,35)

$btnCleanup = New-Object System.Windows.Forms.Button
$btnCleanup.Text = "Start Cleanup (정리)"
$btnCleanup.Location = New-Object Drawing.Point(190,20)
$btnCleanup.Size = New-Object Drawing.Size(170,35)
$btnCleanup.Enabled = $false

$btnHelp = New-Object System.Windows.Forms.Button
$btnHelp.Text = "도움말"
$btnHelp.Location = New-Object Drawing.Point(380,20)
$btnHelp.Size = New-Object Drawing.Size(90,35)

$chkDarkMode = New-Object System.Windows.Forms.CheckBox
$chkDarkMode.Text = "다크 모드"
$chkDarkMode.Location = New-Object Drawing.Point(490,26)
$chkDarkMode.AutoSize = $true

$chkResetBase = New-Object System.Windows.Forms.CheckBox
$chkResetBase.Text = "ResetBase (되돌릴 수 없음)"
$chkResetBase.Location = New-Object Drawing.Point(600,26)
$chkResetBase.AutoSize = $true

# 로그 박스
$logBox = New-Object System.Windows.Forms.TextBox
$logBox.Location = New-Object Drawing.Point(20,70)
$logBox.Size = New-Object Drawing.Size(780,360)
$logBox.Multiline = $true
$logBox.ReadOnly = $true
$logBox.ScrollBars = "Vertical"

# 진행 표시
$progressBar = New-Object System.Windows.Forms.ProgressBar
$progressBar.Location = New-Object Drawing.Point(20,450)
$progressBar.Size = New-Object Drawing.Size(780,20)

$statusLabel = New-Object System.Windows.Forms.Label
$statusLabel.Location = New-Object Drawing.Point(20,480)
$statusLabel.Size = New-Object Drawing.Size(260,20)
$statusLabel.Text = "상태: 대기 중"

$reclaimLabel = New-Object System.Windows.Forms.Label
$reclaimLabel.Location = New-Object Drawing.Point(300,480)
$reclaimLabel.Size = New-Object Drawing.Size(260,20)
$reclaimLabel.Text = "예상 절감 용량: -"
$reclaimLabel.ForeColor = [System.Drawing.Color]::DarkGreen

$remainLabel = New-Object System.Windows.Forms.Label
$remainLabel.Location = New-Object Drawing.Point(580,480)
$remainLabel.Size = New-Object Drawing.Size(220,20)

$form.Controls.AddRange(@(
    $btnAnalyze,$btnCleanup,$btnHelp,
    $chkDarkMode,$chkResetBase,
    $logBox,$progressBar,
    $statusLabel,$reclaimLabel,$remainLabel
))

# =====================================================
# 타이머 이벤트
# =====================================================
$script:ElapsedTimer.Add_Tick({
    if (-not $script:IsCleaning) { return }

    $elapsed = (Get-Date) - $script:CleanupStartTime
    $m = [int]$elapsed.TotalMinutes
    $s = $elapsed.Seconds

    $statusLabel.Text = "상태: 정리 중... (${m}분 ${s}초 경과)"
})

# =====================================================
# 초기 메시지
# =====================================================
$form.Add_Shown({
    $logBox.Clear()
    $logBox.AppendText("$AppName $AppVersion`r`n")
    $logBox.AppendText("$Vendor`r`n`r`n")
    $logBox.AppendText("▶ '분석(Analyze)' 버튼을 눌러 WinSxS 상태를 확인하세요.`r`n")
    $logBox.AppendText("▶ 정리 중에는 멈춘 것처럼 보일 수 있으나 정상 동작입니다.`r`n`r`n")

    if (Test-Path $ConfigPath) {
        if ((Get-Content $ConfigPath) -eq "Dark") {
            $chkDarkMode.Checked = $true
            Apply-Theme "Dark"
        }
    }
})

# =====================================================
# 다크 모드
# =====================================================
$chkDarkMode.Add_CheckedChanged({
    Apply-Theme ($(if ($chkDarkMode.Checked) {"Dark"} else {"Light"}))
    Save-Theme
})

# =====================================================
# 도움말
# =====================================================
$btnHelp.Add_Click({
    [System.Windows.Forms.MessageBox]::Show(
@"
• Analyze: WinSxS 분석
• 예상 절감 용량 표시
• Start Cleanup: 실제 정리 수행
• ResetBase: 되돌릴 수 없음

Windows 10 / 11 전용
관리자 권한 필수
"@,
    "도움말",
    "OK",
    "Information"
    )
})

# =====================================================
# Analyze
# =====================================================
$btnAnalyze.Add_Click({
    $btnAnalyze.Enabled = $false
    $btnCleanup.Enabled = $false
    $progressBar.Value = 0

    $logBox.AppendText("▶ WinSxS 분석 시작...`r`n")

    $psi = New-Object Diagnostics.ProcessStartInfo
    $psi.FileName = "cmd.exe"
    $psi.Arguments = "/c dism /Online /Cleanup-Image /AnalyzeComponentStore"
    $psi.RedirectStandardOutput = $true
    $psi.UseShellExecute = $false
    $psi.CreateNoWindow = $true

    $p = [Diagnostics.Process]::Start($psi)
    $out = $p.StandardOutput.ReadToEnd()
    $p.WaitForExit()

    $progressBar.Value = 100
    $statusLabel.Text = "상태: 분석 완료"

    $line = ($out | Select-String "Reclaimable").Line
    if ($line) {
        $size = ($line -replace '.*:\s*','').Trim()
        $reclaimLabel.Text = "예상 절감 용량: $size"
        if ($size -notmatch '^0') {
            $btnCleanup.Enabled = $true
        }
    }

    $logBox.AppendText("▶ 분석 완료`r`n")
    $btnAnalyze.Enabled = $true
})

# =====================================================
# Cleanup
# =====================================================
$btnCleanup.Add_Click({

    if ($script:IsCleaning) { return }
    $script:IsCleaning = $true

    $btnAnalyze.Enabled = $false
    $btnCleanup.Enabled = $false
    $chkResetBase.Enabled = $false

    $progressBar.Style = 'Marquee'
    $progressBar.MarqueeAnimationSpeed = 30

    $statusLabel.ForeColor = [System.Drawing.Color]::DarkRed
    $statusLabel.Text = "상태: 정리 중 (강제 종료하지 마세요)"

    $script:CleanupStartTime = Get-Date
    $script:ElapsedTimer.Start()

    $logBox.AppendText("▶ WinSxS 정리 시작...`r`n")

    $worker = New-Object System.ComponentModel.BackgroundWorker

    $worker.DoWork += {
        $args = "/Online /Cleanup-Image /StartComponentCleanup"
        if ($chkResetBase.Checked) { $args += " /ResetBase" }
        cmd /c "dism $args" | Out-Null
    }

    $worker.RunWorkerCompleted += {

        $script:ElapsedTimer.Stop()
        $script:IsCleaning = $false

        $progressBar.Style = 'Blocks'
        $progressBar.Value = 100

        $elapsed = (Get-Date) - $script:CleanupStartTime
        $m = [int]$elapsed.TotalMinutes
        $s = $elapsed.Seconds

        $statusLabel.ForeColor = [System.Drawing.Color]::DarkGreen
        $statusLabel.Text = "상태: 정리 완료"

        $logBox.AppendText("✔ 정리 완료 (소요 시간: ${m}분 ${s}초)`r`n")

        $btnAnalyze.Enabled = $true
        $chkResetBase.Enabled = $true
    }

    $worker.RunWorkerAsync()
})

# =====================================================
# 실행
# =====================================================
[void]$form.ShowDialog()
