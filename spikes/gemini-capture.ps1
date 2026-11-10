param([string]$OutDir = "C:\Temp\spike")
# M0 spike: retrieve latest Gemini image via (A) "이미지 복사" -> clipboard, (B) "원본 크기 이미지 다운로드" -> Downloads
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes, System.Windows.Forms, System.Drawing
$AE = [System.Windows.Automation.AutomationElement]
$CT = [System.Windows.Automation.ControlType]
New-Item -ItemType Directory -Force $OutDir | Out-Null
$p = Get-Process Gemini | Where-Object { $_.MainWindowHandle -ne 0 } | Select-Object -First 1
$win = $AE::FromHandle($p.MainWindowHandle)
function Last-Button($name) {
    $cond = New-Object System.Windows.Automation.AndCondition(
        (New-Object System.Windows.Automation.PropertyCondition($AE::ControlTypeProperty, $CT::Button)),
        (New-Object System.Windows.Automation.PropertyCondition($AE::NameProperty, $name)))
    $all = $win.FindAll([System.Windows.Automation.TreeScope]::Descendants, $cond)
    if ($all.Count -eq 0) { return $null }; $all[$all.Count - 1]
}
"stop button present: " + [bool](Last-Button '대답 생성 중지')
"send button present: " + [bool](Last-Button '메시지 보내기')

# (A) clipboard
[System.Windows.Forms.Clipboard]::Clear()
$copy = Last-Button '이미지 복사'
if ($copy) {
    $copy.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
    $img = $null
    for ($i = 0; $i -lt 20 -and -not $img; $i++) { Start-Sleep -Milliseconds 500; $img = [System.Windows.Forms.Clipboard]::GetImage() }
    if ($img) { $f = Join-Path $OutDir "copy.png"; $img.Save($f, [System.Drawing.Imaging.ImageFormat]::Png); "A OK: $f $($img.Width)x$($img.Height)" } else { "A FAIL: no image on clipboard" }
} else { "A FAIL: no copy button" }

# (B) download
$dl = Join-Path $env:USERPROFILE "Downloads"
$t0 = Get-Date
$btn = Last-Button '원본 크기 이미지 다운로드'
if ($btn) {
    $btn.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
    $file = $null
    for ($i = 0; $i -lt 60 -and -not $file; $i++) {
        Start-Sleep -Milliseconds 500
        $file = Get-ChildItem $dl -File | Where-Object { $_.LastWriteTime -gt $t0 -and $_.Extension -notin '.crdownload','.tmp' } | Sort-Object LastWriteTime -Descending | Select-Object -First 1
    }
    if ($file) { "B OK: $($file.FullName) $($file.Length) bytes" } else { "B FAIL: no new file in Downloads (dialog?)" }
} else { "B FAIL: no download button" }
