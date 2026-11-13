param([Parameter(Mandatory)][string]$Prompt)
# Spike: paste a prompt into Gemini's input and press Enter (same as GeminiResponder.DeliverAsync).
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes, System.Windows.Forms
$AE = [System.Windows.Automation.AutomationElement]
$p = Get-Process Gemini | Where-Object { $_.MainWindowHandle -ne 0 } | Select-Object -First 1
$win = $AE::FromHandle($p.MainWindowHandle)
$cond = New-Object System.Windows.Automation.PropertyCondition($AE::ControlTypeProperty, [System.Windows.Automation.ControlType]::Edit)
$input = $win.FindAll([System.Windows.Automation.TreeScope]::Descendants, $cond) | Select-Object -First 1
$input.SetFocus()
[System.Windows.Forms.Clipboard]::SetText($Prompt)
[System.Windows.Forms.SendKeys]::SendWait("^v")
Start-Sleep -Milliseconds 300
[System.Windows.Forms.SendKeys]::SendWait("{ENTER}")
"sent"
