# This script is provided for transparency and educational purposes.
# The EXE file is generated from this script using ps2exe.

# ==================================================
# WinSxS Cleanup Tool - FINAL FINAL
# ==================================================

Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing

# ---- Admin Check ----
$identity  = [Security.Principal.WindowsIdentity]::GetCurrent()
$principal = New-Object Security.Principal.WindowsPrincipal($identity)

if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    [System.Windows.Forms.MessageBox]::Show(
        "관리자 권한으로 실행해야 합니다.",
        "Permission Required",
        "OK",
        "Error"
    ) | Out-Null
    exit
}

# ==================================================
# Helper : Get Reclaimable Size from DISM
# ==================================================
function Get-ReclaimableSizeMB {
    $output = dism.exe /Online /Cleanup-Image /AnalyzeComponentStore
    foreach ($line in $output) {
        if ($line -match "Backups and Disabled Features\s*:\s*([\d\.]+)\s*GB") {
            return [math]::Round(([double]$matches[1] * 1024), 0)
        }
        if ($line -match "Backups and Disabled Features\s*:\s*([\d\.]+)\s*MB") {
            return [math]::Round([double]$matches[1], 0)
        }
    }
    return 0
}

# ==================================================
# Form / Theme
# ==================================================
$form = New-Object System.Windows.Forms.Form
$form.Text = "WinSxS Cleanup Tool"
$form.Size = New-Object System.Drawing.Size(780,520)
$form.StartPosition = "CenterScreen"
$form.FormBorderStyle = "FixedDialog"
$form.MaximizeBox = $false

$bgDark    = [System.Drawing.Color]::FromArgb(30,30,30)
$panelDark = [System.Drawing.Color]::FromArgb(45,45,45)
$fgLight   = [System.Drawing.Color]::White
$btnDark   = [System.Drawing.Color]::FromArgb(60,60,60)

$form.BackColor = $bgDark
$form.ForeColor = $fgLight

# ==================================================
# Header
# ==================================================
$header = New-Object System.Windows.Forms.Panel
$header.Dock = "Top"
$header.Height = 80
$header.BackColor = $panelDark
$form.Controls.Add($header)

$lblTitle = New-Object System.Windows.Forms.Label
$lblTitle.Text = "WinSxS Cleanup Tool"
$lblTitle.Font = New-Object System.Drawing.Font("Segoe UI",16,[System.Drawing.FontStyle]::Bold)
$lblTitle.ForeColor = $fgLight
$lblTitle.Location = New-Object System.Drawing.Point(20,15)
$lblTitle.AutoSize = $true
$header.Controls.Add($lblTitle)

$lblSub = New-Object System.Windows.Forms.Label
$lblSub.Text = "Windows Component Store Maintenance Utility"
$lblSub.Font = New-Object System.Drawing.Font("Segoe UI",9)
$lblSub.ForeColor = [System.Drawing.Color]::Gainsboro
$lblSub.Location = New-Object System.Drawing.Point(22,45)
$lblSub.AutoSize = $true
$header.Controls.Add($lblSub)

# ==================================================
# Controls
# ==================================================
$btnAnalyze = New-Object System.Windows.Forms.Button
$btnAnalyze.Text = "Analyze"
$btnAnalyze.Size = New-Object System.Drawing.Size(130,36)
$btnAnalyze.Location = New-Object System.Drawing.Point(20,100)
$btnAnalyze.BackColor = $btnDark
$btnAnalyze.ForeColor = $fgLight
$form.Controls.Add($btnAnalyze)

$btnCleanup = New-Object System.Windows.Forms.Button
$btnCleanup.Text = "Start Cleanup"
$btnCleanup.Size = New-Object System.Drawing.Size(160,36)
$btnCleanup.Location = New-Object System.Drawing.Point(165,100)
$btnCleanup.BackColor = $btnDark
$btnCleanup.ForeColor = $fgLight
$form.Controls.Add($btnCleanup)

$normalBtnColor = $btnCleanup.BackColor

$chkReset = New-Object System.Windows.Forms.CheckBox
$chkReset.Text = "ResetBase (irreversible)"
$chkReset.Location = New-Object System.Drawing.Point(340,108)
$chkReset.ForeColor = [System.Drawing.Color]::Orange
$chkReset.AutoSize = $true
$form.Controls.Add($chkReset)

# ==================================================
# Log Box
# ==================================================
$txtLog = New-Object System.Windows.Forms.TextBox
$txtLog.Location = New-Object System.Drawing.Point(20,150)
$txtLog.Size = New-Object System.Drawing.Size(720,285)
$txtLog.Multiline = $true
$txtLog.ScrollBars = "Vertical"
$txtLog.ReadOnly = $true
$txtLog.BackColor = [System.Drawing.Color]::FromArgb(40,40,40)
$txtLog.ForeColor = $fgLight
$form.Controls.Add($txtLog)

function Write-Log {
    param($msg)
    $txtLog.AppendText("[$(Get-Date -Format HH:mm:ss)] $msg`r`n")
}

# ==================================================
# Status Bar
# ==================================================
$statusBar = New-Object System.Windows.Forms.Panel
$statusBar.Dock = "Bottom"
$statusBar.Height = 32
$statusBar.BackColor = $panelDark
$form.Controls.Add($statusBar)

$statusIcon = New-Object System.Windows.Forms.PictureBox
$statusIcon.Size = New-Object System.Drawing.Size(12,12)
$statusIcon.Location = New-Object System.Drawing.Point(12,10)
$statusIcon.SizeMode = "StretchImage"
$statusIcon.Image = [System.Drawing.SystemIcons]::Information.ToBitmap()
$statusBar.Controls.Add($statusIcon)

$lblStatus = New-Object System.Windows.Forms.Label
$lblStatus.Text = "Status: Idle"
$lblStatus.Location = New-Object System.Drawing.Point(30,7)
$lblStatus.ForeColor = [System.Drawing.Color]::Gainsboro
$lblStatus.AutoSize = $true
$statusBar.Controls.Add($lblStatus)

$lblSaved = New-Object System.Windows.Forms.Label
$lblSaved.Text = "Saved: 0 MB"
$lblSaved.Location = New-Object System.Drawing.Point(260,7)
$lblSaved.ForeColor = [System.Drawing.Color]::LightGreen
$lblSaved.AutoSize = $true
$statusBar.Controls.Add($lblSaved)

$progress = New-Object System.Windows.Forms.ProgressBar
$progress.Size = New-Object System.Drawing.Size(220,16)
$progress.Location = New-Object System.Drawing.Point(520,8)
$progress.Anchor = "Right,Top"
$statusBar.Controls.Add($progress)

function Set-Status {
    param($text,$icon,$color)
    $lblStatus.Text = "Status: $text"
    $lblStatus.ForeColor = $color
    $statusIcon.Image = $icon.ToBitmap()
}

# ==================================================
# Logic
# ==================================================
$beforeSize = 0

$btnAnalyze.Add_Click({
    Set-Status "Analyzing" ([System.Drawing.SystemIcons]::Shield) ([System.Drawing.Color]::LightSkyBlue)
    $progress.Value = 10
    Write-Log "Analyzing component store..."

    $beforeSize = Get-ReclaimableSizeMB
    Write-Log "Reclaimable size: $beforeSize MB"

    $lblSaved.Text = "Reclaimable: $beforeSize MB"
    $progress.Value = 100
    Set-Status "Idle" ([System.Drawing.SystemIcons]::Information) ([System.Drawing.Color]::Gainsboro)
})

$chkReset.Add_CheckedChanged({
    if ($chkReset.Checked) {
        $btnCleanup.BackColor = [System.Drawing.Color]::Firebrick
        Set-Status "ResetBase Enabled" ([System.Drawing.SystemIcons]::Warning) ([System.Drawing.Color]::Orange)
    } else {
        $btnCleanup.BackColor = $normalBtnColor
        Set-Status "Idle" ([System.Drawing.SystemIcons]::Information) ([System.Drawing.Color]::Gainsboro)
    }
})

$btnCleanup.Add_Click({
    if ($chkReset.Checked) {
        $confirm = [System.Windows.Forms.MessageBox]::Show(
            "ResetBase는 되돌릴 수 없습니다.`n계속하시겠습니까?",
            "Warning",
            "YesNo",
            "Warning"
        )
        if ($confirm -ne "Yes") { return }
    }

    Set-Status "Cleaning" ([System.Drawing.SystemIcons]::Shield) ([System.Drawing.Color]::LightSkyBlue)
    $progress.Value = 10
    Write-Log "Starting cleanup..."

    $args = "/Online /Cleanup-Image /StartComponentCleanup"
    if ($chkReset.Checked) { $args += " /ResetBase" }

    Start-Process dism.exe -ArgumentList $args -Wait -NoNewWindow

    Write-Log "Re-analyzing after cleanup..."
    $afterSize = Get-ReclaimableSizeMB
    $saved = [math]::Max(($beforeSize - $afterSize),0)

    Write-Log "Saved space: $saved MB"
    $lblSaved.Text = "Saved: $saved MB"

    $progress.Value = 100
    Set-Status "Completed" ([System.Drawing.SystemIcons]::Application) ([System.Drawing.Color]::LightGreen)
})

# ==================================================
# Show
# ==================================================
[void]$form.ShowDialog()
