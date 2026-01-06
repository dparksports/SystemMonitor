# --- Date Range ---
$start = (Get-Date).AddDays(-90)

# Expected boot events (REAL BOOT ONLY)
$expectedBootEvents = @(
    @{ Provider = 'Microsoft-Windows-Kernel-General'; Id = 12 },
    @{ Provider = 'Microsoft-Windows-Wininit'; Id = 12 },
    @{ Provider = 'Microsoft-Windows-Security-Auditing'; Id = 4608 }
)

# --- Collect ONLY REAL boot events ---
$rawBoots = @()

# Kernel-General 12
$rawBoots += Get-WinEvent -FilterHashtable @{
    LogName = 'System'; Id = 12; ProviderName = 'Microsoft-Windows-Kernel-General'; StartTime = $start
} -ErrorAction SilentlyContinue | Select TimeCreated, Id, ProviderName

# Wininit 12
$rawBoots += Get-WinEvent -FilterHashtable @{
    LogName = 'System'; Id = 12; ProviderName = 'Microsoft-Windows-Wininit'; StartTime = $start
} -ErrorAction SilentlyContinue | Select TimeCreated, Id, ProviderName

# Security 4608
$rawBoots += Get-WinEvent -FilterHashtable @{
    LogName = 'Security'; Id = 4608; StartTime = $start
} -ErrorAction SilentlyContinue | Select TimeCreated, Id, ProviderName

$rawBoots = $rawBoots | Sort-Object TimeCreated

# --- Cluster boot events ---
$clusters = @()
$currentCluster = @()

foreach ($evt in $rawBoots) {

    if ($currentCluster.Count -eq 0) {
        $currentCluster += $evt
        continue
    }

    $last = $currentCluster[-1]

    if (($evt.TimeCreated - $last.TimeCreated).TotalSeconds -le 10) {
        $currentCluster += $evt
    }
    else {
        $clusters += , @($currentCluster)
        $currentCluster = @($evt)
    }
}

if ($currentCluster.Count -gt 0) {
    $clusters += , @($currentCluster)
}

# Convert clusters into boot objects
$boots = foreach ($cluster in $clusters) {

    $clusterEvents = $cluster | Select ProviderName, Id, TimeCreated

    # Determine missing events
    $missing = foreach ($exp in $expectedBootEvents) {
        $found = $clusterEvents | Where-Object {
            $_.ProviderName -eq $exp.Provider -and $_.Id -eq $exp.Id
        }
        if (-not $found) {
            "$($exp.Provider) ID $($exp.Id)"
        }
    }

    [PSCustomObject]@{
        BootTime      = ($cluster | Select-Object -First 1).TimeCreated
        Events        = $clusterEvents
        MissingEvents = if ($missing.Count -gt 0) { $missing -join ", " } else { "" }
    }
}

$boots = $boots | Sort-Object BootTime
