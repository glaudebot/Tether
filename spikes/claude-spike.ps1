param([Parameter(Mandatory)][string]$Text, [string]$TitleContains = "", [int]$WaitSec = 600)
# M0 spike: paste text into Claude desktop input + Enter.
# Guard: never send into a "Claude Code" conversation; if -TitleContains given, the open conversation title must contain it.
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes, System.Windows.Forms
$AE = [System.Windows.Automation.AutomationElement]; $CT = [System.Windows.Automation.ControlType]
$p = Get-Process claude | Where-Object MainWindowHandle -ne 0 | Select-Object -First 1
if (-not $p) { throw "claude 창을 찾을 수 없습니다" }
$win = $AE::FromHandle($p.MainWindowHandle)
function Find-ByName($ct, $name) {
    $c = New-Object System.Windows.Automation.AndCondition(
        (New-Object System.Windows.Automation.PropertyCondition($AE::ControlTypeProperty, $ct)),
        (New-Object System.Windows.Automation.PropertyCondition($AE::NameProperty, $name)))
    $win.FindAll([System.Windows.Automation.TreeScope]::Descendants, $c)
}
function Conversation-Title {
    $docs = $win.FindAll([System.Windows.Automation.TreeScope]::Descendants, (New-Object System.Windows.Automation.PropertyCondition($AE::ControlTypeProperty, $CT::Document)))
    ($docs | ForEach-Object { $_.Current.Name } | Where-Object { $_ }) | Select-Object -First 1
}
$sw = [Diagnostics.Stopwatch]::StartNew()
while ($true) {
    $title = Conversation-Title
    $ok = $title -and ($title -notlike '*- Claude Code') -and (-not $TitleContains -or $title -like "*$TitleContains*")
    if ($ok) {
        $box = $win.FindAll([System.Windows.Automation.TreeScope]::Descendants, (New-Object System.Windows.Automation.PropertyCondition($AE::ControlTypeProperty, $CT::Edit))) | Where-Object { $_.Current.Name -like '*프롬프트*' -and -not $_.Current.IsOffscreen } | Select-Object -First 1
        $val = if ($box) { $box.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).Current.Value } else { $null }
        if ($box -and $val.Trim().Length -eq 0) { break }
        "waiting: input busy or missing (title='$title')"
    } else { "waiting: conversation '$title' not allowed" }
    if ($sw.Elapsed.TotalSeconds -gt $WaitSec) { throw "claude 입력창이 사용 중입니다" }
    Start-Sleep -Seconds 2
}
$bak = Get-Clipboard -Raw
Set-Clipboard -Value $Text
$box.SetFocus(); Start-Sleep -Milliseconds 300
[System.Windows.Forms.SendKeys]::SendWait("^v"); Start-Sleep -Milliseconds 300
[System.Windows.Forms.SendKeys]::SendWait("{ENTER}"); Start-Sleep -Milliseconds 300
if ($bak) { Set-Clipboard -Value $bak }
$after = Get-Clipboard -Raw
"SENT to '$title'; clipboard restored: $($after -eq $bak)"
