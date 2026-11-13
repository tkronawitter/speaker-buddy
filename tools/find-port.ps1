# Prints the COM port of the board's USB Serial/JTAG interface 0 (USB\VID_303A&PID_1001&MI_00) and
# nothing else, so the flash command does not need a hardwired port:
#   idf.py -p (pwsh tools/find-port.ps1) flash
# Same lookup as the laptop app (WindowsPortDiscovery): the "Device Parameters\PortName" value of every
# instance of that interface in the Windows device registry, counted only while Windows reports the
# port as present, so a stale entry of an unplugged board is ignored. Bluetooth serial ports enumerate
# under BTHENUM, not USB, and never match.
# Exit codes: 0 one port found and printed, 1 no board found, 2 more than one board found.

$ErrorActionPreference = 'Stop'

$deviceKey = 'VID_303A&PID_1001&MI_00'
$devicePath = "HKLM:\SYSTEM\CurrentControlSet\Enum\USB\$deviceKey"

$present = @([System.IO.Ports.SerialPort]::GetPortNames())

$boards = @()
if (Test-Path -LiteralPath $devicePath) {
    foreach ($instance in Get-ChildItem -LiteralPath $devicePath) {
        $parameters = Join-Path $instance.PSPath 'Device Parameters'
        $portName = (Get-ItemProperty -LiteralPath $parameters -Name PortName -ErrorAction SilentlyContinue).PortName
        if ($portName -and ($present -contains $portName)) {
            $boards += [pscustomobject]@{ Port = $portName; Instance = "USB\$deviceKey\$($instance.PSChildName)" }
        }
    }
}
$boards = @($boards | Sort-Object Port -Unique)

if ($boards.Count -eq 0) {
    [Console]::Error.WriteLine("no board found (USB\$deviceKey). Connect the board by USB; if it still " +
        'does not appear, hold BOOT while plugging it in and run again.')
    exit 1
}
if ($boards.Count -gt 1) {
    [Console]::Error.WriteLine("more than one board found (USB\$deviceKey); unplug all but one, or " +
        'pass the port to idf.py -p yourself:')
    $boards | ForEach-Object { [Console]::Error.WriteLine("  $($_.Port)  $($_.Instance)") }
    exit 2
}

Write-Output $boards[0].Port
exit 0
