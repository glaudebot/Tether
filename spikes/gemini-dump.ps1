param([string]$Out = "$env:TEMP\gemini-tree.txt", [int]$MaxText = 80)
# Spike: dump the Gemini window's UIA control tree (type, name, class, automation id, rect) to a UTF-8 file.
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes
$AE = [System.Windows.Automation.AutomationElement]
$walker = [System.Windows.Automation.TreeWalker]::ControlViewWalker
$p = Get-Process Gemini | Where-Object { $_.MainWindowHandle -ne 0 } | Select-Object -First 1
$win = $AE::FromHandle($p.MainWindowHandle)
$sb = New-Object System.Text.StringBuilder
function Walk($e, $depth) {
    $c = $e.Current
    $name = ($c.Name -replace "\s+", " ")
    if ($name.Length -gt $MaxText) { $name = $name.Substring(0, $MaxText) + "..." }
    $r = $c.BoundingRectangle
    $ry = if ($r.IsEmpty) { "-" } else { "$([int]$r.X),$([int]$r.Y) $([int]$r.Width)x$([int]$r.Height)" }
    [void]$sb.AppendLine(("  " * $depth) + "$($c.ControlType.ProgrammaticName.Replace('ControlType.','')) [$name] class=$($c.ClassName) id=$($c.AutomationId) $ry")
    $ch = $walker.GetFirstChild($e)
    while ($ch -ne $null) { Walk $ch ($depth + 1); $ch = $walker.GetNextSibling($ch) }
}
Walk $win 0
[IO.File]::WriteAllText($Out, $sb.ToString(), (New-Object System.Text.UTF8Encoding $false))
"wrote $Out"
