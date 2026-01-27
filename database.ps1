<#
.SYNOPSIS
    Reads UEFI Secure Boot variables (db, dbx, KEK) and parses them to find
    readable Vendor Names (Common Names) from the authorized certificates.

.DESCRIPTION
    This script utilizes the Get-SecureBootUEFI cmdlet to fetch raw firmware data.
    It then uses Regex to scrape X.509 Certificate Subject names (CN=...) from the 
    binary blob.
    
    It is useful for verifying if the "Microsoft Windows Production PCA 2011" or
    the newer "Windows UEFI CA 2023" are present.
#>

# 1. Check for Administrator Privileges
if (!([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole] "Administrator")) {
    Write-Warning "This script requires Administrator privileges to access UEFI variables."
    Write-Warning "Please right-click the script and select 'Run with PowerShell' -> 'Run as Administrator', or run strictly from an Admin console."
    Break
}

function Get-UefiVarNames {
    param (
        [string]$VarName,
        [string]$Description
    )

    Write-Host "==========================================" -ForegroundColor Cyan
    Write-Host " Scanning: $VarName ($Description)" -ForegroundColor Cyan
    Write-Host "=========================================="

    try {
        # Fetch the raw UEFI variable data
        $uefiBlob = Get-SecureBootUEFI -Name $VarName -ErrorAction Stop
        
        if (-not $uefiBlob) {
            Write-Host "  [!] Variable is empty." -ForegroundColor Yellow
            return
        }

        # Convert bytes to an ASCII string to perform a "strings" analysis
        # (Standard UEFI certs store the Subject Name in readable ASCII/UTF8 text inside the binary DER)
        $rawString = [System.Text.Encoding]::ASCII.GetString($uefiBlob.Bytes)

        # Regex to look for "CN=" followed by standard certificate characters
        # Matches stop at common delimiters like commas or non-printable characters
        $pattern = "CN=[A-Za-z0-9 \-\.\(\)]+"
        $matches = [Regex]::Matches($rawString, $pattern)

        if ($matches.Count -gt 0) {
            # Extract values and select unique ones
            $vendors = $matches | ForEach-Object { $_.Value } | Select-Object -Unique
            
            foreach ($v in $vendors) {
                Write-Host "  [+] Found Certificate: $v" -ForegroundColor Green
            }
        } else {
            Write-Host "  [-] No readable vendor names found." -ForegroundColor DarkGray
            Write-Host "      (This usually means the list contains only cryptographic hashes, not full certificates.)" -ForegroundColor DarkGray
        }

    } catch {
        Write-Error "  [!] Could not read variable '$VarName'. Your system might not support accessing this via OS."
    }
    Write-Host ""
}

# --- Main Execution ---

# 1. Check the Allowed Database (DB)
# This controls what OS bootloaders are allowed to run.
Get-UefiVarNames "db" "Allowed Signatures Database"

# 2. Check the Key Exchange Keys (KEK)
# This controls who is allowed to update the DB/DBX (usually Microsoft + The Hardware Vendor).
Get-UefiVarNames "KEK" "Key Exchange Keys"

# 3. Check the Forbidden Database (DBX)
# This is the revocation list. It is usually full of file hashes, so seeing 'No readable names' is normal/good.
Get-UefiVarNames "dbx" "Forbidden Signatures Database"

Write-Host "Scan Complete." -ForegroundColor White