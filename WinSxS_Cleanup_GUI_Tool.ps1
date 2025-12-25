Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing

#============================
# App Info 
#============================
$AppName    = "WinSxS Cleanup Tool"
$AppVersion = "v1.0.1"
$Vendor     = "powered by ChatGPT"

# =============================
# 관리자 권한 체크
# =============================
$principal = New-Object Security.Principal.WindowsPrincipal(
    [Security.Principal.WindowsIdentity]::GetCurrent()
)
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    [System.Windows.Forms.MessageBox]::Show(
        "관리자 권한으로 실행해주세요.",
        "권한 오류",
        [System.Windows.Forms.MessageBoxButtons]::OK,
        [System.Windows.Forms.MessageBoxIcon]::Error
    )
    exit
}

# =============================
# OS 체크 (Windows 10 / 11)
# =============================
$os = Get-CimInstance Win32_OperatingSystem
if ([int]$os.BuildNumber -lt 10240) {
    [System.Windows.Forms.MessageBox]::Show(
        "Windows 10 / 11에서만 지원됩니다.",
        "오류",
        [System.Windows.Forms.MessageBoxButtons]::OK,
        [System.Windows.Forms.MessageBoxIcon]::Error
    )
    exit
}

# =============================
# 전역 변수
# =============================
$script:Initialized   = $false
$script:AnalyzeResult = ""
$script:ReclaimValue  = "0 MB"

# =============================
# 분석 결과 파싱
# =============================
function Parse-AnalyzeResult {
    param ([string]$Text)

    $patterns = @(
        "Reclaimable Package Size",
        "정리 가능 패키지 크기",
        "구성 요소 저장소 정리 가능 크기"
    )

    foreach ($p in $patterns) {
        $m = $Text | Select-String $p
        if ($m) {
            $value = ($m.Line -replace '.*:\s*', '').Trim()
            $script:ReclaimValue = $value
            return $value
        }
    }
    return "0 MB"
}

# =============================
# UI 생성
# =============================
$form = New-Object System.Windows.Forms.Form
$form.Text = "WinSxS Cleanup Tool($AppVersion)"
$form.Size = New-Object System.Drawing.Size(820, 560)
$form.StartPosition = "CenterScreen"
$form.MaximizeBox = $false
$form.Icon = [System.Drawing.Icon]::ExtractAssociatedIcon(
    [System.Diagnostics.Process]::GetCurrentProcess().MainModule.FileName
)

$btnAnalyze = New-Object System.Windows.Forms.Button
$btnAnalyze.Text = "Analyze (분석)"
$btnAnalyze.Location = New-Object Drawing.Point(20, 20)
$btnAnalyze.Size = New-Object Drawing.Size(150, 35)

$btnCleanup = New-Object System.Windows.Forms.Button
$btnCleanup.Text = "Start Cleanup (정리)"
$btnCleanup.Location = New-Object Drawing.Point(190, 20)
$btnCleanup.Size = New-Object Drawing.Size(170, 35)
$btnCleanup.Enabled = $false

$chkResetBase = New-Object System.Windows.Forms.CheckBox
$chkResetBase.Text = "ResetBase (되돌릴 수 없음)"
$chkResetBase.Location = New-Object Drawing.Point(380, 26)
$chkResetBase.AutoSize = $true

$logBox = New-Object System.Windows.Forms.TextBox
$logBox.Location = New-Object Drawing.Point(20, 70)
$logBox.Size = New-Object Drawing.Size(760, 360)
$logBox.Multiline = $true
$logBox.ScrollBars = "Vertical"
$logBox.ReadOnly = $true

$progressBar = New-Object System.Windows.Forms.ProgressBar
$progressBar.Location = New-Object Drawing.Point(20, 450)
$progressBar.Size = New-Object Drawing.Size(760, 20)

$statusLabel = New-Object System.Windows.Forms.Label
$statusLabel.Location = New-Object Drawing.Point(20, 480)
$statusLabel.Size = New-Object Drawing.Size(260, 20)
$statusLabel.Text = "상태: 대기 중"

$reclaimLabel = New-Object System.Windows.Forms.Label
$reclaimLabel.Location = New-Object Drawing.Point(300, 480)
$reclaimLabel.Size = New-Object Drawing.Size(260, 20)
$reclaimLabel.ForeColor = [System.Drawing.Color]::DarkGreen
$reclaimLabel.Text = "예상 절감 용량: -"

$remainLabel = New-Object System.Windows.Forms.Label
$remainLabel.Location = New-Object Drawing.Point(580, 480)
$remainLabel.Size = New-Object Drawing.Size(200, 20)

$form.Controls.AddRange(@(
    $btnAnalyze, $btnCleanup, $chkResetBase,
    $logBox, $progressBar,
    $statusLabel, $reclaimLabel, $remainLabel
))

# =============================
# 최초 안내
# =============================
$form.Add_Shown({
    if (-not $script:Initialized) {
        $logBox.Clear()
        $logBox.AppendText("▶ $AppName $AppVersion`r`n")
        $logBox.AppendText("   $Vendor`r`n`r`n")
        $logBox.AppendText("▶ '분석(Analyze)' 버튼을 눌러 WinSxS 정리를 시작하세요.`r`n")
        $logBox.AppendText("   분석에는 약 1~5분 정도 소요될 수 있습니다.`r`n")
        $logBox.AppendText("   분석 중에는 창이 잠시 멈춘 것처럼 보일 수 있으나 정상 동작입니다.`r`n")
        $logBox.AppendText("   ResetBase 옵션은 되돌릴 수 없습니다.`r`n`r`n")
        $script:Initialized = $true
    }
})
# =============================
# Analyze
# =============================
$btnAnalyze.Add_Click({

    $btnAnalyze.Enabled = $false
    $btnCleanup.Enabled = $false
    $progressBar.Value = 0
    $logBox.AppendText("▶ WinSxS 분석 시작...`r`n")

    $worker = New-Object System.ComponentModel.BackgroundWorker
    $worker.WorkerReportsProgress = $true

    $worker.Add_DoWork({
        param($sender, $e)

        for ($i=1; $i -le 90; $i+=5) {
            Start-Sleep -Milliseconds 400
            $sender.ReportProgress($i)
        }

        $psi = New-Object System.Diagnostics.ProcessStartInfo
        $psi.FileName = "cmd.exe"
        $psi.Arguments = "/c dism /Online /Cleanup-Image /AnalyzeComponentStore"
        $psi.RedirectStandardOutput = $true
        $psi.UseShellExecute = $false
        $psi.CreateNoWindow = $true

        $proc = [System.Diagnostics.Process]::Start($psi)
        $out = $proc.StandardOutput.ReadToEnd()
        $proc.WaitForExit()

        $e.Result = $out
    })

    $worker.Add_ProgressChanged({
        $progressBar.Value = $_.ProgressPercentage
        $statusLabel.Text = "상태: 분석 중..."
    })

    $worker.Add_RunWorkerCompleted({
        $progressBar.Value = 100
        $statusLabel.Text = "상태: 분석 완료"

        $value = Parse-AnalyzeResult $_.Result
        $reclaimLabel.Text = "예상 절감 용량: $value"

        if ($value -ne "0 MB") {
            $btnCleanup.Enabled = $true
        }

        $logBox.AppendText("▶ 분석 완료`r`n")
        $btnAnalyze.Enabled = $true
    })

    $worker.RunWorkerAsync()
})

# =============================
# Cleanup
# =============================
$btnCleanup.Add_Click({

    $btnCleanup.Enabled = $false
    $statusLabel.Text = "상태: 정리 중..."
    $progressBar.Value = 0

    $args = "/Online /Cleanup-Image /StartComponentCleanup"
    if ($chkResetBase.Checked) { $args += " /ResetBase" }

    $worker = New-Object System.ComponentModel.BackgroundWorker
    $worker.Add_DoWork({
        cmd /c "dism $args" | Out-Null
    })

    $worker.Add_RunWorkerCompleted({
        $progressBar.Value = 100
        $statusLabel.Text = "상태: 정리 완료"
        $logBox.AppendText("▶ 정리 완료`r`n")
    })

    $worker.RunWorkerAsync()
})

# =============================
# 실행
# =============================
[void]$form.ShowDialog()
