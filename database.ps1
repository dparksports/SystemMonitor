function Check-SecureBootUpdate {
    Write-Host "--- Checking Secure Boot 'db' Database ---" -ForegroundColor Cyan
    
    try {
        # 1. Get the raw data
        $uefiVar = Get-SecureBootUEFI -Name "db" -ErrorAction Stop
        
        # 2. Decode as simple text (ignoring special formatting)
        $rawDataAscii = [System.Text.Encoding]::Default.GetString($uefiVar.Bytes)
        
        # 3. Check for the specific keys
        $hasOldKey = $rawDataAscii -match "Windows Production PCA 2011"
        $hasNewKey = $rawDataAscii -match "Windows UEFI CA 2023"
        
        # 4. Report Results
        if ($hasOldKey) {
            Write-Host "  [YES] Found Old Standard: 'Microsoft Windows Production PCA 2011'" -ForegroundColor Green
        } else {
            Write-Host "  [NO]  Missing Old Standard (Unusual)" -ForegroundColor Yellow
        }

        if ($hasNewKey) {
            Write-Host "  [YES] Found New Update:   'Windows UEFI CA 2023'" -ForegroundColor Green
            Write-Host "`n  SUCCESS: The BlackLotus mitigation update is installed." -ForegroundColor Cyan
        } else {
            Write-Host "  [NO]  Missing New Update: 'Windows UEFI CA 2023'" -ForegroundColor Red
            Write-Host "`n  STATUS: The update is NOT installed yet." -ForegroundColor Yellow
        }

    } catch {
        Write-Error "Could not read the database. Ensure you are Administrator."
    }
}

Check-SecureBootUpdate