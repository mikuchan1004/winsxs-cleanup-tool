Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing

# =====================================================
# 앱 정보
# =====================================================
$AppName    = "WinSxS Cleanup Tool"
$AppVersion = "v1.0.2"
$Vendor     = "powered by ChatGPT"

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
# 설정 파일 (다크모드 저장)
# =====================================================
$ConfigPath = Join-Path $env:APPDATA "WinSxS_Cleanup_Tool.cfg"

# =====================================================
# 테마 함수
# =====================================================
function Apply-Theme($mode) {
    if ($mode -eq "Dark") {
        $bg = [System.Drawing.Color]::FromArgb(32,32,32)
        $fg = [System.Drawing.Color]::White
        $btn = [System.Drawing.Color]::FromArgb(64,64,64)
    } else {
        $bg = [System.Drawing.Color]::White
        $fg = [System.Drawing.Color]::Black
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
        $mode = "Dark"
    } else {
        $mode = "Light"
    }
    Set-Content -Path $ConfigPath -Value $mode -Encoding UTF8
}

# =====================================================
# UI 생성
# =====================================================
$form = New-Object System.Windows.Forms.Form
$form.Text = "$AppName $AppVersion"
$form.Size = New-Object Drawing.Size(840, 560)
$form.StartPosition = "CenterScreen"
$form.MaximizeBox = $false

# 실행 중인 EXE의 아이콘을 Form에 적용
$form.Icon = [System.Drawing.Icon]::ExtractAssociatedIcon(
    [System.Diagnostics.Process]::GetCurrentProcess().MainModule.FileName
)

# 버튼들
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
$chkDarkMode.Location = New-Object Drawing.Point(500,26)
$chkDarkMode.AutoSize = $true

# 로그 박스
$logBox = New-Object System.Windows.Forms.TextBox
$logBox.Location = New-Object Drawing.Point(20,70)
$logBox.Size = New-Object Drawing.Size(780,360)
$logBox.Multiline = $true
$logBox.ReadOnly = $true
$logBox.ScrollBars = "Vertical"

# 프로그레스
$progressBar = New-Object System.Windows.Forms.ProgressBar
$progressBar.Location = New-Object Drawing.Point(20,450)
$progressBar.Size = New-Object Drawing.Size(780,20)

$statusLabel = New-Object System.Windows.Forms.Label
$statusLabel.Location = New-Object Drawing.Point(20,480)
$statusLabel.Size = New-Object Drawing.Size(250,20)
$statusLabel.Text = "상태: 대기 중"

$reclaimLabel = New-Object System.Windows.Forms.Label
$reclaimLabel.Location = New-Object Drawing.Point(290,480)
$reclaimLabel.Size = New-Object Drawing.Size(260,20)
$reclaimLabel.Text = "예상 절감 용량: -"
$reclaimLabel.ForeColor = [System.Drawing.Color]::DarkGreen

$remainLabel = New-Object System.Windows.Forms.Label
$remainLabel.Location = New-Object Drawing.Point(580,480)
$remainLabel.Size = New-Object Drawing.Size(220,20)
$remainLabel.Text = ""

$form.Controls.AddRange(@(
    $btnAnalyze,$btnCleanup,$btnHelp,$chkDarkMode,
    $logBox,$progressBar,
    $statusLabel,$reclaimLabel,$remainLabel
))

# =====================================================
# 초기 메시지
# =====================================================
$form.Add_Shown({
    $logBox.Clear()
    $logBox.AppendText("$AppName $AppVersion`r`n")
    $logBox.AppendText("$Vendor`r`n")
    $logBox.AppendText("Ready.`r`n`r`n")

    if (Test-Path $ConfigPath) {
        $mode = Get-Content $ConfigPath
        if ($mode -eq "Dark") {
            $chkDarkMode.Checked = $true
            Apply-Theme "Dark"
        }
    }
})

# =====================================================
# 다크 모드 토글
# =====================================================
$chkDarkMode.Add_CheckedChanged({
    if ($chkDarkMode.Checked) {
        Apply-Theme "Dark"
    } else {
        Apply-Theme "Light"
    }
    Save-Theme
})

# =====================================================
# 도움말 창
# =====================================================
$btnHelp.Add_Click({
    [System.Windows.Forms.MessageBox]::Show(
@"
• Analyze: WinSxS 분석 수행
• 예상 절감 용량 표시
• Start Cleanup: 실제 정리 실행
• ResetBase 사용 시 되돌릴 수 없음

Windows 10 / 11 전용
관리자 권한 필수
"@,
    "도움말",
    "OK",
    "Information"
    )
})

# =====================================================
# 분석 버튼
# =====================================================
$btnAnalyze.Add_Click({
    $btnAnalyze.Enabled = $false
    $btnCleanup.Enabled = $false
    $progressBar.Value = 0
    $logBox.AppendText("▶ WinSxS 분석 시작...`r`n")

    $start = Get-Date
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
    $remainLabel.Text = "예상 남은 시간: -"

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
# 정리 버튼
# =====================================================
$btnCleanup.Add_Click({
    $btnCleanup.Enabled = $false
    $statusLabel.Text = "상태: 정리 중..."
    $progressBar.Value = 0

    cmd /c "dism /Online /Cleanup-Image /StartComponentCleanup" | Out-Null

    $progressBar.Value = 100
    $statusLabel.Text = "상태: 정리 완료"
})

# =====================================================
# 실행
# =====================================================
[void]$form.ShowDialog()
