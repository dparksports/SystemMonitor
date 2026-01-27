function Get-AllSecureBootStrings {
    param ( [string]$DBName, [string]$Description )
    
    Write-Host "=============================================" -ForegroundColor Cyan
    Write-Host " Contents of: $DBName ($Description)" -ForegroundColor Cyan
    Write-Host "============================================="

    try {
        # 1. Fetch the raw binary data
        $blob = Get-SecureBootUEFI -Name $DBName -ErrorAction Stop
        
        if (-not $blob) { 
            Write-Host "  [Empty] Variable has no data." -ForegroundColor DarkGray
            return 
        }

        # 2. Convert bytes to text (Try both ASCII and Unicode)
        # This catches names regardless of how the vendor encoded them.
        $textAscii   = [System.Text.Encoding]::ASCII.GetString($blob.Bytes)
        $textUnicode = [System.Text.Encoding]::Unicode.GetString($blob.Bytes)

        # 3. Regex: Find all "words" longer than 3 characters
        # This filters out random binary noise but keeps names like "Microsoft", "Canonical", "Intel"
        $pattern = "[A-Za-z0-9][A-Za-z0-9 \-\.]{3,}"
        
        $foundStrings = @()
        $foundStrings += ([Regex]::Matches($textAscii,   $pattern) | ForEach-Object { $_.Value })
        $foundStrings += ([Regex]::Matches($textUnicode, $pattern) | ForEach-Object { $_.Value })

        # 4. Clean up and Print
        # We select 'Unique' to remove duplicates (Certs often repeat the Issuer name)
        $cleanList = $foundStrings | Select-Object -Unique | Sort-Object

        if ($cleanList.Count -gt 0) {
            foreach ($item in $cleanList) {
                # specific cleanup to ignore common noise
                if ($item -notmatch "^(M)?[A-Z]{3,}$") { 
                    Write-Host "  Found: '$item'" -ForegroundColor Green 
                }
            }
        } else {
            Write-Host "  [Binary Only] No readable names found (Likely File Hashes)." -ForegroundColor Yellow
        }

    } catch {
        Write-Error "Could not read $DBName."
    }
    Write-Host ""
}

# --- Execute ---

# 1. THE WHITELIST (Who can boot)
Get-AllSecureBootStrings "db" "Allowed Database"

# 2. THE ADMINS (Who can update the whitelist)
Get-AllSecureBootStrings "KEK" "Key Exchange Keys"

# 3. THE BLACKLIST (Who is banned)
# Note: This is usually empty of names because it stores hashes (fingerprints) of files, not vendor names.
Get-AllSecureBootStrings "dbx" "Forbidden Database"