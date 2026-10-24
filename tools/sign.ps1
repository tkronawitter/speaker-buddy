# Signs the published speaker-buddy.exe with the self-signed code signing certificate from
# Cert:\CurrentUser\My and an RFC 3161 timestamp, then verifies the signature.
# Usage: pwsh tools/sign.ps1 [-Path <exe>] [-Thumbprint <thumbprint>]
# Without -Thumbprint the certificate is found by its subject. Exits 1 if no usable certificate is
# found or the signature does not verify.
param(
    [string] $Path = (Join-Path $PSScriptRoot '..\laptop\publish\speaker-buddy.exe'),
    [string] $Thumbprint,
    [string] $Subject = 'CN=Speaker Buddy (self-signed)',
    [string] $TimestampServer = 'http://timestamp.digicert.com'
)

$ErrorActionPreference = 'Stop'

if (-not (Test-Path -LiteralPath $Path)) {
    Write-Error "File not found: $Path (run tools/publish-laptop.ps1 first)"
}

$now = Get-Date
$candidates = Get-ChildItem Cert:\CurrentUser\My -CodeSigningCert |
    Where-Object { $_.HasPrivateKey -and $_.NotBefore -le $now -and $_.NotAfter -gt $now }
if ($Thumbprint) {
    $candidates = $candidates | Where-Object { $_.Thumbprint -eq $Thumbprint.Replace(' ', '').ToUpperInvariant() }
}
else {
    $candidates = $candidates | Where-Object { $_.Subject -eq $Subject }
}
$certificates = @($candidates | Sort-Object NotAfter -Descending)
if ($certificates.Count -eq 0) {
    Write-Error "No valid code signing certificate with a private key found in Cert:\CurrentUser\My for $(if ($Thumbprint) { "thumbprint $Thumbprint" } else { "subject '$Subject'" })"
}
if ($certificates.Count -gt 1 -and -not $Thumbprint) {
    Write-Warning "$($certificates.Count) certificates match '$Subject'; using the one valid longest ($($certificates[0].Thumbprint)). Pass -Thumbprint to choose."
}
$certificate = $certificates[0]

$result = Set-AuthenticodeSignature -LiteralPath $Path -Certificate $certificate -HashAlgorithm SHA256 `
    -TimestampServer $TimestampServer -IncludeChain NotRoot
if ($null -eq $result.SignerCertificate) {
    Write-Error "Signing failed: $($result.Status) $($result.StatusMessage)"
}

# A self-signed root is only trusted where its certificate was imported, so the status can be
# UnknownError (untrusted root) here; what must hold is that the file is signed by this certificate,
# unchanged since, and timestamped.
$check = Get-AuthenticodeSignature -LiteralPath $Path
$problems = @()
if ($check.Status -notin 'Valid', 'UnknownError') {
    $problems += "status $($check.Status): $($check.StatusMessage)"
}
if ($check.SignerCertificate.Thumbprint -ne $certificate.Thumbprint) {
    $problems += 'signed by another certificate'
}
if ($null -eq $check.TimeStamperCertificate) {
    $problems += "no timestamp from $TimestampServer"
}
if ($problems.Count -gt 0) {
    Write-Error ("Signature check failed: " + ($problems -join '; '))
}

"signed $Path"
"  certificate $($certificate.Subject), thumbprint $($certificate.Thumbprint), valid until $($certificate.NotAfter.ToString('yyyy-MM-dd'))"
"  timestamp by $($check.TimeStamperCertificate.Subject)"
"  status $($check.Status)$(if ($check.Status -ne 'Valid') { ' (root not trusted on this machine; expected for a self-signed certificate)' })"
