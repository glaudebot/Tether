param([string]$Out = "$env:TEMP\gemini-text")
# Spike: read the last Gemini answer two ways — (A) the answer's copy button -> clipboard, (B) UIA Text descendants.
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes, System.Windows.Forms
$AE = [System.Windows.Automation.AutomationElement]
$TS = [System.Windows.Automation.TreeScope]
$utf8 = New-Object System.Text.UTF8Encoding $false
New-Item -ItemType Directory -Force $Out | Out-Null
$p = Get-Process Gemini | Where-Object { $_.MainWindowHandle -ne 0 } | Select-Object -First 1
$win = $AE::FromHandle($p.MainWindowHandle)
$groups = $win.FindAll($TS::Descendants, (New-Object System.Windows.Automation.PropertyCondition($AE::ControlTypeProperty, [System.Windows.Automation.ControlType]::Group)))
$answers = @($groups | Where-Object { $_.Current.AutomationId -like 'model-response-message-content*' })
"answers: $($answers.Count)"
$last = $answers[-1]; $r = $last.Current.BoundingRectangle
"last answer id=$($last.Current.AutomationId) y=$([int]$r.Y) bottom=$([int]$r.Bottom)"

# (B) Text descendants
$texts = $last.FindAll($TS::Descendants, (New-Object System.Windows.Automation.PropertyCondition($AE::ControlTypeProperty, [System.Windows.Automation.ControlType]::Text)))
[IO.File]::WriteAllText("$Out\b-uia.txt", (($texts | ForEach-Object { $_.Current.Name }) -join "`n"), $utf8)

# (A) copy button just below the answer. Pass its name ("복사") as the first argument (script stays ASCII).
$copyName = $args[0]
$buttons = $win.FindAll($TS::Descendants, (New-Object System.Windows.Automation.PropertyCondition($AE::ControlTypeProperty, [System.Windows.Automation.ControlType]::Button)))
$copy = $buttons | Where-Object { $_.Current.Name -eq $copyName -and $_.Current.BoundingRectangle.Y -ge $r.Bottom -and $_.Current.BoundingRectangle.Y -le $r.Bottom + 120 } | Select-Object -First 1
"copy button found: $([bool]$copy)"
if ($copy) {
    [System.Windows.Forms.Clipboard]::Clear()
    $copy.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
    $t = $null; for ($i = 0; $i -lt 20 -and -not $t; $i++) { Start-Sleep -Milliseconds 250; $t = [System.Windows.Forms.Clipboard]::GetText() }
    [IO.File]::WriteAllText("$Out\a-copy.txt", "$t", $utf8)
    "formats: " + ([System.Windows.Forms.Clipboard]::GetDataObject().GetFormats() -join ', ')
}
