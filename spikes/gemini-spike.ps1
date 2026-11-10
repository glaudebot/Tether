param([Parameter(Mandatory)][string]$Prompt, [string]$OutDir = "C:\Temp\spike", [int]$TimeoutSec = 180)
# M0 spike: send prompt to Gemini desktop, watch UI changes until done, log what we see.
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes, System.Windows.Forms
$AE = [System.Windows.Automation.AutomationElement]
$CT = [System.Windows.Automation.ControlType]
New-Item -ItemType Directory -Force $OutDir | Out-Null
$log = Join-Path $OutDir "gemini-spike.log"
function Log($m) { $line = "{0:HH:mm:ss} {1}" -f (Get-Date), $m; $line; Add-Content $log $line -Encoding UTF8 }

$p = Get-Process Gemini | Where-Object { $_.MainWindowHandle -ne 0 } | Select-Object -First 1
if (-not $p) { throw "gemini 창을 찾을 수 없습니다" }
$win = $AE::FromHandle($p.MainWindowHandle)

function Find-All($ctrlType) {
    $cond = New-Object System.Windows.Automation.PropertyCondition($AE::ControlTypeProperty, $ctrlType)
    $win.FindAll([System.Windows.Automation.TreeScope]::Descendants, $cond)
}
function Button-Names { (Find-All $CT::Button | ForEach-Object { $_.Current.Name }) }

$box = Find-All $CT::Edit | Where-Object { $_.Current.Name -eq 'Gemini 프롬프트 입력' } | Select-Object -First 1
if (-not $box) { throw "입력창을 찾을 수 없습니다" }

$before = @(Button-Names)
$imgBefore = (Find-All $CT::Image).Count

# backup clipboard, paste, enter, restore
$bak = Get-Clipboard -Raw
Set-Clipboard -Value $Prompt
$box.SetFocus()
Start-Sleep -Milliseconds 300
[System.Windows.Forms.SendKeys]::SendWait("^v")
Start-Sleep -Milliseconds 300
[System.Windows.Forms.SendKeys]::SendWait("{ENTER}")
Start-Sleep -Milliseconds 300
if ($bak) { Set-Clipboard -Value $bak }
Log "SENT: $Prompt"

$seen = @{}; $before | ForEach-Object { $seen[$_] = $true }
$sw = [Diagnostics.Stopwatch]::StartNew()
while ($sw.Elapsed.TotalSeconds -lt $TimeoutSec) {
    Start-Sleep -Seconds 2
    $names = @(Button-Names)
    $new = $names | Where-Object { -not $seen.ContainsKey($_) } | Sort-Object -Unique
    foreach ($n in $new) { Log "NEW BUTTON: $n"; $seen[$n] = $true }
    $gone = $before | Where-Object { $names -notcontains $_ }
    $imgs = Find-All $CT::Image
    Log ("t={0:N0}s buttons={1} images={2}" -f $sw.Elapsed.TotalSeconds, $names.Count, $imgs.Count)
    $before = $names
}
# final image list
foreach ($i in (Find-All $CT::Image)) { $c = $i.Current; Log "IMAGE: name='$($c.Name)' rect=$($c.BoundingRectangle)" }
Log "END"
