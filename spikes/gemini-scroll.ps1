# Does ScrollItem.ScrollIntoView on an answer Group make its copy button appear in the UIA tree?
# usage: gemini-scroll.ps1 [-Index 0]   (answer index, 0 = oldest)
param([int]$Index = 0)
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes
$A = [System.Windows.Automation.AutomationElement]
$p = Get-Process Gemini | Where-Object MainWindowHandle -ne 0 | Select-Object -First 1
$root = $A::FromHandle($p.MainWindowHandle)
function Dump($label) {
    "-- $label"
    foreach ($e in $root.FindAll('Descendants', [System.Windows.Automation.Condition]::TrueCondition)) {
        $c = $e.Current
        if ($c.AutomationId -like 'model-response-message-content*' -or $c.Name -in '복사', '원본 크기 이미지 다운로드', '이미지 복사' -or
            (($c.ControlType.ProgrammaticName -eq 'ControlType.Image') -and $c.BoundingRectangle.Width -ge 200)) {
            "{0} | {1} | y={2} h={3} off={4}" -f ($c.ControlType.ProgrammaticName -replace 'ControlType.'), $c.Name, [int]$c.BoundingRectangle.Y, [int]$c.BoundingRectangle.Height, $c.IsOffscreen
        }
    }
}
$answers = @($root.FindAll('Descendants', [System.Windows.Automation.Condition]::TrueCondition) |
    Where-Object { $_.Current.AutomationId -like 'model-response-message-content*' })
Dump 'before'
$target = $answers[$Index]
$target.GetCurrentPattern([System.Windows.Automation.ScrollItemPattern]::Pattern).ScrollIntoView()
Start-Sleep -Milliseconds 800
Dump "after ScrollIntoView answer[$Index]"
